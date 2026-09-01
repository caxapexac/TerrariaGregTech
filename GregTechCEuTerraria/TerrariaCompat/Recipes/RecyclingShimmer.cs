#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.TerrariaCompat.Items.Registry;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

public static class RecyclingShimmer
{
	public static bool TurnRecyclingIntoShimmer = true;

	private const string MaceratorCategory = "gtceu:macerator_recycling";
	private const string ArcCategory       = "gtceu:arc_furnace_recycling";
	private const string Namespace         = "gtceu:";

	private static readonly Dictionary<int, List<(int Type, int Count)>> _transforms = new();

	public static readonly HashSet<Terraria.Recipe> Registered = new();

	public static bool ShouldDrop(GTRecipe recipe)
	{
		if (!TurnRecyclingIntoShimmer) return false;
		string? cat = recipe.CategoryId;
		return cat is MaceratorCategory or ArcCategory;
	}

	public static void Capture(GTRecipe recipe)
	{
		if (!TurnRecyclingIntoShimmer || recipe.CategoryId != ArcCategory) return;

		var inputs = recipe.GetInputContents(ItemRecipeCapability.CAP);
		if (inputs.Count != 1) return;
		if (!VanillaCraftingBridge.TryResolveItem((Ingredient)inputs[0].Payload, out int inType, out _, out string inKey))
			return;
		if (!IsMachineOrBlock(inKey)) return;

		var outputs = recipe.GetOutputContents(ItemRecipeCapability.CAP);
		if (outputs.Count == 0) return;

		var results = new List<(int, int)>(outputs.Count);
		foreach (var o in outputs)
		{
			if (!VanillaCraftingBridge.TryResolveItem((Ingredient)o.Payload, out int t, out int n, out _)) return;
			results.Add((t, n));
		}

		_transforms[inType] = results;
	}

	private static bool IsMachineOrBlock(string upstreamId)
	{
		if (!upstreamId.StartsWith(Namespace, System.StringComparison.Ordinal)) return false;
		return RegistryDump.TryGet(upstreamId[Namespace.Length..], out var entry) && entry.Prefix is null;
	}

	public static void AddRecipes(Mod mod)
	{
		if (!TurnRecyclingIntoShimmer || _transforms.Count == 0) return;

		var neverCraftable = new Condition(LocalizedText.Empty, () => false);

		foreach (var (inputType, results) in _transforms)
		{
			var recipe = Terraria.Recipe.Create(inputType, 1);
			foreach (var (type, count) in results)
				recipe.AddCustomShimmerResult(type, count);
			recipe.AddCondition(neverCraftable);
			recipe.Register();
			Registered.Add(recipe);
		}

		mod.Logger.Info($"[shimmer] {_transforms.Count} machine/block arc-recycling transforms registered");
	}

	public static void Unload()
	{
		_transforms.Clear();
		Registered.Clear();
	}
}
