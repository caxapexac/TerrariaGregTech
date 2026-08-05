#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Data.Chemical.Material;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools;

public static class ToolTier
{
	public const int TierCount = 10;

	private static readonly int[] HLToTier = { 0, 0, 1, 2, 4, 6, 7, 9, 9 };

	private static readonly Dictionary<string, int> Overrides = new(StringComparer.Ordinal)
	{
		// Platinum (59)
		["flint"]            = 0,
		["bronze"]           = 0,
		["invar"]            = 0,
		["iron"]             = 0,
		["wrought_iron"]     = 0,
		// Deathbringer (70)
		["steel"]            = 1,
		["diamond"]          = 1,
		["cobalt_brass"]     = 1,
		// Cobalt (110)
		["sterling_silver"]  = 2,
		["rose_gold"]        = 2,
		["aluminium"]        = 2,
		// Titanium pick (190)
		["damascus_steel"]   = 4,
		["stainless_steel"]  = 4,
		["vanadium_steel"]   = 4,
		// Pickaxe Axe / Drax (200)
		["titanium"]         = 5,
		["red_steel"]        = 5,
		["blue_steel"]       = 5,
		// Picksaw (210)
		["tungsten_carbide"] = 6,
		["tungsten_steel"]   = 6,
		// Luminite (225)
		["ultimet"]          = 7,
		["hsse"]             = 7,
		["duranium"]         = 7,
		["naquadah_alloy"]   = 7,
		// Laser Drill (230)
		["neutronium"]       = 9,
	};

	public static int For(Material m)
	{
		if (Overrides.TryGetValue(m.Id, out int t)) return t;
		int hl = m.Tool?.HarvestLevel ?? 0;
		return HLToTier[Math.Clamp(hl, 0, HLToTier.Length - 1)];
	}

	public readonly record struct Anchor(int Pick, int Axe, int Hammer, int Damage, int UseTime);

	private static readonly Anchor[] Anchors =
	{
		new( 59, 12,  59,  16, 18), // 0  Platinum
		new( 70, 13,  62,  22, 16), // 1  Deathbringer
		new(110, 16,  70,  40, 14), // 2  Cobalt
		new(150, 17,  78,  50, 12), // 3
		new(190, 18,  85,  61, 11), // 4  Titanium pick
		new(200, 20,  95,  72,  9), // 5  Pickaxe Axe / Drax / True Excalibur
		new(210, 22, 105,  85,  8), // 6  Picksaw / Terra Blade
		new(225, 24, 118, 100,  7), // 7  Luminite / Influx Waver
		new(228, 25, 128, 150,  6), // 8
		new(230, 26, 140, 200,  5), // 9  Laser Drill
	};

	public static Anchor AnchorFor(int tier) =>
		Anchors[Math.Clamp(tier, 0, Anchors.Length - 1)];

	private static readonly Dictionary<string, int> SpeedUseTimes = new(StringComparer.Ordinal)
	{
		// Slow
		["iron"]             = 20,
		["wrought_iron"]     = 20,
		["steel"]            = 20,
		// Average
		["flint"]            = 15,
		["diamond"]          = 15,
		["ultimet"]          = 15,
		["hsse"]             = 15,
		["vanadium_steel"]   = 15,
		["damascus_steel"]   = 15,
		// Fast
		["bronze"]           = 11,
		["invar"]            = 11,
		["cobalt_brass"]     = 11,
		["sterling_silver"]  = 11,
		["rose_gold"]        = 11,
		["naquadah_alloy"]   = 11,
		// Very fast
		["aluminium"]        = 8,
		["stainless_steel"]  = 8,
		["red_steel"]        = 8,
		["blue_steel"]       = 8,
		["duranium"]         = 8,
		// Insanely fast
		["titanium"]         = 5,
		["tungsten_carbide"] = 5,
		["tungsten_steel"]   = 5,
		["neutronium"]       = 5,
	};

	public static int SpeedUseTime(Material m)
	{
		if (SpeedUseTimes.TryGetValue(m.Id, out int t)) return t;
		return AnchorFor(For(m)).UseTime;
	}

}
