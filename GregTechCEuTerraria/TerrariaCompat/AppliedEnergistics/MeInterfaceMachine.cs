#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using GregTechCEuTerraria.AppliedEnergistics.Helpers.ExternalStorage;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;

public sealed class MeInterfaceMachine : MetaMachine, IMeInventoryExposer, IMeCraftingRequester
{
	public const int Slots = 9;

	private GenericStackInv _config = null!;
	private GenericStackInv _storage = null!;
	private readonly MultiCraftingTracker _craftingTracker;

	private readonly GenericStack?[] _plannedWork = new GenericStack?[Slots];
	private bool _hasConfig;

	public int Priority;
	public bool CraftMissing;

	private const int RateMin = 5, RateMax = 120;
	private int _rateMc = RateMax;
	private int _cooldown = RateMax;

	public MeInterfaceMachine()
	{
		_config = new GenericStackInv(OnConfigRowChanged, GenericStackInv.Mode.CONFIG_STACKS, Slots);
		_storage = new GenericStackInv(OnStorageChanged, GenericStackInv.Mode.STORAGE, Slots);
		_craftingTracker = new MultiCraftingTracker(this, Slots);
	}

	public MeNetwork? RequesterNetwork => HomeNetwork;

	public IReadOnlyCollection<CraftingLink> GetRequestedJobs() => _craftingTracker.GetRequestedJobs();

	public long InsertCraftedItems(CraftingLink link, AEKey what, long amount, Actionable mode)
	{
		int slot = _craftingTracker.GetSlot(link);
		if (slot < 0) return 0;
		return _storage.Insert(slot, what, amount, mode);
	}

	public void JobStateChange(CraftingLink link) => _craftingTracker.JobStateChange(link);

	private GenericStackInvItemHandler? _itemHandler;
	private GenericStackInvFluidHandler? _fluidHandler;
	public override IItemHandler? GetItemHandlerCap(IODirection side, bool useCoverCapability = true)
		=> _itemHandler ??= new GenericStackInvItemHandler(_storage);
	public override IFluidHandler? GetFluidHandlerCap(IODirection side, bool useCoverCapability = true)
		=> _fluidHandler ??= new GenericStackInvFluidHandler(_storage);

	protected override string Label => "ME Interface";
	public override bool SupportsCovers => false;

	public GenericStackInv Config => _config;
	public GenericStackInv Storage => _storage;

	public AEKey? ConfigKeyAt(int slot) => slot >= 0 && slot < Slots ? _config.GetKey(slot) : null;
	public long ConfigAmountAt(int slot) => slot >= 0 && slot < Slots ? _config.GetAmount(slot) : 0;
	public GenericStack? StorageStackAt(int slot) => slot >= 0 && slot < Slots ? _storage.GetStack(slot) : null;

	public void ApplySetConfig(int slot, AEKey? key, long amount)
	{
		if (slot < 0 || slot >= Slots) return;
		_config.SetStack(slot, key == null ? null : new GenericStack(key, System.Math.Max(1, amount)));
	}

	public void ApplySetPriority(int value) => Priority = value;

	public void ApplyToggleCraftMissing()
	{
		CraftMissing = !CraftMissing;

		if (!CraftMissing)
			_craftingTracker.Cancel();

		UpdatePlan();
	}

	public void ApplyPickup(int slot, Terraria.Item cursor, int whoAmI)
	{
		if (slot < 0 || slot >= Slots) return;

		if (!cursor.IsAir)
		{
			if (AEItemKey.Of(cursor) is not { } cursorKey) return;
			long inserted = _storage.Insert(slot, cursorKey, cursor.stack, Actionable.MODULATE);
			if (inserted <= 0) return;
			var rem = cursor.Clone();
			rem.stack -= (int)inserted;
			global::GregTechCEuTerraria.TerrariaCompat.Net.CursorUpdatePacket.SetCursor(
				whoAmI, rem.stack <= 0 ? new Terraria.Item() : rem);
			return;
		}

		if (_storage.GetKey(slot) is not AEItemKey itemKey) return;
		long have = _storage.GetAmount(slot);
		if (have <= 0) return;
		long take = System.Math.Min(have, itemKey.GetMaxStackSize());
		long extracted = _storage.Extract(slot, itemKey, take, Actionable.MODULATE);
		if (extracted <= 0) return;
		global::GregTechCEuTerraria.TerrariaCompat.Net.CursorUpdatePacket.SetCursor(
			whoAmI, itemKey.ToStack((int)extracted));
	}

