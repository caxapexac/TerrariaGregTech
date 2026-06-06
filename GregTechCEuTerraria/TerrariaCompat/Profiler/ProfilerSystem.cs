#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Profiler;

// Drives per-tick sampling and provides JSON dump-to-disk. Hooks into
// PostUpdateEverything (runs on both server and clients in MP, unlike
// PostUpdateWorld which is server-only) so samples advance on every machine
// that opens the profiler
public sealed class ProfilerSystem : ModSystem
{
	private static int  _lastGc0, _lastGc1, _lastGc2;
	private static long _lastAllocBytes;
	private static bool _gcBaselineSet;

	// Real wall-clock timing
	private static long   _lastFrameTs, _lastSampleTs, _updateStartTs;
	private static double _frameMsSum, _frameMsMax, _updatePhaseMsSum;
	private static int    _frameCount;

	// Stamped at the start of the update loop
	public override void PreUpdateEntities()
	{
		if (Profiler.Enabled)
			_updateStartTs = System.Diagnostics.Stopwatch.GetTimestamp();
	}

	public override void OnWorldLoad()
	{
		Profiler.Reset();
		_gcBaselineSet = false;
		_lastFrameTs = _lastSampleTs = _updateStartTs = 0;
		_frameMsSum = _frameMsMax = _updatePhaseMsSum = 0;
		_frameCount = 0;
		Profiler.Gauge("engine", "fps", (long)Main.frameRate);
	}

	public override void OnWorldUnload() => Profiler.Reset();

