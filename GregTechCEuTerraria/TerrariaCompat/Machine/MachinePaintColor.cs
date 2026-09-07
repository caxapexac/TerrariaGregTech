#nullable enable
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public static class MachinePaintColor
{
	public static int Of(MetaMachine machine)
	{
		foreach (var (x, y) in machine.Cells())
		{
			if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY) continue;
			Tile tile = Main.tile[x, y];
			if (!tile.HasTile) continue;
			if (tile.TileColor != PaintID.None) return tile.TileColor;
		}
		return -1;
	}
}
