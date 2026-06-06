#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Recipe.Content;

namespace GregTechCEuTerraria.Api.Recipe.Modifier;

// Port of com.gregtechceu.gtceu.api.recipe.modifier.ParallelLogic.
//
// Two parallel-amount finders:
//   - GetParallelAmount        - full upstream two-step search
//   - GetParallelAmountFast    - power-of-2 fast finder
//   - AdjustMultiplier         - verbatim port of upstream adjustMultiplier.
//
// adaptations:
// Upstream dispatches input-check / output-check through
// `IRecipeCapabilityHolder.getCapabilitiesFlat(IO, cap)` + per-capability
// `RecipeCapability.getMaxParallelByInput / limitMaxParallelByOutput` (which
// use inventory aggregation for input ratios and simulated
// `handler.handleRecipe(IO.OUT, ..., simulate=true)` for output binary-search).
//
public static class ParallelLogic
{
	// === Public entry points ===================================================

	//   1. parallelLimit <= 1 -> return parallelLimit.
	//   2. max-by-input - largest N <= parallelLimit where (recipe x N) inputs fit.
	//   3. limit-by-output-merging from maxByInput.
	public static int GetParallelAmount(IRecipeLogicMachine machine, GTRecipe recipe, int parallelLimit)
	{
		if (parallelLimit <= 1) return parallelLimit;

		int maxByInput = GetMaxByInput(machine, recipe, parallelLimit, skipEu: false);
		if (maxByInput == 0) return 0;

		return LimitByOutputMerging(machine, recipe, maxByInput, skipEu: false);
	}

	public static int GetParallelAmountWithoutEU(IRecipeLogicMachine machine, GTRecipe recipe, int parallelLimit)
	{
		if (parallelLimit <= 1) return parallelLimit;

		int maxByInput = GetMaxByInput(machine, recipe, parallelLimit, skipEu: true);
		if (maxByInput == 0) return 0;

		return LimitByOutputMerging(machine, recipe, maxByInput, skipEu: true);
	}

	public static int GetParallelAmountFast(IRecipeLogicMachine machine, GTRecipe recipe, int parallelLimit)
	{
		if (parallelLimit <= 1) return parallelLimit;

		while (parallelLimit > 0)
		{
			var copied = recipe.Copy(ContentModifier.Multiplier_(parallelLimit), false);
			if (MatchRecipeInputs(machine, copied) && MatchTickRecipeInputs(machine, copied, skipEu: false))
				return parallelLimit;
			parallelLimit /= 2;
		}
		return 1;
	}

	// binary-search step for the output-merge / parallel finder.
	public static int[] AdjustMultiplier(bool mergedAll, int minMultiplier, int multiplier, int maxMultiplier)
	{
		if (mergedAll)
		{
			minMultiplier = multiplier;
			int remainder = (maxMultiplier - multiplier) % 2;
			multiplier = multiplier + remainder + (maxMultiplier - multiplier) / 2;
		}
		else
		{
			maxMultiplier = multiplier;
			multiplier = (multiplier + minMultiplier) / 2;
		}
		if (maxMultiplier - minMultiplier <= 1)
		{
			multiplier = maxMultiplier = minMultiplier;
		}
		return new[] { minMultiplier, multiplier, maxMultiplier };
	}

	private static int GetMaxByInput(IRecipeLogicMachine machine, GTRecipe recipe, int parallelLimit, bool skipEu)
	{
		if (InputsFitAt(machine, recipe, parallelLimit, skipEu)) return parallelLimit;
		int min = 0, max = parallelLimit, mid = parallelLimit;
		while (min != max)
		{
			bool ok = InputsFitAt(machine, recipe, mid, skipEu);
			var bin = AdjustMultiplier(ok, min, mid, max);
			min = bin[0]; mid = bin[1]; max = bin[2];
		}
		return mid;
	}

	private static int LimitByOutputMerging(IRecipeLogicMachine machine, GTRecipe recipe, int parallelLimit, bool skipEu)
	{
		if (OutputsFitAt(machine, recipe, parallelLimit, skipEu)) return parallelLimit;

		int min = 0, max = parallelLimit, mid = parallelLimit;
		while (min != max)
		{
			bool ok = OutputsFitAt(machine, recipe, mid, skipEu);
			var bin = AdjustMultiplier(ok, min, mid, max);
			min = bin[0]; mid = bin[1]; max = bin[2];
		}
		return mid;
	}

	// === Per-step feasibility predicates =======================================
	// does (recipe x N) satisfy ALL input capabilities the machine cares about
	private static bool InputsFitAt(IRecipeLogicMachine machine, GTRecipe recipe, int n, bool skipEu)
	{
		if (n <= 0) return false;
		var copied = recipe.Copy(ContentModifier.Multiplier_(n), false);
		return MatchRecipeInputs(machine, copied) && MatchTickRecipeInputs(machine, copied, skipEu);
	}

	// does (recipe x N) deposit cleanly into all non-voided output capabilities?
	private static bool OutputsFitAt(IRecipeLogicMachine machine, GTRecipe recipe, int n, bool skipEu)
	{
		if (n <= 0) return false;
		var copied = recipe.Copy(ContentModifier.Multiplier_(n), false);

		var itemsOut  = machine.CanVoidRecipeOutputs(ItemRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: copied.GetOutputContents(ItemRecipeCapability.CAP);
		var fluidsOut = machine.CanVoidRecipeOutputs(FluidRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: copied.GetOutputContents(FluidRecipeCapability.CAP);
		if (!machine.HasOutputRoomContents(copied, itemsOut, fluidsOut).IsSuccess) return false;

		var itemsTickOut  = machine.CanVoidRecipeOutputs(ItemRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: copied.GetTickOutputContents(ItemRecipeCapability.CAP);
		var fluidsTickOut = machine.CanVoidRecipeOutputs(FluidRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: copied.GetTickOutputContents(FluidRecipeCapability.CAP);
		if (itemsTickOut.Count > 0 || fluidsTickOut.Count > 0)
		{
			if (!machine.HasOutputRoomContents(copied, itemsTickOut, fluidsTickOut).IsSuccess) return false;
		}

		if (!skipEu && !machine.CanVoidRecipeOutputs(EURecipeCapability.CAP))
		{
			long outEU = copied.OutputEUt.GetTotalEU();
			if (outEU > 0)
			{
				_ = outEU;
			}
		}

		return true;
	}

	private static bool MatchRecipeInputs(IRecipeLogicMachine machine, GTRecipe recipe)
	{
		var itemIn  = recipe.GetInputContents(ItemRecipeCapability.CAP);
		var fluidIn = recipe.GetInputContents(FluidRecipeCapability.CAP);
		return machine.TryMatchInputContents(recipe, itemIn, fluidIn).IsSuccess;
	}

	private static bool MatchTickRecipeInputs(IRecipeLogicMachine machine, GTRecipe recipe, bool skipEu)
	{
		if (!recipe.HasTick()) return true;
		if (!skipEu)
		{
			long tickEu = recipe.InputEUt.Voltage;
			if (tickEu > 0 && machine.EnergyStored < tickEu) return false;
		}
		var itemIn  = recipe.GetTickInputContents(ItemRecipeCapability.CAP);
		var fluidIn = recipe.GetTickInputContents(FluidRecipeCapability.CAP);
		return machine.TryMatchInputContents(recipe, itemIn, fluidIn).IsSuccess;
	}
}
