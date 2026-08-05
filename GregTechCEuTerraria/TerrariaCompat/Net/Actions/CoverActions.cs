#nullable enable
using System;
using System.IO;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net.Actions;

public static class CoverActions
{
	public static void Send(ICoverAction action, ICoverable target)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
		{
			action.Apply(target, Main.myPlayer);
			if (Main.netMode == NetmodeID.Server) BroadcastPostApply(target);
			return;
		}

		var p = NetRouter.NewPacket(action.Type);
		WriteTarget(p, target);
		action.Write(p);
		p.Send();
	}

	public static void HandleIncoming<T>(BinaryReader r, int whoAmI) where T : ICoverAction, new()
	{
		if (Main.netMode != NetmodeID.Server)
		{
			NetHelpers.LogBadPacket("cover-action", $"{typeof(T).Name} received on non-server side");
			return;
		}

		var target = ResolveTarget(r, out string targetDesc);
		var action = new T();
		action.Read(r);

		if (target is null)
		{
			NetHelpers.LogBadPacket("cover-action",
				$"{typeof(T).Name}: target not found ({targetDesc}) from player {whoAmI}");
			return;
		}

		if (target is MetaMachine machine)
		{
			if (!machine.HasViewer(whoAmI))
			{
				NetHelpers.LogBadPacket("cover-action",
					$"{typeof(T).Name}: player {whoAmI} not in viewer set for {targetDesc}");
				return;
			}
		}

		action.Apply(target, whoAmI);
		BroadcastPostApply(target);
	}

	private static void BroadcastPostApply(ICoverable target)
	{
		switch (target)
		{
			case MetaMachine machine:
				MachineStateSyncPacket.Broadcast(machine);
				break;
			case PipeCoverable pipe:
				PipeCoverSyncPacket.Broadcast(pipe.Layer, pipe.X, pipe.Y);
				if (!((ICoverable)pipe).HasAnyCover())
				{
					if (pipe.Layer == PipeKind.Fluid) FluidPipeLayerSystem.DropSides(pipe.X, pipe.Y);
					else                              ItemPipeLayerSystem .DropSides(pipe.X, pipe.Y);
				}
				break;
		}
	}

	private static void WriteTarget(BinaryWriter w, ICoverable target)
	{
		switch (target)
		{
			case MetaMachine machine:
				w.Write((byte)0);
				w.WritePoint16(machine.Position);
				break;
			case PipeCoverable pipe:
				w.Write((byte)1);
				w.Write((byte)pipe.Layer);
				w.Write((short)pipe.X);
				w.Write((short)pipe.Y);
				break;
			default:
				throw new InvalidOperationException(
					$"CoverActions.Send: unknown ICoverable type {target.GetType().Name}");
		}
	}

	private static ICoverable? ResolveTarget(BinaryReader r, out string desc)
	{
		byte kind = r.ReadByte();
		if (kind == 0)
		{
			var pos = r.ReadPoint16();
			desc = $"machine@{pos}";
			return TileEntity.ByPosition.TryGetValue(pos, out var te) && te is MetaMachine m
				? m
				: null;
		}
		if (kind == 1)
		{
			var layer = (PipeKind)r.ReadByte();
			int x = r.ReadInt16();
			int y = r.ReadInt16();
			desc = $"pipe[{layer}]@({x},{y})";
			bool cellExists = layer == PipeKind.Fluid
				? FluidPipeLayerSystem.Pipes.Has(x, y)
				: ItemPipeLayerSystem .Pipes.Has(x, y);
			if (!cellExists) return null;
			return layer == PipeKind.Fluid
				? FluidPipeLayerSystem.EnsureSides(x, y)
				: ItemPipeLayerSystem .EnsureSides(x, y);
		}
		desc = $"unknown(kind={kind})";
		return null;
	}
}
