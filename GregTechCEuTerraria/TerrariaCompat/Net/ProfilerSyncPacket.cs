#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Profiler;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

// Server profiler counter snapshot
//
//   1. DELTA-SYNC - only counters whose (ValueRaw, newestSample) changed since
//      the last broadcast ship a value. Static counters (per-machine mem gauges,
//      idle-machine timers) are skipped.
//   2. STRING INTERNING - a counter's Category/Name strings ship ONCE per epoch
//      as a "def" row keyed by its SyncId; every later broadcast ships only a
//      compact value row keyed by that id. A name like
//      machine_systemtick.by_type.WorkableElectric...Machine is ~50 bytes that
//      previously re-shipped at 10 Hz forever.
//
// Wire format (per broadcast):
//   byte   epoch
//   ushort defCount;  defCount x { ushort id, string category, string name, byte kind }
//   ushort valCount;  valCount x { ushort id, byte kind, <value by kind> }
//     where <value> is:
//       Gauge         -> long reading            (sample == ValueRaw, ship once)
//       Counter/Timer -> long raw + float32 rate (cumulative + per-sec sample;
//                       float32 is ample for a display graph at half the bytes)
public static class ProfilerSyncPacket
{
	private const int MaxDefsPerBroadcast = 128;

	private const int DefResyncPeriod = 100;
	private static int _resyncCountdown = 30;

	private static readonly List<ProfilerCounter> _defs = new(MaxDefsPerBroadcast);
	private static readonly List<ProfilerCounter> _vals = new(256);

	// Client-side: wire id -> local "server.*" mirror counter. Cleared when the
	// server's epoch byte changes (world reload reuses ids for new counters).
	private static readonly Dictionary<ushort, ProfilerCounter> _idMap = new(512);
	private static byte _clientEpoch;
	private static bool _clientEpochInit;

	public static void Broadcast()
	{
		if (!Profiler.Profiler.Enabled) return;
		if (Main.netMode != NetmodeID.Server) return;

		long t0 = Stopwatch.GetTimestamp();

		if (--_resyncCountdown <= 0)
		{
			_resyncCountdown = DefResyncPeriod;
			foreach (var c in Profiler.Profiler.All) c.SyncDefSent = false;
		}

		_defs.Clear();
		foreach (var c in Profiler.Profiler.All)
		{
			if (c.SyncDefSent) continue;
			_defs.Add(c);
			c.SyncDefSent = true;
			if (_defs.Count >= MaxDefsPerBroadcast) break;
		}

		_vals.Clear();
		foreach (var c in Profiler.Profiler.All)
		{
			int newest = Newest(c);
			double samp = c.Samples[newest];
			if (c.SyncInit && c.SyncLastVal == c.ValueRaw && c.SyncLastSamp == samp)
				continue;
			c.SyncLastVal = c.ValueRaw;
			c.SyncLastSamp = samp;
			c.SyncInit = true;
			_vals.Add(c);
		}

		if (_defs.Count == 0 && _vals.Count == 0)
		{
			RecordServerCost(t0, 0, 0);
			return;
		}

		var p = NetRouter.NewPacket(PacketType.ProfilerSync);
		p.Write(Profiler.Profiler.SyncEpoch);
		p.Write((ushort)_defs.Count);
		foreach (var c in _defs)
		{
			p.Write(c.SyncId);
			p.Write(c.Category);
			p.Write(c.Name);
			p.Write((byte)c.Kind);
		}
		p.Write((ushort)_vals.Count);
		foreach (var c in _vals)
		{
			p.Write(c.SyncId);
			p.Write((byte)c.Kind);
			if (c.Kind == ProfilerKind.Gauge)
			{
				p.Write(c.ValueRaw);
			}
			else
			{
				p.Write(c.ValueRaw);
				p.Write((float)c.Samples[Newest(c)]);
			}
		}
		p.Send();

		RecordServerCost(t0, _defs.Count, _vals.Count);
	}

	public static void HandleOnClient(BinaryReader reader)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		long t0 = Stopwatch.GetTimestamp();

		byte epoch = reader.ReadByte();
		if (!_clientEpochInit || epoch != _clientEpoch)
		{
			_idMap.Clear();
			_clientEpoch = epoch;
			_clientEpochInit = true;
		}

		int defN = reader.ReadUInt16();
		for (int i = 0; i < defN; i++)
		{
			ushort id   = reader.ReadUInt16();
			string cat  = reader.ReadString();
			string name = reader.ReadString();
			ProfilerKind kind = (ProfilerKind)reader.ReadByte();

			var c = Profiler.Profiler.GetOrCreate("server." + cat, name, kind);
			c.ExternallySampled = true;
			_idMap[id] = c;
		}

		int valN = reader.ReadUInt16();
		for (int i = 0; i < valN; i++)
		{
			ushort id = reader.ReadUInt16();
			ProfilerKind kind = (ProfilerKind)reader.ReadByte();

			long raw; double samp;
			if (kind == ProfilerKind.Gauge)
			{
				raw  = reader.ReadInt64();
				samp = raw;
			}
			else
			{
				raw  = reader.ReadInt64();
				samp = reader.ReadSingle();
			}

			if (!_idMap.TryGetValue(id, out var c)) continue;
			c.ValueRaw = raw;
			c.LastSnapshotValue = raw;
			c.RecordSample(samp);
		}

		Profiler.Profiler.AccumulateTimer("profiler", "sync_handle_client", Stopwatch.GetTimestamp() - t0);
		Profiler.Profiler.Gauge("profiler", "sync_rows_client", defN + valN);
	}

	private static int Newest(ProfilerCounter c) =>
		(c.SampleHead - 1 + c.Samples.Length) % c.Samples.Length;

	private static void RecordServerCost(long t0, int defCount, int valCount)
	{
		Profiler.Profiler.AccumulateTimer("profiler", "sync_serialize_server", Stopwatch.GetTimestamp() - t0);
		Profiler.Profiler.Gauge("profiler", "sync_defs_server", defCount);
		Profiler.Profiler.Gauge("profiler", "sync_vals_server", valCount);
	}
}
