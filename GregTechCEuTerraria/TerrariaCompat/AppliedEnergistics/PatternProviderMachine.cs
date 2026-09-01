#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using GregTechCEuTerraria.AppliedEnergistics.Me.Storage;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Cover;
using GregTechCEuTerraria.TerrariaCompat.Items.Patterns;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;

public sealed class PatternProviderMachine : MetaMachine, IMePatternProvider, IMeCraftingProvider, IWirePulseReceiver
{
	public const int PatternSlots = 36;

	private readonly Item[] _patterns = NewSlots();
	private static Item[] NewSlots()
	{
		var a = new Item[PatternSlots];
		for (int i = 0; i < a.Length; i++) a[i] = new Item();
		return a;
	}

	private sealed class Job
	{
		public AEKey Output;
		public long Amount;
		public int TicksLeft;
		public Point16 Station = new(-1, -1); // X < 0 = none (processing jobs)
		public Job(AEKey output, long amount, int ticks) { Output = output; Amount = amount; TicksLeft = ticks; }
	}
	private readonly List<Job> _jobs = new();

	private readonly List<(AEKey what, long amount)> _sendList = new();
	private (int x, int y, IODirection arrival)? _sendTarget;
	private int _roundRobinIndex;

	public PatternProviderMachine() { }

	protected override string Label => "ME Pattern Provider";
	public override bool SupportsCovers => false;

	public override Item[]? GetSlotGroup(SlotGroup group) =>
		group == SlotGroup.InventoryInput ? _patterns : null;

	public override bool IsItemValidForSlot(SlotGroup group, int index, Item item)
	{
		if (group != SlotGroup.InventoryInput) return true;
		return item.IsAir || (item.ModItem is EncodedPatternItem e && e.Pattern != null);
	}

	public override void NotifySlotGroupChanged(SlotGroup group)
	{
		base.NotifySlotGroupChanged(group);
		if (group == SlotGroup.InventoryInput)
			MeNetworkSystem.RequestCraftingUpdate(this);
	}

	private MeNetworkPushHandler? _pushHandler;
	private MeNetworkPushHandler PushHandler => _pushHandler ??= new MeNetworkPushHandler(this);
	public override IItemHandler? GetItemHandlerCap(IODirection side, bool useCoverCapability = true) => PushHandler;
	public override IFluidHandler? GetFluidHandlerCap(IODirection side, bool useCoverCapability = true) => PushHandler;

	public IReadOnlyList<MePattern> Patterns
	{
		get
		{
			var list = new List<MePattern>(PatternSlots);
			foreach (var it in _patterns)
				if (!it.IsAir && it.ModItem is EncodedPatternItem e && e.Pattern != null)
					list.Add(e.Pattern);
			return list;
		}
	}

	public string CustomName = "";

	public bool Blocking = true;

	public IODirection PushDirection = IODirection.None;

	public void ApplySetPushDirection(IODirection dir)
	{
		PushDirection = dir;
		MeNetworkSystem.MarkEndpointsDirty();
	}

	private IEnumerable<(IODirection side, int x, int y)> ActiveSides()
	{
		var net = MeNetworkSystem.NetOf(this);
		foreach (var (side, x, y) in WorldCapability.Perimeter(this))
		{
			if (PushDirection != IODirection.None && side != PushDirection) continue;
			if (net != null
				&& MachineCellResolver.TryFindMachineAt(x, y, out var m)
				&& !ReferenceEquals(m, this)
				&& (m is IMePatternProvider || m is IMeInventoryExposer)
				&& ReferenceEquals(MeNetworkSystem.NetOf(m), net))
				continue;
			yield return (side, x, y);
		}
	}

	private bool OnPushSide(int x, int y)
	{
		var (w, h) = Size;
		int px = Position.X, py = Position.Y;
		bool up    = y == py - 1 && x >= px && x < px + w;
		bool down  = y == py + h && x >= px && x < px + w;
		bool left  = x == px - 1 && y >= py && y < py + h;
		bool right = x == px + w && y >= py && y < py + h;
		return PushDirection switch
		{
			IODirection.Up    => up,
			IODirection.Down  => down,
			IODirection.Left  => left,
			IODirection.Right => right,
			_                 => up || down || left || right,
		};
	}

	public LockCraftingMode LockMode = LockCraftingMode.None;
	private UnlockCraftingEvent _unlockEvent = UnlockCraftingEvent.None;
	private (AEKey what, long amount)? _unlockStack;
	private bool _redstoneSignal;

	private enum UnlockCraftingEvent { None, RedstonePower, Result, RedstonePulse }

