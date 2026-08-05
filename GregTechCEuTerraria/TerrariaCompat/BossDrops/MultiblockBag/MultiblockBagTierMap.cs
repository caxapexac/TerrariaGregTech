#nullable enable
using System.Collections.Generic;

namespace GregTechCEuTerraria.TerrariaCompat.BossDrops.MultiblockBag;

public static class MultiblockBagTierMap
{
	public const int DefaultTier = 2; // MV

	private static readonly Dictionary<string, int> _tiers = new()
	{
		// === Steam (0) - King Slime ========================================
		["coke_oven"]                = 0,
		["primitive_blast_furnace"]  = 0,
		["primitive_pump"]           = 0,
		["bronze_large_boiler"]      = 0,
		["steam_grinder"]            = 0,
		["steam_oven"]               = 0,
		["wooden_multiblock_tank"]   = 0,
		["bronze_multiblock_tank"]   = 0,

		// === LV (1) - EoC / EoW / BoC / Deerclops ==========================
		["steel_large_boiler"]       = 1,
		["steel_multiblock_tank"]    = 1,

		// === MV (2) - Queen Bee / Skeletron / WoF ==========================
		["electric_blast_furnace"]   = 2,
		["multi_smelter"]            = 2,
		["vacuum_freezer"]           = 2,
		["large_chemical_reactor"]   = 2,
		["pyrolyse_oven"]            = 2,
		["cracker"]                  = 2,
		["mv_fluid_drilling_rig"]    = 2,

		// === HV (3) - Queen Slime / Pirates / Mech Bosses =================
		["implosion_compressor"]     = 3,
		["large_autoclave"]          = 3,
		["distillation_tower"]       = 3,
		["large_distillery"]         = 3,
		["cleanroom"]                = 3,
		["alloy_blast_smelter"]      = 3,
		["titanium_large_boiler"]    = 3,
		["steam_large_turbine"]      = 3,
		["hv_fluid_drilling_rig"]    = 3,
		["large_centrifuge"]         = 3,
		["large_electrolyzer"]       = 3,
		["large_electromagnet"]      = 3,
		["large_packer"]             = 3,
		["large_assembler"]          = 3,
		["large_circuit_assembler"]  = 3,
		["large_arc_smelter"]        = 3,
		["large_engraving_laser"]    = 3,
		["large_sifting_funnel"]     = 3,
		["large_material_press"]     = 3,
		["large_brewer"]             = 3,
		["large_cutter"]             = 3,
		["large_extractor"]          = 3,
		["large_extruder"]           = 3,
		["large_solidifier"]         = 3,
		["large_wiremill"]           = 3,
		["large_chemical_bath"]      = 3,
		["large_maceration_tower"]   = 3,
		["large_mixer"]              = 3,

		// === EV (4) - Plantera =============================================
		["large_combustion_engine"]  = 4,
		["gas_large_turbine"]        = 4,
		["ev_large_miner"]           = 4,
		["ev_fluid_drilling_rig"]    = 4,
		["mega_vacuum_freezer"]      = 4,

		// === IV (5) - Pumpkin Moon / Frost Moon ============================
		["extreme_combustion_engine"]= 5,
		["plasma_large_turbine"]     = 5,
		["tungstensteel_large_boiler"]= 5,
		["assembly_line"]            = 5,
		["mega_blast_furnace"]       = 5,
		["iv_large_miner"]           = 5,

		// === LuV (6) - Golem / Martian Saucer ==============================
		["luv_large_miner"]          = 6,
		["luv_fusion_reactor"]       = 6,
		// Research cluster - LuV-buildable entry pieces (data storage + a
		// basic HPCA on EV/LuV computation components).
		["data_bank"]                = 6,
		["high_performance_computation_array"] = 6,

		// === ZPM (7) - Duke / Cultist ======================================
		["zpm_fusion_reactor"]       = 7,
		["research_station"]         = 7,
		["network_switch"]           = 7,

		// === UV (8) - Pillars / EoL / Moon Lord ============================
		["uv_fusion_reactor"]        = 8,
		["active_transformer"]       = 8,
		["power_substation"]         = 8,
	};

	public static bool HasTier(string multiId) => _tiers.ContainsKey(multiId);

	public static int GetTier(string multiId) =>
		_tiers.TryGetValue(multiId, out var t) ? t : DefaultTier;
}
