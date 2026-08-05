#nullable enable
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public static class MachineStateSyncPacket
{
	public const float NearbyRadiusPx = 2500f;
	private const float NearbyRadiusPxSq = NearbyRadiusPx * NearbyRadiusPx;

	public static void SendTo(MetaMachine machine, int toClient)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server) return;
		byte[] blob = SerializeOnce(machine);
		SendBlobTo(machine.Position, blob, toClient);
	}

	public static void SendFullStateTo(MetaMachine machine, int toClient)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server) return;
		SendTo(machine, toClient);
		MachineEnergySyncPacket.SendTo(machine, toClient);
	}

	public static void Broadcast(MetaMachine machine)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server) return;
		machine.LastBroadcastBlob = null;
		if (machine.ViewerCount == 0) return;
		byte[] blob = SerializeOnce(machine);
		foreach (int viewer in machine.Viewers)
			SendBlobTo(machine.Position, blob, viewer);
	}

	private static readonly HashSet<int> _recipientScratch = new();

	public static void BroadcastNearby(MetaMachine machine)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server) return;

		var recipients = _recipientScratch;
		recipients.Clear();
		foreach (int viewer in machine.Viewers) recipients.Add(viewer);

		float cx = machine.Position.X * 16f + machine.Size.Width * 8f;
		float cy = machine.Position.Y * 16f + machine.Size.Height * 8f;
		for (int i = 0; i < Main.maxPlayers; i++)
		{
			if (recipients.Contains(i)) continue;
			var p = Main.player[i];
			if (!p.active || p.dead) continue;
			float dx = p.Center.X - cx;
			float dy = p.Center.Y - cy;
			if (dx * dx + dy * dy <= NearbyRadiusPxSq) recipients.Add(i);
		}
		if (recipients.Count == 0) return;

		byte[] blob = SerializeOnce(machine);
		string typeName = machine.GetType().Name;

		if (machine.LastBroadcastBlob is { } prev && BlobEquals(prev, blob))
		{
			Profiler.Profiler.Count("net.skipped", "MachineStateSync");
			Profiler.Profiler.Count("net.sync.skipped_by_type", typeName);
			return;
		}
		machine.LastBroadcastBlob = blob;

		Profiler.Profiler.Count("net.sync.sent_by_type", typeName);
		Profiler.Profiler.Count("net.sync.bytes_by_type", typeName, blob.Length);

		foreach (int r in recipients)
			SendBlobTo(machine.Position, blob, r);
	}

	private static byte[] SerializeOnce(MetaMachine machine)
	{
		var tag = new TagCompound();
		machine.SaveDataForSync(tag);
		using var ms = new MemoryStream();
		using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
			TagIO.Write(tag, bw);
		return ms.ToArray();
	}

	private static void SendBlobTo(Point16 pos, byte[] blob, int toClient)
	{
		LargePacket.Send(PacketType.MachineStateSync, w =>
		{
			w.WritePoint16(pos);
			w.Write(blob);
		}, toClient: toClient);
	}

	private static bool BlobEquals(byte[] a, byte[] b)
	{
		if (a.Length != b.Length) return false;
		for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
		return true;
	}

	public static void InvalidateBroadcast(MetaMachine machine)
	{
		machine.LastBroadcastBlob = null;
	}

	public static void HandleOnClient(BinaryReader r)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient) return;
		var pos = r.ReadPoint16();
		var tag = TagIO.Read(r);
		if (TileEntity.ByPosition.TryGetValue(pos, out var te) && te is MetaMachine machine)
		{
			machine.LoadData(tag);
			machine.OnClientSync();
		}
		else
		{
			NetHelpers.LogBadPacket("StateSync", $"no MetaMachine at {pos} on client; sync dropped");
		}
	}
}
