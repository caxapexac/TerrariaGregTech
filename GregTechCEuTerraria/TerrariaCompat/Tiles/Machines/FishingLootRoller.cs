#nullable enable
using System;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public static class FishingLootRoller
{
	private enum Rarity { Junk, Common, Uncommon, Rare, VeryRare, Legendary, Crate }

	public static int FishingPower(VoltageTier tier) => tier switch
	{
		VoltageTier.LV  => 30,
		VoltageTier.MV  => 55,
		VoltageTier.HV  => 85,
		VoltageTier.EV  => 120,
		VoltageTier.IV  => 160,
		VoltageTier.LuV => 210,
		_               => 30,
	};

	public static float SyntheticLuck(VoltageTier tier) => tier switch
	{
		VoltageTier.LV  => 0.00f,
		VoltageTier.MV  => 0.10f,
		VoltageTier.HV  => 0.20f,
		VoltageTier.EV  => 0.35f,
		VoltageTier.IV  => 0.50f,
		VoltageTier.LuV => 0.70f,
		_               => 0.00f,
	};

	public static Item Roll(VoltageTier tier, int waterTileX, int waterTileY,
	                        FisherFilter filters, out bool discarded)
	{
		discarded = false;

		int power = ApplyLuckToPower(FishingPower(tier), SyntheticLuck(tier));
		if (power < 1) power = 1;

		RollDropLevels(power, out bool common, out bool uncommon, out bool rare,
		                      out bool veryRare, out bool legendary, out bool crate);

		bool junk = Main.rand.Next(50) > power
		         && Main.rand.Next(50) > power;

		Rarity outcome =
			crate     ? Rarity.Crate     :
			legendary ? Rarity.Legendary :
			veryRare  ? Rarity.VeryRare  :
			rare      ? Rarity.Rare      :
			uncommon  ? Rarity.Uncommon  :
			junk      ? Rarity.Junk      :
			            Rarity.Common;

		bool legendaryCrate = outcome == Rarity.Legendary && Main.rand.Next(100) < 30;

		FisherFilter category =
			outcome == Rarity.Crate || legendaryCrate ? FisherFilter.Crate :
			outcome == Rarity.Junk                    ? FisherFilter.Junk  :
			                                            FisherFilter.Fish;

		if ((filters & category) != 0)
		{
			discarded = true;
			return new Item();
		}

		var biome = BiomeProbe.GetForTile(waterTileX, waterTileY);

		int itemId =
			legendaryCrate ? (Main.hardMode ? ItemID.GoldenCrateHard : ItemID.GoldenCrate) :
			outcome switch
			{
				Rarity.Crate     => RollCrate(biome, waterTileY),
				Rarity.Legendary => RollLegendary(biome, Main.hardMode),
				Rarity.Junk      => RollJunk(),
				_                => RollFish(biome, outcome, waterTileY),
			};

		if (itemId <= 0) return new Item();
		var item = new Item();
		item.SetDefaults(itemId);
		return item;
	}

	private static int ApplyLuckToPower(int power, float luck)
	{
		if (luck < 0f)
		{
			if (Main.rand.NextFloat() < 0f - luck)
				power = (int)(power * (0.9 - Main.rand.NextFloat() * 0.3));
		}
		else if (Main.rand.NextFloat() < luck)
		{
			power = (int)(power * (1.1 + Main.rand.NextFloat() * 0.3));
		}
		return power;
	}

	private static void RollDropLevels(int power, out bool common, out bool uncommon,
		out bool rare, out bool veryRare, out bool legendary, out bool crate)
	{
		int nCommon   = Math.Max(2, 150 / power);
		int nUncommon = Math.Max(3, 150 * 2 / power);
		int nRare     = Math.Max(4, 150 * 7 / power);
		int nVeryRare = Math.Max(5, 150 * 15 / power);
		int nLegend   = Math.Max(6, 150 * 30 / power);
		int crateChance = 10;

		common    = Main.rand.Next(nCommon)   == 0;
		uncommon  = Main.rand.Next(nUncommon) == 0;
		rare      = Main.rand.Next(nRare)     == 0;
		veryRare  = Main.rand.Next(nVeryRare) == 0;
		legendary = Main.rand.Next(nLegend)   == 0;
		crate     = Main.rand.Next(100) < crateChance;
	}

	private static int RollJunk()
	{
		int junk = Main.rand.Next(2337, 2340);
		if (Main.rand.Next(8) == 0) junk = ItemID.JojaCola;
		return junk;
	}

	private static int RollCrate(BiomeProbe.Biome biome, int waterY)
	{
		bool hm = Main.hardMode;
		switch (biome)
		{
			case BiomeProbe.Biome.Jungle:
				return hm ? ItemID.JungleFishingCrateHard : ItemID.JungleFishingCrate;
			case BiomeProbe.Biome.Snow:
				return hm ? ItemID.FrozenCrateHard : ItemID.FrozenCrate;
			case BiomeProbe.Biome.Ocean:
				return hm ? ItemID.OceanCrateHard : ItemID.OceanCrate;
			case BiomeProbe.Biome.Hallow:
				return hm ? ItemID.HallowedFishingCrateHard : ItemID.HallowedFishingCrate;
			case BiomeProbe.Biome.Corruption:
				return hm ? ItemID.CorruptFishingCrateHard : ItemID.CorruptFishingCrate;
			case BiomeProbe.Biome.Crimson:
				return hm ? ItemID.CrimsonFishingCrateHard : ItemID.CrimsonFishingCrate;
			case BiomeProbe.Biome.Underworld:
				return hm ? ItemID.ObsidianLockbox : ItemID.HellstoneBar;
		}
		bool underground = waterY > Main.worldSurface;
		int roll = Main.rand.Next(100);
		if (hm)
		{
			if (underground)
				return roll < 60 ? ItemID.WoodenCrateHard : roll < 90 ? ItemID.IronCrateHard : ItemID.GoldenCrateHard;
			return roll < 70 ? ItemID.WoodenCrateHard : roll < 95 ? ItemID.IronCrateHard : ItemID.GoldenCrateHard;
		}
		if (underground)
			return roll < 60 ? ItemID.WoodenCrate : roll < 90 ? ItemID.IronCrate : ItemID.GoldenCrate;
		return roll < 70 ? ItemID.WoodenCrate : roll < 95 ? ItemID.IronCrate : ItemID.GoldenCrate;
	}

	private static int RollLegendary(BiomeProbe.Biome biome, bool hm)
	{
		return biome switch
		{
			BiomeProbe.Biome.Hallow     => hm ? ItemID.CrystalSerpent : ItemID.Prismite,
			BiomeProbe.Biome.Corruption => hm ? ItemID.Toxikarp       : ItemID.Ebonkoi,
			BiomeProbe.Biome.Crimson    => hm ? ItemID.Bladetongue    : ItemID.Hemopiranha,
			BiomeProbe.Biome.Jungle     => ItemID.VariegatedLardfish,
			_                           => ItemID.ReaverShark,
		};
	}

	private static int RollFish(BiomeProbe.Biome biome, Rarity rarity, int waterY)
	{
		short[]? pool = (biome, rarity) switch
		{
			(BiomeProbe.Biome.Forest, Rarity.Common)   => new[] { ItemID.Bass, ItemID.Trout },
			(BiomeProbe.Biome.Forest, Rarity.Uncommon) => new[] { ItemID.NeonTetra, ItemID.Trout },
			(BiomeProbe.Biome.Forest, Rarity.Rare)     => new[] { ItemID.GoldenCarp, ItemID.RedSnapper },
			(BiomeProbe.Biome.Forest, Rarity.VeryRare) => new[] { ItemID.ReaverShark, ItemID.GoldenCarp },
			(BiomeProbe.Biome.Snow, Rarity.Common)     => new[] { ItemID.AtlanticCod, ItemID.FrostMinnow },
			(BiomeProbe.Biome.Snow, Rarity.Uncommon)   => new[] { ItemID.Trout, ItemID.AtlanticCod },
			(BiomeProbe.Biome.Snow, Rarity.Rare)       => new[] { ItemID.Salmon, ItemID.RedSnapper },
			(BiomeProbe.Biome.Snow, Rarity.VeryRare)   => new[] { ItemID.ReaverShark, ItemID.Salmon },
			(BiomeProbe.Biome.Jungle, Rarity.Common)   => new[] { ItemID.Bass, ItemID.NeonTetra },
			(BiomeProbe.Biome.Jungle, Rarity.Uncommon) => new[] { ItemID.VariegatedLardfish, ItemID.NeonTetra },
			(BiomeProbe.Biome.Jungle, Rarity.Rare)     => new[] { ItemID.VariegatedLardfish, ItemID.GoldenCarp },
			(BiomeProbe.Biome.Jungle, Rarity.VeryRare) => new[] { ItemID.ReaverShark },
			(BiomeProbe.Biome.Ocean, Rarity.Common)    => new[] { ItemID.Damselfish, ItemID.Tuna, ItemID.AtlanticCod },
			(BiomeProbe.Biome.Ocean, Rarity.Uncommon)  => new[] { ItemID.Tuna, ItemID.RedSnapper },
			(BiomeProbe.Biome.Ocean, Rarity.Rare)      => new[] { ItemID.RedSnapper, ItemID.GoldenCarp },
			(BiomeProbe.Biome.Ocean, Rarity.VeryRare)  => new[] { ItemID.ReaverShark, ItemID.GoldenCarp },
			(BiomeProbe.Biome.Hallow, Rarity.Common)   => new[] { ItemID.PrincessFish, ItemID.Bass },
			(BiomeProbe.Biome.Hallow, Rarity.Uncommon) => new[] { ItemID.PrincessFish, ItemID.NeonTetra },
			(BiomeProbe.Biome.Hallow, Rarity.Rare)     => new[] { ItemID.Prismite, ItemID.PrincessFish },
			(BiomeProbe.Biome.Hallow, Rarity.VeryRare) => new[] { ItemID.ChaosFish, ItemID.Prismite },
			(BiomeProbe.Biome.Corruption, Rarity.Common)   => new[] { ItemID.DoubleCod, ItemID.Bass },
			(BiomeProbe.Biome.Corruption, Rarity.Uncommon) => new[] { ItemID.Ebonkoi, ItemID.DoubleCod },
			(BiomeProbe.Biome.Corruption, Rarity.Rare)     => new[] { ItemID.Ebonkoi, ItemID.Stinkfish },
			(BiomeProbe.Biome.Corruption, Rarity.VeryRare) => new[] { ItemID.Stinkfish, ItemID.RedSnapper },
			(BiomeProbe.Biome.Crimson, Rarity.Common)   => new[] { ItemID.DoubleCod, ItemID.Bass },
			(BiomeProbe.Biome.Crimson, Rarity.Uncommon) => new[] { ItemID.CrimsonTigerfish, ItemID.DoubleCod },
			(BiomeProbe.Biome.Crimson, Rarity.Rare)     => new[] { ItemID.CrimsonTigerfish, ItemID.Hemopiranha },
			(BiomeProbe.Biome.Crimson, Rarity.VeryRare) => new[] { ItemID.Hemopiranha, ItemID.RedSnapper },
			(BiomeProbe.Biome.Mushroom, Rarity.Common)   => new[] { ItemID.Bass, ItemID.NeonTetra },
			(BiomeProbe.Biome.Mushroom, Rarity.Uncommon) => new[] { ItemID.GlowingMushroom, ItemID.NeonTetra },
			(BiomeProbe.Biome.Mushroom, Rarity.Rare)     => new[] { ItemID.GlowingMushroom, ItemID.SpecularFish },
			(BiomeProbe.Biome.Mushroom, Rarity.VeryRare) => new[] { ItemID.GoldenCarp },
			(BiomeProbe.Biome.Desert, Rarity.Common)   => new[] { ItemID.Bass },
			(BiomeProbe.Biome.Desert, Rarity.Uncommon) => new[] { ItemID.NeonTetra, ItemID.Bass },
			(BiomeProbe.Biome.Desert, Rarity.Rare)     => new[] { ItemID.GoldenCarp },
			(BiomeProbe.Biome.Desert, Rarity.VeryRare) => new[] { ItemID.GoldenCarp, ItemID.ReaverShark },
			(BiomeProbe.Biome.Underworld, _)           => new[] { ItemID.Bass },
			(_, Rarity.Common)   => new[] { ItemID.SpecularFish, ItemID.Bass },
			(_, Rarity.Uncommon) => new[] { ItemID.NeonTetra, ItemID.SpecularFish },
			(_, Rarity.Rare)     => new[] { ItemID.GoldenCarp },
			(_, Rarity.VeryRare) => new[] { ItemID.ReaverShark },

			_ => null,
		};

		if (pool is null || pool.Length == 0) return ItemID.Bass;
		return pool[Main.rand.Next(pool.Length)];
	}
}
