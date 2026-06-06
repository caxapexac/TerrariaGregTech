#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Data.Chemical.Material;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools;

// Material -> Terraria-progression tier
public static class ToolTier
{
	public const int TierCount = 10;

	// Default HL -> tier (indexed by HL): HL1 flint/wood->ULV, HL2 iron->MV,
	// HL3 steel->HV (fanned by overrides), HL4 ->IV, HL5 ->ZPM, HL6 ->UHV+.
	private static readonly int[] HLToTier = { 0, 0, 2, 3, 5, 7, 9, 9 };

	private static readonly Dictionary<string, int> Overrides = new(StringComparer.Ordinal)
	{
		// Steam
		["bronze"]           = 0,
		["invar"]            = 0,
		// LV
		["iron"]             = 1,
		["wrought_iron"]     = 1,
		["rose_gold"]        = 1,
		["steel"]            = 1,
		// MV
		["aluminium"]        = 2,
		["cobalt_brass"]     = 2,
		["sterling_silver"]  = 2,
		// HV
		["stainless_steel"]  = 3,
		["damascus_steel"]   = 3,
		["blue_steel"]       = 3,
		["red_steel"]        = 3,
		["diamond"]          = 3,
		// EV
		["vanadium_steel"]   = 4,
		["titanium"]         = 4,
		// IV
		["tungsten_carbide"] = 5,
		["tungsten_steel"]   = 5,
		["netherite"]        = 5,
		// LuV
		["hsse"]             = 6,
		["ultimet"]          = 6,
		// ZPM
		["duranium"]         = 7,
		// UV
		["naquadah_alloy"]   = 8,
		// UHV+
		["neutronium"]       = 9,
	};

	public static int For(Material m)
	{
		if (Overrides.TryGetValue(m.Id, out int t)) return t;
		int hl = m.Tool?.HarvestLevel ?? 0;
		return HLToTier[Math.Clamp(hl, 0, HLToTier.Length - 1)];
	}

	// Pickaxe / Hammer / Damage are raw item stats that Terraria displays
	// Axe is the exception - Terraria displays it as item.axe * 5
	public readonly record struct Anchor(int Pick, int Axe, int Hammer, int Damage, int UseTime);

	private static readonly Anchor[] Anchors =
	{
		new( 40,   8,  35,  10, 23), // 0 ULV   - copper
		new( 60,  11,  45,  16, 21), // 1 LV    - iron
		new( 80,  14,  55,  22, 19), // 2 MV    - silver
		new(130,  17,  70,  40, 17), // 3 HV    - molten
		new(230,  20,  85,  65, 15), // 4 EV    - adamantite
		new(245,  22,  95,  90, 13), // 5 IV    - chloro
		new(250,  24, 105, 115, 11), // 6 LuV   - spectre
		new(255,  25, 115, 145,  9), // 7 ZPM   - luminite
		new(260,  25, 125, 185,  7), // 8 UV    - luminite
		new(265,  26, 140, 230,  5), // 9 UHV+  - post-ml
	};

	public static Anchor AnchorFor(int tier) =>
		Anchors[Math.Clamp(tier, 0, Anchors.Length - 1)];

	// Blend weight toward the tier anchor
	public const float AnchorBlend = 0.8f;

	public static int Blend(int upstream, int anchor) =>
		(int)Math.Round(upstream * (1f - AnchorBlend) + anchor * AnchorBlend);
}
