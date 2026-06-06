#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Pattern;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

// Mirrors upstream registerSimpleMachines data rows.
public static class MachineDefinitions
{
	private static readonly VoltageTier[] AllTiers = (VoltageTier[])Enum.GetValues(typeof(VoltageTier));

	// Steam boilers aren't voltage-tiered - they register a single variant
	private static readonly VoltageTier[] OneTier = { VoltageTier.LV };
	private static readonly VoltageTier[] MaxTier = { VoltageTier.MAX };

	// Transformer steps tier<->tier+1
	private static readonly VoltageTier[] TransformerTiers =
	{
		VoltageTier.ULV, VoltageTier.LV,  VoltageTier.MV,  VoltageTier.HV,
		VoltageTier.EV,  VoltageTier.IV,  VoltageTier.LuV, VoltageTier.ZPM,
		VoltageTier.UV,  VoltageTier.UHV, VoltageTier.UEV, VoltageTier.UIV,
		VoltageTier.UXV, VoltageTier.OpV,
	};

	// The Quantum Tank / Quantum Chest family splits its voltage range across
	// two upstream ids: LV..EV register as `super_tank` / `super_chest`,
	// IV..OpV as `quantum_tank` / `quantum_chest`
	private static readonly VoltageTier[] SuperTiers =
		{ VoltageTier.LV, VoltageTier.MV, VoltageTier.HV, VoltageTier.EV };

	private static readonly VoltageTier[] QuantumTiers =
	{
		VoltageTier.IV,  VoltageTier.LuV, VoltageTier.ZPM, VoltageTier.UV,
		VoltageTier.UHV, VoltageTier.UEV, VoltageTier.UIV, VoltageTier.UXV,
		VoltageTier.OpV,
	};

	private static readonly VoltageTier[] GeneratorTiers =
		{ VoltageTier.LV, VoltageTier.MV, VoltageTier.HV };

	private static readonly VoltageTier[] FisherTiers =
	{
		VoltageTier.LV, VoltageTier.MV, VoltageTier.HV,
		VoltageTier.EV, VoltageTier.IV, VoltageTier.LuV,
	};

	private static readonly VoltageTier[] WorldAcceleratorTiers =
	{
		VoltageTier.LV,  VoltageTier.MV,  VoltageTier.HV, VoltageTier.EV,
		VoltageTier.IV,  VoltageTier.LuV, VoltageTier.ZPM, VoltageTier.UV,
	};

	private static readonly VoltageTier[] BlockBreakerTiers =
	{
		VoltageTier.LV,  VoltageTier.MV, VoltageTier.HV,
		VoltageTier.EV,  VoltageTier.IV, VoltageTier.LuV,
		VoltageTier.ZPM,
	};

	private static readonly VoltageTier[] MultiHatchTiers =
	{
		VoltageTier.EV,  VoltageTier.IV,  VoltageTier.LuV, VoltageTier.ZPM,
		VoltageTier.UV,  VoltageTier.UHV, VoltageTier.UEV, VoltageTier.UIV,
		VoltageTier.UXV, VoltageTier.OpV, VoltageTier.MAX,
	};

	private static readonly VoltageTier[] DualHatchTiers =
	{
		VoltageTier.LuV, VoltageTier.ZPM, VoltageTier.UV,  VoltageTier.UHV,
		VoltageTier.UEV, VoltageTier.UIV, VoltageTier.UXV, VoltageTier.OpV,
		VoltageTier.MAX,
	};

	private static readonly VoltageTier[] LaserHatchTiers =
	{
		VoltageTier.IV,  VoltageTier.LuV, VoltageTier.ZPM, VoltageTier.UV,
		VoltageTier.UHV, VoltageTier.UEV, VoltageTier.UIV, VoltageTier.UXV,
		VoltageTier.OpV, VoltageTier.MAX,
	};

	private static readonly VoltageTier[] ElectricTiers =
	{
		VoltageTier.LV,  VoltageTier.MV,  VoltageTier.HV,  VoltageTier.EV,
		VoltageTier.IV,  VoltageTier.LuV, VoltageTier.ZPM, VoltageTier.UV,
		VoltageTier.UHV, VoltageTier.UEV, VoltageTier.UIV, VoltageTier.UXV,
		VoltageTier.OpV,
	};

	// Used by the steam macerator, steam grinder/oven, and the simple generators
	private static Func<VoltageTier, IReadOnlyDictionary<object, int>> ConstOutputLimit(
		params (object cap, int n)[] entries)
	{
		var d = new Dictionary<object, int>();
		foreach (var (cap, n) in entries) d[cap] = n;
		IReadOnlyDictionary<object, int> ro = d;
		return _ => ro;
	}

	private static readonly Func<VoltageTier, IReadOnlyDictionary<object, int>> MaceratorOutputLimit =
		tier => new Dictionary<object, int>
		{
			[ItemRecipeCapability.CAP] = (int)tier switch { 1 or 2 => 1, 3 => 3, _ => 4 },
		};

	private static readonly Func<VoltageTier, IReadOnlyDictionary<object, int>> GeneratorOutputLimit =
		ConstOutputLimit((ItemRecipeCapability.CAP, 0), (FluidRecipeCapability.CAP, 0));

