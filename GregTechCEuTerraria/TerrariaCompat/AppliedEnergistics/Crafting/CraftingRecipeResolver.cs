#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.TerrariaCompat.Tiles.CraftingStations;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;

public static class CraftingRecipeResolver
{
	public static Terraria.Recipe? Find(MePattern pattern)
	{
		if (pattern.Type != MePatternType.Crafting) return null;
		if (pattern.PrimaryOutput is not AEItemKey outKey) return null;
		int outType = outKey.GetItem();
		long outAmt = pattern.PrimaryOutputAmount;
		var stations = pattern.StationIds;

		foreach (var (_, r) in CandidatesFor(outType))
		{
			if (r.createItem.stack != outAmt) continue;
			if (!StationMatches(r, stations)) continue;
			if (InputsMatch(r, pattern.Inputs)) return r;
		}
		return null;
	}

	public static IEnumerable<Terraria.Recipe> ForOutput(int outType)
	{
		foreach (var (_, r) in CandidatesFor(outType))
			yield return r;
	}

	private static Dictionary<int, List<(int index, Terraria.Recipe recipe)>>? _byOutputType;

	private static readonly List<(int index, Terraria.Recipe recipe)> _noCandidates = new();

	private static List<(int index, Terraria.Recipe recipe)> CandidatesFor(int outType)
	{
		if (_byOutputType == null)
		{
			var d = new Dictionary<int, List<(int, Terraria.Recipe)>>();
			int count = Terraria.Recipe.numRecipes;
			for (int i = 0; i < count; i++)
			{
				var r = Main.recipe[i];
				if (r is null || r.createItem is null || r.createItem.IsAir) continue;
				if (Recipes.RecyclingShimmer.Registered.Contains(r)) continue;
				int t = r.createItem.type;
				if (!d.TryGetValue(t, out var l)) d[t] = l = new List<(int, Terraria.Recipe)>();
				l.Add((i, r));
			}
			_byOutputType = d;
		}
		return _byOutputType.TryGetValue(outType, out var hit) ? hit : _noCandidates;
	}

	private static bool StationMatches(Terraria.Recipe r, IReadOnlyList<string> stations)
	{
		var required = MePattern.StationIdsOf(r);
		if (required.Length != stations.Count) return false;
		for (int i = 0; i < required.Length; i++)
			if (required[i] != stations[i]) return false;
		return true;
	}

	public static IReadOnlyList<string> StationIdsOf(GTRecipe gt)
	{
		var resolver = IIngredientResolver.Default;
		var ids = new List<string>();

		foreach (var key in CraftingStationRegistry.StationKeysFor(gt))
		{
			if (!CraftingStationRegistry.TryGetTile(key, out int t)) return System.Array.Empty<string>();
			var id = resolver?.StableTileId(t) ?? "";
			if (id.Length == 0) return System.Array.Empty<string>();
			if (!ids.Contains(id)) ids.Add(id);
		}
		if (ids.Count > 0) { ids.Sort(System.StringComparer.Ordinal); return ids; }

		int native = gt.Data.GetInt("nativeTile");
		if (native > 0)
		{
			var id = resolver?.StableTileId(native) ?? "";
			if (id.Length > 0) return new[] { id };
		}

		string station = gt.RecipeType.RegistryName ?? "";
		if (VanillaCraftingBridge.IsHandStation(station)) return System.Array.Empty<string>();
		if (VanillaCraftingBridge.TryGetStationTile(station, out int tile))
		{
			var id = resolver?.StableTileId(tile) ?? "";
			if (id.Length > 0) return new[] { id };
		}
		return System.Array.Empty<string>();
	}

	private static AEItemKey? KeyOf(Ingredient ing)
	{
		var items = ing.GetItems();
		return items.Count > 0 && !items[0].IsAir ? AEItemKey.Of(items[0]) : null;
	}

	private static readonly Dictionary<GTRecipe, (Terraria.Recipe? rec, int index)> _resolved = new();

	public static void ClearResolvedCache()
	{
		_resolved.Clear();
		_byOutputType = null;
	}

	public static Terraria.Recipe? FindForGtRecipe(GTRecipe gt, out int index)
	{
		if (_resolved.TryGetValue(gt, out var hit)) { index = hit.index; return hit.rec; }
		var found = FindForGtRecipeUncached(gt, out index);
		_resolved[gt] = (found, index);
		return found;
	}

	private static Terraria.Recipe? FindForGtRecipeUncached(GTRecipe gt, out int index)
	{
		index = -1;

		int outType = 0;
		long outAmt = 0;
		foreach (var (ing, count) in gt.GetItemOutputs())
			if (KeyOf(ing) is { } k) { outType = k.GetItem(); outAmt = count; break; }
		if (outType <= 0) return null;

		var inputs = new List<(AEKey what, long amount)>();
		foreach (var (ing, count) in gt.GetItemInputs())
		{
			if (KeyOf(ing) is not { } k) return null;
			inputs.Add((k, count));
		}
		if (inputs.Count == 0) return null;

		var stations = StationIdsOf(gt);

		foreach (var (i, r) in CandidatesFor(outType))
		{
			if (r.createItem.stack != outAmt) continue;
			if (!StationMatches(r, stations)) continue;
			if (!InputsMatch(r, inputs)) continue;
			index = i;
			return r;
		}
		return null;
	}

	private static bool InputsMatch(Terraria.Recipe r, IReadOnlyList<(AEKey what, long amount)> inputs)
	{
		var reqs = new List<Item>();
		foreach (var it in r.requiredItem)
			if (it != null && !it.IsAir) reqs.Add(it);

		if (reqs.Count != inputs.Count) return false;

		var used = new bool[inputs.Count];
		foreach (var req in reqs)
		{
			var accepted = AcceptedTypes(r, req.type);
			bool found = false;
			for (int i = 0; i < inputs.Count; i++)
			{
				if (used[i]) continue;
				if (inputs[i].what is not AEItemKey ik) continue;
				if (inputs[i].amount != req.stack) continue;
				if (!accepted.Contains(ik.GetItem())) continue;
				used[i] = true;
				found = true;
				break;
			}
			if (!found) return false;
		}
		return true;
	}

	private static HashSet<int> AcceptedTypes(Terraria.Recipe r, int reqType)
	{
		var set = new HashSet<int> { reqType };
		foreach (var gid in r.acceptedGroups)
		{
			if (RecipeGroup.recipeGroups.TryGetValue(gid, out var g) && g.ValidItems.Contains(reqType))
				set.UnionWith(g.ValidItems);
		}
		return set;
	}
}
