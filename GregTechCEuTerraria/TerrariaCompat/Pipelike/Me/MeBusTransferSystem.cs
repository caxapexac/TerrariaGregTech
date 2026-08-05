#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class MeBusTransferSystem : ModSystem
{
	private const int ImportMin = 5, ImportMax = 40;
	private const int ExportMin = 5, ExportMax = 60;
	private const int SpeedUp = 2, SlowDown = 1;

	private struct BusTick { public int RateMc; public long NextFire; }
	private static readonly Dictionary<(int x, int y, int side), BusTick> _ticks = new();
	private static readonly Dictionary<(int x, int y, int side), MeBusExportRequester> _exportRequesters = new();
	private static long _lastPrune;

	public override void ClearWorld()
	{
		_ticks.Clear();
		_exportRequesters.Clear();
	}

	private static MeBusExportRequester GetOrCreateRequester(int x, int y, int sideIdx)
	{
		var key = (x, y, sideIdx);
		if (!_exportRequesters.TryGetValue(key, out var r))
			_exportRequesters[key] = r = new MeBusExportRequester(
				x, y, MeBusLayer.SideFromIndex(sideIdx), MeBusAttachment.FilterSize);
		return r;
	}

	public override void PostUpdateWorld()
	{
		lock (Api.SaveTickGate.Lock)
			TickBusTransfers();
	}

	private void TickBusTransfers()
	{
		if (MeBusLayerSystem.Buses.All.Count == 0)
		{
			if (_ticks.Count > 0) _ticks.Clear();
			return;
		}

		long now = Main.GameUpdateCount;
		var src = IActionSource.Empty();

		foreach (var kv in MeBusLayerSystem.Buses.All)
		{
			int cx = kv.Key.x, cy = kv.Key.y;
			for (int i = 0; i < 4; i++)
			{
				var att = kv.Value[i];
				if (att is null || (att.Kind != MeBusKind.Import && att.Kind != MeBusKind.Export)) continue;

				bool import = att.Kind == MeBusKind.Import;
				int min = import ? ImportMin : ExportMin;
				int max = import ? ImportMax : ExportMax;

				var tkey = (cx, cy, i);
				if (!_ticks.TryGetValue(tkey, out var st))
					st = new BusTick { RateMc = max, NextFire = now };
				if (now < st.NextFire) { _ticks[tkey] = st; continue; }

				bool didWork = false;
				bool canWork = false;

				var net = MeNetworkSystem.NetAt(cx, cy);
				if (net is not null)
				{
					var side = MeBusLayer.SideFromIndex(i);
					var (dx, dy) = side.Offset();
					int nx = cx + dx, ny = cy + dy;
					var arrival = side.Opposite();
					var itemH = WorldCapability.ItemHandlerAt(nx, ny, arrival);
					var fluidH = WorldCapability.FluidHandlerAt(nx, ny, arrival);
					canWork = itemH != null || fluidH != null;

					if (canWork)
					{
						var netStorage = net.GetStorage();
						MEStorage item = new ItemHandlerMeStorage(() => WorldCapability.ItemHandlerAt(nx, ny, arrival));
						MEStorage fluid = new FluidHandlerMeStorage(() => WorldCapability.FluidHandlerAt(nx, ny, arrival));
						int ops = MeBusAttachment.OperationsForSpeed(att.Speed);
						if (import)
						{
							didWork |= RunImport(item, AEKeyType.Items(), netStorage, att, src, ref ops);
							didWork |= RunImport(fluid, AEKeyType.Fluids(), netStorage, att, src, ref ops);
						}
						else
						{
							var requester = att.CraftMissing ? GetOrCreateRequester(cx, cy, i) : null;
							didWork |= RunExport(att, netStorage, item, fluid, src, ref ops, requester, net);
						}
					}
				}

				int newRate = !canWork ? max : (didWork ? st.RateMc - SpeedUp : st.RateMc + SlowDown);
				st.RateMc = Math.Clamp(newRate, min, max);
				st.NextFire = now + global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(st.RateMc);
				_ticks[tkey] = st;
			}
		}

		if (now - _lastPrune >= 1200)
		{
			_lastPrune = now;
			PruneStale();
		}
	}

	private static void PruneStale()
	{
		var live = new HashSet<(int, int, int)>();
		foreach (var kv in MeBusLayerSystem.Buses.All)
			for (int i = 0; i < 4; i++)
			{
				var a = kv.Value[i];
				if (a is not null && (a.Kind == MeBusKind.Import || a.Kind == MeBusKind.Export))
					live.Add((kv.Key.x, kv.Key.y, i));
			}
		if (live.Count != _ticks.Count)
		{
			var dead = new List<(int, int, int)>();
			foreach (var k in _ticks.Keys)
				if (!live.Contains(k)) dead.Add(k);
			foreach (var k in dead) _ticks.Remove(k);
		}

		List<(int, int, int)>? deadReq = null;
		foreach (var kv in _exportRequesters)
		{
			var att = MeBusLayerSystem.Buses.Get(kv.Key.x, kv.Key.y, MeBusLayer.SideFromIndex(kv.Key.side));
			bool stillExport = att is { Kind: MeBusKind.Export };
			if (!stillExport && kv.Value.GetRequestedJobs().Count == 0)
				(deadReq ??= new()).Add(kv.Key);
		}
		if (deadReq != null)
			foreach (var k in deadReq) _exportRequesters.Remove(k);
	}

	public override void SaveWorldData(TagCompound tag)
	{
		lock (Api.SaveTickGate.Lock)
			WriteWorldData(tag);
	}

	private void WriteWorldData(TagCompound tag)
	{
		if (_exportRequesters.Count == 0) return;
		var list = new List<TagCompound>();
		foreach (var kv in _exportRequesters)
		{
			var trackerTag = new TagCompound();
			kv.Value.Tracker.WriteToNBT(trackerTag);
			if (trackerTag.Count == 0) continue;
			list.Add(new TagCompound
			{
				["x"] = kv.Key.x,
				["y"] = kv.Key.y,
				["side"] = (byte)kv.Key.side,
				["t"] = trackerTag,
			});
		}
		if (list.Count > 0) tag["me_bus.export_jobs"] = list;
	}

	public override void LoadWorldData(TagCompound tag)
	{
		_exportRequesters.Clear();
		if (!tag.ContainsKey("me_bus.export_jobs")) return;
		foreach (var t in tag.GetList<TagCompound>("me_bus.export_jobs"))
		{
			var r = GetOrCreateRequester(t.GetInt("x"), t.GetInt("y"), t.GetByte("side"));
			r.Tracker.ReadFromNBT(t.GetCompound("t"));
		}
	}

	private static bool RunImport(MEStorage external, AEKeyType keyType, MEStorage net,
		MeBusAttachment att, IActionSource src, ref int ops)
	{
		if (ops <= 0) return false;
		long remainingTransferAmount = (long)ops * keyType.GetAmountPerOperation();
		bool did = false;
		var avail = new KeyCounter();
		external.GetAvailableStacks(avail);
		foreach (var kv in avail)
		{
			if (remainingTransferAmount <= 0) break;
			var what = kv.Key;
			if (!att.ImportAllows(what)) continue;

			long amountForThisResource = net.Insert(what, remainingTransferAmount, Actionable.SIMULATE, src);
			long amount = external.Extract(what, amountForThisResource, Actionable.MODULATE, src);
			if (amount > 0)
			{
				long inserted = net.Insert(what, amount, Actionable.MODULATE, src);
				if (inserted < amount)
					external.Insert(what, amount - inserted, Actionable.MODULATE, src);
				long opsUsed = Math.Max(1, inserted / keyType.GetAmountPerOperation());
				ops -= (int)opsUsed;
				remainingTransferAmount -= inserted;
				if (inserted > 0) did = true;
			}
		}
		return did;
	}

	private static bool RunExport(MeBusAttachment att, MEStorage net, MEStorage item, MEStorage fluid,
		IActionSource src, ref int ops, MeBusExportRequester? requester, MeNetwork meNet)
	{
		bool did = false;
		bool crafting = att.CraftMissing && requester != null;
		bool craftOnly = crafting && att.CraftOnly;
		int count = MeBusAttachment.FilterSize;
		int x = 0;
		for (; x < count && ops > 0; x++)
		{
			int slot = GetStartingSlot(att, x, count);
			var key = att.Filter[slot];
			if (key is null) continue;

			var keyType = key.KeyType;
			var to = key is AEFluidKey ? fluid : item;

			if (craftOnly)
			{
				did |= AttemptCrafting(requester!, meNet, slot, key, keyType, to, src, ref ops);
				continue;
			}

			int before = ops;
			long amount = (long)ops * keyType.GetAmountPerOperation();

			long extracted = net.Extract(key, amount, Actionable.SIMULATE, src);
			long wasInserted = to.Insert(key, extracted, Actionable.SIMULATE, src);
			if (wasInserted > 0)
			{
				extracted = net.Extract(key, wasInserted, Actionable.MODULATE, src);
				long inserted = to.Insert(key, extracted, Actionable.MODULATE, src);
				if (inserted < extracted)
					net.Insert(key, extracted - inserted, Actionable.MODULATE, src);
				long opsUsed = Math.Max(1, inserted / keyType.GetAmountPerOperation());
				ops -= (int)opsUsed;
				if (inserted > 0) did = true;
			}

			if (before == ops && crafting)
				did |= AttemptCrafting(requester!, meNet, slot, key, keyType, to, src, ref ops);
		}

		if (did)
			UpdateSchedulingMode(att, x, count);
		return did;
	}

	private static bool AttemptCrafting(MeBusExportRequester requester, MeNetwork net, int slot,
		AEKey key, AEKeyType keyType, MEStorage to, IActionSource src, ref int ops)
	{
		long maxAmount = (long)ops * keyType.GetAmountPerOperation();
		if (maxAmount < 1) return false;
		long amount = to.Insert(key, maxAmount, Actionable.SIMULATE, src);
		if (amount <= 0) return false;
		requester.Tracker.HandleCrafting(slot, key, amount, net, src);
		ops -= (int)Math.Max(1, amount / keyType.GetAmountPerOperation());
		return true;
	}

	private static int GetStartingSlot(MeBusAttachment att, int x, int count) => att.Scheduling switch
	{
		MeBusSchedulingMode.Random     => Main.rand.Next(count),
		MeBusSchedulingMode.RoundRobin => (att.NextSlot + x) % count,
		_                              => x,
	};

	private static void UpdateSchedulingMode(MeBusAttachment att, int x, int count)
	{
		if (att.Scheduling == MeBusSchedulingMode.RoundRobin)
			att.NextSlot = (att.NextSlot + x) % count;
	}
}
