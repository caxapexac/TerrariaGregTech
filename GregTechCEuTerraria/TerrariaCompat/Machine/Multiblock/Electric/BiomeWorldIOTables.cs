#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Worldgen;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

public static class BiomeWorldIOTables
{
	public enum MinerBucket
	{
		Ocean, Underworld,
		Forest, Desert, Snow, Jungle, Corruption, Crimson, Hallow, Mushroom,
		Underground, UndergroundDesert, UndergroundSnow, UndergroundJungle,
		UndergroundCorruption, UndergroundCrimson, UndergroundHallow, UndergroundMushroom,
		Cavern, CavernDesert, CavernSnow, CavernJungle,
		CavernCorruption, CavernCrimson, CavernHallow, CavernMushroom,
		Granite, Marble, Hive, GemCave, LihzahrdTemple, Dungeon,
	}

	private static readonly Dictionary<MinerBucket, string[]> _veinsByBucket = new()
	{
		[MinerBucket.Ocean]                 = new[] { "salts_vein" },
		[MinerBucket.Underworld]            = new[] { "sulfur_vein", "nether_quartz_vein" },

		[MinerBucket.Forest]                = new[] { "iron_vein", "copper_tin_vein" },
		[MinerBucket.Desert]                = new[] { "mineral_sand_vein" },
		[MinerBucket.Snow]                  = new[] { "cassiterite_vein" },
		[MinerBucket.Jungle]                = new[] { "magnetite_vein_ow" },
		[MinerBucket.Corruption]            = new[] { "galena_vein" },
		[MinerBucket.Crimson]               = new[] { "garnet_vein" },
		[MinerBucket.Hallow]                = new[] { "sheldonite_vein" },
		[MinerBucket.Mushroom]              = new[] { "manganese_vein_ow" },

		[MinerBucket.Underground]           = new[] { "coal_vein" },
		[MinerBucket.UndergroundDesert]     = new[] { "oilsands_vein" },
		[MinerBucket.UndergroundSnow]       = new[] { "nickel_vein" },
		[MinerBucket.UndergroundJungle]     = new[] { "lubricant_vein", "manganese_vein" },
		[MinerBucket.UndergroundCorruption] = new[] { "saltpeter_vein" },
		[MinerBucket.UndergroundCrimson]    = new[] { "banded_iron_vein" },
		[MinerBucket.UndergroundHallow]     = new[] { "olivine_vein" },
		[MinerBucket.UndergroundMushroom]   = new[] { "apatite_vein" },

		[MinerBucket.Cavern]                = new[] { "redstone_vein_ow" },
		[MinerBucket.CavernDesert]          = new[] { "monazite_vein" },
		[MinerBucket.CavernSnow]            = new[] { "copper_vein", "tetrahedrite_vein" },
		[MinerBucket.CavernJungle]          = new[] { "mica_vein", "beryllium_vein" },
		[MinerBucket.CavernCorruption]      = new[] { "sapphire_vein" },
		[MinerBucket.CavernCrimson]         = new[] { "redstone_vein" },
		[MinerBucket.CavernHallow]          = new[] { "diamond_vein" },
		[MinerBucket.CavernMushroom]        = new[] { "lapis_vein" },

		[MinerBucket.Granite]               = new[] { "pitchblende_vein_end" },
		[MinerBucket.Marble]                = new[] { "certus_quartz" },
		[MinerBucket.Hive]                  = new[] { "molybdenum_vein" },
		[MinerBucket.GemCave]               = new[] { "topaz_vein" },
		[MinerBucket.LihzahrdTemple]        = new[] { "naquadah_vein" },
		[MinerBucket.Dungeon]               = new[] { "scheelite_vein" },
	};

