#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Net;


public static class MachineStateRequestPacket
{
	private const int MaxPerPacket = 2000;

	private static readonly ConcurrentQueue<Point16> _pending = new();

	public static void Enqueue(Point16 pos)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		_pending.Enqueue(pos);
	}

	public static void Clear() => _pending.Clear();

	private static readonly List<Point16> _batch = new();

	public static void Flush()
	{
		if (Main.netMode != NetmodeID.MultiplayerClient || _pending.IsEmpty) return;

		while (true)
		{
			_batch.Clear();
			while (_batch.Count < MaxPerPacket && _pending.TryDequeue(out var pos))
				_batch.Add(pos);
			if (_batch.Count == 0) return;

			var p = NetRouter.NewPacket(PacketType.MachineStateRequest);
			p.Write((ushort)_batch.Count);
			foreach (var pos in _batch) p.WritePoint16(pos);
			p.Send();
		}
	}

	public static void Handle(BinaryReader r, int whoAmI)
	{
		int n = r.ReadUInt16();
		var positions = new Point16[n];
		for (int i = 0; i < n; i++) positions[i] = r.ReadPoint16();

		if (Main.netMode != NetmodeID.Server)
		{
			NetHelpers.LogBadPacket("StateRequest", "received on non-server side");
			return;
		}

		foreach (var pos in positions)
			if (TileEntity.ByPosition.TryGetValue(pos, out var te) && te is MetaMachine machine)
				MachineStateSyncPacket.SendFullStateTo(machine, whoAmI);
	}
}

public sealed class MachineStateRequestSystem : ModSystem
{
	public override void OnWorldLoad()   => MachineStateRequestPacket.Clear();
	public override void OnWorldUnload() => MachineStateRequestPacket.Clear();

	public override void PostUpdateEverything() => MachineStateRequestPacket.Flush();
}