	public string ProviderName =>
		string.IsNullOrWhiteSpace(CustomName) ? DefaultName() : CustomName;
	public Point16 ProviderPos => Position;

	public bool ShowInAccessTerminal = true;
	public bool IsVisibleInTerminal => ShowInAccessTerminal;

	public int TerminalIconItemType
	{
		get
		{
			if (string.IsNullOrWhiteSpace(CustomName))
				foreach (var (_, x, y) in WorldCapability.Perimeter(this))
				{
					if (x <= 0 || y <= 0 || x >= Main.maxTilesX - 1 || y >= Main.maxTilesY - 1) continue;
					if (MachineCellResolver.TryFindMachineAt(x, y, out var m) && m is IMeNetworkConnected) continue;
					var t = Main.tile[x, y];
					if (!t.HasTile) continue;
					int item = StationIcon.ItemTypeForTile(t.TileType);
					if (item > 0) return item;
				}
			return StationIcon.ItemTypeForTile(Main.tile[Position.X, Position.Y].TileType);
		}
	}

	public string DefaultName()
	{
		var names = new List<string>();
		void Add(string n) { if (!string.IsNullOrEmpty(n) && !names.Contains(n)) names.Add(n); }

		foreach (var (_, x, y) in ActiveSides())
		{
			if (MachineCellResolver.TryFindMachineAt(x, y, out var m)
				&& !ReferenceEquals(m, this)
				&& (m is not IMeNetworkConnected || m is IMeInventoryExposer))
				Add(m.DisplayName);
		}

		var (w, h) = Size;
		int px = Position.X, py = Position.Y;
		for (int x = px - 1; x <= px + w; x++)
			for (int y = py - 1; y <= py + h; y++)
			{
				if (x <= 0 || y <= 0 || x >= Main.maxTilesX - 1 || y >= Main.maxTilesY - 1) continue;
				if (!OnPushSide(x, y)) continue;
				var t = Main.tile[x, y];
				if (t.HasTile && IsCraftingStation(t.TileType))
					Add(StationTileName(t.TileType));
			}

		return names.Count > 0 ? string.Join(", ", names) : DisplayName;
	}

	private static HashSet<int>? _stationTiles;
	private static bool IsCraftingStation(int tileType)
	{
		if (_stationTiles == null)
		{
			_stationTiles = new HashSet<int>();
			foreach (var r in Main.recipe)
			{
				if (r == null) continue;
				foreach (var tile in r.requiredTile)
					if (tile > -1) _stationTiles.Add(tile);
			}
		}
		return _stationTiles.Contains(tileType);
	}

	private static Dictionary<int, string>? _stationTileNames;
	private static string StationTileName(int tileType)
	{
		if (_stationTileNames == null)
		{
			_stationTileNames = new Dictionary<int, string>();
			foreach (var kv in Terraria.ID.ContentSamples.ItemsByType)
			{
				var it = kv.Value;
				if (it != null && !it.IsAir && it.createTile > -1 && !_stationTileNames.ContainsKey(it.createTile))
					_stationTileNames[it.createTile] = it.Name;
			}
		}
		if (_stationTileNames.TryGetValue(tileType, out var n)) return n;

		string mapName = TryMapName(tileType);
		if (!string.IsNullOrEmpty(mapName)) return mapName;
		if (tileType == Terraria.ID.TileID.DemonAltar)
			return Terraria.Localization.Language.GetTextValue("MapObject.DemonAltar");
		return "crafting station";
	}

	private static string TryMapName(int tileType) => WorldCapability.MapObjectName(tileType);

	public void ApplySetName(string name) => CustomName = name ?? "";

	public bool IsBusy => _jobs.Count > 0 || _sendList.Count > 0;

	public bool PushPattern(MePattern details, KeyCounter[] inputHolder)
	{
		if (!IsServer) return false;
		if (GetCraftingLockedReason() != LockCraftingMode.None) return false;
		bool ok = details.Type == MePatternType.Crafting
			? PushCrafting(details, inputHolder)
			: PushProcessing(details, inputHolder);
		if (ok) OnPushPatternSuccess(details);
		return ok;
	}

	public LockCraftingMode GetCraftingLockedReason()
	{
		if (_unlockEvent != UnlockCraftingEvent.None)
			return _unlockEvent == UnlockCraftingEvent.Result
				? LockCraftingMode.LockUntilResult
				: LockCraftingMode.LockUntilPulse;
		return LockCraftingMode.None;
	}

	private void ResetCraftingLock()
	{
		_unlockEvent = UnlockCraftingEvent.None;
		_unlockStack = null;
	}

