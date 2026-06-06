#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace GregTechCEuTerraria.TerrariaCompat.Profiler;

// Lightweight in-process profiler. Three counter kinds:
//   - Counter - monotonic delta; UI shows events/sec averaged over the window.
//   - Gauge   - instantaneous reading; UI shows current + min/max over the window.
//   - Timer   - like Counter but in ticks; UI shows ms/sec (= % of frame time).
//
// Sampling cadence: ProfilerSystem.PostUpdateEverything pushes one sample per
// 6 frames into a ring buffer (10 Hz, 1800 samples = 3 min window). All counters
// share the same sampling clock so the graph X-axes are comparable.
//
// No locks, no Interlocked. tML is single-threaded outside FastParallel; we
// never increment from a parallel worker. (If we ever need to, the call site
// must marshal back to the main thread before touching the counter.)
public enum ProfilerKind : byte { Counter, Gauge, Timer }

public sealed class ProfilerCounter
{
	public readonly string Category;
	public readonly string Name;
	public readonly ProfilerKind Kind;

	// Raw value:
	//   Counter - total events since registration (monotonic).
	//   Gauge   - last set value.
	//   Timer   - total Stopwatch ticks since registration (monotonic).
	public long ValueRaw;

	// Sample ring (last N samples; oldest at SampleHead, newest at SampleHead-1).
	public readonly double[] Samples;
	public int SampleHead;
	public int SamplesWritten;
	public long LastSnapshotValue;

	// When true, this counter's samples are written externally (e.g. by
	// ProfilerSyncPacket pushing server-side values into client-side
	// `server.*` mirrors)
	public bool ExternallySampled;

	public long   SyncLastVal;
	public double SyncLastSamp;
	public bool   SyncInit;

	public ushort SyncId;
	public bool   SyncDefSent;

	internal ProfilerCounter(string category, string name, ProfilerKind kind, int windowSamples)
	{
		Category = category;
		Name     = name;
		Kind     = kind;
		Samples  = new double[windowSamples];
	}

	internal void RecordSample(double sample)
	{
		Samples[SampleHead] = sample;
		SampleHead = (SampleHead + 1) % Samples.Length;
		if (SamplesWritten < Samples.Length) SamplesWritten++;
	}


	public (double current, double min, double max, double avg) Summarize()
	{
		double current, min = double.MaxValue, max = double.MinValue, sum = 0;
		int n = Samples.Length;
		int filled = SamplesWritten < n ? SamplesWritten : n;
		for (int i = 0; i < filled; i++)
		{
			double v = Samples[i];
			if (v < min) min = v;
			if (v > max) max = v;
			sum += v;
		}
		double avg = filled > 0 ? sum / filled : 0;
		int newestIdx = (SampleHead - 1 + n) % n;
		current = Kind == ProfilerKind.Gauge ? ValueRaw : Samples[newestIdx];
		if (min == double.MaxValue) min = 0;
		if (max == double.MinValue) max = 0;
		return (current, min, max, avg);
	}
}

public static class Profiler
{
	public const int WindowSamples = 1800;        // 3 min at 10 Hz (180 s)
	public const int SamplePeriodFrames = 6;      // sample every 6 ticks (10 Hz)

	public static bool Enabled = true;

	private static readonly Dictionary<string, ProfilerCounter> _counters = new();
	private static readonly List<ProfilerCounter> _ordered = new();

	public static IReadOnlyList<ProfilerCounter> All => _ordered;

	public static byte SyncEpoch { get; private set; }

	public static ProfilerCounter GetOrCreate(string category, string name, ProfilerKind kind)
	{
		string key = category + "." + name;
		if (_counters.TryGetValue(key, out var c)) return c;
		c = new ProfilerCounter(category, name, kind, WindowSamples) { SyncId = (ushort)_ordered.Count };
		_counters[key] = c;
		_ordered.Add(c);
		return c;
	}

	public static void Count(string category, string name, long n = 1)
	{
		if (!Enabled) return;
		var c = GetOrCreate(category, name, ProfilerKind.Counter);
		c.ValueRaw += n;
	}

	public static void Gauge(string category, string name, long value)
	{
		if (!Enabled) return;
		var c = GetOrCreate(category, name, ProfilerKind.Gauge);
		c.ValueRaw = value;
	}

	public static void Gauge(string category, string name, int value) => Gauge(category, name, (long)value);

