#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Recipe.Content;

namespace GregTechCEuTerraria.Api.Recipe.Modifier;

public static class ParallelLogic
{
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
		if (!skipEu)
		{
			parallelLimit = LimitByInputEUt(machine, recipe, parallelLimit);
			if (parallelLimit <= 0) return 0;
		}

		if (InputsFitAt(machine, recipe, parallelLimit)) return parallelLimit;
		int min = 0, max = parallelLimit, mid = parallelLimit;
		while (min != max)
		{
			bool ok = InputsFitAt(machine, recipe, mid);
			var bin = AdjustMultiplier(ok, min, mid, max);
			min = bin[0]; mid = bin[1]; max = bin[2];
		}
		return mid;
	}

	private static int LimitByOutputMerging(IRecipeLogicMachine machine, GTRecipe recipe, int parallelLimit, bool skipEu)
	{
		if (!skipEu)
		{
			parallelLimit = LimitByOutputEUt(machine, recipe, parallelLimit);
			if (parallelLimit <= 0) return 0;
		}

		if (OutputsFitAt(machine, recipe, parallelLimit)) return parallelLimit;

		int min = 0, max = parallelLimit, mid = parallelLimit;
		while (min != max)
		{
			bool ok = OutputsFitAt(machine, recipe, mid);
			var bin = AdjustMultiplier(ok, min, mid, max);
			min = bin[0]; mid = bin[1]; max = bin[2];
		}
		return mid;
	}

	private static int LimitByInputEUt(IRecipeLogicMachine machine, GTRecipe recipe, int limit)
	{
		long maxVoltage = long.MaxValue;
		if (machine is IOverclockMachine overclockMachine)
			maxVoltage = overclockMachine.OverclockVoltage;
		else if (machine is ITieredMachine tieredMachine)
			maxVoltage = tieredMachine.GetMaxVoltage();

		long recipeEUt = recipe.InputEUt.GetTotalEU();
		if (recipeEUt == 0) return limit;
		return System.Math.Min(limit, System.Math.Abs(SaturatedCast(maxVoltage / recipeEUt)));
	}

	private static int LimitByOutputEUt(IRecipeLogicMachine machine, GTRecipe recipe, int limit)
	{
		if (machine.CanVoidRecipeOutputs(EURecipeCapability.CAP)) return limit;

		long recipeEUt = recipe.OutputEUt.GetTotalEU();
		if (recipeEUt == 0) return limit;

		long maxVoltage = long.MaxValue;
		if (machine is IOverclockMachine overclockMachine)
			maxVoltage = overclockMachine.OverclockVoltage;
		else if (machine is ITieredMachine tieredMachine)
			maxVoltage = tieredMachine.GetMaxVoltage();

		return System.Math.Min(limit, System.Math.Abs(SaturatedCast(maxVoltage / recipeEUt)));
	}

	private static int SaturatedCast(long value) =>
		value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;

	private static bool InputsFitAt(IRecipeLogicMachine machine, GTRecipe recipe, int n)
	{
		if (n <= 0) return false;
		var copied = recipe.Copy(ContentModifier.Multiplier_(n), false);
		return MatchRecipeInputs(machine, copied) && MatchTickRecipeInputs(machine, copied, skipEu: true);
	}

	private static bool OutputsFitAt(IRecipeLogicMachine machine, GTRecipe recipe, int n)
	{
		if (n <= 0) return false;
		var copied = recipe.Copy(ContentModifier.Multiplier_(n), false);

		var itemsOut  = machine.CanVoidRecipeOutputs(ItemRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: ScaleAllOutputs(recipe.GetOutputContents(ItemRecipeCapability.CAP), ItemRecipeCapability.CAP, n);
		var fluidsOut = machine.CanVoidRecipeOutputs(FluidRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: ScaleAllOutputs(recipe.GetOutputContents(FluidRecipeCapability.CAP), FluidRecipeCapability.CAP, n);
		if (!machine.HasOutputRoomContents(copied, itemsOut, fluidsOut).IsSuccess) return false;

		var itemsTickOut  = machine.CanVoidRecipeOutputs(ItemRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: ScaleAllOutputs(recipe.GetTickOutputContents(ItemRecipeCapability.CAP), ItemRecipeCapability.CAP, n);
		var fluidsTickOut = machine.CanVoidRecipeOutputs(FluidRecipeCapability.CAP)
			? System.Array.Empty<Content.Content>()
			: ScaleAllOutputs(recipe.GetTickOutputContents(FluidRecipeCapability.CAP), FluidRecipeCapability.CAP, n);
		if (itemsTickOut.Count > 0 || fluidsTickOut.Count > 0)
		{
			if (!machine.HasOutputRoomContents(copied, itemsTickOut, fluidsTickOut).IsSuccess) return false;
		}

		return true;
	}

	private static System.Collections.Generic.IReadOnlyList<Content.Content> ScaleAllOutputs(
		System.Collections.Generic.IReadOnlyList<Content.Content> contents, object cap, int n)
	{
		var mod = ContentModifier.Multiplier_(n);
		var list = new System.Collections.Generic.List<Content.Content>(contents.Count);
		foreach (var c in contents) list.Add(c.CopyChanced(cap, mod));
		return list;
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