	private void OnPushPatternSuccess(MePattern pattern)
	{
		ResetCraftingLock();
		switch (LockMode)
		{
			case LockCraftingMode.LockUntilPulse:
				_unlockEvent = _redstoneSignal ? UnlockCraftingEvent.RedstonePulse : UnlockCraftingEvent.RedstonePower;
				break;
			case LockCraftingMode.LockUntilResult:
				_unlockEvent = UnlockCraftingEvent.Result;
				_unlockStack = (pattern.PrimaryOutput, pattern.PrimaryOutputAmount);
				break;
		}
	}

	public void ApplySetLockMode(LockCraftingMode mode)
	{
		LockMode = mode;
		ResetCraftingLock();
	}

	public void OnWirePulse()
	{
		if (!IsServer) return;
		_redstoneSignal = !_redstoneSignal;
		if (_unlockEvent == UnlockCraftingEvent.RedstonePower && _redstoneSignal)
			_unlockEvent = UnlockCraftingEvent.None;
		else if (_unlockEvent == UnlockCraftingEvent.RedstonePulse && !_redstoneSignal)
			_unlockEvent = UnlockCraftingEvent.RedstonePower;
	}

	public void OnStackReturnedToNetwork(AEKey what, long amount)
	{
		if (_unlockEvent != UnlockCraftingEvent.Result) return;
		if (_unlockStack is not { } us) { _unlockEvent = UnlockCraftingEvent.None; return; }
		if (!us.what.Equals(what)) return;
		long remaining = us.amount - amount;
		if (remaining <= 0) { _unlockEvent = UnlockCraftingEvent.None; _unlockStack = null; }
		else _unlockStack = (us.what, remaining);
	}

	public bool CanFulfill(MePattern details)
	{
		if (details.Type == MePatternType.Crafting)
			return TryFindStations(details, out _);
		return HasAdjacentInventory();
	}

	private bool HasAdjacentInventory()
	{
		foreach (var (side, x, y) in ActiveSides())
		{
			var arrival = side.Opposite();
			if (WorldCapability.HasInventoryAt(x, y, arrival))
				return true;
		}
		return false;
	}

	public static string StationDisplayName(int tileType) => StationTileName(tileType);

	private bool PushCrafting(MePattern details, KeyCounter[] inputHolder)
	{
		if (IsBusy) return false;

		// TODO water / lava / honey, snow, graveyard biome, torch god

		if (!TryFindStations(details, out Point16 fxPos)) return false;
		if (fxPos.X < 0)
		{
			var (w, h) = Size;
			fxPos = new Point16(Position.X + w / 2, Position.Y + h / 2);
		}

		foreach (var list in inputHolder) list.Clear();
		_jobs.Add(new Job(details.PrimaryOutput, details.PrimaryOutputAmount,
			CraftingPatternTimes.TicksFor(details.ResolveStationTiles())) { Station = fxPos });
		return true;
	}

	private bool PushProcessing(MePattern details, KeyCounter[] inputHolder)
	{
		if (_sendList.Count > 0) return false;

		var targets = new List<(int x, int y, IODirection arrival)>();
		foreach (var (side, x, y) in ActiveSides())
		{
			var arrival = side.Opposite();
			if (WorldCapability.HasInventoryAt(x, y, arrival))
				targets.Add((x, y, arrival));
		}
		RearrangeRoundRobin(targets);

		for (int i = 0; i < targets.Count; i++)
		{
			var t = targets[i];
			if (Blocking && CellHoldsAnyInput(t)) continue;
			if (!CellAcceptsAll(t, inputHolder)) continue;

			foreach (var list in inputHolder)
				foreach (var entry in list)
				{
					long inserted = InsertIntoCell(t, entry.Key, entry.Value, Actionable.MODULATE);
					if (inserted < entry.Value) AddToSendList(entry.Key, entry.Value - inserted);
				}
			foreach (var list in inputHolder) list.Clear();
			_sendTarget = t;
			SendStacksOut();
			_roundRobinIndex += i + 1;
			return true;
		}
		return false;
	}

	protected override void OnTick()
	{
		if (!IsServer) return;
		if (_sendList.Count > 0) SendStacksOut();
		if (_jobs.Count == 0) return;
		var net = MeNetworkSystem.NetOf(this);
		var storage = net?.GetStorage();
		var src = IActionSource.Empty();
		for (int i = _jobs.Count - 1; i >= 0; i--)
		{
			var j = _jobs[i];
			if (j.TicksLeft > 0) { j.TicksLeft--; continue; }
			if (storage is null) continue;
			long ins = storage.Insert(j.Output, j.Amount, Actionable.MODULATE, src);
			if (ins >= j.Amount)
			{
				if (j.Station.X >= 0)
					Net.MeStationCraftEffectPacket.Emit(j.Station.X, j.Station.Y);
				_jobs.RemoveAt(i);
			}
			else j.Amount -= ins;
		}
	}

