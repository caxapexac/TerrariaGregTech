#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric.Research;

public class ResearchStationMachine : WorkableElectricMultiblockMachine, IOpticalComputationReceiver
{
	public IOpticalComputationProvider? ComputationProvider { get; private set; }
	public ObjectHolderMachine? ObjectHolder { get; private set; }

	public ResearchStationMachine() : base() { }

	protected override RecipeLogic CreateRecipeLogic() => new ResearchStationRecipeLogic();

	public override bool RegressWhenWaiting() => false;

	protected override bool SupportsRecipeLookupCore => false;

	public IOpticalComputationProvider? GetComputationProvider() => ComputationProvider;

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		ComputationProvider = null;
		ObjectHolder = null;
		foreach (var part in GetParts())
		{
			if (part is ObjectHolderMachine holder)
				ObjectHolder = holder;
			if (part is IOpticalComputationHatch hatch)
				ComputationProvider = hatch;
			else if (part is IOpticalComputationReceiver recv)
				ComputationProvider ??= recv.GetComputationProvider();
		}
		if (ObjectHolder == null)
		{
			SetUnformedReason("No Object Holder");
			OnStructureInvalid();
		}
		else if (ComputationProvider == null)
		{
			SetUnformedReason("No Computation Receiver Hatch");
			OnStructureInvalid();
		}
	}

	public override void OnStructureInvalid()
	{
		ComputationProvider = null;
		ObjectHolder?.SetLocked(false);
		ObjectHolder = null;
		base.OnStructureInvalid();
	}

	private int _displayCapacityCwu;
	private int _displayReqCwu;
	public int DisplayCapacityCwu => _displayCapacityCwu;
	public int DisplayRequiredCwu => _displayReqCwu;

	protected override void OnTick()
	{
		base.OnTick();
		if (!IsServer || !IsFormed) return;
		_displayCapacityCwu = (ComputationProvider as OpticalComputationHatchMachine)?.GetAvailableCwu()
			?? ComputationProvider?.GetMaxCWUt() ?? 0;
		_displayReqCwu      = ResolveRequiredCwu();
	}

	private int ResolveRequiredCwu()
	{
		var cand = Recipe.GetLastRecipe();
		if (cand == null)
		{
			foreach (var r in TerrariaCompat.Recipes.RecipeRegistry.ForStation(GetRecipeType().RegistryName))
			{
				if (r.GetTickInputContents(CWURecipeCapability.CAP).Count == 0) continue;
				var items  = r.GetInputContents(ItemRecipeCapability.CAP);
				var fluids = r.GetInputContents(FluidRecipeCapability.CAP);
				if (TryMatchInputContents(r, items, fluids).IsSuccess) { cand = r; break; }
			}
		}
		int req = 0;
		if (cand != null)
			foreach (var c in cand.GetTickInputContents(CWURecipeCapability.CAP))
				if (c.Payload is int v) req += v;
		return req;
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["rsCapCwu"] = _displayCapacityCwu;
		tag["rsReqCwu"] = _displayReqCwu;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		_displayCapacityCwu = tag.GetInt("rsCapCwu");
		_displayReqCwu      = tag.GetInt("rsReqCwu");
	}

	public sealed class ResearchStationRecipeLogic : RecipeLogic
	{
		protected override IReadOnlyList<Type> ValidMachineClasses() =>
			new[] { typeof(ResearchStationMachine) };

		private ResearchStationMachine M => (ResearchStationMachine)Machine;

		public override void SaveForSync(Terraria.ModLoader.IO.TagCompound tag) => Save(tag);

		protected override ActionResult MatchRecipe(GTRecipe recipe)
		{
			var machine = GetRLMachine();
			var itemIn  = recipe.GetInputContents(ItemRecipeCapability.CAP);
			var fluidIn = recipe.GetInputContents(FluidRecipeCapability.CAP);
			var inResult = machine.TryMatchInputContents(recipe, itemIn, fluidIn);
			if (!inResult.IsSuccess) return inResult;
			return MatchTickRecipe(recipe);
		}

		public override bool CheckMatchedRecipeAvailable(GTRecipe match)
		{
			var modified = ApplyAmbient(GetRLMachine(), GetRLMachine().FullModifyRecipe(match));
			if (modified != null)
			{
				if (modified.GetInputContents(CWURecipeCapability.CAP).Count == 0 &&
				    modified.GetTickInputContents(CWURecipeCapability.CAP).Count == 0)
				{
					return true;
				}

				var itemIn  = modified.GetInputContents(ItemRecipeCapability.CAP);
				var fluidIn = modified.GetInputContents(FluidRecipeCapability.CAP);
				if (!M.TryMatchInputContents(modified, itemIn, fluidIn).IsSuccess)
					return false;

				var recipeMatch = CheckRecipe(modified);
				if (recipeMatch.IsSuccess)
				{
					SetupRecipe(modified);
				}
				else
				{
					SetWaiting(recipeMatch.ReasonText());
				}
				if (_lastRecipe != null &&
				    GetStatus() == global::GregTechCEuTerraria.Api.Machine.Feature.RecipeLogicStatus.WORKING)
				{
					_lastOriginRecipe = match;
					lastFailedMatches = null;
					return true;
				}
			}
			return false;
		}

		protected override ActionResult HandleRecipeIO(GTRecipe recipe, IO io)
		{
			var holder = M.ObjectHolder;
			if (holder == null) return ActionResult.SUCCESS;

			if (io == IO.IN)
			{
				holder.SetLocked(true);
				return ActionResult.SUCCESS;
			}

			if (_lastRecipe == null)
			{
				holder.SetLocked(false);
				return ActionResult.SUCCESS;
			}

			holder.SetHeldItem(new Item());
			var outItem = ResolveResearchOutput(_lastRecipe);
			if (outItem != null && !outItem.IsAir)
				holder.SetDataItem(outItem);
			holder.SetLocked(false);
			return ActionResult.SUCCESS;
		}

		protected override ActionResult HandleTickRecipeIO(GTRecipe recipe, IO io)
		{
			if (io != IO.OUT) return base.HandleTickRecipeIO(recipe, io);
			return ActionResult.SUCCESS;
		}

		private static Item? ResolveResearchOutput(GTRecipe recipe)
		{
			foreach (var content in recipe.GetOutputContents(ItemRecipeCapability.CAP))
			{
				if (content.Payload is not Ingredient ing) continue;
				var (type, outNbt) = TerrariaCompat.Items.ResearchSnbt.PeelItem(ing);
				if (type <= 0) continue;
				var stack = new Item();
				stack.SetDefaults(type);
				if (!string.IsNullOrEmpty(outNbt))
				{
					var (rid, rtype) = TerrariaCompat.Items.ResearchSnbt.Parse(outNbt!);
					if (!string.IsNullOrEmpty(rid))
					{
						var recType = GTRecipeType.Get(rtype) ?? recipe.RecipeType;
						ResearchManager.WriteResearchToStack(stack, rid, recType);
					}
				}
				return stack;
			}
			return null;
		}

	}
}
