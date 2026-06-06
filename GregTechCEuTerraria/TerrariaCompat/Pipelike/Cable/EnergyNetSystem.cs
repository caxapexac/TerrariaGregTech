#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.Api.Capability;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;

// Orchestrates the energy net: rebuilds connected components when the cable
// layer changes, links endpoints via "wire behind machine" same-cell adjacency
// (see LinkEndpoints), ticks every network in PostUpdateWorld.
public sealed class EnergyNetSystem : ModSystem
{
	private static readonly List<EnergyNet> _networks = new();
	private static readonly Dictionary<(int x, int y), EnergyNet> _byCell = new();
	private static readonly Dictionary<(int x, int y), IEnergyContainer> _endpoints = new();
	private static bool _endpointsDirty;

	private static readonly Dictionary<(int x, int y), (long extracted, long delivered)> _clientStats = new();
	public  static Dictionary<(int x, int y), (long extracted, long delivered)> ClientStats => _clientStats;

	public static int NetCount => _networks.Count;
	public static IReadOnlyList<EnergyNet> Nets => _networks;
	public static EnergyNet? NetAt(int x, int y) =>
		_byCell.TryGetValue((x, y), out var n) ? n : null;

	public static (long extracted, long delivered) GetThroughput(EnergyNet net)
	{
		if (TerrariaCompat.Machine.MetaMachine.IsClient)
		{
			if (_clientStats.TryGetValue(net.AnchorCell, out var stats)) return stats;
			return (0, 0);
		}
		return (net.LastTickExtracted, net.LastTickDelivered);
	}

	public static void RegisterEndpoint(int x, int y, IEnergyContainer container)
	{
		_endpoints[(x, y)] = container;
		_endpointsDirty = true;
	}

	public static void UnregisterEndpoint(int x, int y)
	{
		if (_endpoints.Remove((x, y)))
			_endpointsDirty = true;
	}

	public static void MarkEndpointsDirty() => _endpointsDirty = true;

	public override void OnWorldLoad()
	{
		_endpoints.Clear();
		foreach (var kv in TileEntity.ByID)
			RegisterEndpointCells(kv.Value);
		_endpointsDirty = true;
	}

	private static void RegisterEndpointCells(TileEntity te)
	{
		if (te is not IEnergyContainer container) return;
		if (container is ILaserContainer) return;
		if (te is TerrariaCompat.Machine.MetaMachine machine)
		{
			foreach (var (cx, cy) in machine.Cells())
				_endpoints[(cx, cy)] = container;
		}
		else
		{
			_endpoints[(te.Position.X, te.Position.Y)] = container;
		}
	}

	private static int _lastEntityCount = -1;

	// Default ~10 Hz at 60 fps, overridden per-tick by config
	private const int DefaultStateSyncPeriod = 6;
	private static int StateSyncPeriod =>
		global::GregTechCEuTerraria.Config.GTConfig.Instance?.NetworkSyncPeriod ?? DefaultStateSyncPeriod;
	private static int _stateSyncCounter;

	// Rebuild trigger - runs on BOTH server (via PostUpdateWorld) and client
	public static void MaybeRebuild()
	{
		int currentCount = TileEntity.ByID.Count;
		if (currentCount != _lastEntityCount)
		{
			_lastEntityCount = currentCount;
			_endpointsDirty = true;
		}

		if (CableLayerSystem.Cables.IsDirty || _endpointsDirty)
		{
			using (Profiler.Profiler.TimeAlloc("tick", "energy_net_rebuild"))
				Rebuild();
			CableLayerSystem.Cables.ClearDirty();
			_endpointsDirty = false;
		}
	}

	public override void PostUpdateEverything()
	{
		MaybeRebuild();

		if (TerrariaCompat.Machine.MetaMachine.IsClient)
		{
			using (Profiler.Profiler.TimeAlloc("tick", "client_post_update"))
			{
				foreach (var te in TileEntity.ByID.Values)
				{
					if (te is not TerrariaCompat.Machine.MetaMachine machine) continue;
					long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
					long b0 = System.GC.GetAllocatedBytesForCurrentThread();
					machine.OnClientPostUpdate();
					string typeName = machine.GetType().Name;
					Profiler.Profiler.AccumulateTimer(
						"tick.client_post_update.by_type", typeName,
						System.Diagnostics.Stopwatch.GetTimestamp() - t0);
					Profiler.Profiler.AccumulateAlloc(
						"tick.client_post_update.by_type", typeName,
						System.GC.GetAllocatedBytesForCurrentThread() - b0);
				}
			}
		}
	}