	private MEStorage? FindTarget((int x, int y, IODirection arrival) t)
	{
		if (MachineCellResolver.TryFindMachineAt(t.x, t.y, out var m) && m is IMeInventoryExposer exposer)
		{
			var meStorage = exposer.GetExposedInventory();
			if (meStorage != null) return meStorage;
		}

		var externalStorages = new Dictionary<AEKeyType, MEStorage>();
		if (WorldCapability.ItemHandlerAt(t.x, t.y, t.arrival) != null)
			externalStorages[AEKeyType.Items()] = new ItemHandlerMeStorage(
				() => WorldCapability.ItemHandlerAt(t.x, t.y, t.arrival), extractableOnly: false);
		if (WorldCapability.FluidHandlerAt(t.x, t.y, t.arrival) != null)
			externalStorages[AEKeyType.Fluids()] = new FluidHandlerMeStorage(
				() => WorldCapability.FluidHandlerAt(t.x, t.y, t.arrival), extractableOnly: false);

		return externalStorages.Count > 0 ? new CompositeStorage(externalStorages) : null;
	}

	private long InsertIntoCell((int x, int y, IODirection arrival) t, AEKey what, long amount, Actionable mode)
		=> FindTarget(t)?.Insert(what, amount, mode, IActionSource.Empty()) ?? 0;

	private bool CellAcceptsAll((int x, int y, IODirection arrival) t, KeyCounter[] inputHolder)
	{
		foreach (var list in inputHolder)
			foreach (var input in list)
				if (InsertIntoCell(t, input.Key, input.Value, Actionable.SIMULATE) == 0)
					return false;
		return true;
	}

	private bool CellHoldsAnyInput((int x, int y, IODirection arrival) t)
	{
		var inputs = AllPatternInputs();
		if (inputs.Count == 0) return false;
		var target = FindTarget(t);
		if (target is null) return false;
		var combined = new KeyCounter();
		target.GetAvailableStacks(combined);
		foreach (var entry in combined)
			if (inputs.Contains(entry.Key.DropSecondary())) return true;
		return false;
	}

	private HashSet<AEKey> AllPatternInputs()
	{
		var set = new HashSet<AEKey>();
		foreach (var p in Patterns)
			foreach (var (what, _) in p.Inputs)
				if (what != null) set.Add(what.DropSecondary());
		return set;
	}

	private void AddToSendList(AEKey what, long amount)
	{
		if (amount > 0) _sendList.Add((what, amount));
	}

	private void SendStacksOut()
	{
		if (_sendTarget is not { } t) return;
		for (int i = _sendList.Count - 1; i >= 0; i--)
		{
			var (what, amount) = _sendList[i];
			long inserted = InsertIntoCell(t, what, amount, Actionable.MODULATE);
			if (inserted >= amount) _sendList.RemoveAt(i);
			else if (inserted > 0) _sendList[i] = (what, amount - inserted);
		}
		if (_sendList.Count == 0) _sendTarget = null;
	}

	private void RearrangeRoundRobin(List<(int x, int y, IODirection arrival)> list)
	{
		if (list.Count == 0) return;
		_roundRobinIndex %= list.Count;
		for (int i = 0; i < _roundRobinIndex; i++) list.Add(list[i]);
		list.RemoveRange(0, _roundRobinIndex);
	}

	private bool TryFindStations(MePattern details, out Point16 pos)
	{
		pos = new Point16(-1, -1);
		foreach (int tileId in details.ResolveStationTiles())
		{
			if (!TryFindStation(tileId, out var found)) return false;
			if (pos.X < 0) pos = found;
		}
		return true;
	}

