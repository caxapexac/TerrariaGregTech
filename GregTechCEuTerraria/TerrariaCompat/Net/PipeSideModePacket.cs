#nullable enable
using System.IO;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public static class PipeSideModePacket
{
	public static void Send(PipeKind layer, int x, int y, CoverSide side, PipeSideMode mode)
	{
		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			ApplyServer(layer, x, y, side, mode);
			return;
		}
		var p = NetRouter.NewPacket(PacketType.PipeSideModeSet);
		p.Write((byte)layer);
		p.Write((short)x);
		p.Write((short)y);
		p.Write((byte)side);
		p.Write((byte)mode);
		p.Send();
	}

	public static void HandleSet(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server)
		{
			NetHelpers.LogBadPacket("pipe-side-mode", "received on non-server side");
			return;
		}
		var layer = (PipeKind)r.ReadByte();
		int x = r.ReadInt16();
		int y = r.ReadInt16();
		byte sideRaw = r.ReadByte();
		byte modeRaw = r.ReadByte();
		if (sideRaw >= CoverSides.Count || modeRaw > (byte)PipeSideMode.Active)
		{
			NetHelpers.LogBadPacket("pipe-side-mode",
				$"out-of-range side={sideRaw} mode={modeRaw} from player {whoAmI}");
			return;
		}
		ApplyServer(layer, x, y, (CoverSide)sideRaw, (PipeSideMode)modeRaw);
	}

	private static void ApplyServer(PipeKind layer, int x, int y, CoverSide side, PipeSideMode mode)
	{
		bool cellExists = layer == PipeKind.Fluid
			? FluidPipeLayerSystem.Pipes.Has(x, y)
			: ItemPipeLayerSystem .Pipes.Has(x, y);
		if (!cellExists) return;

		var pcv = layer == PipeKind.Fluid
			? FluidPipeLayerSystem.EnsureSides(x, y)
			: ItemPipeLayerSystem .EnsureSides(x, y);

		pcv.SetMode(side, mode);
		PipeCoverSyncPacket.Broadcast(layer, x, y);
	}

	public static PipeSideMode ReadCurrentMode(PipeCoverable pcv, CoverSide side)
		=> pcv.GetMode(side);
}
