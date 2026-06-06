#!/usr/bin/env python3
"""Analyze a GregTechCEuTerraria profiler dump.

The mod writes profile-YYYYMMDD-HHMMSS.json into
  <Terraria SavePath>/tModLoader/GregTechCEuTerraria/
(via Profiler `[Dump JSON]` / the profiler UI). This script turns one of those
dumps into the standard breakdown we look at every time, so we stop hand-writing
ad-hoc Python.

Usage:
    py tools/scripts/analyze-profile.py                # newest dump in the default dir
    py tools/scripts/analyze-profile.py <file.json>    # a specific dump
    py tools/scripts/analyze-profile.py --by max       # sort tables by max (default: avg)
    py tools/scripts/analyze-profile.py --grep energy  # only counters whose cat/name matches
    py tools/scripts/analyze-profile.py --top 25       # rows per table (default 15)
    py tools/scripts/analyze-profile.py --list         # list available dumps + exit

Notes on reading the numbers (post the 2026-06-06 instrumentation pass):
  * Timers are ms-per-second of wall time (= % of a second spent in that scope).
  * `engine/real_frame_ms` is ground-truth wall-clock frame time; compare to
    `engine/update_phase_ms` to see update-bound vs draw/GC-bound.
  * Rates are now computed against REAL elapsed time, so dumps from before
    2026-06-06 (which assumed 60fps) read ~12x higher at low fps - don't compare
    old and new dumps directly.
  * `cur` = latest sample (a spike moment); `avg` = mean over the window. Trust
    avg for steady-state, cur/max for spikes.
"""
import json
import os
import sys
import glob

DEFAULT_DIRS = [
    os.path.expanduser(
        r"~/Documents/My Games/Terraria/tModLoader/GregTechCEuTerraria"),
    r"C:/Users/caxap/Documents/My Games/Terraria/tModLoader/GregTechCEuTerraria",
]


def find_dumps():
    for d in DEFAULT_DIRS:
        hits = sorted(glob.glob(os.path.join(d, "profile-*.json")))
        if hits:
            return hits
    return []


# Set once in main() from the dump's current_sample_index. The mod's per-counter
# avg/min/max were (until 2026-06-06) computed over the whole 1800-slot ring even
# when a session filled only N slots, diluting avg ~ (N/1800) and pinning min to
# 0. We recompute over the FILLED tail of the samples array so OLD dumps read
# correctly too. In the dumped samples array (ring order oldest->newest), an
# unfilled ring puts the real data in the LAST `csi` entries.
_CSI = None


def _stats(c):
    """(avg, cur, min, max) over filled samples; falls back to dump fields."""
    cached = c.get("_stats")
    if cached:
        return cached
    samples = c.get("samples")
    if not samples or _CSI is None:
        v = lambda k: c.get(k, 0) if isinstance(c.get(k, 0), (int, float)) else 0
        r = (v("avg"), v("current"), v("min"), v("max"))
        c["_stats"] = r
        return r
    n = len(samples)
    filled = min(_CSI, n) if _CSI > 0 else n
    tail = samples[-filled:] if 0 < filled < n else samples
    if not tail:
        r = (0, 0, 0, 0)
    else:
        r = (sum(tail) / len(tail), tail[-1], min(tail), max(tail))
    c["_stats"] = r
    return r


def num(c, key):
    avg, cur, mn, mx = _stats(c)
    return {"avg": avg, "cur": cur, "current": cur, "min": mn, "max": mx}.get(key, 0)


def table(counters, title, *, cat_sub=None, cat_eq=None, name_sub=None,
          kind=None, by="avg", top=15, grep=None):
    sel = []
    for c in counters:
        cat, nm = c.get("category", ""), c.get("name", "")
        if cat_eq is not None and cat != cat_eq:
            continue
        if cat_sub is not None and cat_sub not in cat:
            continue
        if name_sub is not None and name_sub not in nm:
            continue
        if kind is not None and c.get("kind") != kind:
            continue
        if grep is not None and grep.lower() not in (cat + " " + nm).lower():
            continue
        sel.append(c)
    if not sel:
        return
    sel.sort(key=lambda c: -num(c, by))
    print(f"\n=== {title} ===  (sorted by {by})")
    for c in sel[:top]:
        print(f"  {num(c,'avg'):10.2f} avg  {num(c,'cur'):10.1f} cur  "
              f"{num(c,'max'):10.1f} max   {c['category']} | {c['name']}")


