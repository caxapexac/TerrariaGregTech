#nullable enable
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike;

public static class PipeIntersection
{
	public static int TileType = -1;

	public static bool BlocksPipeAt(int x, int y)
	{
		if (TileType < 0) return false;
		if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return false;
		Tile t = Main.tile[x, y];
		return t.HasTile && t.TileType == TileType;
	}

	public static void InstallHook() => Api.Pipenet.PipePassthrough.IsCrossover = BlocksPipeAt;

	public static void UninstallHook() => Api.Pipenet.PipePassthrough.IsCrossover = static (_, _) => false;

	private static readonly List<(int x, int y, bool placed)> _pendingRelinks = new();

	private static void QueueRelink(int x, int y, bool placed)
	{
		_pendingRelinks.RemoveAll(e => e.x == x && e.y == y);
		_pendingRelinks.Add((x, y, placed));
	}

	public static void ClearPending() => _pendingRelinks.Clear();

	public static void TickRecheck()
	{
		if (_pendingRelinks.Count == 0) return;
		bool applied = false;
		int kept = 0;
		for (int i = 0; i < _pendingRelinks.Count; i++)
		{
			var (rx, ry, placed) = _pendingRelinks[i];
			if (BlocksPipeAt(rx, ry) != placed)
			{
				_pendingRelinks[kept++] = _pendingRelinks[i];
				continue;
			}
			if (placed)
			{
				Laser.LaserConn.LinkAcross(Laser.LaserPipeLayerSystem.Pipes, rx, ry);
				Optical.OpticalConn.LinkAcross(Optical.OpticalPipeLayerSystem.Pipes, rx, ry);
			}
			else
			{
				Laser.LaserConn.UnlinkAcross(Laser.LaserPipeLayerSystem.Pipes, rx, ry);
				Optical.OpticalConn.UnlinkAcross(Optical.OpticalPipeLayerSystem.Pipes, rx, ry);
			}
			applied = true;
		}
		_pendingRelinks.RemoveRange(kept, _pendingRelinks.Count - kept);
		if (!applied) return;
		CableLayerSystem.Cables.MarkDirty();
		ItemPipeLayerSystem.Pipes.MarkDirty();
		FluidPipeLayerSystem.Pipes.MarkDirty();
		Me.MeCableLayerSystem.Cables.MarkDirty();
		LongDistance.LongDistancePipeLayerSystem.Pipes.MarkDirty();
		Laser.LaserPipeLayerSystem.Pipes.MarkDirty();
		Optical.OpticalPipeLayerSystem.Pipes.MarkDirty();
	}

	public static void OnPlaced(int x, int y, Player placer)
	{
		if (CableLayerHandle.Instance.Has(x, y))     CableLayerHandle.Instance.CutAt(x, y, placer);
		if (ItemPipeLayerHandle.Instance.Has(x, y))  ItemPipeLayerHandle.Instance.CutAt(x, y, placer);
		if (FluidPipeLayerHandle.Instance.Has(x, y)) FluidPipeLayerHandle.Instance.CutAt(x, y, placer);
		if (Me.MeCableLayerHandle.Instance.Has(x, y)) Me.MeCableLayerHandle.Instance.CutAt(x, y, placer);
		if (LongDistance.LongDistancePipeLayerHandle.Item.Has(x, y))
			LongDistance.LongDistancePipeLayerHandle.Item.CutAt(x, y, placer);
		if (Laser.LaserPipeLayerHandle.Instance.Has(x, y))     Laser.LaserPipeLayerHandle.Instance.CutAt(x, y, placer);
		if (Optical.OpticalPipeLayerHandle.Instance.Has(x, y)) Optical.OpticalPipeLayerHandle.Instance.CutAt(x, y, placer);
		QueueRelink(x, y, placed: true);
		SendChange(x, y, placed: true);
	}

	public static void OnRemoved(int x, int y)
	{
		QueueRelink(x, y, placed: false);
		SendChange(x, y, placed: false);
	}

	private static void SendChange(int x, int y, bool placed)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) return;
		var p = NetRouter.NewPacket(PacketType.CrossoverChange);
		p.Write((short)x);
		p.Write((short)y);
		p.Write(placed);
		p.Send();
	}

	public static void HandleChange(BinaryReader r, int whoAmI)
	{
		int x = r.ReadInt16();
		int y = r.ReadInt16();
		bool placed = r.ReadBoolean();
		QueueRelink(x, y, placed);
		if (Main.netMode == NetmodeID.Server)
		{
			var p = NetRouter.NewPacket(PacketType.CrossoverChange);
			p.Write((short)x);
			p.Write((short)y);
			p.Write(placed);
			p.Send(ignoreClient: whoAmI);
		}
	}
}
