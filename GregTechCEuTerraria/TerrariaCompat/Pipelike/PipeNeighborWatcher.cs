#nullable enable
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Laser;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike;

public sealed class PipeNeighborWatcher : GlobalTile
{
	public override void PlaceInWorld(int i, int j, int type, Item item) => NotifyAround(i, j);

	public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
	{
		if (fail || effectOnly) return;
		NotifyAround(i, j);
	}

	private static readonly HashSet<MetaMachine> PingedMachines = new();
	private static bool _sweeping;

	public static void NotifyAround(int x, int y) => NotifyAroundBox(x, y, 1, 1, null);

	public static void NotifyAroundBox(int x, int y, int w, int h, MetaMachine? self)
	{
		PipeRenderer.InvalidateGeomAround(x, y);

		if (Main.netMode == NetmodeID.MultiplayerClient) return;
		if (_sweeping) return;
		_sweeping = true;
		try
		{
			PingedMachines.Clear();
			if (self != null) PingedMachines.Add(self);
			for (int dy = -2; dy < h + 2; dy++)
			for (int dx = -2; dx < w + 2; dx++)
			{
				if (dx >= 0 && dx < w && dy >= 0 && dy < h) continue;
				PingNeighbor(x + dx, y + dy);
			}
			PingedMachines.Clear();
		}
		finally
		{
			_sweeping = false;
		}
	}

	private static void PingNeighbor(int x, int y)
	{
		if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;
		if (ItemPipeLayerSystem.GetSides(x, y) is { } itemPcv)
		{
			((ICoverable)itemPcv).OnCoversNeighborChanged();
			itemPcv.InvalidateLocal();
		}
		if (FluidPipeLayerSystem.GetSides(x, y) is { } fluidPcv)
		{
			((ICoverable)fluidPcv).OnCoversNeighborChanged();
			fluidPcv.InvalidateLocal();
		}
		if (MachineCellResolver.TryFindMachineAt(x, y, out var machine) && PingedMachines.Add(machine))
			((ICoverable)machine).OnCoversNeighborChanged();
		if (LaserPipeLayerSystem.Pipes.Has(x, y))
			LaserPipeNetSystem.Level.GetNetFromPos((x, y))?.OnNeighbourUpdate((x, y));
		if (Optical.OpticalPipeLayerSystem.Pipes.Has(x, y))
			Optical.OpticalPipeNetSystem.Level.GetNetFromPos((x, y))?.OnNeighbourUpdate((x, y));
	}
}