	public override void PostUpdateEverything()
	{
		if (!Profiler.Enabled) return;
		long nowTs = System.Diagnostics.Stopwatch.GetTimestamp();
		double tickFreq = System.Diagnostics.Stopwatch.Frequency;
		if (_lastFrameTs != 0)
		{
			double frameMs = (nowTs - _lastFrameTs) * 1000.0 / tickFreq;
			_frameMsSum += frameMs;
			_frameCount++;
			if (frameMs > _frameMsMax) _frameMsMax = frameMs;
		}
		_lastFrameTs = nowTs;
		if (_updateStartTs != 0)
			_updatePhaseMsSum += (nowTs - _updateStartTs) * 1000.0 / tickFreq;

		// Sample every N frames (10 Hz)
		if (Main.GameUpdateCount % (ulong)Profiler.SamplePeriodFrames != 0) return;

		double realWindowSec = _lastSampleTs != 0
			? (nowTs - _lastSampleTs) / tickFreq
			: Profiler.SamplePeriodFrames / 60.0;
		_lastSampleTs = nowTs;

		double realFrameAvgMs = _frameCount > 0 ? _frameMsSum / _frameCount : 0;
		double realFrameMaxMs = _frameMsMax;
		double updatePhaseAvgMs = _frameCount > 0 ? _updatePhaseMsSum / _frameCount : 0;
		Profiler.Gauge("engine", "real_frame_ms",     (long)realFrameAvgMs);
		Profiler.Gauge("engine", "real_frame_ms_max", (long)realFrameMaxMs);
		Profiler.Gauge("engine", "update_phase_ms",   (long)updatePhaseAvgMs);
		_frameMsSum = _frameMsMax = _updatePhaseMsSum = 0;
		_frameCount = 0;

		Profiler.Gauge("engine", "fps", (long)Main.frameRate);
		int activeNpcs = 0;
		foreach (var n in Main.npc) if (n != null && n.active) activeNpcs++;
		Profiler.Gauge("engine", "active_npcs", activeNpcs);
		int activeProj = 0;
		foreach (var p in Main.projectile) if (p != null && p.active) activeProj++;
		Profiler.Gauge("engine", "active_projectiles", activeProj);
		int activeItems = 0;
		foreach (var it in Main.item) if (it != null && it.active) activeItems++;
		Profiler.Gauge("engine", "active_items", activeItems);
		Profiler.Gauge("engine", "tile_entities", Terraria.DataStructures.TileEntity.ByID.Count);
		long heapMb = GC.GetTotalMemory(forceFullCollection: false) >> 20;
		Profiler.Gauge("engine", "managed_heap_mb", heapMb);
		// Alarm (chat + error log) if the heap crosses the danger threshold,
		// before it OOM-crashes the game
		MemoryGuard.Check(heapMb);

		int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
		long allocBytes = GC.GetTotalAllocatedBytes(precise: false);
		Profiler.Gauge("engine", "gc_gen0_total", gc0);
		Profiler.Gauge("engine", "gc_gen1_total", gc1);
		Profiler.Gauge("engine", "gc_gen2_total", gc2);
		int  gc0Delta = 0, gc1Delta = 0, gc2Delta = 0;
		long allocDelta = 0;
		if (_gcBaselineSet)
		{
			double sec = realWindowSec;
			gc0Delta = gc0 - _lastGc0;
			gc1Delta = gc1 - _lastGc1;
			gc2Delta = gc2 - _lastGc2;
			allocDelta = allocBytes - _lastAllocBytes;
			Profiler.Gauge("engine", "gc_gen0_per_sec", (long)(gc0Delta / sec));
			Profiler.Gauge("engine", "gc_gen1_per_sec", (long)(gc1Delta / sec));
			Profiler.Gauge("engine", "gc_gen2_per_sec", (long)(gc2Delta / sec));
			Profiler.Gauge("engine", "alloc_mb_per_sec", (long)(allocDelta / sec / (1024.0 * 1024.0)));
		}
		_lastGc0 = gc0; _lastGc1 = gc1; _lastGc2 = gc2; _lastAllocBytes = allocBytes;
		_gcBaselineSet = true;

		double frameBudgetTotalMsPerSec = 0;
		double netInBytesPerSec        = 0;
		double sampleWindowSec         = realWindowSec;
		List<(string name, double ms)>? timerDeltas = null;
		foreach (var c in Profiler.All)
		{
			if (c.ExternallySampled) continue;
			long delta = c.ValueRaw - c.LastSnapshotValue;
			if (c.Kind == ProfilerKind.Timer)
			{
				double ms = delta * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
				frameBudgetTotalMsPerSec += ms / sampleWindowSec;
				if (ms > 0)
				{
					timerDeltas ??= new List<(string, double)>(32);
					timerDeltas.Add((c.Category + "." + c.Name, ms));
				}
			}
			else if (c.Kind == ProfilerKind.Counter
				&& (c.Category == "net.in.bytes" || c.Category == "server.net.in.bytes"))
			{
				netInBytesPerSec += delta / sampleWindowSec;
			}
		}
		Profiler.Gauge("aggregate", "frame_budget_ms_s", (long)frameBudgetTotalMsPerSec);
		Profiler.Gauge("aggregate", "net_in_bytes_s",    (long)netInBytesPerSec);

		if (Profiler.CurrentSampleIndex % 50 == 0)
			MachineMemoryProbe.Sample();

		if (realFrameMaxMs > Profiler.SpikeFrameMs)
		{
			List<(string, double)> top;
			if (timerDeltas != null)
			{
				timerDeltas.Sort((a, b) => b.ms.CompareTo(a.ms));
				int take = System.Math.Min(5, timerDeltas.Count);
				top = new List<(string, double)>(take);
				for (int i = 0; i < take; i++) top.Add(timerDeltas[i]);
			}
			else top = new List<(string, double)>();
			Profiler.RecordSpike(new Profiler.SpikeRecord
			{
				SampleIndex    = Profiler.CurrentSampleIndex,
				FrameBudgetMs  = frameBudgetTotalMsPerSec,
				RealFrameMs    = realFrameMaxMs,
				HeapMb         = GC.GetTotalMemory(false) >> 20,
				ActiveMachines = Terraria.DataStructures.TileEntity.ByID.Count,
				Gc0Delta       = gc0Delta,
				Gc1Delta       = gc1Delta,
				Gc2Delta       = gc2Delta,
				AllocMbPerSec  = (long)(allocDelta / sampleWindowSec / (1024.0 * 1024.0)),
				TopTimers      = top,
			});
		}

		Profiler.SampleAll(realWindowSec);
		Profiler.AdvanceSampleIndex();

		if (Main.netMode == Terraria.ID.NetmodeID.Server)
			TerrariaCompat.Net.ProfilerSyncPacket.Broadcast();
	}

