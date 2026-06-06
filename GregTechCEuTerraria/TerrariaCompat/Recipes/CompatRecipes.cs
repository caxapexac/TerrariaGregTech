#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

// Recipes the port needs but all.json doesn't have
// JSON shape goes through the same GTRecipeSerializer + IngredientResolver as the bundle
public static class CompatRecipes
{
	public static readonly System.Collections.Generic.HashSet<string> OverriddenIds = new()
	{
		// --- Overrides (replaced in Json below) ---
		"shaped/steam_miner_bronze",
		"shaped/steam_miner_steel",
		"shaped/steam_macerator_bronze",

		// --- Removal ---
		"cutter/cut_stone_into_slab",
		"cutter/cut_stone_into_slab_water",
		"cutter/cut_stone_into_slab_distilled_water",
	};


	// additional compat recipes:
	//

	//
	// -
	//
	// -

	// compat_{ulv,lv}_{input,output}_{hatch,bus} - make ULV/LV hatches/buses in hand.
	private const string HatchesAndBuses = """
	[
	  { "id": "crafting_shaped/compat_ulv_input_hatch", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:ulv_machine_hull" } },
	      { "content": { "tag": "forge:glass" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:ulv_input_hatch" } } ] } },

	  { "id": "crafting_shaped/compat_ulv_output_hatch", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:ulv_machine_hull" } },
	      { "content": { "tag": "forge:glass" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:ulv_output_hatch" } } ] } },

	  { "id": "crafting_shaped/compat_lv_input_hatch", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:lv_machine_hull" } },
	      { "content": { "item": "gtceu:wood_drum" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:lv_input_hatch" } } ] } },

	  { "id": "crafting_shaped/compat_lv_output_hatch", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:lv_machine_hull" } },
	      { "content": { "item": "gtceu:wood_drum" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:lv_output_hatch" } } ] } },

	  { "id": "crafting_shaped/compat_ulv_input_bus", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:ulv_machine_hull" } },
	      { "content": { "tag": "forge:chests/wooden" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:ulv_input_bus" } } ] } },

	  { "id": "crafting_shaped/compat_ulv_output_bus", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:ulv_machine_hull" } },
	      { "content": { "tag": "forge:chests/wooden" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:ulv_output_bus" } } ] } },

	  { "id": "crafting_shaped/compat_lv_input_bus", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:lv_machine_hull" } },
	      { "content": { "item": "gtceu:wood_crate" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:lv_input_bus" } } ] } },

	  { "id": "crafting_shaped/compat_lv_output_bus", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:lv_machine_hull" } },
	      { "content": { "item": "gtceu:wood_crate" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:lv_output_bus" } } ] } }
	]
	""";

	// steam_boiler/compat_wood - use wood as boiler fuel
	// compat_rubber_ingot + compat_rubber_plate - make rubber plates in hand
	// compat_wrought_iron - Iron smelting into WroughtIron
	private const string Bootstrap = """
	[
	  { "id": "steam_boiler/compat_wood", "type": "gtceu:steam_boiler", "duration": 300,
	    "inputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 1,
	      "ingredient": { "tag": "minecraft:logs" } } } ] } },

	  { "id": "smelting/compat_rubber_ingot", "type": "minecraft:smelting",
	    "inputs":  { "item": [ { "content": { "type": "gtceu:sized", "count": 2,
	      "ingredient": { "item": "gtceu:sticky_resin" } } } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:rubber_ingot" } } ] } },

	  { "id": "smelting/compat_wrought_iron", "type": "minecraft:smelting",
	    "inputs":  { "item": [ { "content": { "item": "minecraft:iron_ingot" } } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:wrought_iron_ingot" } } ] } },

	  { "id": "crafting_shaped/compat_rubber_plate", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:rubber_ingot" } } },
	      { "content": { "tag": "gtceu:tools/crafting_hammers" } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:rubber_plate" } } ] } }
	]
	""";