def alloc_table(counters, title, *, cat_eq=None, cat_sub=None, by="avg", top=15):
    """Alloc counters are bytes/sec; print as MB/s for readability.

    Server-side counters arrive prefixed with 'server.' (ProfilerSync), so an
    'alloc.tick' cat_eq is matched against both 'alloc.tick' and
    'server.alloc.tick'.
    """
    sel = []
    for c in counters:
        cat = c.get("category", "")
        bare = cat[len("server."):] if cat.startswith("server.") else cat
        if cat_eq is not None and bare != cat_eq:
            continue
        if cat_sub is not None and cat_sub not in cat:
            continue
        sel.append(c)
    if not sel:
        return
    sel.sort(key=lambda c: -num(c, by))
    print(f"\n=== {title} ===  (MB/s, sorted by {by})")
    for c in sel[:top]:
        mb = lambda k: num(c, k) / (1024.0 * 1024.0)
        print(f"  {mb('avg'):9.2f} avg  {mb('cur'):9.1f} cur  {mb('max'):9.1f} max"
              f"   {c['category']} | {c['name']}")


def gauge_get(counters, cat, name):
    for c in counters:
        if c.get("category") == cat and c.get("name") == name:
            return c
    return None


def health(counters):
    print("\n=== HEALTH (engine, window avg / cur / max) ===")
    rows = [
        ("engine", "fps", "render fps"),
        ("engine", "real_frame_ms", "real frame ms (NEW)"),
        ("engine", "update_phase_ms", "update-phase ms (NEW)"),
        ("aggregate", "frame_budget_ms_s", "our client timers ms/s"),
        ("server.aggregate", "frame_budget_ms_s", "server timers ms/s"),
        ("aggregate", "net_in_bytes_s", "net in bytes/s"),
        ("engine", "managed_heap_mb", "client heap MB"),
        ("server.engine", "managed_heap_mb", "server heap MB"),
        ("engine", "alloc_mb_per_sec", "alloc MB/s"),
        ("engine", "gc_gen0_per_sec", "gen0 GC/s"),
        ("engine", "gc_gen1_per_sec", "gen1 GC/s"),
        ("engine", "gc_gen2_per_sec", "gen2 GC/s"),
        ("engine", "tile_entities", "tile entities"),
        ("engine", "active_npcs", "active NPCs"),
        ("engine", "active_projectiles", "active projectiles"),
        ("engine", "active_items", "active items"),
        ("mem.machine_count", "TOTAL", "machines TOTAL"),
        ("mem.subsystem", "cable_cells", "cable cells"),
    ]
    for cat, name, label in rows:
        c = gauge_get(counters, cat, name)
        if c is None:
            continue
        print(f"  {label:28s} {num(c,'avg'):11.1f} / {num(c,'cur'):11.1f} / "
              f"{num(c,'max'):11.1f}")


def spikes(d):
    sp = d.get("spikes", [])
    print(f"\n=== SPIKES ({len(sp)}) ===")
    if not sp:
        print("  (none - if fps is low and this is empty, the dump predates the "
              "real-frame spike trigger or no frame exceeded SpikeFrameMs)")
        return
    for s in sp[-12:]:
        rf = s.get("real_frame_ms", 0)
        print(f"  idx{s.get('sample_index',0):>6}  real {rf:7.0f}ms  "
              f"budget {s.get('frame_budget_ms_s',0):7.0f}ms/s  "
              f"heap {s.get('heap_mb',0)}MB  "
              f"gc {s.get('gc0_delta',0)}/{s.get('gc1_delta',0)}/{s.get('gc2_delta',0)}  "
              f"alloc {s.get('alloc_mb_per_sec',0)}MB/s")
        for t in s.get("top_timers", [])[:5]:
            print(f"        {t.get('ms',0):8.1f}ms  {t.get('name','')}")