	public void ApplySplitOrPlaceSingle(int slot, Terraria.Item cursor, int whoAmI)
	{
		if (slot < 0 || slot >= Slots) return;

		if (!cursor.IsAir)
		{
			if (AEItemKey.Of(cursor) is not { } cursorKey) return;
			long inserted = _storage.Insert(slot, cursorKey, 1, Actionable.MODULATE);
			if (inserted <= 0) return;
			var rem = cursor.Clone();
			rem.stack -= (int)inserted;
			global::GregTechCEuTerraria.TerrariaCompat.Net.CursorUpdatePacket.SetCursor(
				whoAmI, rem.stack <= 0 ? new Terraria.Item() : rem);
			return;
		}

		if (_storage.GetKey(slot) is not AEItemKey itemKey) return;
		long have = _storage.GetAmount(slot);
		if (have <= 0) return;
		long take = System.Math.Min((have + 1) / 2, itemKey.GetMaxStackSize());
		long extracted = _storage.Extract(slot, itemKey, take, Actionable.MODULATE);
		if (extracted <= 0) return;
		global::GregTechCEuTerraria.TerrariaCompat.Net.CursorUpdatePacket.SetCursor(
			whoAmI, itemKey.ToStack((int)extracted));
	}

	public MeNetwork? HomeNetwork
	{
		get
		{
			foreach (var (cx, cy) in Cells())
			{
				var own = MeNetworkSystem.NetAt(cx, cy);
				if (own != null) return own;
			}
			return null;
		}
	}

	private InterfaceRequestSource? _requestSource;
	private InterfaceRequestSource RequestSource => _requestSource ??= new InterfaceRequestSource(this);

	private InterfaceInventory? _localInv;
	private InterfaceInventory LocalInv => _localInv ??= new InterfaceInventory(this);

	public MEStorage? GetExposedInventory() => _hasConfig ? LocalInv : HomeNetwork?.GetStorage();

	private void ReadConfig()
	{
		_hasConfig = !_config.IsEmpty();
		UpdatePlan();
		MeNetworkSystem.MarkEndpointsDirty();
	}

	private static bool Matches(AEKey what, GenericStack? stack) =>
		stack != null && what.Equals(stack.What);

	private bool HasWorkToDo()
	{
		foreach (var w in _plannedWork)
			if (w != null) return true;
		return false;
	}

	private void Wake()
	{
		_rateMc = RateMin;
		_cooldown = 1;
	}

	private void Sleep()
	{
		_rateMc = RateMax;
		_cooldown = RateMax;
	}

	private void UpdatePlan()
	{
		bool hadWork = HasWorkToDo();
		for (int x = 0; x < _config.Size(); x++)
			UpdatePlan(x);
		bool hasWork = HasWorkToDo();
		if (hadWork != hasWork)
		{
			if (hasWork) Wake();
			else Sleep();
		}
	}

	private void UpdatePlan(int slot)
	{
		var req = _config.GetStack(slot);
		var stored = _storage.GetStack(slot);

		if (req == null && stored != null)
			_plannedWork[slot] = new GenericStack(stored.What, -stored.Amount);
		else if (req != null)
		{
			if (stored == null)
				_plannedWork[slot] = req;
			else if (req.What.Equals(stored.What))
				_plannedWork[slot] = req.Amount != stored.Amount
					? new GenericStack(req.What, req.Amount - stored.Amount)
					: null;
			else
				_plannedWork[slot] = new GenericStack(stored.What, -stored.Amount);
		}
		else
			_plannedWork[slot] = null;
	}

	private bool UpdateStorage()
	{
		bool didSomething = false;
		for (int x = 0; x < _plannedWork.Length; x++)
		{
			var work = _plannedWork[x];
			if (work != null)
				didSomething = UsePlan(x, work.What, work.Amount) || didSomething;
		}
		return didSomething;
	}

	private bool UsePlan(int slot, AEKey what, long amount)
	{
		bool changed = TryUsePlan(slot, what, amount);
		if (changed) UpdatePlan(slot);
		return changed;
	}

	private bool TryUsePlan(int slot, AEKey what, long amount)
	{
		var net = HomeNetwork;
		if (net is null) return false;
		var networkInv = net.GetStorage();
		var src = RequestSource;

		if (amount < 0)
		{
			amount = -amount;
			var inSlot = _storage.GetStack(slot);
			if (!Matches(what, inSlot) || inSlot!.Amount < amount)
				return true;

			long inserted = networkInv.Insert(what, amount, Actionable.MODULATE, src);
			if (inserted > 0)
				_storage.Extract(slot, what, inserted, Actionable.MODULATE);
			return inserted > 0;
		}

		if (_craftingTracker.IsBusy(slot))
			return HandleCrafting(slot, what, amount);

		if (amount > 0)
		{
			if (_storage.Insert(slot, what, amount, Actionable.SIMULATE) != amount)
				return true;

			if (AcquireFromNetwork(networkInv, slot, what, amount))
				return true;

			return HandleCrafting(slot, what, amount);
		}

		return false;
	}