	public static void RegisterAll()
	{
		MachineRegistry.Clear();

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "machine_hull", Label = "Machine Hull",
			Family = MachineFamily.Hull,
			Tiers = AllTiers,
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			TintedOverlayBasename = "overlay_energy_1a_tinted",
			OverlayBasename       = "overlay_energy_1a_out",
			LayoutKey = "none",
		});

		Wtm("macerator",   "Macerator",   GTRecipeTypes.MACERATOR, 1, 4, outputLimits: MaceratorOutputLimit);
		Wtm("alloy_smelter", "Alloy Smelter", GTRecipeTypes.ALLOY_SMELTER, 2, 1, circuit: true);
		Wtm("brewery",     "Brewery",     GTRecipeTypes.BREWERY,   1, 0, inTanks: 1, outTanks: 1);
		Wtm("compressor",  "Compressor",  GTRecipeTypes.COMPRESSOR, 1, 1);
		Wtm("extractor",   "Extractor",   GTRecipeTypes.EXTRACTOR,  1, 1, outTanks: 1);
		Wtm("forge_hammer", "Forge Hammer", GTRecipeTypes.FORGE_HAMMER, 1, 1);
		Wtm("bender",      "Bender",      GTRecipeTypes.BENDER,     2, 1, circuit: true);
		Wtm("lathe",       "Lathe",       GTRecipeTypes.LATHE,      1, 2);
		Wtm("polarizer",   "Polarizer",   GTRecipeTypes.POLARIZER,  1, 1);
		Wtm("wiremill",    "Wiremill",    GTRecipeTypes.WIREMILL,   2, 1, circuit: true);
		Wtm("electromagnetic_separator", "Electromagnetic Separator", GTRecipeTypes.ELECTROMAGNETIC_SEPARATOR, 1, 3);
		Wtm("packer",      "Packer",      GTRecipeTypes.PACKER,     2, 2, circuit: true);
		Wtm("fluid_solidifier", "Fluid Solidifier", GTRecipeTypes.FLUID_SOLIDIFIER, 1, 1, inTanks: 1);
		Wtm("chemical_reactor", "Chemical Reactor", GTRecipeTypes.CHEMICAL_REACTOR, 2, 2, inTanks: 3, outTanks: 2, circuit: true);
		Wtm("electrolyzer", "Electrolyzer", GTRecipeTypes.ELECTROLYZER, 2, 6, inTanks: 1, outTanks: 6, circuit: true);
		Wtm("centrifuge",  "Centrifuge",  GTRecipeTypes.CENTRIFUGE, 2, 6, inTanks: 1, outTanks: 6, circuit: true);
		Wtm("arc_furnace",        "Arc Furnace",        GTRecipeTypes.ARC_FURNACE,        1, 4, inTanks: 1, outTanks: 1);
		Wtm("cutter",             "Cutter",             GTRecipeTypes.CUTTER,             1, 2, inTanks: 1);
		Wtm("extruder",           "Extruder",           GTRecipeTypes.EXTRUDER,           2, 1);
		Wtm("scanner",            "Scanner",            GTRecipeTypes.SCANNER,            2, 1, inTanks: 1);
		Wtm("laser_engraver",     "Laser Engraver",     GTRecipeTypes.LASER_ENGRAVER,     2, 1);
		Wtm("sifter",             "Sifter",             GTRecipeTypes.SIFTER,             1, 6);
		Wtm("thermal_centrifuge", "Thermal Centrifuge", GTRecipeTypes.THERMAL_CENTRIFUGE, 1, 3);
		Wtm("gas_collector",      "Gas Collector",      GTRecipeTypes.GAS_COLLECTOR,      1, 0, outTanks: 1, circuit: true);
		Wtm("air_scrubber",       "Air Scrubber",       GTRecipeTypes.AIR_SCRUBBER,       1, 3, inTanks: 1, outTanks: 3);
		Wtm("electric_furnace",   "Electric Furnace",   GTRecipeTypes.ELECTRIC_FURNACE,   1, 1);
		Wtm("rock_crusher",       "Rock Crusher",       GTRecipeTypes.ROCK_BREAKER,       1, 4);
		Wtm("distillery",  "Distillery",  GTRecipeTypes.DISTILLERY, 1, 1, inTanks: 1, outTanks: 1, circuit: true);
		Wtm("mixer",       "Mixer",       GTRecipeTypes.MIXER,      6, 1, inTanks: 2, outTanks: 1, circuit: true);
		Wtm("autoclave",   "Autoclave",   GTRecipeTypes.AUTOCLAVE,  2, 2, inTanks: 1, outTanks: 1);
		Wtm("ore_washer",  "Ore Washer",  GTRecipeTypes.ORE_WASHER, 2, 3, inTanks: 1, circuit: true);
		Wtm("chemical_bath", "Chemical Bath", GTRecipeTypes.CHEMICAL_BATH, 1, 6, inTanks: 1, outTanks: 1);
		Wtm("fluid_heater", "Fluid Heater", GTRecipeTypes.FLUID_HEATER, 1, 0, inTanks: 1, outTanks: 1, circuit: true);
		Wtm("fermenter",   "Fermenter",   GTRecipeTypes.FERMENTER,  1, 1, inTanks: 1, outTanks: 1);
		Wtm("canner",      "Canner",      GTRecipeTypes.CANNER,     2, 2, inTanks: 1, outTanks: 1, circuit: true);
		Wtm("assembler",   "Assembler",   GTRecipeTypes.ASSEMBLER,  9, 1, inTanks: 1, circuit: true);
		Wtm("forming_press", "Forming Press", GTRecipeTypes.FORMING_PRESS, 6, 1, circuit: true);
		Wtm("circuit_assembler", "Circuit Assembler", GTRecipeTypes.CIRCUIT_ASSEMBLER, 6, 1, inTanks: 1, circuit: true);

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "steam_turbine", Label = "Steam Turbine",
			Family = MachineFamily.SimpleGenerator,
			Tiers = AllTiers,
			RecipeType = GTRecipeTypes.STEAM_TURBINE,
			InputSlotCount = 0, OutputSlotCount = 0,
			InputFluidTankCount = 1, OutputFluidTankCount = 1,
			OutputLimits = GeneratorOutputLimit,
			OverlayDir = "block/generators/steam_turbine", OverlayBasename = "overlay_side",
			LayoutKey = "steam_turbine",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "gas_turbine", Label = "Gas Turbine",
			Family = MachineFamily.SimpleGenerator,
			Tiers = GeneratorTiers,
			RecipeType = GTRecipeTypes.GAS_TURBINE,
			InputSlotCount = 0, OutputSlotCount = 0,
			InputFluidTankCount = 1, OutputFluidTankCount = 0,
			OutputLimits = GeneratorOutputLimit,
			OverlayDir = "block/generators/gas_turbine", OverlayBasename = "overlay_side",
			LayoutKey = "steam_turbine",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "combustion", Label = "Combustion Generator",
			Family = MachineFamily.SimpleGenerator,
			Tiers = GeneratorTiers,
			RecipeType = GTRecipeTypes.COMBUSTION,
			InputSlotCount = 0, OutputSlotCount = 0,
			InputFluidTankCount = 1, OutputFluidTankCount = 0,
			OutputLimits = GeneratorOutputLimit,
			OverlayDir = "block/generators/combustion", OverlayBasename = "overlay_top",
			LayoutKey = "steam_turbine",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "lp_steam_solid_boiler", Label = "Coal Boiler",
			Family = MachineFamily.SteamSolidBoiler,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.STEAM_BOILER,
			IsHighPressure = false,
			Casing = MachineCasing.BrickedBronze,
			OverlayDir = "block/generators/boiler/coal",
			LayoutKey = "coal_boiler",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "hp_steam_solid_boiler", Label = "HP Coal Boiler",
			Family = MachineFamily.SteamSolidBoiler,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.STEAM_BOILER,
			IsHighPressure = true,
			Casing = MachineCasing.BrickedSteel,
			OverlayDir = "block/generators/boiler/coal",
			LayoutKey = "coal_boiler",
		});

		SolarBoiler("lp_steam_solar_boiler", "Solar Boiler",    highPressure: false);
		SolarBoiler("hp_steam_solar_boiler", "HP Solar Boiler", highPressure: true);
		LiquidBoiler("lp_steam_liquid_boiler", "Liquid Boiler",    highPressure: false);
		LiquidBoiler("hp_steam_liquid_boiler", "HP Liquid Boiler", highPressure: true);

		Steam("macerator",     "Macerator",     GTRecipeTypes.MACERATOR,        1, 4, outputLimits: ConstOutputLimit((ItemRecipeCapability.CAP, 1)));
		Steam("compressor",    "Compressor",    GTRecipeTypes.COMPRESSOR,       1, 1);
		Steam("extractor",     "Extractor",     GTRecipeTypes.EXTRACTOR,        1, 1);
		Steam("alloy_smelter", "Alloy Smelter", GTRecipeTypes.ALLOY_SMELTER,    2, 1);
		Steam("forge_hammer",  "Forge Hammer",  GTRecipeTypes.FORGE_HAMMER,     1, 1);
		Steam("furnace",       "Furnace",       GTRecipeTypes.ELECTRIC_FURNACE, 1, 1);
		Steam("rock_crusher",  "Rock Crusher",  GTRecipeTypes.ROCK_BREAKER,     1, 4);

		SteamMiner("lp_steam_miner", "Steam Miner",    overlayDir: "block/machines/miner", highPressure: false);
		SteamMiner("hp_steam_miner", "HP Steam Miner", overlayDir: "block/machines/miner", highPressure: true);

		Battery("battery_buffer_4x",  "4x Battery Buffer",  4,  BatteryBufferAmps.Normal,  outputAmps: 4);
		Battery("battery_buffer_8x",  "8x Battery Buffer",  8,  BatteryBufferAmps.Normal,  outputAmps: 8);
		Battery("battery_buffer_16x", "16x Battery Buffer", 16, BatteryBufferAmps.Normal,  outputAmps: 16);
		Battery("charger_4x",         "Turbo Charger",      4,  BatteryBufferAmps.Charger, outputAmps: 0);

		Transformer("transformer_1a",  "Transformer",              1);
		Transformer("transformer_2a",  "Hi-Amp (2x) Transformer",  2);
		Transformer("transformer_4a",  "Hi-Amp (4x) Transformer",  4);
		Transformer("transformer_16a", "Power Transformer",        16);

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "solar_panel_machine", Label = "Solar Panel",
			Family = MachineFamily.SolarPanel,
			Tiers = AllTiers,
			Casing = MachineCasing.None,
			OverlayDir = "block/generators/solar",
			LayoutKey = "none",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "lamp", Label = "Lamp",
			Family = MachineFamily.Lamp,
			Tiers = AllTiers,
			OverlayDir = "block", OverlayBasename = "yellow_lamp",
			LayoutKey = "none",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "long_distance_item_pipeline_endpoint", Label = "Long Distance Item Pipeline Endpoint",
			Family = MachineFamily.LongDistanceItemEndpoint,
			Tiered = false, Tiers = OneTier,
			OverlayDir = "block/overlay/machine", OverlayBasename = "overlay_item_hatch_input",
			LayoutKey = "none",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "long_distance_fluid_pipeline_endpoint", Label = "Long Distance Fluid Pipeline Endpoint",
			Family = MachineFamily.LongDistanceFluidEndpoint,
			Tiered = false, Tiers = OneTier,
			OverlayDir = "block/overlay/machine", OverlayBasename = "overlay_fluid_hatch_input",
			LayoutKey = "none",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "fisher", Label = "Fisher",
			Family = MachineFamily.Fisher,
			Tiers = FisherTiers,
			OverlayDir = "block/overlay/machine",
			OverlayBasename = "overlay_screen",
			EmissiveOverlayBasename = "overlay_qtank_emissive",
			LayoutKey = "fisher",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "world_accelerator", Label = "World Accelerator",
			Family = MachineFamily.WorldAccelerator,
			Tiers = WorldAcceleratorTiers,             // LV..UV
			OverlayDir = "block/machines/world_accelerator",
			OverlayBasename = "overlay_front",
			LayoutKey = "world_accelerator",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "block_breaker", Label = "Block Breaker",
			Family = MachineFamily.BlockBreaker,
			Tiers = BlockBreakerTiers,                 // LV..ZPM
			OverlayDir = "block/machines/block_breaker",
			OverlayBasename = "overlay_front",
			LayoutKey = "block_breaker",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "miner", Label = "Miner",
			Family = MachineFamily.Miner,
			Tiers = new[] { VoltageTier.LV, VoltageTier.MV, VoltageTier.HV, VoltageTier.EV },
			OverlayDir = "block/machines/miner",
			OverlayBasename = "overlay_front",
			LayoutKey = "miner",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "pump", Label = "Pump",
			Family = MachineFamily.Pump,
			Tiers = new[] { VoltageTier.LV, VoltageTier.MV, VoltageTier.HV, VoltageTier.EV },
			OverlayDir = "block/overlay/machine",
			OverlayBasename = "overlay_adv_pump",
			LayoutKey = "pump",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "item_collector", Label = "Item Collector",
			Family = MachineFamily.ItemCollector,
			Tiers = new[] { VoltageTier.LV, VoltageTier.MV, VoltageTier.HV, VoltageTier.EV },
			OverlayDir = "block/machines/item_collector",
			OverlayBasename = "overlay_top",
			LayoutKey = "item_collector",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "super_tank", Label = "Super Tank",
			Family = MachineFamily.SuperTank,
			Tiers = SuperTiers,
			OverlayDir = "block/overlay/machine", OverlayBasename = "overlay_qtank",
			LayoutKey = "super_tank",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "quantum_tank", Label = "Quantum Tank",
			Family = MachineFamily.SuperTank,
			Tiers = QuantumTiers,
			OverlayDir = "block/overlay/machine", OverlayBasename = "overlay_qtank",
			LayoutKey = "super_tank",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "super_chest", Label = "Super Chest",
			Family = MachineFamily.SuperChest,
			Tiers = SuperTiers,
			OverlayDir = "block/overlay/machine", OverlayBasename = "overlay_qchest",
			LayoutKey = "super_chest",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "quantum_chest", Label = "Quantum Chest",
			Family = MachineFamily.SuperChest,
			Tiers = QuantumTiers,
			OverlayDir = "block/overlay/machine", OverlayBasename = "overlay_qchest",
			LayoutKey = "super_chest",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "creative_chest", Label = "Creative Chest",
			Family = MachineFamily.CreativeChest,
			Tiered = false, Tiers = MaxTier,
			OverlayDir = "block/overlay/machine",
			OverlayBasename = "overlay_creativecontainer",
			EmissiveOverlayBasename = "overlay_creativecontainer_emissive",
			LayoutKey = "creative_chest",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "creative_tank", Label = "Creative Tank",
			Family = MachineFamily.CreativeTank,
			Tiered = false, Tiers = MaxTier,
			OverlayDir = "block/overlay/machine",
			OverlayBasename = "overlay_creativecontainer",
			EmissiveOverlayBasename = "overlay_creativecontainer_emissive",
			LayoutKey = "creative_tank",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "creative_energy", Label = "Creative Energy Container",
			Family = MachineFamily.CreativeEnergy,
			Tiered = false, Tiers = MaxTier,
			OverlayDir = "block/overlay/machine",
			OverlayBasename = "",
			EmissiveOverlayBasename = "overlay_energy_emitter",
			LayoutKey = "creative_energy",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "coke_oven", Label = "Coke Oven",
			Family = MachineFamily.MultiblockCokeOven,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.COKE_OVEN,
			Casing          = MachineCasing.CokeBricks,
			OverlayDir      = "block/multiblock/coke_oven",
			OverlayBasename = "overlay_front",
			LayoutKey       = "coke_oven",
			PatternFactory  = BuildCokeOvenPattern,
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "primitive_blast_furnace", Label = "Primitive Blast Furnace",
			Family = MachineFamily.MultiblockPrimitiveBlastFurnace,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.PRIMITIVE_BLAST_FURNACE,
			Casing          = MachineCasing.Firebricks,
			OverlayDir      = "block/multiblock/primitive_blast_furnace",
			OverlayBasename = "overlay_front",
			LayoutKey       = "primitive_blast_furnace",
			PatternFactory  = BuildPrimitiveBlastFurnacePattern,
			MultiRecipeModifier = GTRecipeModifiers.PRIMITIVE_BLAST_FURNACE_SPEEDUP,
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "pump_hatch", Label = "Pump Hatch",
			Family = MachineFamily.PumpHatch,
			Tiered = false, Tiers = new[] { VoltageTier.ULV },
			PartIo = IO.OUT,
			PartFluidSlots = 1,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.PUMP_FLUID_HATCH },
			Casing = MachineCasing.PumpDeck,
			OverlayDir = PartOverlayDir,
			OverlayBasename = "overlay_fluid_hatch_output",
			LayoutKey = "fluid_hatch",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "primitive_pump", Label = "Primitive Water Pump",
			Family = MachineFamily.MultiblockPrimitivePump,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.DUMMY,
			Casing          = MachineCasing.PumpDeck,
			OverlayDir      = "block/multiblock/primitive_pump",
			OverlayBasename = "overlay_front",
			LayoutKey       = "primitive_pump",
			PatternFactory  = BuildPrimitivePumpPattern,
		});

		LargeBoiler("bronze",        "Large Bronze Boiler",        "steam_machine_casing",   "bronze_pipe_casing",        "bronze_firebox_casing",        800,  4);
		LargeBoiler("steel",         "Large Steel Boiler",         "solid_machine_casing",   "steel_pipe_casing",         "steel_firebox_casing",        1300,  6);
		LargeBoiler("titanium",      "Large Titanium Boiler",      "stable_machine_casing",  "titanium_pipe_casing",      "titanium_firebox_casing",     2000,  8);
		LargeBoiler("tungstensteel", "Large Tungstensteel Boiler", "robust_machine_casing",  "tungstensteel_pipe_casing", "tungstensteel_firebox_casing", 3000, 12);

		ItemBus("input_bus",  "Input Bus",  IO.IN,  AllTiers);
		ItemBus("output_bus", "Output Bus", IO.OUT, AllTiers);

		FluidHatchDef("input_hatch",     "Input Hatch",            IO.IN,  1, AllTiers);
		FluidHatchDef("input_hatch_4x",  "Quadruple Input Hatch",  IO.IN,  4, MultiHatchTiers);
		FluidHatchDef("input_hatch_9x",  "Nonuple Input Hatch",    IO.IN,  9, MultiHatchTiers);
		FluidHatchDef("output_hatch",    "Output Hatch",           IO.OUT, 1, AllTiers);
		FluidHatchDef("output_hatch_4x", "Quadruple Output Hatch", IO.OUT, 4, MultiHatchTiers);
		FluidHatchDef("output_hatch_9x", "Nonuple Output Hatch",   IO.OUT, 9, MultiHatchTiers);

		DualHatch("dual_input_hatch",  "Dual Input Hatch",  IO.IN,  DualHatchTiers);
		DualHatch("dual_output_hatch", "Dual Output Hatch", IO.OUT, DualHatchTiers);

		EnergyHatchDef("energy_input_hatch",         "Energy Hatch",            IO.IN,  2,  AllTiers);
		EnergyHatchDef("energy_output_hatch",        "Dynamo Hatch",            IO.OUT, 2,  AllTiers);
		EnergyHatchDef("energy_input_hatch_4a",      "4A Energy Hatch",         IO.IN,  4,  MultiHatchTiers);
		EnergyHatchDef("energy_output_hatch_4a",     "4A Dynamo Hatch",         IO.OUT, 4,  MultiHatchTiers);
		EnergyHatchDef("energy_input_hatch_16a",     "16A Energy Hatch",        IO.IN,  16, MultiHatchTiers);
		EnergyHatchDef("energy_output_hatch_16a",    "16A Dynamo Hatch",        IO.OUT, 16, MultiHatchTiers);
		EnergyHatchDef("substation_input_hatch_64a", "64A Substation Energy Hatch", IO.IN,  64, MultiHatchTiers);
		EnergyHatchDef("substation_output_hatch_64a","64A Substation Dynamo Hatch", IO.OUT, 64, MultiHatchTiers);

		LaserHatchDef("256a_laser_target_hatch",  "256A Laser Target Hatch",  IO.IN,  256);
		LaserHatchDef("256a_laser_source_hatch",  "256A Laser Source Hatch",  IO.OUT, 256);
		LaserHatchDef("1024a_laser_target_hatch", "1024A Laser Target Hatch", IO.IN,  1024);
		LaserHatchDef("1024a_laser_source_hatch", "1024A Laser Source Hatch", IO.OUT, 1024);
		LaserHatchDef("4096a_laser_target_hatch", "4096A Laser Target Hatch", IO.IN,  4096);
		LaserHatchDef("4096a_laser_source_hatch", "4096A Laser Source Hatch", IO.OUT, 4096);

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "rotor_holder", Label = "Rotor Holder",
			Family = MachineFamily.RotorHolder,
			Tiers = new[]
			{
				VoltageTier.HV,  VoltageTier.EV,  VoltageTier.IV,  VoltageTier.LuV,
				VoltageTier.ZPM, VoltageTier.UV,  VoltageTier.UHV, VoltageTier.UEV,
				VoltageTier.UIV, VoltageTier.UXV, VoltageTier.OpV,
			},
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.ROTOR_HOLDER },
			Casing          = MachineCasing.Voltage,
			OverlayDir      = PartOverlayDir,
			OverlayBasename = "overlay_rotor_holder",
			LayoutKey       = "rotor_holder",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "parallel_hatch", Label = "Parallel Control Hatch",
			Family = MachineFamily.ParallelHatch,
			Tiers = new[] { VoltageTier.IV, VoltageTier.LuV, VoltageTier.ZPM, VoltageTier.UV },
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.PARALLEL_HATCH },
			Casing = MachineCasing.Voltage,
			OverlayDirByTier = t =>
				$"block/machines/parallel_hatch_mk{(int)t - (int)VoltageTier.IV + 1}",
			OverlayBasename = "overlay_front",
			LayoutKey = "parallel_hatch",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "muffler_hatch", Label = "Muffler Hatch",
			Family = MachineFamily.Muffler,
			Tiers = ElectricTiers,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.MUFFLER },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			OverlayBasename = "overlay_muffler",
			LayoutKey = "muffler",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "maintenance_hatch", Label = "Maintenance Hatch",
			Family = MachineFamily.MaintenanceHatch,
			Tiered = false, Tiers = new[] { VoltageTier.LV },
			PartConfigurable = false,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.MAINTENANCE },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			OverlayBasename = "overlay_maintenance",
			LayoutKey = "maintenance",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "configurable_maintenance_hatch", Label = "Configurable Maintenance Hatch",
			Family = MachineFamily.MaintenanceHatch,
			Tiered = false, Tiers = new[] { VoltageTier.HV },
			PartConfigurable = true,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.MAINTENANCE },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			OverlayBasename = "overlay_maintenance_configurable",
			LayoutKey = "maintenance",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "auto_maintenance_hatch", Label = "Full-Auto Maintenance Hatch",
			Family = MachineFamily.AutoMaintenanceHatch,
			Tiered = false, Tiers = new[] { VoltageTier.HV },
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.MAINTENANCE },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			OverlayBasename = "overlay_maintenance_full_auto",
			EmissiveOverlayBasename = "overlay_maintenance_full_auto_emissive",
			LayoutKey = "none",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "cleaning_maintenance_hatch", Label = "Cleaning Maintenance Hatch",
			Family = MachineFamily.CleaningMaintenanceHatch,
			Tiered = false, Tiers = new[] { VoltageTier.UV },
			PartCleanroomType = Api.Machine.Multiblock.CleanroomType.CLEANROOM,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.MAINTENANCE },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			OverlayBasename = "overlay_maintenance_cleaning",
			EmissiveOverlayBasename = "overlay_maintenance_cleaning_emissive",
			LayoutKey = "none",
		});

		// TODO tiers and paththrough maybe
		Diode("diode", "Diode");

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "steam_input_bus", Label = "Steam Input Bus",
			Family = MachineFamily.ItemBus,
			Tiered = false, Tiers = OneTier,
			PartIo = IO.IN,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.STEAM_IMPORT_ITEMS },
			Casing              = MachineCasing.Voltage,
			FusedCasingTileName = "bronze_machine_casing",
			OverlayDir              = PartOverlayDir,
			PipeOverlayBasename     = "overlay_pipe",
			OverlayBasename         = "overlay_item_hatch_input",
			EmissiveOverlayBasename = "overlay_pipe_in_emissive",
			LayoutKey = "item_bus",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "steam_output_bus", Label = "Steam Output Bus",
			Family = MachineFamily.ItemBus,
			Tiered = false, Tiers = OneTier,
			PartIo = IO.OUT,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.STEAM_EXPORT_ITEMS },
			Casing              = MachineCasing.Voltage,
			FusedCasingTileName = "bronze_machine_casing",
			OverlayDir              = PartOverlayDir,
			PipeOverlayBasename     = "overlay_pipe",
			OverlayBasename         = "overlay_item_hatch_output",
			EmissiveOverlayBasename = "overlay_pipe_out_emissive",
			LayoutKey = "item_bus",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "steam_input_hatch", Label = "Steam Hatch",
			Family = MachineFamily.SteamHatch,
			Tiered = false, Tiers = new[] { VoltageTier.ULV },
			PartIo = IO.IN,
			PartFluidSlots = 1,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.STEAM },
			Casing              = MachineCasing.Voltage,
			FusedCasingTileName = "bronze_machine_casing",
			OverlayDir              = PartOverlayDir,
			PipeOverlayBasename     = "overlay_pipe",
			OverlayBasename         = "overlay_fluid_hatch_input",
			EmissiveOverlayBasename = "overlay_pipe_in_emissive",
			LayoutKey = "fluid_hatch",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "electric_blast_furnace", Label = "Electric Blast Furnace",
			Family = MachineFamily.MultiblockEBF,
			Tiered = false, Tiers = new[] { VoltageTier.LV },
			RecipeType = GTRecipeTypes.BLAST_RECIPES,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/electric_blast_furnace",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			AdditionalDisplay   = Multiblock.CoilAdditionalDisplay.BlastFurnaceMaxTemperature,
			PatternFactory = BuildEBFPattern,
			FusedCasingTileName    = "heatproof_machine_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "cleanroom", Label = "Cleanroom",
			Family = MachineFamily.MultiblockCleanroom,
			Tiered = false, Tiers = new[] { VoltageTier.LV },
			RecipeType = GTRecipeTypes.DUMMY, // Cleanroom runs CleanroomLogic
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/cleanroom",
			OverlayBasename = "overlay_top",
			LayoutKey = "cleanroom",
			PatternFactory = BuildCleanroomPattern,
			FusedCasingTileName    = "plascrete",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "large_chemical_reactor", Label = "Large Chemical Reactor",
			Family = MachineFamily.MultiblockElectricStandard,
			Tiered = false, Tiers = new[] { VoltageTier.LV },
			RecipeType = GTRecipeTypes.LARGE_CHEMICAL_RECIPES,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/large_chemical_reactor",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = BuildLCRPattern,
			MultiRecipeModifier = GTRecipeModifiers.OC_PERFECT_SUBTICK,
			FusedCasingTileName    = "inert_machine_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "vacuum_freezer", Label = "Vacuum Freezer",
			Family = MachineFamily.MultiblockElectricStandard,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.VACUUM_RECIPES,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/vacuum_freezer",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = BuildVacuumFreezerPattern,
			MultiRecipeModifier = GTRecipeModifiers.OC_NON_PERFECT_SUBTICK,
			FusedCasingTileName    = "frostproof_machine_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "implosion_compressor", Label = "Implosion Compressor",
			Family = MachineFamily.MultiblockElectricStandard,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.IMPLOSION_RECIPES,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/implosion_compressor",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = BuildImplosionCompressorPattern,
			MultiRecipeModifier = GTRecipeModifiers.OC_NON_PERFECT_SUBTICK,
			FusedCasingTileName    = "solid_machine_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "large_autoclave", Label = "Large Crystallization Chamber",
			Family = MachineFamily.MultiblockElectricStandard,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.AUTOCLAVE,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/gcym/large_autoclave",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = BuildLargeAutoclavePattern,
			MultiRecipeModifier = GTRecipeModifiers.OC_NON_PERFECT_SUBTICK,
			FusedCasingTileName    = "watertight_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "distillation_tower", Label = "Distillation Tower",
			Family = MachineFamily.MultiblockDistillationTower,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.DISTILLATION_TOWER,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/distillation_tower",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = BuildDistillationTowerPattern,
			MultiRecipeModifier = GTRecipeModifiers.OC_NON_PERFECT_SUBTICK, // BATCH_MODE deferred (just lag optimization)
			FusedCasingTileName = "clean_machine_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "large_distillery", Label = "Large Distillery",
			Family = MachineFamily.MultiblockDistillationTower,
			Tiered = false, Tiers = OneTier,
			RecipeTypes = new[] { GTRecipeTypes.DISTILLATION_TOWER, GTRecipeTypes.DISTILLERY },
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/gcym/large_distillery",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = BuildLargeDistilleryPattern,
			MultiRecipeModifier = GTRecipeModifiers.OC_NON_PERFECT_SUBTICK,
			FusedCasingTileName = "watertight_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "assembly_line", Label = "Assembly Line",
			Family = MachineFamily.MultiblockAssemblyLine,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.ASSEMBLY_LINE,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/assembly_line",
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = BuildAssemblyLinePattern,
			MultiRecipeModifier = GTRecipeModifiers.OC_NON_PERFECT_SUBTICK,
			FusedCasingTileName = "steel_machine_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "steam_grinder", Label = "Steam Grinder",
			Family = MachineFamily.MultiblockSteamParallel,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.MACERATOR,
			OutputLimits = ConstOutputLimit((ItemRecipeCapability.CAP, 1)),
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/steam_grinder",
			OverlayBasename = "overlay_front",
			LayoutKey = "steam_parallel_multi",
			PatternFactory = BuildSteamGrinderPattern,
			FusedCasingTileName = "steam_machine_casing",
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "steam_oven", Label = "Steam Oven",
			Family = MachineFamily.MultiblockSteamParallel,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.ELECTRIC_FURNACE,
			OutputLimits = ConstOutputLimit((ItemRecipeCapability.CAP, 1)),
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/steam_oven",
			OverlayBasename = "overlay_front",
			LayoutKey = "steam_parallel_multi",
			PatternFactory = BuildSteamOvenPattern,
			FusedCasingTileName = "steam_machine_casing",
		});

		LargeCombustionEngine("large_combustion_engine",  "Large Combustion Engine",       (int)VoltageTier.EV,
			"stable_machine_casing", "titanium_gearbox",     "engine_intake_casing",
			"block/multiblock/generator/large_combustion_engine");
		LargeCombustionEngine("extreme_combustion_engine","Extreme Combustion Engine",     (int)VoltageTier.IV,
			"robust_machine_casing", "tungstensteel_gearbox", "extreme_engine_intake_casing",
			"block/multiblock/generator/extreme_combustion_engine");

		LargeTurbine("steam_large_turbine",  "Large Steam Turbine",  VoltageTier.HV,
			GTRecipeTypes.STEAM_TURBINE,    "steel_turbine_casing",          "steel_gearbox",
			"block/multiblock/generator/large_steam_turbine",  needsMuffler: false);
		LargeTurbine("gas_large_turbine",    "Large Gas Turbine",    VoltageTier.EV,
			GTRecipeTypes.GAS_TURBINE,      "stainless_steel_turbine_casing","stainless_steel_gearbox",
			"block/multiblock/generator/large_gas_turbine",    needsMuffler: true);
		LargeTurbine("plasma_large_turbine", "Large Plasma Turbine", VoltageTier.IV,
			GTRecipeTypes.PLASMA_GENERATOR, "tungstensteel_turbine_casing",  "tungstensteel_gearbox",
			"block/multiblock/generator/large_plasma_turbine", needsMuffler: false);

		LargeMiner("ev_large_miner",  "Large Miner",  VoltageTier.EV,
			"solid_machine_casing",   "steel");
		LargeMiner("iv_large_miner",  "Advanced Large Miner",  VoltageTier.IV,
			"stable_machine_casing",  "titanium");
		LargeMiner("luv_large_miner", "Advanced Large Miner II", VoltageTier.LuV,
			"robust_machine_casing",  "tungsten_steel");

		FluidDrillingRig("mv_fluid_drilling_rig", "Fluid Drilling Rig",          VoltageTier.MV,
			"solid_machine_casing",   "steel");
		FluidDrillingRig("hv_fluid_drilling_rig", "Advanced Fluid Drilling Rig", VoltageTier.HV,
			"stable_machine_casing",  "titanium");
		FluidDrillingRig("ev_fluid_drilling_rig", "Advanced Fluid Drilling Rig II", VoltageTier.EV,
			"robust_machine_casing",  "tungsten_steel");

		FusionReactor("luv_fusion_reactor", "Fusion Reactor Mk I",   VoltageTier.LuV,
			"fusion_casing",      "superconducting_coil");
		FusionReactor("zpm_fusion_reactor", "Fusion Reactor Mk II",  VoltageTier.ZPM,
			"fusion_casing_mk2",  "fusion_coil");
		FusionReactor("uv_fusion_reactor",  "Fusion Reactor Mk III", VoltageTier.UV,
			"fusion_casing_mk3",  "fusion_coil");

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "active_transformer", Label = "Active Transformer",
			Family = MachineFamily.MultiblockActiveTransformer,
			Tiered = false, Tiers = new[] { VoltageTier.UV },
			RecipeType = GTRecipeTypes.DUMMY, // has its own per-tick conversion loop
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/data_bank", OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = () => BuildActiveTransformerPattern(),
			FusedCasingTileName = "high_power_casing",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "power_substation", Label = "Power Substation",
			Family = MachineFamily.MultiblockPowerSubstation,
			Tiered = false, Tiers = new[] { VoltageTier.UV },
			RecipeType = GTRecipeTypes.DUMMY,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/power_substation",
			OverlayBasename         = "overlay_front",
			EmissiveOverlayBasename = "overlay_front_emissive",
			LayoutKey = "power_substation",
			PatternFactory = () => BuildPowerSubstationPattern(),
			FusedCasingTileName = "palladium_substation",
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "high_performance_computation_array", Label = "High Performance Computing Array",
			Family = MachineFamily.MultiblockHPCA,
			Tiered = false, Tiers = new[] { VoltageTier.ZPM },
			RecipeType = GTRecipeTypes.DUMMY,
			Casing = MachineCasing.Voltage,
			FusedCasingTileName = "computer_casing",
			OverlayDir = "block/multiblock/hpca", OverlayBasename = "overlay_front",
			LayoutKey = "research_provider",
			AdditionalDisplay = Multiblock.HpcaAdditionalDisplay.HpcaInfo,
			PatternFactory = () => BuildHpcaPattern(),
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "data_bank", Label = "Data Bank",
			Family = MachineFamily.MultiblockDataBank,
			Tiered = false, Tiers = new[] { VoltageTier.ZPM },
			RecipeType = GTRecipeTypes.DUMMY,
			Casing = MachineCasing.Voltage,
			FusedCasingTileName = "computer_casing",
			OverlayDir = "block/multiblock/data_bank", OverlayBasename = "overlay_front",
			LayoutKey = "research_provider",
			AdditionalDisplay = Multiblock.HpcaAdditionalDisplay.DataBankInfo,
			PatternFactory = () => BuildDataBankPattern(),
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "network_switch", Label = "Network Switch",
			Family = MachineFamily.MultiblockNetworkSwitch,
			Tiered = false, Tiers = new[] { VoltageTier.ZPM },
			RecipeType = GTRecipeTypes.DUMMY,
			Casing = MachineCasing.Voltage,
			FusedCasingTileName = "computer_casing",
			OverlayDir = "block/multiblock/network_switch", OverlayBasename = "overlay_front",
			LayoutKey = "research_provider",
			AdditionalDisplay = Multiblock.HpcaAdditionalDisplay.NetworkSwitchComputation,
			PatternFactory = () => BuildNetworkSwitchPattern(),
		});
		MachineRegistry.Register(new MachineDefinition
		{
			Id = "research_station", Label = "Research Station",
			Family = MachineFamily.MultiblockResearchStation,
			Tiered = false, Tiers = new[] { VoltageTier.ZPM },
			RecipeType = GTRecipeTypes.RESEARCH_STATION,
			Casing = MachineCasing.Voltage,
			FusedCasingTileName = "advanced_computer_casing",
			OverlayDir = "block/multiblock/research_station", OverlayBasename = "overlay_front",
			LayoutKey = "research_station",
			PatternFactory = () => BuildResearchStationPattern(),
		});

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "object_holder", Label = "Object Holder",
			Family = MachineFamily.ObjectHolder,
			Tiered = false, Tiers = new[] { VoltageTier.ZPM },
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.OBJECT_HOLDER },
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/machines/object_holder", OverlayBasename = "overlay_front",
			LayoutKey = "object_holder",
		});

		void Hpca(string id, string label, Multiblock.Part.Hpca.HpcaComponentKind kind, string overlay) =>
			MachineRegistry.Register(new MachineDefinition
			{
				Id = id, Label = label,
				Family = MachineFamily.HpcaComponent,
				Tiered = false, Tiers = new[] { VoltageTier.ZPM },
				PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.HPCA_COMPONENT },
				HpcaKind = kind,
				Casing = MachineCasing.Voltage,
				FusedCasingTileName = "computer_casing",
				OverlayDir = "block/overlay/machine/hpca", OverlayBasename = overlay,
				LayoutKey = "none",
			});
		Hpca("hpca_empty_component",                "HPCA Empty Component",                Multiblock.Part.Hpca.HpcaComponentKind.Empty,               "empty");
		Hpca("hpca_computation_component",          "HPCA Computation Component",          Multiblock.Part.Hpca.HpcaComponentKind.Computation,         "computation");
		Hpca("hpca_advanced_computation_component", "HPCA Advanced Computation Component", Multiblock.Part.Hpca.HpcaComponentKind.AdvancedComputation, "advanced_computation");
		Hpca("hpca_heat_sink_component",            "HPCA Heat Sink Component",            Multiblock.Part.Hpca.HpcaComponentKind.HeatSink,           "heat_sink");
		Hpca("hpca_active_cooler_component",        "HPCA Active Cooler Component",        Multiblock.Part.Hpca.HpcaComponentKind.ActiveCooler,       "active_cooler");
		Hpca("hpca_bridge_component",               "HPCA Bridge Component",               Multiblock.Part.Hpca.HpcaComponentKind.Bridge,             "bridge");

		void DataAccess(string id, string label, VoltageTier tier, bool creative) =>
			MachineRegistry.Register(new MachineDefinition
			{
				Id = id, Label = label,
				Family = MachineFamily.DataAccessHatch,
				Tiered = false, Tiers = new[] { tier },
				DataAccessCreative = creative,
				PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.DATA_ACCESS },
				Casing = MachineCasing.Voltage,
				OverlayDir = PartOverlayDir,
				OverlayBasename         = creative ? "overlay_data_hatch_creative"          : "overlay_data_hatch",
				EmissiveOverlayBasename = creative ? "overlay_data_hatch_creative_emissive" : "overlay_data_hatch_emissive",
				LayoutKey = "data_access",
			});
		DataAccess("basic_data_access_hatch",    "Basic Data Access Hatch",    VoltageTier.HV,  false);
		DataAccess("data_access_hatch",          "Data Access Hatch",          VoltageTier.EV,  false);
		DataAccess("advanced_data_access_hatch", "Advanced Data Access Hatch", VoltageTier.LuV, false);
		DataAccess("creative_data_access_hatch", "Creative Data Access Hatch", VoltageTier.MAX, true);

		void CompHatch(string id, string label, bool transmitter, Api.Machine.Multiblock.PartAbility ability) =>
			MachineRegistry.Register(new MachineDefinition
			{
				Id = id, Label = label,
				Family = MachineFamily.OpticalComputationHatch,
				Tiered = false, Tiers = new[] { VoltageTier.ZPM },
				OpticalTransmitter = transmitter,
				PartAbilities = new[] { ability },
				Casing = MachineCasing.Voltage,
				OverlayDir = PartOverlayDir,
				OverlayBasename         = "overlay_data_hatch_optical",
				EmissiveOverlayBasename = "overlay_data_hatch_optical_emissive",
				LayoutKey = "none",
			});
		CompHatch("computation_transmitter_hatch", "Computation Data Transmission Hatch", true,  Api.Machine.Multiblock.PartAbility.COMPUTATION_DATA_TRANSMISSION);
		CompHatch("computation_receiver_hatch",    "Computation Data Reception Hatch",    false, Api.Machine.Multiblock.PartAbility.COMPUTATION_DATA_RECEPTION);

		void DataHatch(string id, string label, bool transmitter, Api.Machine.Multiblock.PartAbility ability) =>
			MachineRegistry.Register(new MachineDefinition
			{
				Id = id, Label = label,
				Family = MachineFamily.OpticalDataHatch,
				Tiered = false, Tiers = new[] { VoltageTier.LuV },
				OpticalTransmitter = transmitter,
				PartAbilities = new[] { ability },
				Casing = MachineCasing.Voltage,
				OverlayDir = PartOverlayDir,
				OverlayBasename         = "overlay_data_hatch_optical",
				EmissiveOverlayBasename = "overlay_data_hatch_optical_emissive",
				LayoutKey = "none",
			});
		DataHatch("data_transmitter_hatch", "Optical Data Transmission Hatch", true,  Api.Machine.Multiblock.PartAbility.OPTICAL_DATA_TRANSMISSION);
		DataHatch("data_receiver_hatch",    "Optical Data Reception Hatch",    false, Api.Machine.Multiblock.PartAbility.OPTICAL_DATA_RECEPTION);

		Std("large_centrifuge",        "Large Centrifugal Unit",      BuildLargeCentrifugePattern,        "vibration_safe_casing",            "block/multiblock/gcym/large_centrifuge",        GTRecipeTypes.CENTRIFUGE, GTRecipeTypes.THERMAL_CENTRIFUGE);
		Std("large_electrolyzer",      "Large Electrolysis Chamber",  BuildLargeElectrolyzerPattern,      "nonconducting_casing",             "block/multiblock/gcym/large_electrolyzer",      GTRecipeTypes.ELECTROLYZER);
		Std("large_electromagnet",     "Large Electromagnet",         BuildLargeElectromagnetPattern,     "nonconducting_casing",             "block/multiblock/gcym/large_electrolyzer",      GTRecipeTypes.ELECTROMAGNETIC_SEPARATOR, GTRecipeTypes.POLARIZER);
		Std("large_packer",            "Large Packaging Machine",     BuildLargePackerPattern,            "robust_machine_casing",            "block/multiblock/gcym/large_packer",            GTRecipeTypes.PACKER);
		Std("large_assembler",         "Large Assembling Factory",    BuildLargeAssemblerPattern,         "large_scale_assembler_casing",     "block/multiblock/gcym/large_assembler",         GTRecipeTypes.ASSEMBLER);
		Std("large_circuit_assembler", "Large Circuit Assembling Facility", BuildLargeCircuitAssemblerPattern, "large_scale_assembler_casing", "block/multiblock/gcym/large_circuit_assembler", GTRecipeTypes.CIRCUIT_ASSEMBLER);
		Std("large_arc_smelter",       "Large Arc Smelter",           BuildLargeArcSmelterPattern,        "high_temperature_smelting_casing", "block/multiblock/gcym/large_arc_smelter",       GTRecipeTypes.ARC_FURNACE);
		Std("large_engraving_laser",   "Large Engraving Laser",       BuildLargeEngravingLaserPattern,    "laser_safe_engraving_casing",      "block/multiblock/gcym/large_engraving_laser",   GTRecipeTypes.LASER_ENGRAVER);
		Std("large_sifting_funnel",    "Large Sifting Funnel",        BuildLargeSiftingFunnelPattern,     "vibration_safe_casing",            "block/multiblock/gcym/large_sifting_funnel",    GTRecipeTypes.SIFTER);
		Std("large_material_press",    "Large Material Press",        BuildLargeMaterialPressPattern,     "stress_proof_casing",              "block/multiblock/gcym/large_material_press",    GTRecipeTypes.BENDER, GTRecipeTypes.COMPRESSOR, GTRecipeTypes.FORGE_HAMMER, GTRecipeTypes.FORMING_PRESS);
		Std("large_brewer",            "Large Brewing Vat",           BuildLargeBrewerPattern,            "corrosion_proof_casing",           "block/multiblock/gcym/large_brewer",            GTRecipeTypes.BREWERY, GTRecipeTypes.FERMENTER, GTRecipeTypes.FLUID_HEATER);
		Std("large_cutter",            "Large Cutting Saw",           BuildLargeCutterPattern,            "shock_proof_cutting_casing",       "block/multiblock/gcym/large_cutter",            GTRecipeTypes.CUTTER, GTRecipeTypes.LATHE);
		Std("large_extractor",         "Large Extraction Machine",    BuildLargeExtractorPattern,         "watertight_casing",                "block/multiblock/gcym/large_extractor",         GTRecipeTypes.EXTRACTOR, GTRecipeTypes.CANNER);
		Std("large_extruder",          "Large Extrusion Machine",     BuildLargeExtruderPattern,          "stress_proof_casing",              "block/multiblock/gcym/large_extruder",          GTRecipeTypes.EXTRUDER);
		Std("large_solidifier",        "Large Solidification Array",  BuildLargeSolidifierPattern,        "watertight_casing",                "block/multiblock/gcym/large_solidifier",        GTRecipeTypes.FLUID_SOLIDIFIER);
		Std("large_wiremill",          "Large Wire Factory",          BuildLargeWiremillPattern,          "stress_proof_casing",              "block/multiblock/gcym/large_wiremill",          GTRecipeTypes.WIREMILL);
		Std("large_chemical_bath",     "Large Chemical Bath",         BuildLargeChemicalBathPattern,      "watertight_casing",                "block/multiblock/gcym/large_chemical_bath",     GTRecipeTypes.CHEMICAL_BATH, GTRecipeTypes.ORE_WASHER);
		Std("large_maceration_tower",  "Large Maceration Tower",      BuildLargeMacerationTowerPattern,   "secure_maceration_casing",         "block/multiblock/gcym/large_maceration_tower",  GTRecipeTypes.MACERATOR);
		Std("large_mixer",             "Large Mixing Vessel",         BuildLargeMixerPattern,             "reaction_safe_mixing_casing",      "block/multiblock/gcym/large_mixer",             GTRecipeTypes.MIXER);
		Std("mega_vacuum_freezer",     "Bulk Blast Chiller",          BuildMegaVacuumFreezerPattern,      "frostproof_machine_casing",        "block/multiblock/gcym/mega_vacuum_freezer",     GTRecipeTypes.VACUUM_RECIPES);

		Coil("multi_smelter",        "Multi Smelter",               BuildMultiSmelterPattern,       "heatproof_machine_casing",
			"block/multiblock/multi_furnace",           GTRecipeModifiers.MULTI_SMELTER_PARALLEL,
			Multiblock.CoilAdditionalDisplay.MultiSmelterCoilStats,
			GTRecipeTypes.ELECTRIC_FURNACE, GTRecipeTypes.ALLOY_SMELTER);
		Coil("pyrolyse_oven",        "Pyrolyse Oven",               BuildPyrolyseOvenPattern,       "ulv_machine_casing",
			"block/multiblock/pyrolyse_oven",           GTRecipeModifiers.PYROLYSE_OVERCLOCK,
			Multiblock.CoilAdditionalDisplay.PyrolyseOvenSpeed,
			GTRecipeTypes.PYROLYSE_RECIPES);
		Coil("cracker",              "Cracking Unit",               BuildCrackerPattern,            "clean_machine_casing",
			"block/multiblock/cracking_unit",           GTRecipeModifiers.CRACKER_OVERCLOCK,
			Multiblock.CoilAdditionalDisplay.CrackingUnitEnergy,
			GTRecipeTypes.CRACKING_RECIPES);
		Coil("alloy_blast_smelter",  "Alloy Blast Smelter",         BuildAlloyBlastSmelterPattern,  "high_temperature_smelting_casing",
			"block/multiblock/gcym/blast_alloy_smelter", GTRecipeModifiers.EBF_OVERCLOCK,
			Multiblock.CoilAdditionalDisplay.BlastFurnaceMaxTemperature,
			GTRecipeTypes.ALLOY_BLAST_RECIPES);
		Coil("mega_blast_furnace",   "Rotary Hearth Furnace",       BuildMegaBlastFurnacePattern,   "high_temperature_smelting_casing",
			"block/multiblock/gcym/mega_blast_furnace", GTRecipeModifiers.EBF_OVERCLOCK,
			Multiblock.CoilAdditionalDisplay.BlastFurnaceMaxTemperature,
			GTRecipeTypes.BLAST_RECIPES);

		MachineRegistry.Register(new MachineDefinition
		{
			Id = "coke_oven_hatch", Label = "Coke Oven Hatch",
			Family = MachineFamily.CokeOvenHatch,
			Tiered = false, Tiers = OneTier,
			Casing          = MachineCasing.CokeBricks,
			OverlayDir      = "block/overlay/machine",
			OverlayBasename = "overlay_hatch",
			LayoutKey       = "none",
		});

		MultiblockTankValve("wooden_tank_valve", "Wooden Tank Valve", "wood_wall");
		MultiblockTankValve("bronze_tank_valve", "Bronze Tank Valve", "steam_machine_casing");
		MultiblockTankValve("steel_tank_valve",  "Steel Tank Valve",  "steel_machine_casing");
		MultiblockTank("wooden_multiblock_tank", "Wooden Multiblock Tank", "wood",   250_000,   "wood_wall",            "wooden_tank_valve");
		MultiblockTank("bronze_multiblock_tank", "Bronze Multiblock Tank", "bronze", 500_000,   "steam_machine_casing",  "bronze_tank_valve");
		MultiblockTank("steel_multiblock_tank",  "Steel Multiblock Tank",  null,     1_000_000, "steel_machine_casing", "steel_tank_valve");

		Drum("wood_drum",            "Wooden Drum",           "wood",             16_000);
		Drum("bronze_drum",          "Bronze Drum",           "bronze",           32_000);
		Drum("steel_drum",           "Steel Drum",            "steel",            64_000);
		Drum("aluminium_drum",       "Aluminium Drum",        "aluminium",       128_000);
		Drum("stainless_steel_drum", "Stainless Steel Drum",  "stainless_steel", 256_000);
		Drum("gold_drum",            "Gold Drum",             "gold",             32_000);
		Drum("titanium_drum",        "Titanium Drum",         "titanium",        512_000);
		Drum("tungsten_steel_drum",  "Tungstensteel Drum",    "tungsten_steel", 1024_000);

		Crate("wood_crate",            "Wooden Crate",          "wood",             27);
		Crate("bronze_crate",          "Bronze Crate",          "bronze",           54);
		Crate("steel_crate",           "Steel Crate",           "steel",            72);
		Crate("aluminium_crate",       "Aluminium Crate",       "aluminium",        90);
		Crate("stainless_steel_crate", "Stainless Steel Crate", "stainless_steel", 108);
		Crate("titanium_crate",        "Titanium Crate",        "titanium",        126);
		Crate("tungsten_steel_crate",  "Tungstensteel Crate",   "tungsten_steel",  144);
	}

	private static void Crate(string id, string label, string materialId, int slotCount)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.Crate,
			Tiered = false,
			Tiers = OneTier,
			MaterialId = materialId,
			Capacity = slotCount,
			Casing = MachineCasing.None,
			LayoutKey = "crate",
		});
	}

	private static void Drum(string id, string label, string materialId, int capacity)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.Drum,
			Tiered = false,
			Tiers = OneTier,
			MaterialId = materialId,
			Capacity = capacity,
			Casing = MachineCasing.None,
			LayoutKey = "drum",
		});
	}

	private const string PartOverlayDir = "block/overlay/machine";

	private static void ItemBus(string id, string label, IO io, VoltageTier[] tiers)
	{
		string ioTok = io == IO.IN ? "in" : "out";
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.ItemBus,
			Tiers = tiers,
			PartIo = io,
			PartAbilities = new[] { io == IO.IN
				? Api.Machine.Multiblock.PartAbility.IMPORT_ITEMS
				: Api.Machine.Multiblock.PartAbility.EXPORT_ITEMS },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			PipeOverlayBasename     = "overlay_pipe",
			OverlayBasename         = io == IO.IN ? "overlay_item_hatch_input" : "overlay_item_hatch_output",
			EmissiveOverlayBasename = $"overlay_pipe_{ioTok}_emissive",
			LayoutKey = "item_bus",
		});
	}

	private static void FluidHatchDef(string id, string label, IO io, int slots, VoltageTier[] tiers)
	{
		string ioTok = io == IO.IN ? "in" : "out";
		string pipeOverlay = slots switch
		{
			4 => "overlay_pipe_4x",
			9 => "overlay_pipe_9x",
			_ => "overlay_pipe",
		};
		var baseAbility = io == IO.IN ? Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS
		                              : Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS;
		var slotAbility = slots switch
		{
			4 => io == IO.IN ? Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS_4X
			                 : Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS_4X,
			9 => io == IO.IN ? Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS_9X
			                 : Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS_9X,
			_ => io == IO.IN ? Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS_1X
			                 : Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS_1X,
		};
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.FluidHatch,
			Tiers = tiers,
			PartIo = io,
			PartFluidSlots = slots,
			PartAbilities = new[] { baseAbility, slotAbility },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			PipeOverlayBasename     = pipeOverlay,
			OverlayBasename         = io == IO.IN ? "overlay_fluid_hatch_input" : "overlay_fluid_hatch_output",
			EmissiveOverlayBasename = $"overlay_pipe_{ioTok}_emissive",
			LayoutKey = "fluid_hatch",
		});
	}

	private static void DualHatch(string id, string label, IO io, VoltageTier[] tiers)
	{
		string ioTok = io == IO.IN ? "in" : "out";
		var abilities = io == IO.IN
			? new[] { Api.Machine.Multiblock.PartAbility.IMPORT_ITEMS,
			          Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS }
			: new[] { Api.Machine.Multiblock.PartAbility.EXPORT_ITEMS,
			          Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS };
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.DualHatch,
			Tiers = tiers,
			PartIo = io,
			PartAbilities = abilities,
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			PipeOverlayBasename     = "overlay_pipe",
			OverlayBasename         = io == IO.IN ? "overlay_dual_hatch_input" : "overlay_dual_hatch_output",
			EmissiveOverlayBasename = $"overlay_pipe_{ioTok}_emissive",
			LayoutKey = "dual_hatch",
		});
	}

	private static void Diode(string id, string label)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.Diode,
			Tiers = ElectricTiers,
			PartAbilities = new[] { Api.Machine.Multiblock.PartAbility.PASSTHROUGH_HATCH },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			TintedOverlayBasename   = "overlay_energy_1a_tinted",
			OverlayBasename         = "overlay_energy_1a_in",
			EmissiveOverlayBasename = "overlay_energy_1a_in_emissive",
			LayoutKey = "none",
		});
	}

	private static void EnergyHatchDef(string id, string label, IO io, int amperage, VoltageTier[] tiers)
	{
		string ioTok = io == IO.IN ? "in" : "out";
		int pngAmp = amperage == 2 ? 1 : amperage;
		// 64A -> SUBSTATION_*, 2/4/16 -> standard INPUT/OUTPUT
		Api.Machine.Multiblock.PartAbility ability;
		if (amperage == 64)
			ability = io == IO.IN ? Api.Machine.Multiblock.PartAbility.SUBSTATION_INPUT_ENERGY
			                      : Api.Machine.Multiblock.PartAbility.SUBSTATION_OUTPUT_ENERGY;
		else
			ability = io == IO.IN ? Api.Machine.Multiblock.PartAbility.INPUT_ENERGY
			                      : Api.Machine.Multiblock.PartAbility.OUTPUT_ENERGY;
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.EnergyHatch,
			Tiers = tiers,
			PartIo = io,
			PartAmperage = amperage,
			PartAbilities = new[] { ability },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			TintedOverlayBasename   = $"overlay_energy_{pngAmp}a_tinted",
			OverlayBasename         = $"overlay_energy_{pngAmp}a_{ioTok}",
			EmissiveOverlayBasename = $"overlay_energy_{pngAmp}a_{ioTok}_emissive",
			LayoutKey = "none",
		});
	}

	private static void LaserHatchDef(string id, string label, IO io, int amperage)
	{
		string name = io == IO.IN ? "target" : "source";
		var ability = io == IO.IN
			? Api.Machine.Multiblock.PartAbility.INPUT_LASER
			: Api.Machine.Multiblock.PartAbility.OUTPUT_LASER;
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.LaserHatch,
			Tiers = LaserHatchTiers,
			PartIo = io,
			PartAmperage = amperage,
			PartAbilities = new[] { ability },
			Casing = MachineCasing.Voltage,
			OverlayDir = PartOverlayDir,
			OverlayBasename         = $"overlay_laser_{name}",
			EmissiveOverlayBasename = $"overlay_laser_{name}_emissive",
			LayoutKey = "none",
		});
	}

	private static class BatteryBufferAmps
	{
		public const long Normal  = 2L;
		public const long Charger = 4L;
	}

	private static void Wtm(string id, string label, Api.Recipe.GTRecipeType recipeType,
		int inSlots, int outSlots, int inTanks = 0, int outTanks = 0,
		bool circuit = false, string layout = "generic",
		Func<VoltageTier, IReadOnlyDictionary<object, int>>? outputLimits = null)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.WorkableTiered,
			Tiers = AllTiers,
			RecipeType = recipeType,
			InputSlotCount = inSlots, OutputSlotCount = outSlots,
			InputFluidTankCount = inTanks, OutputFluidTankCount = outTanks,
			UsesCircuit = circuit,
			LayoutKey = layout,
			OutputLimits = outputLimits,
		});
	}

	private static void SolarBoiler(string id, string label, bool highPressure)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id     = id,
			Label  = label,
			Family = MachineFamily.SteamSolarBoiler,
			Tiered = false,
			Tiers  = OneTier,
			RecipeType = GTRecipeTypes.STEAM_BOILER,
			IsHighPressure = highPressure,
			Casing = highPressure ? MachineCasing.BrickedSteel : MachineCasing.BrickedBronze,
			OverlayDir = "block/generators/boiler/solar",
			LayoutKey = "solar_boiler",
		});
	}

	private static void LiquidBoiler(string id, string label, bool highPressure)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id     = id,
			Label  = label,
			Family = MachineFamily.SteamLiquidBoiler,
			Tiered = false,
			Tiers  = OneTier,
			RecipeType = GTRecipeTypes.STEAM_BOILER,
			IsHighPressure = highPressure,
			Casing = highPressure ? MachineCasing.BrickedSteel : MachineCasing.BrickedBronze,
			OverlayDir = "block/generators/boiler/lava",
			LayoutKey = "liquid_boiler",
		});
	}

	private static void Steam(string shortName, string label, Api.Recipe.GTRecipeType recipeType,
		int inSlots, int outSlots,
		Func<VoltageTier, IReadOnlyDictionary<object, int>>? outputLimits = null)
	{
		SteamVariant(shortName, label, recipeType, inSlots, outSlots, "lp", "Steam ",    highPressure: false, outputLimits);
		SteamVariant(shortName, label, recipeType, inSlots, outSlots, "hp", "HP Steam ", highPressure: true,  outputLimits);
	}

	private static void SteamVariant(string shortName, string label, Api.Recipe.GTRecipeType recipeType,
		int inSlots, int outSlots, string prefix, string pressureLabel, bool highPressure,
		Func<VoltageTier, IReadOnlyDictionary<object, int>>? outputLimits = null)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id     = $"{prefix}_steam_{shortName}",
			Label  = pressureLabel + label,
			Family = MachineFamily.SimpleSteam,
			Tiered = false,
			Tiers  = OneTier,
			RecipeType = recipeType,
			InputSlotCount = inSlots, OutputSlotCount = outSlots,
			OutputLimits = outputLimits,
			IsHighPressure = highPressure,
			Casing = highPressure ? MachineCasing.BrickedSteel : MachineCasing.BrickedBronze,
			OverlayDir = $"block/machines/{shortName}",
			LayoutKey = "steam_machine",
		});
	}

	private static void SteamMiner(string id, string label, string overlayDir, bool highPressure)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id     = id, Label = label,
			Family = MachineFamily.SteamMiner,
			Tiered = false, Tiers = OneTier,
			IsHighPressure = highPressure,
			Casing = highPressure ? MachineCasing.BrickedSteel : MachineCasing.BrickedBronze,
			OverlayDir = overlayDir,
			OverlayBasename = "overlay_front",
			LayoutKey = "steam_miner",
		});
	}

	private static void Battery(string id, string label, int slots, long inputAmpsPerItem, long outputAmps)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.BatteryBuffer,
			Tiers = AllTiers,
			BatterySlotCount = slots,
			InputAmpsPerItem = inputAmpsPerItem,
			OutputAmps = outputAmps,
			OverlayDir = "block/machines/charger",
			OverlayBasename = "overlay_charger_idle",
			LayoutKey = "battery_buffer",
		});
	}

	private static IBlockPattern BuildLCRPattern()
	{
		var lcrDef    = MachineRegistry.Get("large_chemical_reactor")!;
		var casing    = Predicates.Blocks("inert_machine_casing");
		var abilities = Predicates.AutoAbilities(GTRecipeTypes.LARGE_CHEMICAL_RECIPES)
			.Or(Predicates.AutoAbilities(true, false, false));

		return new BlockPattern(MultiblockShapes.LargeChemicalReactor, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(lcrDef),
			['X'] = casing.Or(abilities),
			['P'] = Predicates.Blocks("ptfe_pipe_casing"),
			['C'] = Predicates.HeatingCoils().SetExactLimit(1).Or(abilities).Or(casing),
		});
	}

	private static IBlockPattern BuildVacuumFreezerPattern()
	{
		var def = MachineRegistry.Get("vacuum_freezer")!;
		return new BlockPattern(MultiblockShapes.VacuumFreezer, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("frostproof_machine_casing", new[] { GTRecipeTypes.VACUUM_RECIPES }),
		});
	}

	private static IBlockPattern BuildImplosionCompressorPattern()
	{
		var def = MachineRegistry.Get("implosion_compressor")!;
		return new BlockPattern(MultiblockShapes.ImplosionCompressor, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("solid_machine_casing",
				new[] { GTRecipeTypes.IMPLOSION_RECIPES }, maintenance: true, muffler: true),
		});
	}

	private static IBlockPattern BuildLargeAutoclavePattern()
	{
		var def = MachineRegistry.Get("large_autoclave")!;
		return new BlockPattern(MultiblockShapes.LargeAutoclave, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("watertight_casing",
				new[] { GTRecipeTypes.AUTOCLAVE }, maintenance: true, parallel: true),
			['T'] = Predicates.Blocks("steel_pipe_casing"),
		});
	}

	private static IBlockPattern BuildDistillationTowerPattern()
	{
		var def = MachineRegistry.Get("distillation_tower")!;
		var casing = Predicates.Blocks("clean_machine_casing");
		var perLayerExport = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS_1X).SetMaxLayerLimited(1);
		var maint = Predicates.AutoAbilities(checkMaintenance: true, checkMuffler: false, checkParallel: false);
		var energyIn   = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY)
			.SetMinGlobalLimited(1).SetMaxGlobalLimited(2);
		var itemOut    = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_ITEMS)
			.SetMaxGlobalLimited(1);
		var fluidIn    = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS)
			.SetExactLimit(1);
		var yWall = casing.Or(energyIn).Or(itemOut).Or(fluidIn).Or(maint);
		return new RepeatableBlockPattern(MultiblockShapes.DistillationTower, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['Y'] = yWall,
			['Z'] = casing.Or(perLayerExport).Or(maint),
			['X'] = casing.Or(perLayerExport),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeDistilleryPattern()
	{
		var def = MachineRegistry.Get("large_distillery")!;
		var casing = Predicates.Blocks("watertight_casing");
		var perLayerExport = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS_1X).SetMaxLayerLimited(1);
		var energyIn = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY)
			.SetMinGlobalLimited(1).SetMaxGlobalLimited(2);
		var itemOut  = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_ITEMS)
			.SetMaxGlobalLimited(1);
		var fluidIn  = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS)
			.SetMinGlobalLimited(1);
		var maintParallel = Predicates.AutoAbilities(checkMaintenance: true, checkMuffler: false, checkParallel: true);
		var yWall = casing.Or(energyIn).Or(itemOut).Or(fluidIn).Or(maintParallel);
		return new RepeatableBlockPattern(MultiblockShapes.LargeDistillery, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['Y'] = yWall,
			['Z'] = casing,
			['X'] = casing.Or(perLayerExport),
			['P'] = Predicates.Blocks("steel_pipe_casing"),
			['A'] = Predicates.Air(),
			['#'] = Predicates.Any(),
		});
	}

	private static void Std(string id, string label, Func<IBlockPattern> patternFactory,
		string fusedCasingTile, string overlayDir, params Api.Recipe.GTRecipeType[] recipeTypes)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockElectricStandard,
			Tiered = false, Tiers = OneTier,
			Casing = MachineCasing.Voltage,
			OverlayDir = overlayDir,
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = patternFactory,
			MultiRecipeModifier = GTRecipeModifiers.OC_NON_PERFECT_SUBTICK,
			FusedCasingTileName = fusedCasingTile,
			RecipeType  = recipeTypes.Length > 0 ? recipeTypes[0] : null,
			RecipeTypes = recipeTypes.Length > 0 ? recipeTypes : null,
		});
	}

	private static void Coil(string id, string label, Func<IBlockPattern> patternFactory,
		string fusedCasingTile, string overlayDir, Api.Recipe.Modifier.RecipeModifier modifier,
		Action<MetaMachine, List<string>>? additionalDisplay,
		params Api.Recipe.GTRecipeType[] recipeTypes)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockCoilStandard,
			Tiered = false, Tiers = OneTier,
			Casing = MachineCasing.Voltage,
			OverlayDir = overlayDir,
			OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = patternFactory,
			MultiRecipeModifier = modifier,
			AdditionalDisplay   = additionalDisplay,
			FusedCasingTileName = fusedCasingTile,
			RecipeType  = recipeTypes.Length == 1 ? recipeTypes[0] : null,
			RecipeTypes = recipeTypes.Length > 1  ? recipeTypes    : null,
		});
	}

	private static void LargeCombustionEngine(string id, string label, int tier,
		string casingTile, string gearTile, string intakeTile, string overlayDir)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockLargeCombustionEngine,
			Tiered = false, Tiers = new[] { (VoltageTier)tier },
			RecipeType = GTRecipeTypes.COMBUSTION,
			IsGenerator = true,
			Casing = MachineCasing.Voltage,
			OverlayDir = overlayDir, OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = () => BuildLargeCombustionEnginePattern(id, tier, casingTile, gearTile, intakeTile),
			FusedCasingTileName = casingTile,
		});
	}

	private static IBlockPattern BuildLargeCombustionEnginePattern(string id, int tier,
		string casingTile, string gearTile, string intakeTile)
	{
		var def = MachineRegistry.Get(id)!;
		var casing = Predicates.Blocks(casingTile);
		var allowedTiers = new[]
		{
			(int)VoltageTier.ULV, (int)VoltageTier.LV, (int)VoltageTier.MV, (int)VoltageTier.HV,
			(int)VoltageTier.EV,  (int)VoltageTier.IV, (int)VoltageTier.LuV,
			(int)VoltageTier.ZPM, (int)VoltageTier.UV, (int)VoltageTier.UHV,
		}.Where(t => t >= tier).ToArray();
		var energyOut = Predicates.Ability(Api.Machine.Multiblock.PartAbility.OUTPUT_ENERGY, allowedTiers);
		var cCell = casing
			.Or(Predicates.AutoAbilities(new[] { GTRecipeTypes.COMBUSTION },
				checkEnergyIn: false, checkEnergyOut: false,
				checkItemIn: true, checkItemOut: true,
				checkFluidIn: true, checkFluidOut: true))
			.Or(Predicates.AutoAbilities(checkMaintenance: true, checkMuffler: true, checkParallel: false));
		return new BlockPattern(MultiblockShapes.LargeCombustionEngine,
			new Dictionary<char, TraceabilityPredicate>
		{
			['Y'] = Predicates.Controller(def),
			['X'] = casing,
			['D'] = energyOut,
			['C'] = cCell,
			['G'] = Predicates.Blocks(gearTile),
			['A'] = Predicates.Blocks(intakeTile),
		});
	}

	private static void LargeTurbine(string id, string label, VoltageTier tier,
		Api.Recipe.GTRecipeType recipeType, string casingTile, string gearTile,
		string overlayDir, bool needsMuffler)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockLargeTurbine,
			Tiered = false, Tiers = new[] { tier },
			RecipeType = recipeType,
			IsGenerator = true,
			Casing = MachineCasing.Voltage,
			OverlayDir = overlayDir, OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = () => BuildLargeTurbinePattern(id, casingTile, gearTile, needsMuffler),
			FusedCasingTileName = casingTile,
		});
	}

	private static IBlockPattern BuildLargeTurbinePattern(string id,
		string casingTile, string gearTile, bool needsMuffler)
	{
		var def = MachineRegistry.Get(id)!;
		var casing = Predicates.Blocks(casingTile);
		var recipeType = def.RecipeType!;
		var hCell = casing
			.Or(Predicates.AutoAbilities(new[] { recipeType },
				checkEnergyIn: false, checkEnergyOut: false,
				checkItemIn: true, checkItemOut: true,
				checkFluidIn: true, checkFluidOut: true))
			.Or(Predicates.AutoAbilities(checkMaintenance: true, checkMuffler: needsMuffler, checkParallel: false));
		var rotorHolder = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.ROTOR_HOLDER)
			.SetExactLimit(1);
		var energyOut = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.OUTPUT_ENERGY)
			.SetExactLimit(1);
		return new BlockPattern(MultiblockShapes.LargeTurbine,
			new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['C'] = casing,
			['G'] = Predicates.Blocks(gearTile),
			['R'] = rotorHolder.Or(energyOut),
			['H'] = hCell,
		});
	}

	private static void LargeMiner(string id, string label, VoltageTier tier,
		string casingTile, string frameMaterialId)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockLargeMiner,
			Tiered = false, Tiers = new[] { tier },
			RecipeType = GTRecipeTypes.LARGE_MINER,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/large_miner", OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = () => BuildLargeMinerPattern(id, casingTile, frameMaterialId),
			FusedCasingTileName = casingTile,
		});
	}

	private static IBlockPattern BuildLargeMinerPattern(string id,
		string casingTile, string frameMaterialId)
	{
		var def    = MachineRegistry.Get(id)!;
		var casing = Predicates.Blocks(casingTile);
		var exportItems  = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_ITEMS).SetExactLimit(1);
		var importFluids = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS).SetExactLimit(1);
		var energyIn     = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY)
			.SetMinGlobalLimited(1).SetMaxGlobalLimited(2);
		var frameMat = Common.Materials.MaterialRegistry.Get(frameMaterialId)
			?? throw new System.InvalidOperationException(
				$"LargeMiner({id}): material '{frameMaterialId}' not found in MaterialRegistry");
		return new BlockPattern(MultiblockShapes.LargeMiner, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = casing.Or(exportItems).Or(importFluids).Or(energyIn),
			['C'] = casing,
			['F'] = Predicates.Frames(frameMat),
		});
	}

	private static void FluidDrillingRig(string id, string label, VoltageTier tier,
		string casingTile, string frameMaterialId)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockFluidDrillingRig,
			Tiered = false, Tiers = new[] { tier },
			RecipeType = GTRecipeTypes.FLUID_DRILLING_RIG,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/fluid_drilling_rig", OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = () => BuildFluidDrillingRigPattern(id, casingTile, frameMaterialId),
			FusedCasingTileName = casingTile,
		});
	}

	private static IBlockPattern BuildFluidDrillingRigPattern(string id,
		string casingTile, string frameMaterialId)
	{
		var def    = MachineRegistry.Get(id)!;
		var casing = Predicates.Blocks(casingTile);
		var energyIn   = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY)
			.SetMinGlobalLimited(1).SetMaxGlobalLimited(2);
		var fluidOut   = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS)
			.SetMaxGlobalLimited(1);
		var frameMat = Common.Materials.MaterialRegistry.Get(frameMaterialId)
			?? throw new System.InvalidOperationException(
				$"FluidDrillingRig({id}): material '{frameMaterialId}' not found in MaterialRegistry");
		return new BlockPattern(MultiblockShapes.FluidDrillingRig, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = casing.Or(energyIn).Or(fluidOut),
			['C'] = casing,
			['F'] = Predicates.Frames(frameMat),
		});
	}

	private static void FusionReactor(string id, string label, VoltageTier tier,
		string casingTile, string coilTile)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockFusionReactor,
			Tiered = false, Tiers = new[] { tier },
			RecipeType = GTRecipeTypes.FUSION_REACTOR,
			Casing = MachineCasing.Voltage,
			OverlayDir = "block/multiblock/fusion_reactor", OverlayBasename = "overlay_front",
			LayoutKey = "generic_multi",
			PatternFactory = () => BuildFusionReactorPattern(id, casingTile, coilTile),
			MultiRecipeModifier = GTRecipeModifiers.FUSION_OC,
			FusedCasingTileName = casingTile,
		});
	}

	private static IBlockPattern BuildFusionReactorPattern(string id,
		string casingTile, string coilTile)
	{
		var def    = MachineRegistry.Get(id)!;
		var casing = Predicates.Blocks(casingTile);
		var glass  = Predicates.Blocks("fusion_glass").Or(casing);
		var coil   = Predicates.Blocks(coilTile);
		int reactorTier = def.Tiers[0] is VoltageTier vt ? (int)vt : (int)VoltageTier.LuV;
		var hatchTiles = Api.Machine.Multiblock.PartAbility.INPUT_ENERGY
			.GetTileRange(reactorTier, (int)VoltageTier.UV);
		var energyIn = Predicates.Blocks(System.Linq.Enumerable.ToArray(hatchTiles))
			.SetMinGlobalLimited(1).SetPreviewCount(16);
		var fluidIn  = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS).SetMinGlobalLimited(2);
		var fluidOut = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS);
		return new BlockPattern(MultiblockShapes.FusionReactor, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['C'] = casing,
			['K'] = coil,
			['G'] = glass,
			['E'] = casing.Or(energyIn),
			['O'] = casing.Or(fluidOut),
			['I'] = casing.Or(fluidIn),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildActiveTransformerPattern()
	{
		var def    = MachineRegistry.Get("active_transformer")!;
		var casing = Predicates.Blocks("high_power_casing").SetMinGlobalLimited(2);
		var coil   = Predicates.Blocks("superconducting_coil");
		var inputHatch = Predicates.Abilities(
			Api.Machine.Multiblock.PartAbility.INPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.SUBSTATION_INPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.INPUT_LASER).SetMinGlobalLimited(1);
		var outputHatch = Predicates.Abilities(
			Api.Machine.Multiblock.PartAbility.OUTPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.SUBSTATION_OUTPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.OUTPUT_LASER).SetMinGlobalLimited(1);
		return new BlockPattern(MultiblockShapes.ActiveTransformer, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['C'] = coil,
			['X'] = casing.Or(inputHatch).Or(outputHatch),
		});
	}

	private static IBlockPattern BuildPowerSubstationPattern()
	{
		var def    = MachineRegistry.Get("power_substation")!;
		var casing = Predicates.Blocks("palladium_substation").SetMinGlobalLimited(3);
		var glass  = Predicates.Blocks("laminated_glass");

		var batteryTileNames = new List<string>(
			Multiblock.Electric.PssBatteryData.All.Keys);
		var batteries = Predicates.Blocks(batteryTileNames.ToArray());

		var inputHatch = Predicates.Abilities(
			Api.Machine.Multiblock.PartAbility.INPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.SUBSTATION_INPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.INPUT_LASER).SetMinGlobalLimited(1);
		var outputHatch = Predicates.Abilities(
			Api.Machine.Multiblock.PartAbility.OUTPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.SUBSTATION_OUTPUT_ENERGY,
			Api.Machine.Multiblock.PartAbility.OUTPUT_LASER).SetMinGlobalLimited(1);

		return new BlockPattern(MultiblockShapes.PowerSubstation, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['C'] = casing,
			['G'] = glass,
			['B'] = batteries,
			['X'] = casing.Or(inputHatch).Or(outputHatch),
		});
	}

	private static IBlockPattern BuildSteamGrinderPattern()
	{
		var def    = MachineRegistry.Get("steam_grinder")!;
		var casing = Predicates.Blocks("steam_machine_casing");
		var steamIn  = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.STEAM_IMPORT_ITEMS);
		var steamOut = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.STEAM_EXPORT_ITEMS);
		var steamEnergy = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.STEAM).SetExactLimit(1);
		return new BlockPattern(MultiblockShapes.SteamGrinder, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = casing.Or(steamIn).Or(steamOut).Or(steamEnergy),
		});
	}

	private static IBlockPattern BuildSteamOvenPattern()
	{
		var def         = MachineRegistry.Get("steam_oven")!;
		var casing      = Predicates.Blocks("steam_machine_casing");
		var firebox     = Predicates.Blocks("bronze_firebox_casing");
		var steamIn     = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.STEAM_IMPORT_ITEMS);
		var steamOut    = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.STEAM_EXPORT_ITEMS);
		var steamEnergy = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.STEAM).SetExactLimit(1);
		return new BlockPattern(MultiblockShapes.SteamOven, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = casing.Or(steamIn).Or(steamOut),
			['F'] = firebox.Or(steamEnergy),
			['#'] = Predicates.Any(),
		});
	}

	private static IBlockPattern BuildAssemblyLinePattern()
	{
		var def = MachineRegistry.Get("assembly_line")!;
		var steel = Predicates.Blocks("steel_machine_casing");
		var energyIn   = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY);
		var fluidImport = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS_1X);
		var dataAccess = Predicates.Abilities(
				Api.Machine.Multiblock.PartAbility.DATA_ACCESS,
				Api.Machine.Multiblock.PartAbility.OPTICAL_DATA_RECEPTION)
			.SetExactLimit(1);
		return new RepeatableBlockPattern(MultiblockShapes.AssemblyLine,
			new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['I'] = Predicates.Blocks("ulv_input_bus"),
			['O'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_ITEMS),
			['G'] = Predicates.Blocks("assembly_line_grating"),
			['D'] = Predicates.Blocks("assembly_line_grating").Or(dataAccess),
			['R'] = Predicates.Blocks("laminated_glass"),
			['Y'] = steel.Or(energyIn),
			['F'] = steel.Or(fluidImport),
		});
	}

	private static IBlockPattern BuildHpcaPattern()
	{
		var def = MachineRegistry.Get("high_performance_computation_array")!;
		return new BlockPattern(MultiblockShapes.HighPerformanceComputationArray,
			new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['A'] = Predicates.Blocks("advanced_computer_casing"),
			['V'] = Predicates.Blocks("computer_heat_vent"),
			['X'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.HPCA_COMPONENT),
			['C'] = Predicates.Blocks("computer_casing")
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.COMPUTATION_DATA_TRANSMISSION).SetExactLimit(1))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MAINTENANCE)),
		});
	}

	private static IBlockPattern BuildDataBankPattern()
	{
		var def = MachineRegistry.Get("data_bank")!;
		return new BlockPattern(MultiblockShapes.DataBank,
			new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.Blocks("computer_heat_vent"),
			['D'] = Predicates.Blocks("computer_casing")
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.DATA_ACCESS))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.OPTICAL_DATA_TRANSMISSION))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.OPTICAL_DATA_RECEPTION))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MAINTENANCE)),
		});
	}

	private static IBlockPattern BuildNetworkSwitchPattern()
	{
		var def = MachineRegistry.Get("network_switch")!;
		return new BlockPattern(MultiblockShapes.NetworkSwitch,
			new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.Blocks("computer_casing")
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.COMPUTATION_DATA_TRANSMISSION))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.COMPUTATION_DATA_RECEPTION))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MAINTENANCE)),
		});
	}

	private static IBlockPattern BuildResearchStationPattern()
	{
		var def = MachineRegistry.Get("research_station")!;
		return new BlockPattern(MultiblockShapes.ResearchStation,
			new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.Blocks("computer_casing"),
			['V'] = Predicates.Blocks("computer_heat_vent"),
			['A'] = Predicates.Blocks("advanced_computer_casing"),
			['H'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.OBJECT_HOLDER),
			['P'] = Predicates.Blocks("computer_casing")
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.COMPUTATION_DATA_RECEPTION).SetExactLimit(1))
				.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MAINTENANCE)),
		});
	}

	private static IBlockPattern BuildLargeCentrifugePattern()
	{
		var def = MachineRegistry.Get("large_centrifuge")!;
		return new BlockPattern(MultiblockShapes.LargeCentrifuge, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("vibration_safe_casing",
				new[] { GTRecipeTypes.CENTRIFUGE, GTRecipeTypes.THERMAL_CENTRIFUGE }, parallel: true),
			['P'] = Predicates.Blocks("steel_pipe_casing"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeElectrolyzerPattern()
	{
		var def = MachineRegistry.Get("large_electrolyzer")!;
		return new BlockPattern(MultiblockShapes.LargeElectrolyzer, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("nonconducting_casing",
				new[] { GTRecipeTypes.ELECTROLYZER }, parallel: true),
			['C'] = Predicates.Blocks("electrolytic_cell"),
		});
	}

	private static IBlockPattern BuildLargeElectromagnetPattern()
	{
		var def = MachineRegistry.Get("large_electromagnet")!;
		return new BlockPattern(MultiblockShapes.LargeElectromagnet, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("nonconducting_casing",
				new[] { GTRecipeTypes.ELECTROMAGNETIC_SEPARATOR, GTRecipeTypes.POLARIZER }, parallel: true),
			['C'] = Predicates.Blocks("electrolytic_cell"),
		});
	}

	private static IBlockPattern BuildLargePackerPattern()
	{
		var def = MachineRegistry.Get("large_packer")!;
		return new BlockPattern(MultiblockShapes.LargePacker, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("robust_machine_casing",
				new[] { GTRecipeTypes.PACKER }, parallel: true),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeAssemblerPattern()
	{
		var def = MachineRegistry.Get("large_assembler")!;
		var wall = Predicates.Blocks("large_scale_assembler_casing")
			.Or(Predicates.AutoAbilities(new[] { GTRecipeTypes.ASSEMBLER },
				checkEnergyIn: false, checkEnergyOut: false,
				checkItemIn: true, checkItemOut: true, checkFluidIn: true, checkFluidOut: true))
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY).SetExactLimit(1))
			.Or(Predicates.AutoAbilities(checkMaintenance: true, checkMuffler: false, checkParallel: true));
		return new BlockPattern(MultiblockShapes.LargeAssembler, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = wall,
			['G'] = Predicates.Blocks("tempered_glass"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeCircuitAssemblerPattern()
	{
		var def = MachineRegistry.Get("large_circuit_assembler")!;
		var wall = Predicates.Blocks("large_scale_assembler_casing")
			.Or(Predicates.AutoAbilities(new[] { GTRecipeTypes.CIRCUIT_ASSEMBLER },
				checkEnergyIn: false, checkEnergyOut: false,
				checkItemIn: true, checkItemOut: true, checkFluidIn: true, checkFluidOut: true))
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY).SetExactLimit(1))
			.Or(Predicates.AutoAbilities(checkMaintenance: true, checkMuffler: false, checkParallel: true));
		return new BlockPattern(MultiblockShapes.LargeCircuitAssembler, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = wall,
			['T'] = Predicates.Blocks("tempered_glass"),
			['G'] = Predicates.Blocks("assembly_line_grating"),
			['P'] = Predicates.Blocks("tungstensteel_pipe_casing"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeArcSmelterPattern()
	{
		var def = MachineRegistry.Get("large_arc_smelter")!;
		return new BlockPattern(MultiblockShapes.LargeArcSmelter, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("high_temperature_smelting_casing",
				new[] { GTRecipeTypes.ARC_FURNACE }, parallel: true),
			['C'] = Predicates.Blocks("molybdenum_disilicide_coil_block"),
			['M'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MUFFLER),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeEngravingLaserPattern()
	{
		var def = MachineRegistry.Get("large_engraving_laser")!;
		return new BlockPattern(MultiblockShapes.LargeEngravingLaser, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("laser_safe_engraving_casing",
				new[] { GTRecipeTypes.LASER_ENGRAVER }, parallel: true),
			['C'] = Predicates.Blocks("tungstensteel_pipe_casing"),
			['G'] = Predicates.Blocks("tempered_glass"),
			['K'] = Predicates.Blocks("assembly_line_grating"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeSiftingFunnelPattern()
	{
		var def = MachineRegistry.Get("large_sifting_funnel")!;
		return new BlockPattern(MultiblockShapes.LargeSiftingFunnel, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("vibration_safe_casing",
				new[] { GTRecipeTypes.SIFTER }, parallel: true),
			['K'] = Predicates.Blocks("assembly_line_grating"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeMaterialPressPattern()
	{
		var def = MachineRegistry.Get("large_material_press")!;
		return new BlockPattern(MultiblockShapes.LargeMaterialPress, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("stress_proof_casing",
				new[] { GTRecipeTypes.BENDER, GTRecipeTypes.COMPRESSOR,
					GTRecipeTypes.FORGE_HAMMER, GTRecipeTypes.FORMING_PRESS }, parallel: true),
			['G'] = Predicates.Blocks("steel_gearbox"),
			['C'] = Predicates.Blocks("tempered_glass"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeBrewerPattern()
	{
		var def = MachineRegistry.Get("large_brewer")!;
		return new BlockPattern(MultiblockShapes.LargeBrewer, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("corrosion_proof_casing",
				new[] { GTRecipeTypes.BREWERY, GTRecipeTypes.FERMENTER, GTRecipeTypes.FLUID_HEATER },
				parallel: true),
			['P'] = Predicates.Blocks("steel_pipe_casing"),
			['C'] = Predicates.Blocks("molybdenum_disilicide_coil_block"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeCutterPattern()
	{
		var def = MachineRegistry.Get("large_cutter")!;
		return new BlockPattern(MultiblockShapes.LargeCutter, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("shock_proof_cutting_casing",
				new[] { GTRecipeTypes.CUTTER, GTRecipeTypes.LATHE }, parallel: true),
			['G'] = Predicates.Blocks("tempered_glass"),
			['C'] = Predicates.Blocks("slicing_blades"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeExtractorPattern()
	{
		var def = MachineRegistry.Get("large_extractor")!;
		return new BlockPattern(MultiblockShapes.LargeExtractor, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("watertight_casing",
				new[] { GTRecipeTypes.EXTRACTOR, GTRecipeTypes.CANNER }, parallel: true),
			['C'] = Predicates.Blocks("steel_pipe_casing"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeExtruderPattern()
	{
		var def = MachineRegistry.Get("large_extruder")!;
		return new BlockPattern(MultiblockShapes.LargeExtruder, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("stress_proof_casing",
				new[] { GTRecipeTypes.EXTRUDER }, parallel: true),
			['P'] = Predicates.Blocks("titanium_pipe_casing"),
			['G'] = Predicates.Blocks("tempered_glass"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeSolidifierPattern()
	{
		var def = MachineRegistry.Get("large_solidifier")!;
		return new BlockPattern(MultiblockShapes.LargeSolidifier, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("watertight_casing",
				new[] { GTRecipeTypes.FLUID_SOLIDIFIER }, parallel: true),
			['C'] = Predicates.Blocks("steel_pipe_casing"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeWiremillPattern()
	{
		var def = MachineRegistry.Get("large_wiremill")!;
		return new BlockPattern(MultiblockShapes.LargeWiremill, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("stress_proof_casing",
				new[] { GTRecipeTypes.WIREMILL }, parallel: true),
			['C'] = Predicates.Blocks("titanium_gearbox"),
		});
	}

	private static IBlockPattern BuildMegaVacuumFreezerPattern()
	{
		var def = MachineRegistry.Get("mega_vacuum_freezer")!;
		return new BlockPattern(MultiblockShapes.MegaVacuumFreezer, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("frostproof_machine_casing",
				new[] { GTRecipeTypes.VACUUM_RECIPES }, parallel: true),
			['G'] = Predicates.Blocks("tempered_glass"),
			['K'] = Predicates.Blocks("clean_machine_casing"),
			['P'] = Predicates.Blocks("tungstensteel_pipe_casing"),
			['V'] = Predicates.Blocks("heat_vent"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildMultiSmelterPattern()
	{
		var def = MachineRegistry.Get("multi_smelter")!;
		return new BlockPattern(MultiblockShapes.MultiSmelter, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("heatproof_machine_casing",
				new[] { GTRecipeTypes.ELECTRIC_FURNACE, GTRecipeTypes.ALLOY_SMELTER }),
			['C'] = Predicates.HeatingCoils(),
			['M'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MUFFLER),
		});
	}

	private static IBlockPattern BuildPyrolyseOvenPattern()
	{
		var def = MachineRegistry.Get("pyrolyse_oven")!;
		return new BlockPattern(MultiblockShapes.PyrolyseOven, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("ulv_machine_casing",
				new[] { GTRecipeTypes.PYROLYSE_RECIPES }, muffler: true),
			['C'] = Predicates.HeatingCoils(),
		});
	}

	private static IBlockPattern BuildCrackerPattern()
	{
		var def = MachineRegistry.Get("cracker")!;
		return new BlockPattern(MultiblockShapes.Cracker, new Dictionary<char, TraceabilityPredicate>
		{
			['O'] = Predicates.Controller(def),
			['H'] = Predicates.StandardWall("clean_machine_casing",
				new[] { GTRecipeTypes.CRACKING_RECIPES }, muffler: true),
			['C'] = Predicates.HeatingCoils(),
		});
	}

	private static IBlockPattern BuildAlloyBlastSmelterPattern()
	{
		var def = MachineRegistry.Get("alloy_blast_smelter")!;
		return new BlockPattern(MultiblockShapes.AlloyBlastSmelter, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("high_temperature_smelting_casing",
				new[] { GTRecipeTypes.ALLOY_BLAST_RECIPES }),
			['C'] = Predicates.HeatingCoils(),
			['M'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MUFFLER),
			['G'] = Predicates.Blocks("heat_vent"),
			['F'] = Predicates.Blocks("hastelloy_x_frame"),
		});
	}

	private static IBlockPattern BuildMegaBlastFurnacePattern()
	{
		var def = MachineRegistry.Get("mega_blast_furnace")!;
		return new BlockPattern(MultiblockShapes.MegaBlastFurnace, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("high_temperature_smelting_casing",
				new[] { GTRecipeTypes.BLAST_RECIPES }, parallel: true),
			['C'] = Predicates.HeatingCoils(),
			['M'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MUFFLER),
			['T'] = Predicates.Blocks("robust_machine_casing"),
			['B'] = Predicates.Blocks("tungstensteel_firebox_casing"),
			['P'] = Predicates.Blocks("tungstensteel_pipe_casing"),
			['I'] = Predicates.Blocks("extreme_engine_intake_casing"),
			['V'] = Predicates.Blocks("heat_vent"),
			['F'] = Predicates.Blocks("naquadah_alloy_frame"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildEBFPattern()
	{
		var ebfDef = MachineRegistry.Get("electric_blast_furnace")!;
		return new BlockPattern(MultiblockShapes.ElectricBlastFurnace, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(ebfDef),
			['X'] = Predicates.StandardWall("heatproof_machine_casing", new[] { GTRecipeTypes.BLAST_RECIPES }),
			['C'] = Predicates.HeatingCoils(),
			['M'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MUFFLER),
		});
	}

	private static IBlockPattern BuildCleanroomPattern()
	{
		// Cleanroom doesn't use autoAbilities upstream - its walls accept PASSTHROUGH_HATCH
		// (max 30) + INPUT_ENERGY (min 1, max 3) + MAINTENANCE (exact 1) alongside
		// the casing
		var cleanroomDef = MachineRegistry.Get("cleanroom")!;
		var wallPred = Predicates.Blocks("plascrete", "cleanroom_glass")
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.INPUT_ENERGY)
				.SetMinGlobalLimited(1).SetMaxGlobalLimited(3))
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MAINTENANCE)
				.SetExactLimit(1))
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.PASSTHROUGH_HATCH)
				.SetMaxGlobalLimited(30));

		return new RepeatableBlockPattern(MultiblockShapes.Cleanroom, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(cleanroomDef),
			['X'] = wallPred,
			['F'] = Predicates.CleanroomFilters(),
			[' '] = Predicates.Custom(Multiblock.Electric.CleanroomMachine.InnerPredicateMatch,
				() => System.Array.Empty<Terraria.Item>()),
		});
	}

	private static IBlockPattern BuildCokeOvenPattern()
	{
		var cokeOvenDef = MachineRegistry.Get("coke_oven")!;
		return new BlockPattern(MultiblockShapes.CokeOven, new Dictionary<char, TraceabilityPredicate>
		{
			['Y'] = Predicates.Controller(cokeOvenDef),
			['X'] = Predicates.Blocks("coke_oven_bricks")
				.Or(Predicates.Blocks("coke_oven_hatch").SetMaxGlobalLimited(5)),
		});
	}

	private static void LargeBoiler(string tierId, string label,
		string casingTile, string pipeTile, string fireboxTile,
		int maxTemperature, int heatSpeed)
	{
		string id = $"{tierId}_large_boiler";
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockLargeBoiler,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.LARGE_BOILER,
			BoilerMaxTemperature = maxTemperature,
			BoilerHeatSpeed      = heatSpeed,
			FusedCasingTileName = casingTile,
			OverlayDir          = $"block/multiblock/generator/large_{tierId}_boiler",
			OverlayBasename     = "overlay_front",
			LayoutKey           = "large_boiler",
			PatternFactory      = () => BuildLargeBoilerPattern(id, casingTile, pipeTile, fireboxTile),
		});
	}

	private static IBlockPattern BuildLargeBoilerPattern(string id,
		string casingTile, string pipeTile, string fireboxTile)
	{
		var def = MachineRegistry.Get(id)!;
		var casingOrExportFluids = Predicates.Blocks(casingTile)
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.EXPORT_FLUIDS)
				.SetMinGlobalLimited(1));
		var fireboxOrInputs = Predicates.Blocks(fireboxTile).SetMinGlobalLimited(3)
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_FLUIDS)
				.SetMinGlobalLimited(1))
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.IMPORT_ITEMS)
				.SetMaxGlobalLimited(1))
			.Or(Predicates.Abilities(Api.Machine.Multiblock.PartAbility.MUFFLER)
				.SetExactLimit(1));
		return new BlockPattern(MultiblockShapes.LargeBoiler, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['C'] = casingOrExportFluids,
			['P'] = Predicates.Blocks(pipeTile),
			['X'] = fireboxOrInputs,
		});
	}

	private static IBlockPattern BuildPrimitivePumpPattern()
	{
		var def      = MachineRegistry.Get("primitive_pump")!;
		var treated  = Common.Materials.MaterialRegistry.Get("treated_wood")!;
		return new BlockPattern(MultiblockShapes.PrimitivePump, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.Blocks("pump_deck"),
			['F'] = Predicates.Frames(treated),
			['H'] = Predicates.Abilities(Api.Machine.Multiblock.PartAbility.PUMP_FLUID_HATCH),
		});
	}

	private static IBlockPattern BuildPrimitiveBlastFurnacePattern()
	{
		var def = MachineRegistry.Get("primitive_blast_furnace")!;
		return new BlockPattern(MultiblockShapes.PrimitiveBlastFurnace, new Dictionary<char, TraceabilityPredicate>
		{
			['Y'] = Predicates.Controller(def),
			['X'] = Predicates.Blocks("firebricks"),
		});
	}

	private static IBlockPattern BuildLargeChemicalBathPattern()
	{
		var def = MachineRegistry.Get("large_chemical_bath")!;
		return new BlockPattern(MultiblockShapes.LargeChemicalBath, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("watertight_casing",
				new[] { GTRecipeTypes.CHEMICAL_BATH, GTRecipeTypes.ORE_WASHER }, parallel: true),
			['T'] = Predicates.Blocks("titanium_pipe_casing"),
		});
	}

	private static IBlockPattern BuildLargeMacerationTowerPattern()
	{
		var def = MachineRegistry.Get("large_maceration_tower")!;
		return new BlockPattern(MultiblockShapes.LargeMacerationTower, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("secure_maceration_casing",
				new[] { GTRecipeTypes.MACERATOR }, parallel: true),
			['G'] = Predicates.Blocks("crushing_wheels"),
			['A'] = Predicates.Air(),
		});
	}

	private static IBlockPattern BuildLargeMixerPattern()
	{
		var def       = MachineRegistry.Get("large_mixer")!;
		var hastelloy = Common.Materials.MaterialRegistry.Get("hastelloy_x")!;
		return new BlockPattern(MultiblockShapes.LargeMixer, new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.StandardWall("reaction_safe_mixing_casing",
				new[] { GTRecipeTypes.MIXER }, parallel: true),
			['F'] = Predicates.Frames(hastelloy),
			['G'] = Predicates.Blocks("stainless_steel_gearbox"),
			['P'] = Predicates.Blocks("titanium_pipe_casing"),
		});
	}

	private static void MultiblockTankValve(string id, string label, string casingTile)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.TankValve,
			Tiered = false, Tiers = OneTier,
			PartIo = IO.BOTH,
			PartFluidSlots = 1,
			FusedCasingTileName = casingTile,
			OverlayDir          = "block/multiblock/tank_valve",
			OverlayBasename     = "overlay_front",
			LayoutKey           = "none",
		});
	}

	private static void MultiblockTank(string id, string label, string? materialId,
		int capacity, string casingTile, string valveTile)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.MultiblockTank,
			Tiered = false, Tiers = OneTier,
			RecipeType = GTRecipeTypes.DUMMY,
			Capacity   = capacity,
			MaterialId = materialId,
			FusedCasingTileName = casingTile,
			OverlayDir          = "block/multiblock/multiblock_tank",
			OverlayBasename     = "overlay_front",
			LayoutKey           = "multiblock_tank",
			PatternFactory      = () => BuildMultiblockTankPattern(id, casingTile, valveTile),
		});
	}

	private static IBlockPattern BuildMultiblockTankPattern(string id,
		string casingTile, string valveTile)
	{
		var def = MachineRegistry.Get(id)!;
		return new BlockPattern(MultiblockShapes.MultiblockTank,
			new Dictionary<char, TraceabilityPredicate>
		{
			['S'] = Predicates.Controller(def),
			['X'] = Predicates.Blocks(casingTile, valveTile),
		});
	}

	private static void Transformer(string id, string label, int baseAmp)
	{
		MachineRegistry.Register(new MachineDefinition
		{
			Id = id, Label = label,
			Family = MachineFamily.Transformer,
			Tiers = TransformerTiers,
			BaseAmp = baseAmp,
			LayoutKey = "none",
		});
	}
}
