#nullable enable
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria;
using Terraria.DataStructures;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public static class MachineEnergySyncPacket
{
	private static readonly HashSet<int> _recipientScratch = new();

	public static void SendTo(MetaMachine machine, int toClient)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server) return;
		if (!machine.HasSyncEnergy) return;
		Send(machine.Position, machine.SyncEnergyStored, toClient);
	}

	public static void BroadcastNearby(MetaMachine machine)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server) return;
		if (!machine.HasSyncEnergy) return;

		long energy = machine.SyncEnergyStored;
		string typeName = machine.GetType().Name;
		if (machine.LastBroadcastEnergy is { } prev && prev == energy)
		{
			Profiler.Profiler.Count("net.skipped", "MachineEnergySync");
			Profiler.Profiler.Count("net.energysync.skipped_by_type", typeName);
			return;
		}
		machine.LastBroadcastEnergy = energy;

		var recipients = _recipientScratch;
		recipients.Clear();
		foreach (int viewer in machine.Viewers) recipients.Add(viewer);

		float cx = machine.Position.X * 16f + machine.Size.Width * 8f;
		float cy = machine.Position.Y * 16f + machine.Size.Height * 8f;
		float radiusSq = MachineStateSyncPacket.NearbyRadiusPx * MachineStateSyncPacket.NearbyRadiusPx;
		for (int i = 0; i < Main.maxPlayers; i++)
		{
			if (recipients.Contains(i)) continue;
			var p = Main.player[i];
			if (!p.active || p.dead) continue;
			float dx = p.Center.X - cx;
			float dy = p.Center.Y - cy;
			if (dx * dx + dy * dy <= radiusSq) recipients.Add(i);
		}
		if (recipients.Count == 0) return;

		Profiler.Profiler.Count("net.energysync.sent_by_type", typeName);
		Profiler.Profiler.Count("net.energysync.bytes_by_type", typeName, 12);
		foreach (int r in recipients)
			Send(machine.Position, energy, r);
	}

	private static void Send(Point16 pos, long energy, int toClient)
	{
		var p = NetRouter.NewPacket(PacketType.MachineEnergySync);
		p.WritePoint16(pos);
		p.Write(energy);
		p.Send(toClient: toClient);
	}

	public static void HandleOnClient(BinaryReader r)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient) return;
		var pos = r.ReadPoint16();
		long energy = r.ReadInt64();
		if (TileEntity.ByPosition.TryGetValue(pos, out var te) && te is MetaMachine machine)
			machine.ApplySyncEnergy(energy);
	}
}