	// Steam miners + LP steam macerator - remove diamond from the recipe
	private const string SteamMachineOverrides = """
	[
	  { "id": "crafting_shaped/compat_steam_miner_bronze", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:bronze_rod" } } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "tag": "forge:small_gears/bronze" } } },
	      { "content": { "item": "gtceu:bronze_brick_casing" } },
	      { "content": { "type": "gtceu:sized", "count": 4,
	        "ingredient": { "item": "gtceu:bronze_normal_fluid_pipe" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:lp_steam_miner" } } ] } },

	  { "id": "crafting_shaped/compat_steam_macerator_bronze", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:bronze_rod" } } },
	      { "content": { "type": "gtceu:sized", "count": 4,
	        "ingredient": { "item": "gtceu:bronze_small_fluid_pipe" } } },
	      { "content": { "item": "gtceu:bronze_machine_casing" } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "tag": "forge:pistons" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:lp_steam_macerator" } } ] } },

	  { "id": "crafting_shaped/compat_steam_miner_steel", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "gtceu:steel_rod" } } },
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "tag": "forge:small_gears/steel" } } },
	      { "content": { "item": "gtceu:lp_steam_miner" } },
	      { "content": { "type": "gtceu:sized", "count": 4,
	        "ingredient": { "item": "gtceu:tin_alloy_normal_fluid_pipe" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:hp_steam_miner" } } ] } }
	]
	""";

	// coke clay without brick form
	// simple pipes
	private const string Misc = """
	[
	  { "id": "crafting_shaped/compat_compressed_coke_clay_formless", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 3,
	        "ingredient": { "item": "minecraft:clay_ball" } } },
	      { "content": { "type": "gtceu:sized", "count": 5,
	        "ingredient": { "tag": "minecraft:sand" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 2,
	      "ingredient": { "item": "gtceu:compressed_coke_clay" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_item_pipe", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 3,
	        "ingredient": { "item": "minecraft:stone" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_item_pipe" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_fluid_pipe", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 3,
	        "ingredient": { "tag": "minecraft:logs" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_fluid_pipe" } } } ] } }
	]
	""";

	// boss summons
	private const string Casings = """
	[
	  { "id": "chemical_reactor/compat_dirty_stainless_steel_casing", "type": "gtceu:chemical_reactor", "duration": 300,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:clean_machine_casing" } },
	      { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "minecraft:dirt" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 480 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:dirty_stainless_steel_casing" } } ] } },

	  { "id": "chemical_bath/compat_frozen_frostproof_casing", "type": "gtceu:chemical_bath", "duration": 200,
	    "inputs":  {
	      "item":  [ { "content": { "item": "gtceu:frostproof_machine_casing" } } ],
	      "fluid": [ { "content": { "amount": 1000, "value": { "tag": "forge:distilled_water" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 480 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:frozen_frostproof_casing" } } ] } },

	  { "id": "chemical_reactor/compat_acid_etched_inert_casing", "type": "gtceu:chemical_reactor", "duration": 200,
	    "inputs":  {
	      "item":  [ { "content": { "item": "gtceu:inert_machine_casing" } } ],
	      "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:sulfuric_acid" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 480 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:acid_etched_inert_casing" } } ] } },

	  { "id": "large_chemical_reactor/compat_unstable_compressor_charge", "type": "gtceu:large_chemical_reactor", "duration": 240,
	    "inputs":  {
	      "item":  [
	        { "content": { "item": "gtceu:solid_machine_casing" } },
	        { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "gtceu:industrial_tnt" } } },
	        { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "gtceu:titanium_plate" } } }
	      ],
	      "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:lubricant" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 1920 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:unstable_compressor_charge" } } ] } }
	]
	""";

	// compat_clay_from_sand (+ _hand) - there is no infinite clay source, so we need a way to get it from sand
	private const string Clay = """
	[
	  { "id": "chemical_bath/compat_clay_from_sand", "type": "gtceu:chemical_bath", "duration": 200,
	    "inputs":  {
	      "item":  [ { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "minecraft:sand" } } } ],
	      "fluid": [ { "content": { "amount": 1000, "value": { "tag": "forge:water" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 16 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "minecraft:clay" } } } ] } },

	  { "id": "crafting_shaped_fluid_container/compat_clay_from_sand_hand", "type": "gtceu:crafting_shaped_fluid_container",
	    "inputs":  {
	      "item": [
	        { "content": { "type": "gtceu:sized", "count": 8, "ingredient": { "item": "minecraft:sand" } } },
	        { "content": { "type": "gtceu:fluid_container", "fluid": { "amount": 1000, "value": { "tag": "forge:water" } } } }
	      ]
	    },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 8, "ingredient": { "item": "minecraft:clay" } } } ] } }
	]
	""";