def main():
    args = sys.argv[1:]
    by = "avg"
    top = 15
    grep = None
    path = None
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--by":
            by = args[i + 1]; i += 2
        elif a == "--top":
            top = int(args[i + 1]); i += 2
        elif a == "--grep":
            grep = args[i + 1]; i += 2
        elif a == "--list":
            for h in find_dumps():
                sz = os.path.getsize(h)
                print(f"{sz:>9}  {h}")
            return
        else:
            path = a; i += 1

    if path is None:
        dumps = find_dumps()
        if not dumps:
            print("No profile-*.json found in:")
            for d in DEFAULT_DIRS:
                print("  ", d)
            print("Pass a path explicitly.")
            sys.exit(1)
        path = dumps[-1]

    with open(path, encoding="utf-8") as f:
        d = json.load(f)
    cs = d["counters"]
    global _CSI
    _CSI = d.get("current_sample_index", 0)
    interval = d.get("sample_interval_ms", 100)
    window = d.get("window_seconds", 180)
    session_s = _CSI * interval / 1000.0
    filled_pct = 100.0 * min(_CSI, 1800) / 1800.0

    print("#" * 70)
    print(f"# {os.path.basename(path)}")
    print(f"# world={d.get('world')!r}  netMode={d.get('netMode')}  "
          f"window={window}s  interval={interval}ms")
    print(f"# session={session_s:.0f}s ({_CSI} samples, ring {filled_pct:.0f}% full)"
          f"  -- stats computed over FILLED samples only")
    print("#" * 70)

    health(cs)
    spikes(d)

    if grep:
        table(cs, f"GREP '{grep}'", grep=grep, by=by, top=max(top, 40))
        return

    # The standard sweep we run every time.
    table(cs, "TOP TIMERS (server)", cat_eq="server.tick", kind="Timer", by=by, top=top)
    table(cs, "machine_systemtick by type", cat_sub="machine_systemtick.by_type", by=by, top=top)
    table(cs, "client_post_update by type (NEW)", cat_sub="client_post_update.by_type", by=by, top=top)
    table(cs, "CLIENT timers (net.handle etc)", cat_sub="net.handle", kind="Timer", by=by, top=top)

    table(cs, "net.in.bytes by packet", cat_eq="net.in.bytes", by=by, top=top)
    table(cs, "net.in.count by packet", cat_eq="net.in.count", by=by, top=top)
    table(cs, "state-sync SENT by type", cat_eq="server.net.sync.sent_by_type", by=by, top=top)
    table(cs, "state-sync bytes by type", cat_eq="server.net.sync.bytes_by_type", by=by, top=top)
    table(cs, "state-sync SKIPPED by type", cat_eq="server.net.sync.skipped_by_type", by=by, top=top)
    table(cs, "energy-sync bytes by type", cat_eq="server.net.energysync.bytes_by_type", by=by, top=top)

    # Allocation attribution (the GC-churn hunt) - NEW 2026-06-06.
    alloc_table(cs, "ALLOC by scope", cat_eq="alloc.tick", by=by, top=top)
    alloc_table(cs, "ALLOC machine_systemtick by type",
                cat_eq="alloc.tick.machine_systemtick.by_type", by=by, top=top)
    alloc_table(cs, "ALLOC client_post_update by type",
                cat_eq="alloc.tick.client_post_update.by_type", by=by, top=top)

    table(cs, "machine counts", cat_eq="mem.machine_count", by=by, top=top)
    table(cs, "subsystem memory", cat_eq="mem.subsystem", by=by, top=top)

    print("\n(tip: --grep <term> to drill in, --by max for spikes, "
          "--top N for more rows)")


if __name__ == "__main__":
    main()