	private bool TryFindStation(int tileId, out Point16 pos)
	{
		var (w, h) = Size;
		int px = Position.X, py = Position.Y;
		for (int x = px - 1; x <= px + w; x++)
			for (int y = py - 1; y <= py + h; y++)
			{
				if (x <= 0 || y <= 0 || x >= Main.maxTilesX - 1 || y >= Main.maxTilesY - 1) continue;
				if (!OnPushSide(x, y)) continue;
				var t = Main.tile[x, y];
				if (t.HasTile && RecipeNetworkCrafting.TileSatisfies(t.TileType, tileId)) { pos = new Point16(x, y); return true; }
			}
		pos = new Point16(-1, -1);
		return false;
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		if (!string.IsNullOrEmpty(CustomName)) tag["name"] = CustomName;
		if (Blocking) tag["blk"] = true;
		if (PushDirection != IODirection.None) tag["pushdir"] = (int)PushDirection;
		if (!ShowInAccessTerminal) tag["hideterm"] = true;
		if (LockMode != LockCraftingMode.None) tag["lock"] = (int)LockMode;
		if (_unlockEvent != UnlockCraftingEvent.None) tag["ulev"] = (int)_unlockEvent;
		if (_unlockStack is { } us) { tag["ulk"] = us.what.ToTagGeneric(); tag["uln"] = us.amount; }
		if (_redstoneSignal) tag["rs"] = true;
		for (int i = 0; i < PatternSlots; i++)
			if (!_patterns[i].IsAir) tag[$"pat{i}"] = ItemIO.Save(_patterns[i]);

		if (_jobs.Count > 0)
		{
			var jobs = new List<TagCompound>(_jobs.Count);
			foreach (var j in _jobs)
				jobs.Add(new TagCompound
				{
					["k"] = j.Output.ToTagGeneric(), ["n"] = j.Amount, ["t"] = j.TicksLeft,
					["sx"] = (int)j.Station.X, ["sy"] = (int)j.Station.Y,
				});
			tag["jobs"] = jobs;
		}

		if (_sendList.Count > 0)
		{
			var send = new List<TagCompound>(_sendList.Count);
			foreach (var (what, amount) in _sendList)
				send.Add(new TagCompound { ["k"] = what.ToTagGeneric(), ["n"] = amount });
			tag["send"] = send;
			if (_sendTarget is { } st)
			{
				tag["sdx"] = st.x; tag["sdy"] = st.y; tag["sda"] = (int)st.arrival;
			}
		}
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		CustomName = tag.ContainsKey("name") ? tag.GetString("name") : "";
		Blocking = tag.GetBool("blk");
		PushDirection = tag.ContainsKey("pushdir") ? (IODirection)tag.GetInt("pushdir") : IODirection.None;
		ShowInAccessTerminal = !tag.GetBool("hideterm");
		LockMode = tag.ContainsKey("lock") ? (LockCraftingMode)tag.GetInt("lock") : LockCraftingMode.None;
		_unlockEvent = tag.ContainsKey("ulev") ? (UnlockCraftingEvent)tag.GetInt("ulev") : UnlockCraftingEvent.None;
		_unlockStack = null;
		if (tag.ContainsKey("ulk"))
		{
			var k = AEKey.FromTagGeneric(tag.GetCompound("ulk"));
			if (k != null) _unlockStack = (k, tag.GetLong("uln"));
		}
		_redstoneSignal = tag.GetBool("rs");
		for (int i = 0; i < PatternSlots; i++)
			_patterns[i] = tag.ContainsKey($"pat{i}") ? ItemIO.Load(tag.GetCompound($"pat{i}")) : new Item();

		_jobs.Clear();
		if (tag.ContainsKey("jobs"))
			foreach (var j in tag.GetList<TagCompound>("jobs"))
			{
				var k = AEKey.FromTagGeneric(j.GetCompound("k"));
				if (k != null)
				{
					var job = new Job(k, j.GetLong("n"), j.GetInt("t"));
					if (j.ContainsKey("sx")) job.Station = new Point16(j.GetInt("sx"), j.GetInt("sy"));
					_jobs.Add(job);
				}
			}

		_sendList.Clear();
		_sendTarget = null;
		if (tag.ContainsKey("send"))
		{
			foreach (var s in tag.GetList<TagCompound>("send"))
			{
				var k = AEKey.FromTagGeneric(s.GetCompound("k"));
				if (k != null) _sendList.Add((k, s.GetLong("n")));
			}
			if (tag.ContainsKey("sdx"))
				_sendTarget = (tag.GetInt("sdx"), tag.GetInt("sdy"), (IODirection)tag.GetInt("sda"));
		}
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		var net = MeNetworkSystem.NetOf(this);
		lines.Add(net == null ? "[c/FF8888:Not connected]" : "ME Network: connected");
		int count = 0;
		foreach (var it in _patterns) if (!it.IsAir) count++;
		lines.Add($"Patterns: {count} / {PatternSlots}");
		if (_jobs.Count > 0) lines.Add($"Crafting: {_jobs.Count} job(s)");
	}
}
