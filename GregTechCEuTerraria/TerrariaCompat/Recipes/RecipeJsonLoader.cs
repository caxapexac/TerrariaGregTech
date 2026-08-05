#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

// Reads Data/Recipes/all.json
public static class RecipeJsonLoader
{
	private const string BundlePath = "Data/Recipes/all.json";

	public static void Load(Mod mod, IIngredientResolver resolver)
	{
		using var stream = mod.GetFileStream(BundlePath);
		if (stream is null)
		{
			mod.Logger.Warn($"Recipe bundle not found at {BundlePath} - no recipes loaded. " +
			                 "Run `./gradlew runData` upstream + " +
			                 "`python tools/scripts/snapshot-recipes.py` to produce it.");
			return;
		}

		using var doc = JsonDocument.Parse(stream);
		if (doc.RootElement.ValueKind != JsonValueKind.Array)
		{
			mod.Logger.Error($"Recipe bundle is not a JSON array (got {doc.RootElement.ValueKind})");
			return;
		}

		var byStation = new Dictionary<string, List<GTRecipe>>();
		int total = 0, skipped = 0;

		var patches = CompatRecipes.Patches;
		var patchBaseIds = new HashSet<string>(patches.Count);
		var overrideIds = new HashSet<string>(patches.Count);
		foreach (var p in patches)
		{
			patchBaseIds.Add(p.BaseId);
			if (p.IsOverride) overrideIds.Add(p.BaseId);
		}
		var capturedBase = new Dictionary<string, string>(patches.Count);

		void Register(GTRecipe recipe)
		{
			string station = recipe.RecipeType.RegistryName;
			if (!byStation.TryGetValue(station, out var list))
			{
				list = new List<GTRecipe>();
				byStation[station] = list;
			}
			list.Add(recipe);
			total++;

			foreach (var cond in recipe.Conditions)
			{
				if (cond is Common.Recipe.Condition.ResearchCondition rc && !string.IsNullOrEmpty(rc.ResearchId))
					recipe.RecipeType.AddDataStickEntry(rc.ResearchId, recipe);
			}
		}

		foreach (var el in doc.RootElement.EnumerateArray())
		{
			string id = el.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
				? (idEl.GetString() ?? "")
				: "";
			if (string.IsNullOrEmpty(id)) { skipped++; continue; }

			if (patchBaseIds.Contains(id)) capturedBase[id] = el.GetRawText();

			if (overrideIds.Contains(id)) { skipped++; continue; }

			GTRecipe recipe;
			try
			{
				recipe = VanillaRecipeJson.IsVanillaShape(el)
					? VanillaRecipeJson.Read(el, resolver, id)
					: GTRecipeSerializer.Read(el, resolver, id);
			}
			catch (System.Exception ex)
			{
				mod.Logger.Warn($"Skipping recipe {id}: {ex.Message}");
				skipped++;
				continue;
			}

			Register(recipe);
		}

		foreach (var p in patches)
		{
			if (!capturedBase.TryGetValue(p.BaseId, out var baseRaw))
			{
				mod.Logger.Warn($"Recipe patch base not found in bundle: {p.BaseId} (patch {p.NewId} skipped)");
				skipped++;
				continue;
			}
			try
			{
				var (_, recipe) = CompatRecipes.MaterializePatch(p, baseRaw, resolver);
				Register(recipe);
			}
			catch (System.Exception ex)
			{
				mod.Logger.Warn($"Skipping recipe patch {p.NewId}: {ex.Message}");
				skipped++;
			}
		}

		foreach (var (station, recipe) in CompatRecipes.Build(resolver))
		{
			if (!byStation.TryGetValue(station, out var compatList))
			{
				compatList = new List<GTRecipe>();
				byStation[station] = compatList;
			}
			compatList.Add(recipe);
			total++;
		}

		foreach (var derived in CompatRecipes.BuildVanillaOreMaceratorRecipes(byStation))
		{
			if (!byStation.TryGetValue("macerator", out var maceratorList))
			{
				maceratorList = new List<GTRecipe>();
				byStation["macerator"] = maceratorList;
			}
			maceratorList.Add(derived);
			total++;
		}

		NativeRecipeProxy.SynthesizeFromSmelting(byStation);

		int stationed = CraftingStationRecipeTransform.Apply(byStation);
		if (stationed > 0)
			mod.Logger.Info($"[recipes] {stationed} recipes converted to crafting-station requirements.");

		var map = new Dictionary<string, IReadOnlyList<GTRecipe>>(byStation.Count);
		foreach (var (station, list) in byStation) map[station] = list;
		RecipeRegistry.Set(map);

		mod.Logger.Info($"Loaded {total:N0} recipes across {byStation.Count} stations" +
		                 (skipped > 0 ? $" (skipped {skipped})" : "") + ".");
	}
}