	public static string DumpToFile()
	{
		var sb = new StringBuilder();
		sb.Append("{\n");
		sb.Append("  \"timestamp\": \"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")).Append("\",\n");
		sb.Append("  \"netMode\": ").Append((int)Main.netMode).Append(",\n");
		sb.Append("  \"world\": \"").Append(JsonEscape(Main.worldName ?? "")).Append("\",\n");
		sb.Append("  \"window_seconds\": ").Append(Profiler.WindowSamples * Profiler.SamplePeriodFrames / 60.0).Append(",\n");
		sb.Append("  \"sample_interval_ms\": ").Append(Profiler.SamplePeriodFrames * 1000.0 / 60.0).Append(",\n");
		double windowMs = Profiler.WindowSamples * Profiler.SamplePeriodFrames * 1000.0 / 60.0;
		sb.Append("  \"samples_origin_utc\": \"").Append(DateTime.UtcNow.AddMilliseconds(-windowMs).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).Append("\",\n");
		sb.Append("  \"current_sample_index\": ").Append(Profiler.CurrentSampleIndex).Append(",\n");
		sb.Append("  \"spikes\": [\n");
		bool firstSpike = true;
		foreach (var s in Profiler.Spikes)
		{
			if (!firstSpike) sb.Append(",\n");
			firstSpike = false;
			sb.Append("    {");
			sb.Append("\"sample_index\":").Append(s.SampleIndex).Append(',');
			sb.Append("\"frame_budget_ms_s\":").Append(F(s.FrameBudgetMs)).Append(',');
			sb.Append("\"real_frame_ms\":").Append(F(s.RealFrameMs)).Append(',');
			sb.Append("\"heap_mb\":").Append(s.HeapMb).Append(',');
			sb.Append("\"active_machines\":").Append(s.ActiveMachines).Append(',');
			sb.Append("\"gc0_delta\":").Append(s.Gc0Delta).Append(',');
			sb.Append("\"gc1_delta\":").Append(s.Gc1Delta).Append(',');
			sb.Append("\"gc2_delta\":").Append(s.Gc2Delta).Append(',');
			sb.Append("\"alloc_mb_per_sec\":").Append(s.AllocMbPerSec).Append(',');
			sb.Append("\"top_timers\":[");
			for (int i = 0; i < s.TopTimers.Count; i++)
			{
				if (i > 0) sb.Append(',');
				var (name, ms) = s.TopTimers[i];
				sb.Append("{\"name\":\"").Append(JsonEscape(name)).Append("\",\"ms\":").Append(F(ms)).Append('}');
			}
			sb.Append("]}");
		}
		sb.Append("\n  ],\n");
		sb.Append("  \"counters\": [\n");
		bool first = true;
		foreach (var c in Profiler.All)
		{
			if (!first) sb.Append(",\n");
			first = false;
			var (current, min, max, avg) = c.Summarize();
			sb.Append("    {");
			sb.Append("\"category\":\"").Append(JsonEscape(c.Category)).Append("\",");
			sb.Append("\"name\":\"").Append(JsonEscape(c.Name)).Append("\",");
			sb.Append("\"kind\":\"").Append(c.Kind.ToString()).Append("\",");
			sb.Append("\"raw\":").Append(c.ValueRaw).Append(',');
			sb.Append("\"current\":").Append(F(current)).Append(',');
			sb.Append("\"min\":").Append(F(min)).Append(',');
			sb.Append("\"max\":").Append(F(max)).Append(',');
			sb.Append("\"avg\":").Append(F(avg)).Append(',');
			sb.Append("\"samples\":[");
			int n = c.Samples.Length;
			for (int i = 0; i < n; i++)
			{
				if (i > 0) sb.Append(',');
				int idx = (c.SampleHead + i) % n;
				sb.Append(F(c.Samples[idx]));
			}
			sb.Append("]}");
		}
		sb.Append("\n  ]\n}\n");

		string dir = Path.Combine(Main.SavePath, "GregTechCEuTerraria");
		Directory.CreateDirectory(dir);
		string file = Path.Combine(dir, $"profile-{DateTime.Now:yyyyMMdd-HHmmss}.json");
		File.WriteAllText(file, sb.ToString());
		return file;
	}

	private static string F(double v) =>
		double.IsNaN(v) || double.IsInfinity(v) ? "0" : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

	private static string JsonEscape(string s)
	{
		var sb = new StringBuilder(s.Length);
		foreach (var ch in s)
		{
			switch (ch)
			{
				case '\\': sb.Append("\\\\"); break;
				case '"':  sb.Append("\\\""); break;
				case '\n': sb.Append("\\n");  break;
				case '\r': sb.Append("\\r");  break;
				case '\t': sb.Append("\\t");  break;
				default:
					if (ch < 32) sb.Append("\\u").Append(((int)ch).ToString("x4"));
					else sb.Append(ch);
					break;
			}
		}
		return sb.ToString();
	}
}