	// compat_pressure_plate - needed for early game covers but aren't craftable in vanilla terraria (post-dungeon selling)
	private const string TerrariaIntermediates = """
	[
	  { "id": "assembler/compat_heavy_weighted_pressure_plate", "type": "gtceu:assembler", "duration": 100,
	    "inputs":  { "item": [ { "content": { "type": "gtceu:sized", "count": 2,
	      "ingredient": { "tag": "forge:plates/iron" } } } ] },
	    "tickInputs": { "eu": [ { "content": 16 } ] },
	    "outputs": { "item": [ { "content": { "item": "minecraft:heavy_weighted_pressure_plate" } } ] } },

	  { "id": "assembler/compat_light_weighted_pressure_plate", "type": "gtceu:assembler", "duration": 100,
	    "inputs":  { "item": [ { "content": { "type": "gtceu:sized", "count": 2,
	      "ingredient": { "tag": "forge:plates/gold" } } } ] },
	    "tickInputs": { "eu": [ { "content": 16 } ] },
	    "outputs": { "item": [ { "content": { "item": "minecraft:light_weighted_pressure_plate" } } ] } }
	]
	""";

	// compat_terra_prisma - because some people dont like bullet hell
	private const string TerraPrisma = """
	[
	  { "id": "assembler/compat_terra_prisma", "type": "gtceu:assembler", "duration": 2400,
	    "inputs":  {
	      "item":  [
	        { "content": { "item": "gtceu:wetware_processor_assembly" } },
	        { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "gtceu:zpm_emitter" } } },
	        { "content": { "type": "gtceu:sized", "count": 2, "ingredient": { "item": "gtceu:zpm_field_generator" } } },
	        { "content": { "type": "gtceu:sized", "count": 8, "ingredient": { "item": "gtceu:europium_plate" } } },
	        { "content": { "type": "gtceu:sized", "count": 16, "ingredient": { "item": "gtceu:fine_europium_wire" } } }
	      ],
	      "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:soldering_alloy" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 131072 } ] },
	    "recipeConditions": [ { "type": "gtceu:cleanroom" } ],
	    "outputs": { "item": [ { "content": { "item": "terraria:EmpressBlade" } } ] } }
	]
	""";

	private static readonly string[] JsonGroups =
	{
		HatchesAndBuses, Bootstrap, SteamMachineOverrides, Misc, Casings, Clay,
		TerrariaIntermediates, TerraPrisma,
	};

	// Per-tier crafting recipes for our custom-block machines
	private static readonly string[] LampTiers =
	{
		"ulv","lv","mv","hv","ev","iv","luv","zpm","uv","uhv","uev","uiv","uxv","opv","max",
	};
	private static readonly string[] SolarPanelTiers =
	{
		"ulv","lv",
	};

	private static string LampRecipe(string tier) =>
		$$"""
		{ "id": "crafting_shaped/compat_{{tier}}_lamp", "type": "minecraft:crafting_shaped",
		  "inputs":  { "item": [
		    { "content": { "item": "gtceu:{{tier}}_machine_casing" } },
		    { "content": { "item": "minecraft:torch" } }
		  ] },
		  "outputs": { "item": [ { "content": { "item": "gtceu:{{tier}}_lamp" } } ] } }
		""";

	private static string SolarPanelRecipe(string tier) =>
		$$"""
		{ "id": "crafting_shaped/compat_{{tier}}_solar_panel_machine", "type": "minecraft:crafting_shaped",
		  "inputs":  { "item": [
		    { "content": { "item": "gtceu:{{tier}}_steam_turbine" } },
		    { "content": { "item": "gtceu:{{tier}}_solar_panel" } }
		  ] },
		  "outputs": { "item": [ { "content": { "item": "gtceu:{{tier}}_solar_panel_machine" } } ] } }
		""";

	public static List<(string Station, GTRecipe Recipe)> Build(IIngredientResolver resolver)
	{
		var result = new List<(string, GTRecipe)>();
		foreach (var group in JsonGroups)
			ParseArray(group, resolver, result);
		foreach (var tier in LampTiers)
			ParseOne(LampRecipe(tier), resolver, result);
		foreach (var tier in SolarPanelTiers)
			ParseOne(SolarPanelRecipe(tier), resolver, result);
		return result;
	}