	public static MinerBucket Classify(int tileX, int tileY)
	{
		if (BiomeProbe.DepthAt(tileY) == BiomeProbe.Depth.Underworld) return MinerBucket.Underworld;

		switch (BiomeProbe.WallBiomeAt(tileX, tileY))
		{
			case BiomeProbe.WallBiome.Granite:        return MinerBucket.Granite;
			case BiomeProbe.WallBiome.Marble:         return MinerBucket.Marble;
			case BiomeProbe.WallBiome.Hive:           return MinerBucket.Hive;
			case BiomeProbe.WallBiome.GemCave:        return MinerBucket.GemCave;
			case BiomeProbe.WallBiome.LihzahrdTemple: return MinerBucket.LihzahrdTemple;
			case BiomeProbe.WallBiome.Dungeon:        return MinerBucket.Dungeon;
		}

		if (WorldGen.oceanDepths(tileX, tileY)) return MinerBucket.Ocean;

		var m = BiomeProbe.ScanAt(tileX, tileY);
		var depth = BiomeProbe.DepthAt(tileY);
		return Combine(depth, Infection(m));
	}

	private enum Inf { None, Desert, Snow, Jungle, Corruption, Crimson, Hallow, Mushroom }

	private static Inf Infection(SceneMetrics m)
	{
		if (m.EnoughTilesForGlowingMushroom) return Inf.Mushroom;
		if (m.EnoughTilesForJungle)          return Inf.Jungle;
		if (m.EnoughTilesForSnow)            return Inf.Snow;
		if (m.EnoughTilesForHallow)          return Inf.Hallow;
		if (m.EnoughTilesForCrimson)         return Inf.Crimson;
		if (m.EnoughTilesForCorruption)      return Inf.Corruption;
		if (m.EnoughTilesForDesert)          return Inf.Desert;
		return Inf.None;
	}

	private static MinerBucket Combine(BiomeProbe.Depth depth, Inf inf) => depth switch
	{
		BiomeProbe.Depth.Cavern => inf switch
		{
			Inf.Desert => MinerBucket.CavernDesert, Inf.Snow => MinerBucket.CavernSnow,
			Inf.Jungle => MinerBucket.CavernJungle, Inf.Corruption => MinerBucket.CavernCorruption,
			Inf.Crimson => MinerBucket.CavernCrimson, Inf.Hallow => MinerBucket.CavernHallow,
			Inf.Mushroom => MinerBucket.CavernMushroom, _ => MinerBucket.Cavern,
		},
		BiomeProbe.Depth.Underground => inf switch
		{
			Inf.Desert => MinerBucket.UndergroundDesert, Inf.Snow => MinerBucket.UndergroundSnow,
			Inf.Jungle => MinerBucket.UndergroundJungle, Inf.Corruption => MinerBucket.UndergroundCorruption,
			Inf.Crimson => MinerBucket.UndergroundCrimson, Inf.Hallow => MinerBucket.UndergroundHallow,
			Inf.Mushroom => MinerBucket.UndergroundMushroom, _ => MinerBucket.Underground,
		},
		_ => inf switch // Surface
		{
			Inf.Desert => MinerBucket.Desert, Inf.Snow => MinerBucket.Snow,
			Inf.Jungle => MinerBucket.Jungle, Inf.Corruption => MinerBucket.Corruption,
			Inf.Crimson => MinerBucket.Crimson, Inf.Hallow => MinerBucket.Hallow,
			Inf.Mushroom => MinerBucket.Mushroom, _ => MinerBucket.Forest,
		},
	};

	public readonly record struct PoolEntry(string MaterialId, int ItemType, int Weight);

	private static readonly Dictionary<MinerBucket, List<PoolEntry>> _poolCache = new();

