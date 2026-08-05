#nullable enable
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.Machine;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class MeNetworkSystem : ModSystem
{
	//TODO port level emitter
	//TODO port wireless terminals
	//TODO port annihilation/formation planes
	//TODO port toggle bus

	private static readonly List<MeNetwork> _networks = new();
	private static readonly Dictionary<(int x, int y), MeNetwork> _byCell = new();
	private static bool _endpointsDirty;
	private static int _lastEntityCount = -1;

	private static readonly (int dx, int dy)[] _dirs = { (0, -1), (0, 1), (-1, 0), (1, 0) };

	private static readonly (int dx, int dy, IODirection dir)[] _dirsSided =
	{
		(0, -1, IODirection.Up), (0, 1, IODirection.Down), (-1, 0, IODirection.Left), (1, 0, IODirection.Right),
	};

	private static bool BlocksNetworkOn(MetaMachine m, IODirection edge) =>
		m is global::GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.PatternProviderMachine p
		&& p.PushDirection != IODirection.None && p.PushDirection == edge;

	public static int NetCount => _networks.Count;
	public static IReadOnlyList<MeNetwork> Nets => _networks;

	public static MeNetwork? NetAt(int x, int y) =>
		_byCell.TryGetValue((x, y), out var n) ? n : null;

	public static MeNetwork? NetOf(MetaMachine machine)
	{
		foreach (var (cx, cy) in machine.Cells())
		{
			var own = NetAt(cx, cy);
			if (own != null) return own;
		}
		return null;
	}

	public static Terraria.Item PushItemIntoNet(MetaMachine machine, Terraria.Item item, bool simulate)
	{
		if (item is null || item.IsAir) return new Terraria.Item();
		var net = NetOf(machine);
		if (net is null) return item.Clone();
		var key = AEItemKey.Of(item);
		if (key is null) return item.Clone();
		long inserted = net.GetStorage().Insert(key, item.stack,
			simulate ? Actionable.SIMULATE : Actionable.MODULATE, IActionSource.Empty());
		long left = item.stack - inserted;
		if (left <= 0) return new Terraria.Item();
		var rem = item.Clone();
		rem.stack = (int)left;
		return rem;
	}

	public static int FillFluidIntoNet(MetaMachine machine, FluidStack stack, bool simulate)
	{
		if (stack.IsEmpty) return 0;
		var net = NetOf(machine);
		if (net is null) return 0;
		var key = AEFluidKey.Of(stack);
		if (key is null) return 0;
		return (int)net.GetStorage().Insert(key, stack.Amount,
			simulate ? Actionable.SIMULATE : Actionable.MODULATE, IActionSource.Empty());
	}

	public static void MarkEndpointsDirty() => _endpointsDirty = true;

	public static void RequestCraftingUpdate(MetaMachine machine) =>
		NetOf(machine)?.InvalidateCraftingCache();

	public override void ClearWorld()
	{
		_networks.Clear();
		_byCell.Clear();
		_lastEntityCount = -1;
	}

	public override void PostUpdateWorld()
	{
		MaybeRebuild();
		foreach (var net in _networks)
			net.InvalidateCache();
	}

	public override void PostUpdateEverything() => MaybeRebuild();

	public static void MaybeRebuild()
	{
		int currentCount = TileEntity.ByID.Count;
		if (currentCount != _lastEntityCount)
		{
			_lastEntityCount = currentCount;
			_endpointsDirty = true;
		}

		if (MeCableLayerSystem.Cables.IsDirty || MeBusLayerSystem.Buses.IsDirty || _endpointsDirty)
		{
			Rebuild();
			MeCableLayerSystem.Cables.ClearDirty();
			MeBusLayerSystem.Buses.ClearDirty();
			_endpointsDirty = false;
		}
	}

	private static readonly Dictionary<(int x, int y), MeCableCell> EmptyCableCells = new();

	//TODO persistent network to avoid rebuilds
	private static void Rebuild()
	{
		_networks.Clear();
		_byCell.Clear();

		var cables = MeCableLayerSystem.Cables;

		var cableId = new Dictionary<(int x, int y), int>();
		foreach (var kv in cables.All) cableId[kv.Key] = cableId.Count;
		int C = cableId.Count;

		var machines = new List<MetaMachine>();
		var machineCells = new List<List<(int x, int y)>>();
		var cellToMachine = new Dictionary<(int x, int y), int>();
		foreach (var te in TileEntity.ByID.Values)
		{
			if (te is not MetaMachine m || m is not IMeNetworkConnected) continue;
			int mi = machines.Count;
			machines.Add(m);
			var cells = new List<(int x, int y)>();
			foreach (var c in m.Cells()) { cells.Add(c); cellToMachine[c] = mi; }
			machineCells.Add(cells);
		}
		int M = machines.Count;
		int N = C + M;
		if (N == 0) return;

		var parent = new int[N];
		for (int i = 0; i < N; i++) parent[i] = i;
		int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
		void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
		int MUnit(int mi) => C + mi;

		foreach (var kv in cables.All)
		{
			var (x, y) = kv.Key;
			int id = cableId[kv.Key];
			foreach (var (dx, dy) in _dirs)
			{
				var n = PipePassthrough.EffectiveNeighbor(x, y, dx, dy);
				if (cableId.TryGetValue(n, out var nid) && cables.Connects(x, y, n.x, n.y))
					Union(id, nid);
			}
		}
		for (int mi = 0; mi < M; mi++)
			foreach (var (cx, cy) in machineCells[mi])
				foreach (var (dx, dy, dir) in _dirsSided)
				{
					var n = (cx + dx, cy + dy);
					bool blocked = BlocksNetworkOn(machines[mi], dir);
					if (cellToMachine.TryGetValue(n, out var omi) && omi != mi
						&& !blocked && !BlocksNetworkOn(machines[omi], dir.Opposite()))
						Union(MUnit(mi), MUnit(omi));
					if (cableId.TryGetValue(n, out var cid) && !blocked
						&& MeBusLayerSystem.Buses.Get(n.Item1, n.Item2, dir.Opposite()) is null)
						Union(MUnit(mi), cid);
				}

		var rootCableCells = new Dictionary<int, Dictionary<(int x, int y), MeCableCell>>();
		var rootAnchor = new Dictionary<int, (int x, int y)>();
		void NoteAnchor(int root, (int x, int y) cell)
		{
			if (!rootAnchor.TryGetValue(root, out var a) || cell.x < a.x || (cell.x == a.x && cell.y < a.y))
				rootAnchor[root] = cell;
		}
		foreach (var kv in cables.All)
		{
			int root = Find(cableId[kv.Key]);
			if (!rootCableCells.TryGetValue(root, out var d)) rootCableCells[root] = d = new();
			d[kv.Key] = kv.Value;
			NoteAnchor(root, kv.Key);
		}
		for (int mi = 0; mi < M; mi++)
		{
			int root = Find(MUnit(mi));
			foreach (var c in machineCells[mi]) NoteAnchor(root, c);
		}

		var rootNet = new Dictionary<int, MeNetwork>();
		foreach (var (root, anchor) in rootAnchor)
		{
			var cells = rootCableCells.TryGetValue(root, out var d) ? d : EmptyCableCells;
			var net = new MeNetwork(cells, anchor);
			rootNet[root] = net;
			_networks.Add(net);
			foreach (var pos in cells.Keys) _byCell[pos] = net;
		}

		for (int mi = 0; mi < M; mi++)
		{
			if (!rootNet.TryGetValue(Find(MUnit(mi)), out var net)) continue;
			var m = machines[mi];
			if (m is IMeStorageDevice device) net.AddDevice(device);
			if (m is IMePatternProvider provider) net.AddProvider(provider);
			if (m is IMeInventoryExposer) net.AddInterface();
			foreach (var c in machineCells[mi]) _byCell[c] = net;
		}

		foreach (var net in _networks)
			foreach (var pos in net.Cells.Keys)
				MountStorageBuses(net, pos.x, pos.y);
	}

	private static void MountStorageBuses(MeNetwork net, int cx, int cy)
	{
		if (!MeBusLayerSystem.Buses.HasAny(cx, cy)) return;
		foreach (var (side, dx, dy) in IODirectionExtensions.Cardinal4)
		{
			var att = MeBusLayerSystem.Buses.Get(cx, cy, side);
			if (att is null || att.Kind != MeBusKind.Storage) continue;

			int nx = cx + dx, ny = cy + dy;
			var arrival = side.Opposite();

			if (MachineCellResolver.TryFindMachineAt(nx, ny, out var adj) && adj is IMeInventoryExposer exposer)
			{
				net.AddSource(att.Priority, new DelegatingMeStorage(exposer.GetExposedInventory));
				continue;
			}

			bool filterExt = att.FilterOnExtract;
			bool filterAvail = att.ExtractableOnly && att.FilterOnExtract;
			net.AddSource(att.Priority,
				new ItemHandlerMeStorage(
					() => Capabilities.WorldCapability.ItemHandlerAt(nx, ny, arrival), att.Access, att.ImportAllows, att.PartitionListed, filterExt, filterAvail),
				new FluidHandlerMeStorage(
					() => Capabilities.WorldCapability.FluidHandlerAt(nx, ny, arrival), att.Access, att.ImportAllows, att.PartitionListed, filterExt, filterAvail));
		}
	}
}