	private static void ParseArray(string json, IIngredientResolver resolver, List<(string, GTRecipe)> result)
	{
		using var doc = JsonDocument.Parse(json);
		foreach (var el in doc.RootElement.EnumerateArray())
			ParseElement(el, resolver, result);
	}

	private static void ParseOne(string json, IIngredientResolver resolver, List<(string, GTRecipe)> result)
	{
		using var doc = JsonDocument.Parse(json);
		ParseElement(doc.RootElement, resolver, result);
	}

	private static void ParseElement(JsonElement el, IIngredientResolver resolver, List<(string, GTRecipe)> result)
	{
		string id = el.GetProperty("id").GetString()!;
		var recipe = GTRecipeSerializer.Read(el, resolver, id);
		result.Add((recipe.RecipeType.RegistryName, recipe));
	}

	// (vanilla ore, GT material) - mirrors AddVanillaOreToRawOreRecipes so the
	// "1 vanilla ore -> 16 raw -> macerate" hand-chain stays in lock-step with
	// the macerator shortcut emitted below. Tungsten ore folds to tungstate -
	// no raw_tungsten exists.
	private static readonly (int VanillaItemId, string Material)[] VanillaOreMaterials =
	{
		(ItemID.IronOre,     "iron"),
		(ItemID.LeadOre,     "lead"),
		(ItemID.CopperOre,   "copper"),
		(ItemID.TinOre,      "tin"),
		(ItemID.GoldOre,     "gold"),
		(ItemID.PlatinumOre, "platinum"),
		(ItemID.SilverOre,   "silver"),
		(ItemID.TungstenOre, "tungstate"),
	};

	// For each vanilla ore that has a raw_X -> crushed_X macerator recipe,
	// emit a parallel recipe that consumes 1 vanilla ore directly and yields
	// 16x the raw-recipe output at 2x EU/t (same duration)
	public static List<GTRecipe> BuildVanillaOreMaceratorRecipes(
		IReadOnlyDictionary<string, List<GTRecipe>> byStation)
	{
		var result = new List<GTRecipe>();
		if (!byStation.TryGetValue("macerator", out var macerator)) return result;

		var bySrcId = new Dictionary<string, GTRecipe>(macerator.Count);
		foreach (var r in macerator) bySrcId[r.Id] = r;

		var outputScale = Api.Recipe.Content.ContentModifier.Multiplier_(16);
		var euScale     = Api.Recipe.Content.ContentModifier.Multiplier_(2);

		foreach (var (vanillaItemId, material) in VanillaOreMaterials)
		{
			string srcId = $"macerator/macerate_raw_{material}_ore_to_crushed_ore";
			if (!bySrcId.TryGetValue(srcId, out var src)) continue;

			var inputs = new Dictionary<object, List<Api.Recipe.Content.Content>>(src.Inputs.Count);
			foreach (var (cap, list) in src.Inputs)
			{
				if (ReferenceEquals(cap, ItemRecipeCapability.CAP))
				{
					var payload = new SizedIngredient(
						new ItemStackIngredient(vanillaItemId, $"terraria:{material}_ore"), 1);
					int maxChance = Api.Recipe.Chance.Logic.ChanceLogic.GetMaxChancedValue();
					inputs[cap] = new List<Api.Recipe.Content.Content> { new(payload, maxChance, maxChance, 0) };
				}
				else
				{
					inputs[cap] = list.Select(c => c.Copy(cap)).ToList();
				}
			}

			var outputs     = outputScale.ApplyContents(src.Outputs);
			var tickInputs  = euScale.ApplyContents(src.TickInputs);
			var tickOutputs = Api.Recipe.Content.ContentModifier.IDENTITY.ApplyContents(src.TickOutputs);

			var derived = new GTRecipe(
				src.RecipeType,
				$"macerator/compat_macerate_terraria_{material}_ore_to_crushed_ore",
				inputs, outputs, tickInputs, tickOutputs,
				new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(src.InputChanceLogics),
				new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(src.OutputChanceLogics),
				new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(src.TickInputChanceLogics),
				new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(src.TickOutputChanceLogics),
				new List<Api.Recipe.RecipeCondition>(src.Conditions),
				new List<object>(src.IngredientActions),
				src.Data,
				src.Duration,
				src.RecipeCategory,
				src.GroupColor);

			result.Add(derived);
		}

		return result;
	}
}
