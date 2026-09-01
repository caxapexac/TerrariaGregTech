#nullable enable
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Category;
using GregTechCEuTerraria.Api.Recipe.Chance.Logic;
using GregTechCEuTerraria.Api.Recipe.Content;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using RecipeContent = GregTechCEuTerraria.Api.Recipe.Content.Content;
using TRecipe        = Terraria.Recipe;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

public static class NativeRecipeProxy
{
	private const int SynthDuration = 50;

	private const int CraftingTier = -1;
	private static readonly Dictionary<int, (string Station, int Tier)> TileToStation = new()
	{
		{ TileID.Furnaces,        ("electric_furnace", 0) },   // ULV
		{ TileID.Hellforge,       ("electric_furnace", 2) },   // MV
		{ TileID.AdamantiteForge, ("electric_furnace", 4) },   // EV
		{ TileID.LihzahrdFurnace, ("electric_furnace", 6) },   // LuV
		{ TileID.GlassKiln,       ("arc_furnace",      1) },   // LV
		{ TileID.BoneWelder,      ("forming_press",    1) },   // LV
		{ TileID.HoneyDispenser,  ("canner",           2) },   // MV
		{ TileID.DemonAltar,      ("circuit_assembler", 1) },  // LV
		{ TileID.WorkBenches,     ("crafting_shapeless", CraftingTier) },
	};

	private const string HandCraftingStation = "crafting_shapeless";

	private static readonly Dictionary<int, string> _stationByTileCache = new();
	private static string GenericStationForTile(int tile)
	{
		if (_stationByTileCache.TryGetValue(tile, out var name)) return name;
		string raw = TileID.Search.TryGetName(tile, out var found) ? found : $"tile_{tile}";
		name = PascalToSnake(raw);
		_stationByTileCache[tile] = name;
		return name;
	}

