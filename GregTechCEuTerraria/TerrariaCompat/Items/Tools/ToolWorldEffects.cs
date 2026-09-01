#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools;

public sealed class ToolWorldEffects : GlobalTile
{
	private static int _rubberLog = -1;
	private static int RubberLog => _rubberLog >= 0
		? _rubberLog
		: (_rubberLog = IngredientResolverImpl.Instance.ResolveItemType("gtceu:rubber_log"));

	private static int _stickyResin = -1;
	private static int StickyResin => _stickyResin >= 0
		? _stickyResin
		: (_stickyResin = IngredientResolverImpl.Instance.ResolveItemType("gtceu:sticky_resin"));

	private static readonly HashSet<int> SoftTiles = new()
	{
		TileID.Dirt, TileID.Grass, TileID.CorruptGrass, TileID.CrimsonGrass,
		TileID.HallowedGrass, TileID.Mud, TileID.JungleGrass, TileID.MushroomGrass,
		TileID.Sand, TileID.Ebonsand, TileID.Crimsand, TileID.Pearlsand,
		TileID.HardenedSand, TileID.CorruptHardenedSand, TileID.CrimsonHardenedSand,
		TileID.HallowHardenedSand, TileID.Silt, TileID.Slush, TileID.SnowBlock,
		TileID.ClayBlock, TileID.Cloud, TileID.RainCloud, TileID.Ash,
	};

	private static readonly HashSet<int> GrassTiles = new()
	{
		TileID.Grass, TileID.CorruptGrass, TileID.CrimsonGrass, TileID.HallowedGrass,
		TileID.JungleGrass, TileID.MushroomGrass, TileID.AshGrass, TileID.GolfGrass,
	};

	private static readonly HashSet<int> WoodTrees = new()
	{
		TileID.Trees, TileID.TreeAsh, TileID.VanityTreeSakura, TileID.VanityTreeYellowWillow,
	};

	public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
	{
		if (Main.netMode == NetmodeID.Server) return true;
		var p = Main.LocalPlayer;
		if (p?.HeldItem?.ModItem is ToolItem t && p.itemAnimation > 0)
		{
			if (t.IsSoftDigger) return SoftTiles.Contains(type);
			if (t.IsHoe)
				return type == TileID.ImmatureHerbs
				    || type == TileID.MatureHerbs
				    || type == TileID.BloomingHerbs;
		}
		return true;
	}

	public override bool CanDrop(int i, int j, int type)
	{
		var holder = NearestToolHolder(i, j);
		if (holder?.HeldItem?.ModItem is not ToolItem tool) return true;

		if (tool.IsSawLike && type == TileID.PalmTree && StickyResin > 0)
		{
			SpawnItem(i, j, StickyResin, 1);
			return false;
		}

		if (tool.IsSawLike && WoodTrees.Contains(type) && RubberLog > 0)
		{
			SpawnItem(i, j, RubberLog, 1);
			if (type == TileID.Trees && j + 1 < Main.maxTilesY)
			{
				var below = Main.tile[i, j + 1];
				if (below.HasTile && below.TileType != TileID.Trees)
					SpawnItem(i, j, ItemID.Acorn, tool.Tier + 1);
			}
			return false;
		}

		if (tool.IsMortar)
		{
			int ground = type switch
			{
				TileID.Stone     => ItemID.SiltBlock,
				TileID.Silt      => ItemID.DirtBlock,
				TileID.Dirt      => ItemID.ClayBlock,
				TileID.ClayBlock => ItemID.SandBlock,
				_                => 0,
			};
			if (ground > 0)
			{
				SpawnItem(i, j, ground, 1);
				return false;
			}
		}

		return true;
	}

	public override void Drop(int i, int j, int type)
	{
		var holder = NearestToolHolder(i, j);
		if (holder?.HeldItem?.ModItem is not ToolItem tool) return;

		if (tool.IsShovel && GrassTiles.Contains(type))
		{
			float chance = 0.10f + System.Math.Clamp(tool.Tier / 9f, 0f, 1f) * 0.90f;
			if (Main.rand.NextFloat() < chance)
				SpawnItem(i, j, ItemID.Worm, 1);
		}

		if (type != TileID.MatureHerbs && type != TileID.BloomingHerbs) return;
		if (!tool.IsHoe) return;

		// Verbatim WorldGen.KillTile_GetItemDrops math.
		int num = Main.tile[i, j].TileFrameX / 18;
		int seed = num == 6 ? 2357 : 307 + num;
		int herb = num == 6 ? 2358 : 313 + num;

		// tier 2 -> up to 5 (Staff parity); tier 0 -> up to 1; tier 9 -> up to ~22.
		int seedCap = System.Math.Max(1, (int)System.Math.Round(5.0 * tool.Tier / 2.0));
		int bonusSeeds = Main.rand.Next(0, seedCap + 1);
		SpawnItem(i, j, seed, bonusSeeds);

		// Aluminium and above also get the Staff's bonus harvested herb.
		if (tool.Tier >= 2) SpawnItem(i, j, herb, 1);
	}

	private static void SpawnItem(int i, int j, int itemType, int stack)
	{
		if (itemType <= 0 || stack <= 0) return;
		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		Item.NewItem(WorldGen.GetItemSource_FromTileBreak(i, j),
			i * 16, j * 16, 16, 16, itemType, stack);
	}

	private static Player? NearestToolHolder(int i, int j)
	{
		var c = new Vector2(i * 16f + 8f, j * 16f + 8f);
		Player? best = null;
		float bestSq = float.MaxValue;
		for (int p = 0; p < Main.maxPlayers; p++)
		{
			var pl = Main.player[p];
			if (pl is null || !pl.active || pl.dead) continue;
			if (pl.HeldItem?.ModItem is not ToolItem) continue;
			float d = Vector2.DistanceSquared(pl.Center, c);
			if (d < bestSq) { bestSq = d; best = pl; }
		}
		const float maxPx = 80f * 16f;
		return best != null && bestSq <= maxPx * maxPx ? best : null;
	}
}
