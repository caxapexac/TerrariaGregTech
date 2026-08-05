#nullable enable
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public static class StationSounds
{
	private static SoundStyle Loop(string assetName, float volume = 0.45f) =>
		new($"GregTechCEuTerraria/Content/Sounds/{assetName}")
		{
			Volume = volume,
			IsLooped = true,
			MaxInstances = 3,
			SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
			PauseBehavior = PauseBehavior.PauseWithGame,
		};

	private static SoundStyle OneShot(string assetName, float volume = 0.6f) =>
		new($"GregTechCEuTerraria/Content/Sounds/{assetName}")
		{
			Volume = volume,
			IsLooped = false,
			MaxInstances = 3,
			SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
		};

	public static readonly IReadOnlyDictionary<string, SoundStyle> LoopForStation = new Dictionary<string, SoundStyle>
	{
		["alloy_smelter"]             = Loop("furnace"),
		["electric_furnace"]          = Loop("furnace"),
		["steam_boiler"]              = Loop("furnace"),
		["autoclave"]                 = Loop("furnace"),
		["macerator"]                 = Loop("macerator"),
		["assembler"]                 = Loop("assembler"),
		["packer"]                    = Loop("assembler"),
		["circuit_assembler"]         = Loop("assembler"),
		["brewery"]                   = Loop("chemical"),
		["chemical_reactor"]          = Loop("chemical"),
		["fermenter"]                 = Loop("chemical"),
		["chemical_bath"]             = Loop("bath"),
		["canner"]                    = Loop("bath"),
		["mixer"]                     = Loop("mixer"),
		["ore_washer"]                = Loop("bath"),
		["compressor"]                = Loop("compressor"),
		["extruder"]                  = Loop("compressor"),
		["forming_press"]             = Loop("compressor"),
		["forge_hammer"]              = Loop("forge_hammer"),
		["extractor"]                 = Loop("compressor"),
		["cutter"]                    = Loop("cut"),
		["lathe"]                     = Loop("cut"),
		["electrolyzer"]              = Loop("electrolyzer"),
		["arc_furnace"]               = Loop("arc"),
		["plasma_arc_furnace"]        = Loop("arc"),
		["polarizer"]                 = Loop("arc"),
		["electromagnetic_separator"] = Loop("arc"),
		["laser_engraver"]            = Loop("electrolyzer"),
		["scanner"]                   = Loop("electrolyzer"),
		["motor"]                     = Loop("motor"),
		["bender"]                    = Loop("motor"),
		["wiremill"]                  = Loop("motor"),
		["boiler"]                    = Loop("boiler"),
		["coal_boiler"]               = Loop("boiler"),
		["distillery"]                = Loop("boiler"),
		["fluid_heater"]              = Loop("boiler"),
		["fluid_solidifier"]          = Loop("cooling"),
		["gas_collector"]             = Loop("cooling"),
		["air_scrubber"]              = Loop("cooling"),
		["vacuum_freezer"]            = Loop("cooling"),
		["centrifuge"]                = Loop("centrifuge"),
		["thermal_centrifuge"]        = Loop("centrifuge"),
		["sifter"]                    = Loop("centrifuge"),
		["steam_turbine"]             = Loop("turbine"),
		["gas_turbine"]               = Loop("turbine"),
		["plasma_generator"]          = Loop("turbine"),
		["combustion_generator"]      = Loop("combustion"),
		["rock_breaker"]              = Loop("fire"),
		["research_station"]          = Loop("computation"),
		["electric_blast_furnace"]    = Loop("furnace"),
		["alloy_blast_smelter"]       = Loop("furnace"),
		["large_boiler"]              = Loop("furnace"),
		["coke_oven"]                 = Loop("fire"),
		["primitive_blast_furnace"]   = Loop("fire"),
		["pyrolyse_oven"]             = Loop("fire"),
		["cracker"]                   = Loop("fire"),
		["distillation_tower"]        = Loop("chemical"),
		["large_chemical_reactor"]    = Loop("chemical"),
		["fusion_reactor"]            = Loop("arc"),
		["assembly_line"]             = Loop("assembler"),
		["implosion_compressor"]      = SoundID.Item14 with
		{
			Volume             = 0.55f,
			IsLooped           = true,
			MaxInstances       = 3,
			SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
			PauseBehavior      = PauseBehavior.PauseWithGame,
		},
	};

	public static readonly SoundStyle DefaultFinish = OneShot("furnace", 0.5f);

	public static SoundStyle? TryGetLoop(string stationId) =>
		LoopForStation.TryGetValue(stationId, out var s) ? s : null;
}