	private bool AcquireFromNetwork(MEStorage networkInv, int slot, AEKey what, long amount)
	{
		var src = RequestSource;
		long acquired = networkInv.Extract(what, amount, Actionable.MODULATE, src);
		if (acquired > 0)
		{
			long inserted = _storage.Insert(slot, what, acquired, Actionable.MODULATE);
			if (inserted < acquired)
				throw new System.InvalidOperationException("bad attempt at managing inventory. Voided items: " + inserted);
			return true;
		}
		return false;
	}

	private bool HandleCrafting(int slot, AEKey what, long amount)
	{
		if (!CraftMissing || what == null) return false;
		return _craftingTracker.HandleCrafting(slot, what, amount, HomeNetwork, IActionSource.Empty());
	}

	private void OnConfigRowChanged() => ReadConfig();
	private void OnStorageChanged() => UpdatePlan();

	protected override void OnTick()
	{
		if (!IsServer) return;
		if (--_cooldown > 0) return;

		var net = HomeNetwork;
		if (net is null) { _rateMc = RateMax; _cooldown = _rateMc; return; }

		bool didWork = UpdateStorage();
		bool hasWork = HasWorkToDo();
		if (!hasWork) _rateMc = RateMax;
		else if (didWork) _rateMc = RateMin;
		else _rateMc = System.Math.Min(RateMax, _rateMc + 1);
		_cooldown = _rateMc;
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		var ct = new TagCompound();
		_craftingTracker.WriteToNBT(ct);
		tag["craftTracker"] = ct;
		_config.WriteToChildTag(tag, "config");
		_storage.WriteToChildTag(tag, "storage");
		tag["prio"] = Priority;
		tag["craftMissing"] = CraftMissing;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("craftTracker"))
			_craftingTracker.ReadFromNBT(tag.GetCompound("craftTracker"));
		_config.ReadFromChildTag(tag, "config");
		_storage.ReadFromChildTag(tag, "storage");
		Priority = tag.ContainsKey("prio") ? tag.GetInt("prio") : 0;
		CraftMissing = tag.ContainsKey("craftMissing") && tag.GetBool("craftMissing");
		ReadConfig();
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		var net = HomeNetwork;
		lines.Add(net == null ? "[c/FF8888:Not connected]" : "ME Network: connected");
		int cfg = 0, sto = 0;
		for (int i = 0; i < Slots; i++)
		{
			if (_config.GetStack(i) != null) cfg++;
			if (_storage.GetStack(i) != null) sto++;
		}
		lines.Add(_hasConfig ? $"Stocking: {cfg} type(s), {sto} filled" : "Passthrough (no config)");
		lines.Add($"Priority: {Priority}");
		if (CraftMissing) lines.Add("[c/88FF88:Craft missing: on]");
	}

	private sealed class InterfaceRequestContext
	{
		public readonly int Priority;
		public readonly MeNetwork? Grid;
		public InterfaceRequestContext(int priority, MeNetwork? grid) { Priority = priority; Grid = grid; }
	}

	private sealed class InterfaceRequestSource : IActionSource
	{
		private readonly MeInterfaceMachine _iface;
		public InterfaceRequestSource(MeInterfaceMachine iface) => _iface = iface;
		public Terraria.Player? GetPlayer() => null;
		public T? GetContext<T>() where T : class =>
			typeof(T) == typeof(InterfaceRequestContext)
				? (T?)(object)new InterfaceRequestContext(_iface.Priority, _iface.HomeNetwork)
				: null;
	}

	private sealed class InterfaceInventory : MEStorage
	{
		private readonly MeInterfaceMachine _owner;
		public InterfaceInventory(MeInterfaceMachine owner) => _owner = owner;

		public long Insert(AEKey what, long amount, Actionable mode, IActionSource source)
		{
			var ctx = source.GetContext<InterfaceRequestContext>();
			if (ctx != null && ReferenceEquals(ctx.Grid, _owner.HomeNetwork))
				return 0;
			return _owner._storage.Insert(what, amount, mode, source);
		}

		public long Extract(AEKey what, long amount, Actionable mode, IActionSource source)
		{
			var ctx = source.GetContext<InterfaceRequestContext>();
			if (ctx != null && ctx.Priority <= _owner.Priority && ReferenceEquals(ctx.Grid, _owner.HomeNetwork))
				return 0;
			return _owner._storage.Extract(what, amount, mode, source);
		}

		public void GetAvailableStacks(KeyCounter @out) => _owner._storage.GetAvailableStacks(@out);
		public string GetDescription() => "ME Interface";
	}
}
