#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal static class MultitoolIntersect
{
	internal enum Result { Normal, Crossed, NoItem }

	internal static Result HandleCrossing(Player p, int x, int y)
	{
		if (PipeIntersection.TileType < 0) return Result.Normal;
		if (PipeIntersection.BlocksPipeAt(x, y)) return Result.Crossed;

		int type = ModContent.ItemType<PipeIntersectionItem>();
		if (CountItem(p, type) <= 0) return Result.NoItem;

		WorldGen.PlaceTile(x, y, PipeIntersection.TileType, mute: false, forced: false, plr: p.whoAmI);
		if (!PipeIntersection.BlocksPipeAt(x, y)) return Result.Normal;

		PipeIntersection.OnPlaced(x, y, p);
		if (Main.netMode != NetmodeID.SinglePlayer)
			NetMessage.SendTileSquare(-1, x, y, 1);
		ConsumeOne(p, type);
		return Result.Crossed;
	}

	private static bool _warnedThisDrag;

	internal static void ResetWarning() => _warnedThisDrag = false;

	internal static void WarnMissingOnce()
	{
		if (_warnedThisDrag) return;
		_warnedThisDrag = true;
		Main.NewText("Have Pipe Intersection items in inventory for automatic intersections of different pipes",
			new Color(255, 180, 60));
	}

	private static int CountItem(Player p, int type)
	{
		int n = 0;
		foreach (var it in MultitoolInventory.Slots(p))
			if (it.type == type) n += it.stack;
		return n;
	}

	private static void ConsumeOne(Player p, int type)
	{
		foreach (var it in MultitoolInventory.Slots(p))
		{
			if (it.type != type) continue;
			it.stack--;
			if (it.stack <= 0) it.TurnToAir();
			return;
		}
	}
}
