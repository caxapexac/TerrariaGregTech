#nullable enable
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Menu.Me.Common;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;

public abstract class MeTerminalMachine : MetaMachine, IMeNetworkConnected
{
	protected MeTerminalMachine() { }

	protected override string Label => "ME Terminal";

	public override bool SupportsCovers => false;

	public MeNetwork? Network => MeNetworkSystem.NetOf(this);

	protected virtual HashSet<AEKey> ResolveCraftables(MeNetwork? net) =>
		net?.GetCraftables() ?? new HashSet<AEKey>();

	private sealed class ViewerSync
	{
		public readonly IncrementalUpdateHelper Helper = new();
		public KeyCounter PreviousAvailableStacks = new();
		public HashSet<AEKey> PreviousCraftables = new();
	}

	private readonly Dictionary<int, ViewerSync> _viewerSync = new();

	public AEKey? ResolveSerial(int whoAmI, long serial) =>
		_viewerSync.TryGetValue(whoAmI, out var sync) ? sync.Helper.GetBySerial(serial) : null;

	public void ResetViewerSync(int whoAmI) => _viewerSync.Remove(whoAmI);

	public void RequestResync()
	{
		if (IsServer) _viewerSync.Remove(Main.myPlayer);
		else TerrariaCompat.Net.MachineViewPacket.SendBegin(Position);
	}

	public void DriveSync()
	{
		if (!IsServer) return;
		var net = Network;

		if (Main.netMode == NetmodeID.Server)
		{
			PruneViewers();
			foreach (var w in _viewerSync.Keys.ToList())
				if (!HasViewer(w))
					_viewerSync.Remove(w);

			foreach (var w in Viewers)
				SyncViewer(w, GetOrCreate(w), net, loopback: false);
		}
		else
		{
			if (MeTerminalClient.RepoFor(Position) == null)
			{
				_viewerSync.Clear();
				return;
			}
			SyncViewer(Main.myPlayer, GetOrCreate(Main.myPlayer), net, loopback: true);
		}
	}

	private ViewerSync GetOrCreate(int whoAmI)
	{
		if (!_viewerSync.TryGetValue(whoAmI, out var sync))
			_viewerSync[whoAmI] = sync = new ViewerSync();
		return sync;
	}

	private void SyncViewer(int whoAmI, ViewerSync sync, MeNetwork? net, bool loopback)
	{
		var helper = sync.Helper;
		var available = net?.GetStorage().GetAvailableStacks() ?? new KeyCounter();
		var craftables = ResolveCraftables(net);

		sync.PreviousAvailableStacks.RemoveAll(available);
		sync.PreviousAvailableStacks.RemoveZeros();
		foreach (var key in sync.PreviousAvailableStacks.KeySet())
			helper.AddChange(key);

		foreach (var key in sync.PreviousCraftables)
			if (!craftables.Contains(key)) helper.AddChange(key);
		foreach (var key in craftables)
			if (!sync.PreviousCraftables.Contains(key)) helper.AddChange(key);

		if (helper.HasChanges())
		{
			bool full = helper.IsFullUpdate();
			var entries = BuildChangeEntries(helper, available, craftables);
			helper.CommitChanges();

			if (loopback)
				MeTerminalClient.RepoFor(Position)?.HandleUpdate(full, entries);
			else
				MeTerminalContentPacket.SendTo(whoAmI, Position, full, entries);
		}

		sync.PreviousAvailableStacks = available;
		sync.PreviousCraftables = craftables;
	}

	private static List<GridInventoryEntry> BuildChangeEntries(IncrementalUpdateHelper helper,
		KeyCounter available, HashSet<AEKey> craftables)
	{
		var entries = new List<GridInventoryEntry>();
		foreach (var key in helper.Changes.ToList())
		{
			AEKey? sendKey;
			long serial;
			var existing = helper.GetSerial(key);
			if (existing == null)
			{
				sendKey = key;
				serial = helper.GetOrAssignSerial(key);
			}
			else
			{
				sendKey = null;
				serial = existing.Value;
			}

			long stored = available.Get(key);
			long requestable = 0;
			bool craftable = craftables.Contains(key);
			if (stored <= 0 && requestable <= 0 && !craftable)
			{
				entries.Add(new GridInventoryEntry(serial, sendKey, 0, 0, false));
				helper.RemoveSerial(key);
			}
			else
			{
				entries.Add(new GridInventoryEntry(serial, sendKey, stored, requestable, craftable));
			}
		}
		return entries;
	}
}
