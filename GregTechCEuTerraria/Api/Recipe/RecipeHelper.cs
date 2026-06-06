#nullable enable
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Common.Energy;

namespace GregTechCEuTerraria.Api.Recipe;

// port of com.gregtechceu.gtceu.api.recipe.RecipeHelper.
public static class RecipeHelper
{
	public static ActionResult MatchRecipe(
		Api.Machine.Feature.IRecipeLogicMachine machine, GTRecipe recipe)
	{
		var itemIn  = recipe.GetInputContents(Api.Capability.Recipe.ItemRecipeCapability.CAP);
		var fluidIn = recipe.GetInputContents(Api.Capability.Recipe.FluidRecipeCapability.CAP);
		return machine.TryMatchInputContents(recipe, itemIn, fluidIn);
	}

	public static ActionResult HandleRecipeIO(
		Api.Machine.Feature.IRecipeLogicMachine machine,
		GTRecipe recipe,
		Api.Capability.Recipe.IO io,
		System.Collections.Generic.IDictionary<string, int>? chanceCache = null)
	{
		var items  = io == Api.Capability.Recipe.IO.IN
			? recipe.GetInputContents (Api.Capability.Recipe.ItemRecipeCapability.CAP)
			: recipe.GetOutputContents(Api.Capability.Recipe.ItemRecipeCapability.CAP);
		var fluids = io == Api.Capability.Recipe.IO.IN
			? recipe.GetInputContents (Api.Capability.Recipe.FluidRecipeCapability.CAP)
			: recipe.GetOutputContents(Api.Capability.Recipe.FluidRecipeCapability.CAP);
		return io == Api.Capability.Recipe.IO.IN
			? machine.TryConsumeInputContents(recipe, items, fluids)
			: machine.DepositOutputContents(recipe, items, fluids, machine.GetRecipeLogic());
	}

	public static ActionResult HandleRecipe(
		Api.Machine.Feature.IRecipeLogicMachine machine,
		GTRecipe recipe, Api.Capability.Recipe.IO io, bool isTick, bool simulate)
	{
		var contents = io == Api.Capability.Recipe.IO.IN
			? (isTick ? recipe.TickInputs  : recipe.Inputs)
			: (isTick ? recipe.TickOutputs : recipe.Outputs);
		return machine.HandleRecipe(recipe, io, contents, isTick, simulate, machine.GetRecipeLogic());
	}

	public static ActionResult MatchContents(
		Api.Machine.Feature.IRecipeLogicMachine machine, GTRecipe recipe)
	{
		var inR = HandleRecipe(machine, recipe, Api.Capability.Recipe.IO.IN,  isTick: false, simulate: true);
		if (!inR.IsSuccess) return inR;
		var outR = HandleRecipe(machine, recipe, Api.Capability.Recipe.IO.OUT, isTick: false, simulate: true);
		if (!outR.IsSuccess) return outR;
		return HandleRecipe(machine, recipe, Api.Capability.Recipe.IO.IN, isTick: true, simulate: true);
	}


	public static GTRecipe TrimRecipeOutputs(
		GTRecipe recipe,
		System.Collections.Generic.IReadOnlyDictionary<object, int>? trimLimits)
	{
		if (trimLimits == null || trimLimits.Count == 0) return recipe;
		bool allUnlimited = true;
		foreach (var v in trimLimits.Values)
			if (v != -1) { allUnlimited = false; break; }
		if (allUnlimited) return recipe;

		var copy = recipe.Copy();

		copy.Outputs.Clear();
		foreach (var kv in DoTrim(recipe.Outputs, trimLimits)) copy.Outputs[kv.Key] = kv.Value;
		copy.TickOutputs.Clear();
		foreach (var kv in DoTrim(recipe.TickOutputs, trimLimits)) copy.TickOutputs[kv.Key] = kv.Value;

		return copy;
	}

	public static System.Collections.Generic.Dictionary<object, System.Collections.Generic.List<Content.Content>>
		DoTrim(
			System.Collections.Generic.Dictionary<object, System.Collections.Generic.List<Content.Content>> current,
			System.Collections.Generic.IReadOnlyDictionary<object, int> trimLimits)
	{
		var outputs =
			new System.Collections.Generic.Dictionary<object, System.Collections.Generic.List<Content.Content>>(current.Count);

		foreach (var entry in current)
		{
			var cap = entry.Key;
			var contents = entry.Value;
			if (contents.Count == 0) continue;
			int n = trimLimits.TryGetValue(cap, out var lim) ? lim : -1;
			if (n == 0) continue;

			if (!outputs.TryGetValue(cap, out var list))
			{
				list = new System.Collections.Generic.List<Content.Content>();
				outputs[cap] = list;
			}
			if (n == -1)
			{
				list.AddRange(contents);
				continue;
			}

			int added = 0;
			var chanced = new System.Collections.Generic.List<Content.Content>();
			// Add non-chanced contents with priority
			foreach (var content in contents)
			{
				if (added == n) break;
				if (content.IsChanced)
				{
					chanced.Add(content);
				}
				else
				{
					list.Add(content);
					added++;
				}
			}

			// Add as many chanced contents as needed
			if (added < n)
			{
				int rem = System.Math.Min(chanced.Count, n - added);
				list.AddRange(chanced.GetRange(0, rem));
			}
		}

		return outputs;
	}


	public static EnergyStack GetRealEUt(GTRecipe recipe)
	{
		var stack = recipe.InputEUt;
		if (!stack.IsEmpty()) return stack;
		return recipe.OutputEUt;
	}

	public static (EnergyStack Stack, bool IsInput) GetRealEUtWithIO(GTRecipe recipe)
	{
		var stack = recipe.InputEUt;
		if (!stack.IsEmpty()) return (stack, true);
		return (recipe.OutputEUt, false);
	}

	public static int GetRecipeEUtTier(GTRecipe recipe)
	{
		var stack = GetRealEUt(recipe);
		long eut = stack.Voltage;
		if (recipe.Parallels > 1) eut /= recipe.Parallels;
		return VoltageTiers.TierByVoltage(eut);
	}

	public static int GetPreOCRecipeEuTier(GTRecipe recipe)
	{
		var stack = GetRealEUt(recipe);
		long eut = stack.GetTotalEU();
		if (recipe.Parallels > 1) eut /= recipe.Parallels;
		eut >>= recipe.OcLevel * 2;
		return VoltageTiers.TierByVoltage(eut);
	}
}