	public readonly struct TimerScope : IDisposable
	{
		private readonly ProfilerCounter? _c;
		private readonly long _start;
		internal TimerScope(ProfilerCounter? c) { _c = c; _start = c == null ? 0 : Stopwatch.GetTimestamp(); }
		public void Dispose() { if (_c != null) _c.ValueRaw += Stopwatch.GetTimestamp() - _start; }
	}

	public static TimerScope Time(string category, string name) =>
		Enabled ? new(GetOrCreate(category, name, ProfilerKind.Timer)) : new((ProfilerCounter?)null);

	public readonly struct TimeAllocScope : IDisposable
	{
		private readonly ProfilerCounter? _timer;
		private readonly ProfilerCounter? _alloc;
		private readonly long _startTicks;
		private readonly long _startBytes;
		internal TimeAllocScope(ProfilerCounter? timer, ProfilerCounter? alloc)
		{
			_timer = timer; _alloc = alloc;
			_startTicks = timer == null ? 0 : Stopwatch.GetTimestamp();
			_startBytes = alloc == null ? 0 : GC.GetAllocatedBytesForCurrentThread();
		}
		public void Dispose()
		{
			if (_timer != null) _timer.ValueRaw += Stopwatch.GetTimestamp() - _startTicks;
			if (_alloc != null) _alloc.ValueRaw += GC.GetAllocatedBytesForCurrentThread() - _startBytes;
		}
	}

	public static TimeAllocScope TimeAlloc(string category, string name) =>
		Enabled
			? new(GetOrCreate(category, name, ProfilerKind.Timer),
			      GetOrCreate("alloc." + category, name, ProfilerKind.Counter))
			: new((ProfilerCounter?)null, (ProfilerCounter?)null);

	public static void AccumulateAlloc(string category, string name, long bytes)
	{
		if (!Enabled) return;
		GetOrCreate("alloc." + category, name, ProfilerKind.Counter).ValueRaw += bytes;
	}

	public static void AccumulateTimer(string category, string name, long stopwatchTicks)
	{
		if (!Enabled) return;
		var c = GetOrCreate(category, name, ProfilerKind.Timer);
		c.ValueRaw += stopwatchTicks;
	}

	internal static void SampleAll(double windowSeconds)
	{
		if (windowSeconds <= 0) windowSeconds = SamplePeriodFrames / 60.0;  // first sample / clock glitch fallback
		foreach (var c in _ordered)
		{
			if (c.ExternallySampled) continue;  // server-mirrored counter; wire owns the ring
			double sample;
			switch (c.Kind)
			{
				case ProfilerKind.Counter:
					sample = (c.ValueRaw - c.LastSnapshotValue) / windowSeconds;
					c.LastSnapshotValue = c.ValueRaw;
					break;
				case ProfilerKind.Timer:
					// Convert Stopwatch ticks to milliseconds per second of wall time.
					double deltaMs = (c.ValueRaw - c.LastSnapshotValue) * 1000.0 / Stopwatch.Frequency;
					sample = deltaMs / windowSeconds;  // ms of work per second of wall time
					c.LastSnapshotValue = c.ValueRaw;
					break;
				default: // Gauge
					sample = c.ValueRaw;
					break;
			}
			c.RecordSample(sample);
		}
	}

	internal static void Reset()
	{
		_counters.Clear();
		_ordered.Clear();
		_spikes.Clear();
		_sampleIndex = 0;
		SyncEpoch++;
	}

	public sealed class SpikeRecord
	{
		public long   SampleIndex;
		public double FrameBudgetMs;
		public double RealFrameMs;
		public long   HeapMb;
		public int    ActiveMachines;
		public int    Gc0Delta, Gc1Delta, Gc2Delta;
		public long   AllocMbPerSec;
		public List<(string name, double ms)> TopTimers = new();
	}

	public const double SpikeThresholdMs = 100.0;
	public const double SpikeFrameMs    = 50.0;
	public const int    SpikeRingSize    = 64;
	private static readonly List<SpikeRecord> _spikes = new();
	public static IReadOnlyList<SpikeRecord> Spikes => _spikes;

	private static long _sampleIndex;
	public  static long CurrentSampleIndex => _sampleIndex;
	internal static void AdvanceSampleIndex() => _sampleIndex++;

	internal static void RecordSpike(SpikeRecord r)
	{
		if (_spikes.Count >= SpikeRingSize) _spikes.RemoveAt(0);
		_spikes.Add(r);
	}
}
