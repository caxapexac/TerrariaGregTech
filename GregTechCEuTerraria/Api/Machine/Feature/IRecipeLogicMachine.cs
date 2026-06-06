#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Recipe;
using Microsoft.Xna.Framework;

namespace GregTechCEuTerraria.Api.Machine.Feature;

// port of com.gregtechceu.gtceu.api.machine.feature.IRecipeLogicMachine.
public interface IRecipeLogicMachine : IWorkable, IVoidable
{
	// (not stuck on no-power, no-redstone, etc.).
	bool IsRecipeLogicAvailable() => true;

	// True = stay subscribed to tick callbacks even when idle (poll every 5 ticks)
	bool KeepSubscribing() => false;

	bool BeforeWorking(GTRecipe recipe) => true;
	bool OnWorking() => true;
	void OnWaiting() { }
	void AfterWorking() { }

	bool AlwaysTryModifyRecipe() => true;

	bool IsMultiblockController() => false;

	bool PreventPowerFail() => false;

	bool HasCustomProgressLine() => false;

	bool RegressWhenWaiting() => true;

	void NotifyStatusChanged(RecipeLogicStatus oldStatus, RecipeLogicStatus newStatus) { }

	GTRecipe? FullModifyRecipe(GTRecipe recipe) => recipe;

	// RecipeLogic.CheckMatchedRecipeAvailable reads this to surface a
	// useful failure on the world-hover tooltip ("No rotor installed",
	// "Out of lubricant")
	string? GetLastModifierFailReason() => null;

	Trait.RecipeLogic GetRecipeLogic();

	// === Recipe search input ================================================
	GTRecipeType GetRecipeType();

	bool ShowsInRecipeBrowser(GTRecipe recipe) => true;

	// === RecipeLookup trie ==================================================
	bool SupportsRecipeLookup => false;

	IReadOnlyList<Terraria.Item> LookupInputItems => System.Array.Empty<Terraria.Item>();
	IReadOnlyList<FluidStack> LookupInputFluids => System.Array.Empty<FluidStack>();

	long RecipeVoltageCap { get; }

	// a per-machine offset so a wall of machines doesn't all scan the recipe registry on the same frame.
	long OffsetTimer { get; }

	// === Energy buffer (for brownout check + drain in HandleRecipeWorking) =
	long EnergyStored { get; set; }

	// === Sound side-channel =================================================
	bool ShouldWorkingPlaySound() => true;
	void EnsureLoopSound(Vector2 worldPos);
	void StopLoopSound();
	void PlayFinishSound(Vector2 worldPos);
	Vector2 GetWorldPos();

	// === Active EU/t - display only =========================================
	long ActiveEut { get; set; }

	// display only, actual logic is stored in GTRecipeNbt
	string? LastRecipeId { get; set; }

	// === Recipe I/O hooks - substitute for upstream's capability proxy =====
	ActionResult TryMatchInputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids);

	ActionResult HasOutputRoomContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids);

	ActionResult TryConsumeInputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids);

	ActionResult DepositOutputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids,
		Trait.RecipeLogic logic);

	// EU-input gate. Upstream's `RecipeRunner` runs for ALL capabilities
	// including EU; we keep a dedicated method for it because EU has a few
	// machine-side checks (brown-out detection, charger-slot equalisation)
	// that aren't part of the generic handler walk. Multiblock implementations
	// still route through their group-aware dispatcher internally for parity
	// with upstream - see `WorkableMultiblockMachine.HandleEUThroughCapProxy`.
	ActionResult TryDrainEU(Api.Recipe.GTRecipe recipe, long voltage);

	ActionResult DepositOutputEU(Api.Recipe.GTRecipe recipe, long voltage);

	ActionResult TryHandleTickCwu(Api.Recipe.GTRecipe recipe, Api.Capability.Recipe.IO io, bool simulate)
		=> ActionResult.SUCCESS;

	ActionResult HandleRecipe(
		Api.Recipe.GTRecipe recipe,
		Api.Capability.Recipe.IO io,
		IReadOnlyDictionary<object, List<Api.Recipe.Content.Content>> contents,
		bool isTick, bool simulate, Trait.RecipeLogic logic)
	{
		var empty  = (IReadOnlyList<Api.Recipe.Content.Content>)System.Array.Empty<Api.Recipe.Content.Content>();
		var items  = contents.TryGetValue(Api.Capability.Recipe.ItemRecipeCapability.CAP,  out var it) ? it : empty;
		var fluids = contents.TryGetValue(Api.Capability.Recipe.FluidRecipeCapability.CAP, out var fl) ? fl : empty;
		bool hasEu  = contents.ContainsKey(Api.Capability.Recipe.EURecipeCapability.CAP);
		bool hasCwu = contents.ContainsKey(Api.Capability.Recipe.CWURecipeCapability.CAP);

		if (io == Api.Capability.Recipe.IO.IN)
		{
			long voltage = recipe.InputEUt.Voltage;
			if (simulate)
			{
				if (hasEu && voltage > 0 && EnergyStored < voltage)
					return ActionResult.Fail("gtceu.recipe.insufficient_eu", Api.Capability.Recipe.EURecipeCapability.CAP, Api.Capability.Recipe.IO.IN);
				var r = TryMatchInputContents(recipe, items, fluids);
				if (!r.IsSuccess) return r;
				return hasCwu ? TryHandleTickCwu(recipe, Api.Capability.Recipe.IO.IN, simulate: true) : ActionResult.SUCCESS;
			}
			else
			{
				if (hasEu && voltage > 0)
				{
					var drain = TryDrainEU(recipe, voltage);
					if (!drain.IsSuccess) return drain;
				}
				var c = TryConsumeInputContents(recipe, items, fluids);
				if (!c.IsSuccess) return c;
				return hasCwu ? TryHandleTickCwu(recipe, Api.Capability.Recipe.IO.IN, simulate: false) : ActionResult.SUCCESS;
			}
		}
		else // OUT
		{
			if (simulate)
				return HasOutputRoomContents(recipe, items, fluids);
			var d = DepositOutputContents(recipe, items, fluids, logic);
			if (!d.IsSuccess) return d;
			long outV = recipe.OutputEUt.Voltage;
			return (hasEu && outV > 0) ? DepositOutputEU(recipe, outV) : ActionResult.SUCCESS;
		}
	}
}

public enum RecipeLogicStatus
{
	IDLE,
	WORKING,
	WAITING,
	SUSPEND,
}