	private static string PascalToSnake(string s)
	{
		var sb = new System.Text.StringBuilder(s.Length + 4);
		for (int i = 0; i < s.Length; i++)
		{
			char c = s[i];
			if (char.IsUpper(c) && i > 0 && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]))))
				sb.Append('_');
			sb.Append(char.ToLowerInvariant(c));
		}
		return sb.ToString();
	}

	public static void SynthesizeFromSmelting(Dictionary<string, List<GTRecipe>> byStation)
	{
		if (!byStation.TryGetValue("smelting", out var sources) || sources.Count == 0) return;

		var efRecipeType = GTRecipeType.GetOrCreate("electric_furnace");
		var ulvEUt       = Common.Energy.VoltageTiers.V(0) / 2;   // = 4

		if (!byStation.TryGetValue("electric_furnace", out var efList))
		{
			efList = new List<GTRecipe>();
			byStation["electric_furnace"] = efList;
		}

		int n = 0;
		foreach (var src in sources)
		{
			if (src.Outputs.Count == 0 || src.Inputs.Count == 0) continue;

			efList.Add(BuildSynthetic(
				efRecipeType,
				id:       $"electric_furnace/proxy/{src.Id}",
				inputs:   CloneContents(src.Inputs),
				outputs:  CloneContents(src.Outputs),
				eutInput: ulvEUt,
				nativeTile: TileID.Furnaces));
			n++;
		}

		ModContent.GetInstance<GregTechCEuTerraria>().Logger
			.Info($"NativeRecipeProxy: synthesized {n} electric_furnace recipes from smelting bundle.");
	}

	public static void SynthesizeFromTerrariaRecipes()
	{
		var per = new Dictionary<string, List<GTRecipe>>();

		int scanned = 0, synth = 0, skippedBridged = 0;
		for (int r = 0; r < TRecipe.numRecipes; r++)
		{
			var rec = Main.recipe[r];
			if (rec is null) continue;

			if (VanillaCraftingBridge.BridgeRegistered.Contains(rec) || RecyclingShimmer.Registered.Contains(rec))
			{
				skippedBridged++;
				continue;
			}

			(string Station, int Tier)? mapping = null;
			for (int t = 0; t < rec.requiredTile.Count; t++)
			{
				int tile = rec.requiredTile[t];
				if (tile <= 0) continue;
				if (TileToStation.TryGetValue(tile, out var m)) { mapping = m; break; }
			}
			if (mapping is null)
			{
				int firstTile = -1;
				for (int t = 0; t < rec.requiredTile.Count; t++)
					if (rec.requiredTile[t] > 0) { firstTile = rec.requiredTile[t]; break; }
				string station = firstTile < 0
					? HandCraftingStation
					: GenericStationForTile(firstTile);
				mapping = (station, CraftingTier);
			}
			scanned++;

			if (rec.createItem.IsAir) continue;
			var outputs = new Dictionary<object, List<RecipeContent>>
			{
				[ItemRecipeCapability.CAP] = new List<RecipeContent>
				{
					ItemContent(new ItemStackIngredient(rec.createItem.type,
						IngredientResolverImpl.StableItemId(rec.createItem.type)), rec.createItem.stack),
				},
			};

			var inputs = new Dictionary<object, List<RecipeContent>>();
			var itemInputs = new List<RecipeContent>();
			bool ok = true;
			for (int i = 0; i < rec.requiredItem.Count; i++)
			{
				var req = rec.requiredItem[i];
				if (req is null || req.IsAir) continue;
				int count = req.stack;

				Ingredient ing;
				if (TryBuildGroupedIngredient(rec, req.type, out var grouped))
					ing = grouped;
				else
					ing = new ItemStackIngredient(req.type, IngredientResolverImpl.StableItemId(req.type));

				itemInputs.Add(ItemContent(ing, count));
			}
			if (!ok || itemInputs.Count == 0) continue;
			inputs[ItemRecipeCapability.CAP] = itemInputs;

			var tickInputs = new Dictionary<object, List<RecipeContent>>();
			int duration = 0;
			if (mapping.Value.Tier >= 0)
			{
				long eut = Common.Energy.VoltageTiers.V(mapping.Value.Tier) / 2;
				EURecipeCapability.PutEUContent(tickInputs, new EnergyStack(eut, 1));
				duration = SynthDuration;
			}

			var data = new TagCompound();
			int sourceTile = -1;
			for (int t = 0; t < rec.requiredTile.Count; t++)
				if (rec.requiredTile[t] > 0) { sourceTile = rec.requiredTile[t]; break; }
			if (sourceTile > 0) data.Set("nativeTile", sourceTile);

			var conditions = new List<RecipeCondition>();
			foreach (var c in rec.Conditions)
			{
				string d = c.Description.Value;
				if (!string.IsNullOrEmpty(d)) conditions.Add(new NativeCraftingCondition(d));
			}

			var gtType = GTRecipeType.GetOrCreate(mapping.Value.Station);
			var synthetic = new GTRecipe(
				recipeType:              gtType,
				id:                      $"{mapping.Value.Station}/native/{r}",
				inputs:                  inputs,
				outputs:                 outputs,
				tickInputs:              tickInputs,
				tickOutputs:             new Dictionary<object, List<RecipeContent>>(),
				inputChanceLogics:       new Dictionary<object, ChanceLogic>(),
				outputChanceLogics:      new Dictionary<object, ChanceLogic>(),
				tickInputChanceLogics:   new Dictionary<object, ChanceLogic>(),
				tickOutputChanceLogics:  new Dictionary<object, ChanceLogic>(),
				conditions:              conditions,
				ingredientActions:       System.Array.Empty<object>(),
				data:                    data,
				duration:                duration,
				recipeCategory:          GTRecipeCategory.DEFAULT,
				groupColor:              -1);

			if (!per.TryGetValue(mapping.Value.Station, out var bucket))
			{
				bucket = new List<GTRecipe>();
				per[mapping.Value.Station] = bucket;
			}
			bucket.Add(synthetic);
			VanillaCraftingBridge.GTToVanilla[synthetic] = rec;
			synth++;
		}

		RecipeRegistry.AppendAll(per);

		ModContent.GetInstance<GregTechCEuTerraria>().Logger
			.Info($"NativeRecipeProxy: synthesized {synth}/{scanned} Terraria native recipes " +
			      $"across {per.Count} GT stations" +
			      (skippedBridged > 0 ? $" (skipped {skippedBridged} bridge-registered)" : "") + ".");
	}

	private static bool TryBuildGroupedIngredient(TRecipe rec, int slotItemType, out TagIngredient grouped)
	{
		grouped = null!;
		foreach (int gid in rec.acceptedGroups)
		{
			if (gid <= 0 || !RecipeGroup.recipeGroups.TryGetValue(gid, out var group)) continue;
			var members = group.ValidItems;
			if (members is null || !members.Contains(slotItemType)) continue;

			var list = members.ToList();
			grouped = new TagIngredient(IngredientResolverImpl.RecipeGroupTag(gid), list);
			return true;
		}
		return false;
	}

	private static GTRecipe BuildSynthetic(GTRecipeType type, string id,
		Dictionary<object, List<RecipeContent>> inputs,
		Dictionary<object, List<RecipeContent>> outputs,
		long eutInput, int nativeTile = -1)
	{
		var tickInputs = new Dictionary<object, List<RecipeContent>>();
		EURecipeCapability.PutEUContent(tickInputs, new EnergyStack(eutInput, 1));

		var data = new TagCompound();
		if (nativeTile > 0) data.Set("nativeTile", nativeTile);

		return new GTRecipe(
			recipeType:              type,
			id:                      id,
			inputs:                  inputs,
			outputs:                 outputs,
			tickInputs:              tickInputs,
			tickOutputs:             new Dictionary<object, List<RecipeContent>>(),
			inputChanceLogics:       new Dictionary<object, ChanceLogic>(),
			outputChanceLogics:      new Dictionary<object, ChanceLogic>(),
			tickInputChanceLogics:   new Dictionary<object, ChanceLogic>(),
			tickOutputChanceLogics:  new Dictionary<object, ChanceLogic>(),
			conditions:              new List<RecipeCondition>(),
			ingredientActions:       System.Array.Empty<object>(),
			data:                    data,
			duration:                SynthDuration,
			recipeCategory:          GTRecipeCategory.DEFAULT,
			groupColor:              -1);
	}

	private static RecipeContent ItemContent(Ingredient ing, int count)
	{
		int max     = ChanceLogic.GetMaxChancedValue();
		Ingredient payload = count > 1 ? SizedIngredient.Create(ing, count) : ing;
		return new RecipeContent(payload, max, max, 0);
	}

	private static Dictionary<object, List<RecipeContent>> CloneContents(
		Dictionary<object, List<RecipeContent>> src)
	{
		var clone = new Dictionary<object, List<RecipeContent>>(src.Count);
		foreach (var (k, v) in src) clone[k] = new List<RecipeContent>(v);
		return clone;
	}
}