	public static IReadOnlyList<PoolEntry> GetPool(MinerBucket bucket)
	{
		if (_poolCache.TryGetValue(bucket, out var cached)) return cached;

		var byItem = new Dictionary<int, PoolEntry>();
		if (_veinsByBucket.TryGetValue(bucket, out var veinIds))
		{
			foreach (var vid in veinIds)
			{
				var vein = VeinRegistry.All.FirstOrDefault(v => v.Id == vid);
				if (vein is null) continue;
				foreach (var mat in vein.Materials)
				{
					int item = ResolveOreItem(mat.MaterialId);
					if (item <= 0) continue;
					byItem[item] = byItem.TryGetValue(item, out var e)
						? e with { Weight = e.Weight + mat.Weight }
						: new PoolEntry(mat.MaterialId, item, mat.Weight);
				}
			}
		}

		var list = byItem.Values.ToList();
		_poolCache[bucket] = list;
		return list;
	}

	public static (int ItemType, string MaterialId) RollFromBucket(MinerBucket bucket, System.Random rng)
	{
		var pool = GetPool(bucket);
		if (pool.Count == 0) return (0, "");

		int totalWeight = 0;
		foreach (var e in pool) totalWeight += e.Weight;
		if (totalWeight <= 0) return (0, "");

		int roll = rng.Next(totalWeight);
		int accum = 0;
		foreach (var e in pool)
		{
			accum += e.Weight;
			if (roll < accum) return (e.ItemType, e.MaterialId);
		}
		return (0, "");
	}

	public static string Label(MinerBucket bucket)
	{
		string name = bucket.ToString();
		var sb = new StringBuilder(name.Length + 4);
		for (int i = 0; i < name.Length; i++)
		{
			if (i > 0 && char.IsUpper(name[i])) sb.Append(' ');
			sb.Append(name[i]);
		}
		return sb.ToString();
	}

	private static readonly Dictionary<string, int> _vanillaOreFallback = new()
	{
		["iron"]     = ItemID.IronOre,
		["copper"]   = ItemID.CopperOre,
		["gold"]     = ItemID.GoldOre,
		["tin"]      = ItemID.TinOre,
		["silver"]   = ItemID.SilverOre,
		["lead"]     = ItemID.LeadOre,
		["platinum"] = ItemID.PlatinumOre,
		["tungsten"] = ItemID.TungstenOre,
		["titanium"] = ItemID.TitaniumOre,
		["cobalt"]   = ItemID.CobaltOre,
	};

	public static int ResolveOreItem(string materialId)
	{
		int? gtRaw = MaterialItemRegistry.Get(materialId, "raw_ore");
		if (gtRaw is not null) return gtRaw.Value;
		if (_vanillaOreFallback.TryGetValue(materialId, out var vanilla)) return vanilla;
		int? gem = MaterialItemRegistry.Get(materialId, "gem");
		if (gem is not null) return gem.Value;
		int? dust = MaterialItemRegistry.Get(materialId, "dust");
		return dust ?? 0;
	}

	private static readonly Dictionary<BiomeProbe.Biome, string> _fluidMaterialIds = new()
	{
		[BiomeProbe.Biome.Forest]     = "oil_light",
		[BiomeProbe.Biome.Desert]     = "oil_medium",
		[BiomeProbe.Biome.Snow]       = "natural_gas",
		[BiomeProbe.Biome.Jungle]     = "natural_gas",
		[BiomeProbe.Biome.Ocean]      = "salt_water",
		[BiomeProbe.Biome.Mushroom]   = "natural_gas",
		[BiomeProbe.Biome.Crimson]    = "lava",
		[BiomeProbe.Biome.Corruption] = "oil_heavy",
		[BiomeProbe.Biome.Hallow]     = "oil",
		[BiomeProbe.Biome.Underworld] = "lava",
		[BiomeProbe.Biome.Underground] = "oil_medium",
		[BiomeProbe.Biome.Cavern]     = "oil",
	};

	public static FluidType? GetFluid(BiomeProbe.Biome biome)
	{
		if (biome == BiomeProbe.Biome.Underworld) return FluidRegistry.Lava;
		if (biome == BiomeProbe.Biome.Crimson)    return FluidRegistry.Lava;
		var id = _fluidMaterialIds.TryGetValue(biome, out var matId) ? matId : "oil";
		return FluidRegistry.Get(id);
	}
}