	public override void PostUpdateWorld()
	{
		using var _energyNetScope = Profiler.Profiler.TimeAlloc("tick", "energy_net_total");
		MaybeRebuild();

		if (TerrariaCompat.Machine.MetaMachine.IsClient) return;

		int machineCount = 0;
		using (Profiler.Profiler.TimeAlloc("tick", "machine_systemtick"))
		{
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is TerrariaCompat.Machine.MetaMachine machine)
				{
					long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
					long b0 = System.GC.GetAllocatedBytesForCurrentThread();
					try { machine.SystemTick(); }
					catch (System.Exception ex)
					{
						ModContent.GetInstance<GregTechCEuTerraria>()?.Logger.Warn(
							$"[SystemTick] ({machine.Position.X},{machine.Position.Y}) " +
							$"{machine.GetType().Name} threw - isolated, continuing", ex);
					}
					long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
					long allocated = System.GC.GetAllocatedBytesForCurrentThread() - b0;
					string typeName = machine.GetType().Name;
					Profiler.Profiler.AccumulateTimer(
						"tick.machine_systemtick.by_type", typeName, elapsed);
					Profiler.Profiler.AccumulateAlloc(
						"tick.machine_systemtick.by_type", typeName, allocated);
					machineCount++;
				}
			}
		}
		Profiler.Profiler.Gauge("counts", "machines", machineCount);
		Profiler.Profiler.Gauge("counts", "energy_networks", _networks.Count);

		if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) == 0)
		{
			using (Profiler.Profiler.TimeAlloc("tick", "energy_net_simulate"))
			{
				foreach (var net in _networks)
					net.Tick();
			}
		}

		// Periodic state-sync broadcast (server only).
		// Targets = GUI viewers union players within NearbyRadiusPx.
		if (Main.netMode == Terraria.ID.NetmodeID.Server)
		{
			int period = StateSyncPeriod;
			if (period < 1) period = 1;

			if (++_stateSyncCounter >= period)
			{
				_stateSyncCounter = 0;
				TerrariaCompat.Net.EnergyNetStatsPacket.Broadcast();
			}

			using var _broadcastScope = Profiler.Profiler.TimeAlloc("tick", "machine_state_broadcast");
			uint gt = Main.GameUpdateCount;
			foreach (var te in TileEntity.ByID.Values)
			{
				if (te is not TerrariaCompat.Machine.MetaMachine machine) continue;

				uint phase = ((uint)machine.Position.X * 2654435761u
				            + (uint)machine.Position.Y * 40503u) % (uint)period;
				if ((gt + phase) % (uint)period != 0) continue;
				try
				{
					machine.PruneViewers();
					TerrariaCompat.Net.MachineStateSyncPacket.BroadcastNearby(machine);
					TerrariaCompat.Net.MachineEnergySyncPacket.BroadcastNearby(machine);
					if (machine.HasViewers)
						TerrariaCompat.Net.EnderChannelSyncPacket.Broadcast(machine);
				}
				catch (System.Exception ex)
				{
					Terraria.ModLoader.ModLoader.GetMod("GregTechCEuTerraria")?.Logger?.Error(
						$"[StateSync] entity ({machine.Position.X},{machine.Position.Y}) " +
						$"{machine.GetType().Name} threw during broadcast - isolated, iteration continues: " +
						$"{ex.GetType().Name}: {ex.Message}");
				}
			}
		}
	}

	public override void ClearWorld()
	{
		_networks.Clear();
		_byCell.Clear();
		_endpoints.Clear();
		_clientStats.Clear();
	}

	private static void Rebuild()
	{
		_networks.Clear();
		_byCell.Clear();
		_endpoints.Clear();
		foreach (var te in TileEntity.ByID.Values)
			RegisterEndpointCells(te);

		var components = EnergyNetGraph.Build(CableLayerSystem.Cables);

		foreach (var comp in components)
		{
			var net = new EnergyNet(comp);
			_networks.Add(net);
			foreach (var pos in comp.Cells.Keys)
				_byCell[pos] = net;

			LinkEndpoints(net, comp);
		}

	}

	// SAME-CELL connectivity model ("wire behind machine")
	internal static void LinkEndpoints(EnergyNet net, NetworkComponent comp)
	{
		var seenProducers = new HashSet<IEnergyContainer>();
		var seenConsumers = new HashSet<IEnergyContainer>();
		foreach (var cell in comp.Cells.Keys)
		{
			TryLink(net, seenProducers, seenConsumers, cell.x, cell.y, cell.x, cell.y);
		}
		net.SetEndpointLookup(pos => _endpoints.TryGetValue(pos, out var ep) ? ep : null);
	}

	private static void TryLink(EnergyNet net,
		HashSet<IEnergyContainer> seenProducers,
		HashSet<IEnergyContainer> seenConsumers,
		int epX, int epY, int cableX, int cableY)
	{
		if (!_endpoints.TryGetValue((epX, epY), out var ep)) return;
		var face = ep.EnergyFaceForCell(epX, epY);
		if (ep.OutputsEnergy(face))
		{
			net.ProducerLinks.Add((cableX, cableY, ep));
			if (seenProducers.Add(ep)) net.Producers.Add(ep);
		}
		if (ep.InputsEnergy(face))
		{
			net.ConsumerLinks.Add((cableX, cableY, ep));
			if (seenConsumers.Add(ep)) net.Consumers.Add(ep);
		}
	}

	internal static void TestOnly_BuildAndLink(CableLayer layer, Dictionary<(int, int), IEnergyContainer> endpoints, List<EnergyNet> output)
	{
		var prev = new Dictionary<(int, int), IEnergyContainer>(_endpoints);
		_endpoints.Clear();
		foreach (var kv in endpoints) _endpoints[kv.Key] = kv.Value;
		try
		{
			output.Clear();
			var components = EnergyNetGraph.Build(layer);
			foreach (var comp in components)
			{
				var net = new EnergyNet(comp);
				output.Add(net);
				LinkEndpoints(net, comp);
			}
		}
		finally
		{
			_endpoints.Clear();
			foreach (var kv in prev) _endpoints[kv.Key] = kv.Value;
		}
	}
}
