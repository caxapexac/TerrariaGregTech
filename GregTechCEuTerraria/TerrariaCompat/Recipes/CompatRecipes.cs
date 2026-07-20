#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Items.Cables;
using GregTechCEuTerraria.AppliedEnergistics.Api.Util;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

// Recipes the port needs but all.json doesn't have
// JSON shape goes through the same GTRecipeSerializer + IngredientResolver as the bundle
public static class CompatRecipes
{
	public static readonly System.Collections.Generic.HashSet<string> OverriddenIds = new() { "shaped/primitive_pump","shaped/pump_hatch", "shaped/pump_deck", "assembler/assemble_treated_wood_frame", "shaped/frame_treated_wood", "chemical_bath/treated_planks", "lathe/treated_wood_sticks", "shaped/large_treated_wooden_pipe", "shaped/treated_wood_stick" , "shaped/normal_treated_wooden_pipe", "shaped/small_treated_wooden_pipe", "assembler/large_treated_wood_pipe", "assembler/normal_treated_wood_pipe", "assembler/small_treated_wood_pipe", "shaped_fluid_container/treated_wood_planks" };

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

	// coke clay without brick form
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
	      "ingredient": { "item": "gtceu:compressed_coke_clay" } } } ] } }
	]
	""";

	private const string SimplePipes = """
	[
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
	      "ingredient": { "item": "gtceu:simple_fluid_pipe" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_item_pipe_small", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "item": "minecraft:stone" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_item_pipe_small" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_item_pipe_large", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 5,
	        "ingredient": { "item": "minecraft:stone" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_item_pipe_large" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_item_pipe_huge", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 8,
	        "ingredient": { "item": "minecraft:stone" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_item_pipe_huge" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_fluid_pipe_tiny", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 1,
	        "ingredient": { "tag": "minecraft:logs" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_fluid_pipe_tiny" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_fluid_pipe_small", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 2,
	        "ingredient": { "tag": "minecraft:logs" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_fluid_pipe_small" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_fluid_pipe_large", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 6,
	        "ingredient": { "tag": "minecraft:logs" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_fluid_pipe_large" } } } ] } },

	  { "id": "crafting_shaped/compat_simple_fluid_pipe_huge", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 12,
	        "ingredient": { "tag": "minecraft:logs" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:simple_fluid_pipe_huge" } } } ] } }
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

	// Terraria Amber to redstone to unlock skyblock
	private const string RedstoneGem = """
	[
	  { "id": "macerator/compat_macerate_redstone_gem", "type": "gtceu:macerator", "duration": 25,
	    "inputs":  { "item": [ { "content": { "type": "gtceu:sized", "count": 1,
	      "ingredient": { "item": "gtceu:redstone_gem" } } } ] },
	    "tickInputs": { "eu": [ { "content": 2 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 16,
	      "ingredient": { "item": "gtceu:redstone_dust" } } } ] } }
	]
	""";

	// Terraria coin conversion in the packer
	private const string Coins = """
	[
	  { "id": "packer/compat_pack_copper_coins", "type": "gtceu:packer", "duration": 100,
	    "inputs": { "item": [
	      { "content": { "type": "gtceu:sized", "count": 100, "ingredient": { "item": "terraria:CopperCoin" } } },
	      { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 1 } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 4 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:SilverCoin" } } ] } },

	  { "id": "packer/compat_pack_silver_coins", "type": "gtceu:packer", "duration": 100,
	    "inputs": { "item": [
	      { "content": { "type": "gtceu:sized", "count": 100, "ingredient": { "item": "terraria:SilverCoin" } } },
	      { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 1 } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 4 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:GoldCoin" } } ] } },

	  { "id": "packer/compat_pack_gold_coins", "type": "gtceu:packer", "duration": 100,
	    "inputs": { "item": [
	      { "content": { "type": "gtceu:sized", "count": 100, "ingredient": { "item": "terraria:GoldCoin" } } },
	      { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 1 } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 4 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:PlatinumCoin" } } ] } },

	  { "id": "packer/compat_unpack_silver_coin", "type": "gtceu:packer", "duration": 100,
	    "inputs": { "item": [
	      { "content": { "item": "terraria:SilverCoin" } },
	      { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 2 } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 4 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 100, "ingredient": { "item": "terraria:CopperCoin" } } } ] } },

	  { "id": "packer/compat_unpack_gold_coin", "type": "gtceu:packer", "duration": 100,
	    "inputs": { "item": [
	      { "content": { "item": "terraria:GoldCoin" } },
	      { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 2 } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 4 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 100, "ingredient": { "item": "terraria:SilverCoin" } } } ] } },

	  { "id": "packer/compat_unpack_platinum_coin", "type": "gtceu:packer", "duration": 100,
	    "inputs": { "item": [
	      { "content": { "item": "terraria:PlatinumCoin" } },
	      { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 2 } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 4 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 100, "ingredient": { "item": "terraria:GoldCoin" } } } ] } }
	]
	""";

	private const string LegacyConversions = """
	[
	  { "id": "crafting_shaped/compat_convert_legacy_platinum_ingot", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/platinum_ingot" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:PlatinumBar" } } ] } },

	  { "id": "crafting_shaped/compat_convert_legacy_silver_ingot", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/silver_ingot" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:SilverBar" } } ] } },

	  { "id": "crafting_shaped/compat_convert_legacy_tin_ingot", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/tin_ingot" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:TinBar" } } ] } },

	  { "id": "crafting_shaped/compat_convert_legacy_lead_ingot", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/lead_ingot" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:LeadBar" } } ] } },

	  { "id": "crafting_shaped/compat_convert_legacy_amethyst_gem", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/amethyst_gem" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:Amethyst" } } ] } },

	  { "id": "crafting_shaped/compat_convert_legacy_ruby_gem", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/ruby_gem" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:Ruby" } } ] } },

	  { "id": "crafting_shaped/compat_convert_legacy_sapphire_gem", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/sapphire_gem" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:Sapphire" } } ] } },

	  { "id": "crafting_shaped/compat_convert_legacy_topaz_gem", "type": "minecraft:crafting_shaped",
	    "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/topaz_gem" } } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:Topaz" } } ] } }
	]
	""";

	private const string Ae2FluixCables = """
	[
	  { "id": "alloy_smelter/compat_me_cable_fluix_ulv", "type": "gtceu:alloy_smelter", "duration": 100,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:bronze_ingot" } },
	      { "content": { "type": "gtceu:sized", "count": 8, "ingredient": { "item": "gtceu:red_alloy_single_wire" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 8 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 8,
	      "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } } ] } },

	  { "id": "alloy_smelter/compat_me_cable_fluix_lv_gt", "type": "gtceu:alloy_smelter", "duration": 100,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:steel_ingot" } },
	      { "content": { "type": "gtceu:sized", "count": 16, "ingredient": { "item": "gtceu:manganese_phosphide_single_wire" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 16,
	      "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } } ] } },

	  { "id": "alloy_smelter/compat_me_cable_fluix_lv_tr", "type": "gtceu:alloy_smelter", "duration": 100,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:steel_ingot" } },
	      { "content": { "type": "gtceu:sized", "count": 16, "ingredient": { "item": "GregTechCEuTerraria/evil_superconductor_single" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 16,
	      "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } } ] } },

	  { "id": "alloy_smelter/compat_me_cable_fluix_mv_gt", "type": "gtceu:alloy_smelter", "duration": 100,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:aluminium_ingot" } },
	      { "content": { "type": "gtceu:sized", "count": 32, "ingredient": { "item": "gtceu:magnesium_diboride_single_wire" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 32,
	      "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } } ] } },

	  { "id": "alloy_smelter/compat_me_cable_fluix_mv_tr", "type": "gtceu:alloy_smelter", "duration": 100,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:aluminium_ingot" } },
	      { "content": { "type": "gtceu:sized", "count": 32, "ingredient": { "item": "GregTechCEuTerraria/meteorite_superconductor_single" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 32,
	      "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } } ] } },

	  { "id": "alloy_smelter/compat_me_cable_fluix_hv_gt", "type": "gtceu:alloy_smelter", "duration": 100,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:stainless_steel_ingot" } },
	      { "content": { "type": "gtceu:sized", "count": 64, "ingredient": { "item": "gtceu:mercury_barium_calcium_cuprate_single_wire" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 512 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 64,
	      "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } } ] } },

	  { "id": "alloy_smelter/compat_me_cable_fluix_hv_tr", "type": "gtceu:alloy_smelter", "duration": 100,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:stainless_steel_ingot" } },
	      { "content": { "type": "gtceu:sized", "count": 64, "ingredient": { "item": "GregTechCEuTerraria/hellstone_superconductor_single" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 512 } ] },
	    "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 64,
	      "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } } ] } }
	]
	""";

	private const string Ae2Machines = """
	[
	  { "id": "crafting_shapeless/compat_me_terminal", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "item": "GregTechCEuTerraria/wood_crate" } },
	      { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_modular_terminal" } } ] } },

	  { "id": "crafting_shapeless/compat_me_crafting_terminal", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "item": "GregTechCEuTerraria/bronze_crate" } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "tag": "gtceu:circuits/lv" } } },
	      { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_crafting_card" } } ] } },

	  { "id": "crafting_shapeless/compat_me_interface", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:bronze_machine_casing" } },
	      { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_interface" } } ] } },

	  { "id": "assembler/compat_me_pattern_encoding_terminal", "type": "gtceu:assembler", "duration": 200,
	    "inputs":  { "item": [
	      { "content": { "item": "GregTechCEuTerraria/steel_crate" } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "tag": "gtceu:circuits/lv" } } },
	      { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_pattern_encoding_card" } } ] } },

	  { "id": "assembler/compat_me_crafting_status_card", "type": "gtceu:assembler", "duration": 200,
	    "inputs":  { "item": [
	      { "content": { "item": "GregTechCEuTerraria/steel_drum" } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "tag": "gtceu:circuits/lv" } } },
	      { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_crafting_status_card" } } ] } },

	  { "id": "assembler/compat_me_pattern_access_terminal", "type": "gtceu:assembler", "duration": 200,
	    "inputs":  { "item": [
	      { "content": { "item": "GregTechCEuTerraria/aluminium_crate" } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "tag": "gtceu:circuits/mv" } } },
	      { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_pattern_access_card" } } ] } },

	  { "id": "assembler/compat_quantum_computer", "type": "gtceu:assembler", "duration": 200,
	    "inputs":  { "item": [
	      { "content": { "item": "gtceu:solid_machine_casing" } },
	      { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "tag": "gtceu:circuits/lv" } } },
	      { "content": { "type": "gtceu:sized", "count": 20, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/quantum_computer" } } ] } },

	  { "id": "assembler/compat_me_storage", "type": "gtceu:assembler", "duration": 200,
	    "inputs":  { "item": [
	      { "content": { "item": "GregTechCEuTerraria/lv_super_chest" } },
	      { "content": { "type": "gtceu:sized", "count": 20, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_storage" } } ] } },

	  { "id": "assembler/compat_me_pattern_provider", "type": "gtceu:assembler", "duration": 200,
	    "inputs":  { "item": [
	      { "content": { "item": "GregTechCEuTerraria/me_interface" } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "tag": "gtceu:circuits/lv" } } },
	      { "content": { "type": "gtceu:sized", "count": 10, "ingredient": { "item": "GregTechCEuTerraria/me_cable_fluix" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_pattern_provider" } } ] } }
	]
	""";

	private const string Money = """
	[
	  { "id": "extractor/compat_extract_silver_coin", "type": "gtceu:extractor", "duration": 40,
	    "inputs":  { "item": [ { "content": { "item": "terraria:SilverCoin" } } ] },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "fluid": [ { "content": { "amount": 1, "value": { "fluid": "gtceu:money" } } } ] } },

	  { "id": "extractor/compat_extract_gold_coin", "type": "gtceu:extractor", "duration": 80,
	    "inputs":  { "item": [ { "content": { "item": "terraria:GoldCoin" } } ] },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "fluid": [ { "content": { "amount": 100, "value": { "fluid": "gtceu:money" } } } ] } },

	  { "id": "extractor/compat_extract_platinum_coin", "type": "gtceu:extractor", "duration": 200,
	    "inputs":  { "item": [ { "content": { "item": "terraria:PlatinumCoin" } } ] },
	    "tickInputs": { "eu": [ { "content": 512 } ] },
	    "outputs": { "fluid": [ { "content": { "amount": 10000, "value": { "fluid": "gtceu:money" } } } ] } },

	  { "id": "fluid_solidifier/compat_money_silver_coin", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 1, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:nugget_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:SilverCoin" } } ] } },

	  { "id": "fluid_solidifier/compat_money_gold_coin", "type": "gtceu:fluid_solidifier", "duration": 80,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 100, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:ingot_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:GoldCoin" } } ] } },

	  { "id": "fluid_solidifier/compat_money_platinum_coin", "type": "gtceu:fluid_solidifier", "duration": 200,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 10000, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:block_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 512 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:PlatinumCoin" } } ] } },

	  { "id": "fluid_solidifier/compat_money_redstone_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 1, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:cylinder_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:redstone_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_iron_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 2, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:anvil_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:iron_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_silver_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 4, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:bottle_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:silver_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_nickel_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 2, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:ball_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:nickel_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_tin_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 1, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:pill_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:tin_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_copper_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 1, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:huge_pipe_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:copper_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_sulfur_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 1, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:tiny_pipe_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:sulfur_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_rubber_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 4, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:small_pipe_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:rubber_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_gold_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 8, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:large_pipe_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:gold_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_gallium_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 100, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:small_gear_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:gallium_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_arsenic_dust", "type": "gtceu:fluid_solidifier", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 100, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:gear_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:arsenic_dust" } } ] } },

	  { "id": "fluid_solidifier/compat_money_fallen_star", "type": "gtceu:fluid_solidifier", "duration": 300,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 100, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:rotor_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 2048 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:FallenStar" } } ] } },

	  { "id": "fluid_solidifier/compat_neutronium_platinum_coin", "type": "gtceu:fluid_solidifier", "duration": 300,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 100, "value": { "fluid": "gtceu:neutronium" } } } ],
	      "item":  [ { "chance": 0, "content": { "item": "gtceu:ball_casting_mold" } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 8192 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:PlatinumCoin" } } ] } },

	  { "id": "fluid_heater/compat_money_to_lava", "type": "gtceu:fluid_heater", "duration": 80,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 100, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 1 } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:lava" } } } ] } },

	  { "id": "fluid_heater/compat_money_to_water", "type": "gtceu:fluid_heater", "duration": 40,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 1, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 2 } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 8 } ] },
	    "outputs": { "fluid": [ { "content": { "amount": 10000, "value": { "fluid": "gtceu:water" } } } ] } },

	  { "id": "fluid_heater/compat_money_to_honey", "type": "gtceu:fluid_heater", "duration": 60,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 10, "value": { "fluid": "gtceu:money" } } } ],
	      "item":  [ { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": 3 } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 32 } ] },
	    "outputs": { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:honey" } } } ] } }
	]
	""";

	private const string Damascus = """
	[
	  { "id": "mixer/compat_damascus_steel_dust", "type": "gtceu:mixer", "duration": 300,
	    "inputs":  {
	      "item":  [ { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "tag": "forge:dusts/steel" } } } ],
	      "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:sulfuric_acid" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 120 } ] },
	    "outputs": { "item": [ { "content": { "item": "gtceu:damascus_steel_dust" } } ] } }
	]
	""";

	private const string Hellforge = """
	[
	  { "id": "assembler/compat_hellforge", "type": "gtceu:assembler", "duration": 200,
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "terraria:Furnace" } } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "tag": "gtceu:circuits/lv" } } },
	      { "content": { "type": "gtceu:sized", "count": 2, "ingredient": { "item": "GregTechCEuTerraria/hellstone_superconductor_double" } } },
	      { "content": { "type": "gtceu:sized", "count": 8, "ingredient": { "item": "terraria:ObsidianBrick" } } }
	    ] },
	    "tickInputs": { "eu": [ { "content": 128 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:Hellforge" } } ] } }
	]
	""";

	private const string HellstoneOre = """
	[
	  { "id": "arc_furnace/compat_hellstone_ore", "type": "gtceu:arc_furnace", "duration": 100,
	    "inputs":  {
	      "item":  [ { "content": { "item": "gtceu:redstone_dust" } } ],
	      "fluid": [ { "content": { "amount": 100, "value": { "tag": "forge:oxygen" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 512 } ] },
	    "outputs": { "item": [ { "content": { "item": "terraria:Hellstone" } } ] } }
	]
	""";
	private const string CreosoteCoating = """
	[
	  { "id": "crafting_shapeless/gel_wood_coating", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "terraria:Gel" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:coal_dust" } } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 4,
	      "ingredient": { "item": "gtceu:wood_coating" } } } ] } },

	  { "id": "fluid_solidifier/compat_creosote_wood_coating", "type": "gtceu:fluid_solidifier", "duration": 300,
	    "inputs": {
	      "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:creosote" } } } ]
	    },
	    "tickInputs": { "eu": [ { "content": 8 } ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 8,
	      "ingredient": { "item": "gtceu:wood_coating" } } } ] } },
	{ "id": "crafting_shapeless/wood_coating_treated_wood_frame", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "gtceu:wood_coating" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_frame" } } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 1,
	      "ingredient": { "item": "gtceu:treated_wood_frame" } } } ] } },
	{ "id": "crafting_shapeless/wood_coating_treated_wood_small_fluid_pipe", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_coating" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_small_fluid_pipe" } } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 1,
	      "ingredient": { "item": "gtceu:treated_wood_small_fluid_pipe" } } } ] } },
	{ "id": "crafting_shapeless/wood_coating_treated_wood_normal_fluid_pipe", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 3, "ingredient": { "item": "gtceu:wood_coating" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_normal_fluid_pipe" } } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 1,
	      "ingredient": { "item": "gtceu:treated_wood_normal_fluid_pipe" } } } ] } },
		  { "id": "crafting_shapeless/wood_coating_treated_wood_large_fluid_pipe", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 6, "ingredient": { "item": "gtceu:wood_coating" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_large_fluid_pipe" } } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 1,
	      "ingredient": { "item": "gtceu:treated_wood_large_fluid_pipe" } } } ] } },
	{ "id": "crafting_shapeless/wood_coating_pump_deck", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 2, "ingredient": { "item": "gtceu:iron_screw" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_plate" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "terraria:StoneSlab" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_coating" } } },
		  { "content": { "tag": "gtceu:tools/crafting_screwdrivers" } },
		  { "content": { "tag": "gtceu:tools/crafting_hammers" } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 2,
	      "ingredient": { "item": "gtceu:pump_deck" } } } ] } },
	{ "id": "crafting_shapeless/wood_coating_primitive_pump", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:iron_ring" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_normal_fluid_pipe" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:iron_screw" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:iron_rotor" } } },
		  { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_plate" } } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_coating" } } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "terraria:StoneSlab" } } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_large_fluid_pipe" } } },
		  { "content": { "tag": "gtceu:tools/crafting_screwdrivers" } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 1,
	      "ingredient": { "item": "gtceu:primitive_pump" } } } ] } },
	{ "id": "crafting_shapeless/wood_coating_pump_hatch", "type": "minecraft:crafting_shapeless",
	    "inputs":  { "item": [
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:iron_screw" } } },
		  { "content": { "type": "gtceu:sized", "count": 2, "ingredient": { "item": "gtceu:iron_ring" } } },
	      { "content": { "type": "gtceu:sized", "count": 2, "ingredient": { "item": "gtceu:wood_plate" } } },
	      { "content": { "type": "gtceu:sized", "count": 2, "ingredient": { "item": "gtceu:wood_coating" } } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "terraria:StoneSlab" } } },
	      { "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:wood_large_fluid_pipe" } } },
		  { "content": { "tag": "gtceu:tools/crafting_screwdrivers" } }
	    ] },
	    "outputs": { "item": [ { "content":{ "type": "gtceu:sized", "count": 1,
	      "ingredient": { "item": "gtceu:pump_hatch" } } } ] } }
	]
	""";

	private static readonly string[] JsonGroups =
	{
		HatchesAndBuses, Bootstrap, Misc, SimplePipes, Casings, Clay,
		TerrariaIntermediates, TerraPrisma, RedstoneGem, Coins, LegacyConversions,
		Ae2FluixCables, Ae2Machines, Money, Damascus, Hellforge, HellstoneOre,
		CreosoteCoating
	};

	public sealed record RecipePatch(string BaseId, string NewId, string MergeJson)
	{
		public bool IsOverride => NewId == BaseId;
	}

	private static RecipePatch Override(string baseId, string mergeJson) => new(baseId, baseId, mergeJson);
	private static RecipePatch Derive(string baseId, string newId, string mergeJson) => new(baseId, newId, mergeJson);

	public static readonly List<RecipePatch> Patches = new()
	{
		// Steam miners, steam macerator remove diamond ingredient
		Override("shaped/steam_miner_bronze", """
			{ "key": { "D": { "tag": null, "item": "gtceu:bronze_rod" } } }
			"""),

		Override("shaped/steam_miner_steel", """
			{ "key": { "D": { "tag": null, "item": "gtceu:steel_rod" } } }
			"""),

		Override("shaped/steam_macerator_bronze", """
			{ "key": { "D": { "tag": null, "item": "gtceu:bronze_rod" } } }
			"""),

		// raw_rubber_dust processing buff
		Override("extractor/raw_rubber_from_resin", """
			{ "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 8,
			  "ingredient": { "item": "gtceu:raw_rubber_dust" } } } ] } }
			"""),

		Override("centrifuge/sticky_resin_separation", """
			{ "outputs": { "item": [
			  { "content": { "type": "gtceu:sized", "count": 8, "ingredient": { "item": "gtceu:raw_rubber_dust" } } },
			  { "chance": 1500, "content": { "type": "gtceu:sized", "count": 1, "ingredient": { "item": "gtceu:plant_ball" } } }
			] } }
			"""),

		Override("extractor/raw_rubber_from_slime", """
			{ "outputs": { "item": [ { "content": { "type": "gtceu:sized", "count": 5,
			  "ingredient": { "item": "gtceu:raw_rubber_dust" } } } ] } }
			"""),

		// Nether star alternative
		Derive("implosion_compressor/implode_dust_nether_star_dynamite",
			"implosion_compressor/compat_implode_mana_crystal_nether_star_dynamite", """
			{ "inputs": { "item": [
			  { "content": { "type": "gtceu:sized", "count": 5, "ingredient": { "item": "terraria:ManaCrystal" } } },
			  { "content": { "type": "gtceu:sized", "count": 2, "ingredient": { "item": "gtceu:dynamite" } } }
			] } }
			"""),

		Derive("implosion_compressor/implode_dust_nether_star_powderbarrel",
			"implosion_compressor/compat_implode_mana_crystal_nether_star_powderbarrel", """
			{ "inputs": { "item": [
			  { "content": { "type": "gtceu:sized", "count": 5, "ingredient": { "item": "terraria:ManaCrystal" } } },
			  { "content": { "type": "gtceu:sized", "count": 8, "ingredient": { "item": "gtceu:powderbarrel" } } }
			] } }
			"""),

		Derive("implosion_compressor/implode_dust_nether_star_tnt",
			"implosion_compressor/compat_implode_mana_crystal_nether_star_tnt", """
			{ "inputs": { "item": [
			  { "content": { "type": "gtceu:sized", "count": 5, "ingredient": { "item": "terraria:ManaCrystal" } } },
			  { "content": { "type": "gtceu:sized", "count": 4, "ingredient": { "item": "minecraft:tnt" } } }
			] } }
			"""),

		// Terraria bottomless bucket infinite liquid
		Derive("extractor/extract_iron_ingot",
			"extractor/compat_extract_bottomless_water", """
			{ "inputs":  { "item":  [ { "chance": 0, "content": { "item": "terraria:BottomlessBucket" } } ] },
			  "outputs": { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:water" } } } ] } }
			"""),

		Derive("extractor/extract_steel_ingot",
			"extractor/compat_extract_bottomless_honey", """
			{ "inputs":  { "item":  [ { "chance": 0, "content": { "item": "terraria:BottomlessHoneyBucket" } } ] },
			  "outputs": { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:honey" } } } ] } }
			"""),

		Derive("extractor/extract_hssg_ingot",
			"extractor/compat_extract_bottomless_lava", """
			{ "inputs":  { "item":  [ { "chance": 0, "content": { "item": "terraria:BottomlessLavaBucket" } } ] },
			  "outputs": { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:lava" } } } ] } }
			"""),

		Derive("extractor/extract_hsse_ingot",
			"extractor/compat_extract_bottomless_shimmer", """
			{ "inputs":     { "item":  [ { "chance": 0, "content": { "item": "terraria:BottomlessShimmerBucket" } } ] },
			  "outputs":    { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:shimmer" } } } ] },
			  "tickInputs": { "eu":    [ { "content": 1920 } ] } }
			"""),

		// Get liquid shimmer
		Derive("extractor/extract_hssg_ingot",
			"extractor/compat_extract_shimmer_block", """
			{ "inputs":  { "item":  [ { "content": { "item": "terraria:ShimmerBlock" } } ] },
			  "outputs": { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:shimmer" } } } ] } }
			"""),

		// Chum Bucket - fermented biomass
		Derive("canner/spray_can_white_dye",
			"extractor/compat_extract_chum_bucket", """
			{ "type": "gtceu:extractor", "category": "gtceu:extractor",
			  "inputs":  { "fluid": null,
			               "item":  [ { "content": { "item": "terraria:ChumBucket" } } ] },
			  "outputs": { "item":  [ { "content": { "item": "terraria:EmptyBucket" } } ],
			               "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:fermented_biomass" } } } ] } }
			"""),

		Derive("canner/spray_can_white_dye",
			"canner/compat_can_chum_bucket", """
			{ "inputs":  { "fluid": [ { "content": { "amount": 1000, "value": { "fluid": "gtceu:fermented_biomass" } } } ],
			               "item":  [ { "content": { "item": "terraria:EmptyBucket" } } ] },
			  "outputs": { "item":  [ { "content": { "item": "terraria:ChumBucket" } } ] } }
			"""),

		// Ultimate Manual Crafting Station
		Derive("electric_blast_furnace/aluminium_from_ruby_dust",
			"electric_blast_furnace/compat_ultimate_manual_station",
			"""{ "inputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/manual_hammer" } }, { "content": { "item": "GregTechCEuTerraria/manual_mallet" } }, { "content": { "item": "GregTechCEuTerraria/manual_knife" } }, { "content": { "item": "GregTechCEuTerraria/manual_file" } }, { "content": { "item": "GregTechCEuTerraria/manual_saw" } }, { "content": { "item": "GregTechCEuTerraria/manual_wrench" } }, { "content": { "item": "GregTechCEuTerraria/manual_screwdriver" } }, { "content": { "item": "GregTechCEuTerraria/manual_wire_cutter" } }, { "content": { "item": "GregTechCEuTerraria/manual_mortar" } }, { "content": { "item": "GregTechCEuTerraria/manual_crowbar" } }, { "content": { "item": "gtceu:nether_star" } } ] }, "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/ultimate_manual" } } ] } }"""),
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

	// spray can to dye
	private static readonly string[] SprayCanColors =
	{
		"black","blue","brown","cyan","gray","green","light_blue","light_gray",
		"lime","magenta","orange","pink","purple","red","white","yellow",
	};

	private static string SprayCanUncanRecipe(string color) =>
		$$"""
		{ "id": "extractor/compat_uncan_{{color}}_dye_spray_can", "type": "gtceu:extractor", "duration": 200,
		  "inputs":  { "item": [ { "content": { "item": "gtceu:{{color}}_dye_spray_can" } } ] },
		  "tickInputs": { "eu": [ { "content": 7 } ] },
		  "outputs": {
		    "fluid": [ { "content": { "amount": 576, "value": { "fluid": "gtceu:{{color}}_dye" } } } ]
		  } }
		""";

	// Terraria superconductors
	private static readonly (byte Size, int Circuit, int InCount, int OutCount, int DurMult)[] WireLadder =
	{
		(1,  1,  1, 2, 1),
		(2,  2,  1, 1, 1),
		(4,  4,  2, 1, 2),
		(8,  8,  4, 1, 4),
		(16, 16, 8, 1, 8),
	};

	private const int SuperconductorBaseDuration = 100;

	private static string SuperconductorWireRecipe(
		SuperconductorWireLoader.ScTier tier,
		(byte Size, int Circuit, int InCount, int OutCount, int DurMult) rung,
		string barItemName, string idSuffix)
	{
		string wireId = SuperconductorWireLoader.WireItemName(tier, rung.Size);
		string sizeWord = WireItem.WireSizeWord(rung.Size);
		int eut = VoltageTiers.VA((int)tier.Tier);
		int dur = SuperconductorBaseDuration * rung.DurMult;
		return $$"""
		{ "id": "wiremill/compat_sc_{{tier.MetalId}}_{{sizeWord}}{{idSuffix}}", "type": "gtceu:wiremill", "duration": {{dur}},
		  "inputs": { "item": [
		    { "content": { "type": "gtceu:sized", "count": {{rung.InCount}}, "ingredient": { "item": "terraria:{{barItemName}}" } } },
		    { "chance": 0, "content": { "type": "gtceu:circuit", "configuration": {{rung.Circuit}} } }
		  ] },
		  "tickInputs": { "eu": [ { "content": {{eut}} } ] },
		  "outputs": { "item": [
		    { "content": { "type": "gtceu:sized", "count": {{rung.OutCount}}, "ingredient": { "item": "GregTechCEuTerraria/{{wireId}}" } } }
		  ] } }
		""";
	}

	private static string SuperconductorCombiningRecipe(
		SuperconductorWireLoader.ScTier tier,
		string idVerb, byte inSize, int inCount, byte outSize, int outCount)
	{
		string inId  = SuperconductorWireLoader.WireItemName(tier, inSize);
		string outId = SuperconductorWireLoader.WireItemName(tier, outSize);
		string inWord = WireItem.WireSizeWord(inSize);
		return $$"""
		{ "id": "crafting_shapeless/compat_sc_{{tier.MetalId}}_{{inWord}}_{{idVerb}}", "type": "minecraft:crafting_shapeless",
		  "inputs": { "item": [
		    { "content": { "type": "gtceu:sized", "count": {{inCount}}, "ingredient": { "item": "GregTechCEuTerraria/{{inId}}" } } }
		  ] },
		  "outputs": { "item": [
		    { "content": { "type": "gtceu:sized", "count": {{outCount}}, "ingredient": { "item": "GregTechCEuTerraria/{{outId}}" } } }
		  ] } }
		""";
	}

	private static IEnumerable<string> SuperconductorCombiningRecipes(SuperconductorWireLoader.ScTier tier)
	{
		var order = SuperconductorWireLoader.Sizes;
		for (int i = 0; i < order.Length; i++)
		{
			if (i < order.Length - 1)
				yield return SuperconductorCombiningRecipe(tier, "doubling", order[i], 2, order[i + 1], 1);
			if (i > 0)
				yield return SuperconductorCombiningRecipe(tier, "splitting", order[i], 1, order[i - 1], 2);
			if (i < order.Length - 2)
				yield return SuperconductorCombiningRecipe(tier, "quadrupling", order[i], 4, order[i + 2], 1);
		}
	}

	public static List<(string Station, GTRecipe Recipe)> Build(IIngredientResolver resolver)
	{
		var result = new List<(string, GTRecipe)>();
		foreach (var group in JsonGroups)
			ParseArray(group, resolver, result);
		foreach (var tier in LampTiers)
			ParseOne(LampRecipe(tier), resolver, result);
		foreach (var tier in SolarPanelTiers)
			ParseOne(SolarPanelRecipe(tier), resolver, result);
		foreach (var color in SprayCanColors)
			ParseOne(SprayCanUncanRecipe(color), resolver, result);

		foreach (var scTier in SuperconductorWireLoader.Tiers)
			foreach (var rung in WireLadder)
			{
				bool multiBar = scTier.BarItemNames.Length > 1;
				foreach (var bar in scTier.BarItemNames)
				{
					string suffix = multiBar ? "_" + bar.ToLowerInvariant() : "";
					ParseOne(SuperconductorWireRecipe(scTier, rung, bar, suffix), resolver, result);
				}
			}

		foreach (var scTier in SuperconductorWireLoader.Tiers)
			foreach (var json in SuperconductorCombiningRecipes(scTier))
				ParseOne(json, resolver, result);

		foreach (var json in Ae2ColoredCableRecipes())
			ParseOne(json, resolver, result);

		return result;
	}

	private static IEnumerable<string> Ae2ColoredCableRecipes()
	{
		foreach (var color in AEColors.VALID_COLORS)
		{
			string prefix = color.RegistryPrefix();
			yield return $$"""
			{ "id": "crafting_shapeless/compat_me_cable_fluix_to_{{prefix}}", "type": "minecraft:crafting_shapeless",
			  "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/me_cable_fluix" } } ] },
			  "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_cable_{{prefix}}" } } ] } }
			""";
			yield return $$"""
			{ "id": "crafting_shapeless/compat_me_cable_{{prefix}}_to_fluix", "type": "minecraft:crafting_shapeless",
			  "inputs":  { "item": [ { "content": { "item": "GregTechCEuTerraria/me_cable_{{prefix}}" } } ] },
			  "outputs": { "item": [ { "content": { "item": "GregTechCEuTerraria/me_cable_fluix" } } ] } }
			""";
		}
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

	public static (string Station, GTRecipe Recipe) MaterializePatch(
		RecipePatch patch, string baseRawJson, IIngredientResolver resolver)
	{
		var merged = JsonNode.Parse(baseRawJson)!.AsObject();
		var delta = JsonNode.Parse(patch.MergeJson)!.AsObject();
		MergeInto(merged, delta);
		merged["id"] = patch.NewId;

		using var doc = JsonDocument.Parse(merged.ToJsonString());
		var el = doc.RootElement;
		var recipe = VanillaRecipeJson.IsVanillaShape(el)
			? VanillaRecipeJson.Read(el, resolver, patch.NewId)
			: GTRecipeSerializer.Read(el, resolver, patch.NewId);
		return (recipe.RecipeType.RegistryName, recipe);
	}

	// JSON Merge Patch (RFC 7386)
	private static void MergeInto(JsonObject target, JsonObject patch)
	{
		foreach (var (key, value) in patch)
		{
			if (value is null) { target.Remove(key); continue; }
			if (value is JsonObject patchChild && target[key] is JsonObject targetChild)
				MergeInto(targetChild, patchChild);
			else
				target[key] = value.DeepClone();
		}
	}

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
