#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

[Flags]
public enum FisherFilter
{
	None  = 0,
	Junk  = 1,
	Fish  = 2,
	Crate = 4,
	All   = Junk | Fish | Crate,
}


public sealed class FisherMachine : TieredEnergyMachine, IWorkable, IControllable, IItemHandler
{
	public FisherMachine() { }
	public FisherMachine(VoltageTier tier) : base(tier) { }

	protected override string Label => Definition?.Label ?? "Fisher";

	public override bool CanAccept => true;
	public override long EnergyCapacity => VoltageTiers.Voltage(Tier) * 64L;

	private long EnergyPerTick
	{
		get
		{
			int idx = Math.Max(0, (int)Tier - 1);
			return VoltageTiers.Voltage((VoltageTier)idx);
		}
	}

	private int InventorySize { get { int t = (int)Tier; return (t + 1) * (t + 1); } }

	private static int CalcMaxProgress(int tier) =>
		(int)(800.0 - 170 * ((double)tier - 1.0) + (Math.Max(0, tier - 4) / 0.012));

	public int MaxProgress => CalcMaxProgress((int)Tier);

	private NotifiableItemStackHandler? _cache;
	private NotifiableItemStackHandler? _baitHandler;
	private AutoOutputTrait? _autoOutput;

	public NotifiableItemStackHandler Cache       { get { EnsureTraits(); return _cache!; } }
	public NotifiableItemStackHandler BaitHandler { get { EnsureTraits(); return _baitHandler!; } }

	public override AutoOutputTrait? AutoOutput { get { EnsureTraits(); return _autoOutput; } }

	protected override bool HasChargerSlot => true;

	private static int _stringItemType = -1;
	private static int StringItemType
	{
		get
		{
			if (_stringItemType < 0)
				_stringItemType = VanillaItemMap.TryGet("minecraft:string", out int t) ? t : 0;
			return _stringItemType;
		}
	}

	private static bool IsBait(Item item)
	{
		if (item is null || item.IsAir) return false;
		return item.type == StringItemType || item.bait > 0;
	}

	private void EnsureTraits()
	{
		if (_cache is not null) return;
		BindDefinition();

		_cache = new NotifiableItemStackHandler(InventorySize, IO.BOTH, IO.OUT);
		Traits.Attach(_cache);
		Traits.RegisterPersistent("Cache", _cache);

		_baitHandler = new NotifiableItemStackHandler(1, IO.BOTH, IO.IN).SetFilter(IsBait);
		Traits.Attach(_baitHandler);
		Traits.RegisterPersistent("Bait", _baitHandler);

		_autoOutput = AutoOutputTrait.OfItems(_cache);
		Traits.Attach(_autoOutput);
		Traits.RegisterPersistent("AutoOutput", _autoOutput);

		var explosion = Traits.GetTrait<EnvironmentalExplosionTrait>(EnvironmentalExplosionTrait.TYPE);
		explosion?.SetEnableEnvironmentalExplosions(false);
	}

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		EnsureTraits();
	}

	public override Item[]? GetSlotGroup(SlotGroup group) => group switch
	{
		SlotGroup.InventoryOutput => Cache.Storage.Stacks,
		SlotGroup.InventoryInput  => BaitHandler.Storage.Stacks,
		_                         => base.GetSlotGroup(group),
	};

	public override bool SupportsAutoOutputItems => true;

	private int BaitCount => BaitHandler.SlotCount;
	public int SlotCount  => BaitHandler.SlotCount + Cache.SlotCount;
	public Item GetSlot(int slot) =>
		slot < BaitCount ? BaitHandler.GetSlot(slot) : Cache.GetSlot(slot - BaitCount);
	public Item Insert(int slot, Item item, bool simulate) =>
		slot < BaitCount ? BaitHandler.Insert(slot, item, simulate) : Cache.Insert(slot - BaitCount, item, simulate);
	public Item Extract(int slot, int maxAmount, bool simulate) =>
		slot < BaitCount ? BaitHandler.Extract(slot, maxAmount, simulate) : Cache.Extract(slot - BaitCount, maxAmount, simulate);
	public int GetSlotLimit(int slot) =>
		slot < BaitCount ? BaitHandler.GetSlotLimit(slot) : Cache.GetSlotLimit(slot - BaitCount);
	public bool IsItemValid(int slot, Item item) =>
		slot < BaitCount ? BaitHandler.IsItemValid(slot, item) : Cache.IsItemValid(slot - BaitCount, item);

	private bool _isWorkingEnabled = true;
	public bool IsWorkingEnabled() => _isWorkingEnabled;
	public void SetWorkingEnabled(bool enabled) => _isWorkingEnabled = enabled;

	private FisherFilter _filters = FisherFilter.None;

	public FisherFilter Filters
	{
		get => _filters;
		set => _filters = value & FisherFilter.All;
	}

	public bool IsFiltered(FisherFilter filter) => (_filters & filter) != 0;

	public void SetFiltered(FisherFilter filter, bool filtered) =>
		_filters = filtered ? _filters | filter : _filters & ~filter;

	private bool AllFiltered => (_filters & FisherFilter.All) == FisherFilter.All;

	public int BaitPerCatch
	{
		get
		{
			int cost = 1;
			if (IsFiltered(FisherFilter.Junk))  cost++;
			if (IsFiltered(FisherFilter.Fish))  cost++;
			if (IsFiltered(FisherFilter.Crate)) cost++;
			return cost;
		}
	}

	private int _progress;
	private bool _active;

	int IWorkable.GetProgress()    => _progress;
	int IWorkable.GetMaxProgress() => MaxProgress;
	bool IWorkable.IsActive()      => _active;
	public override bool IsActive  => _active;

	public const int WaterCheckSize = 5;
	private bool _hasWater;

	protected override void OnTick()
	{
		EnsureTraits();

		if (!_hasWater || GetMcOffsetTimer() % MaxProgress == 0L)
			UpdateHasWater();

		bool canFish = DrainEnergy(simulate: true)
		            && !BaitHandler.Storage.GetStackInSlot(0).IsAir
		            && _isWorkingEnabled
		            && !AllFiltered;
		if (!canFish)
		{
			if (_active || _progress != 0)
			{
				_active = false;
				_progress = 0;
			}
			return;
		}

		if (!_hasWater)
		{
			_active = false;
			return;
		}

		_active = true;

		DrainEnergy(simulate: false);

		if (_progress >= MaxProgress)
		{
			DoFishingRoll();
			_progress = -1;
		}
		_progress++;
	}

	private void UpdateHasWater()
	{
		int left  = Position.X;
		int right = Position.X + Size.Width - 1;
		int baseY = Position.Y + Size.Height;

		for (int dy = 0; dy < WaterCheckSize; dy++)
		{
			int tileY = baseY + dy;
			if (tileY < 0 || tileY >= Main.maxTilesY) break;
			for (int x = left; x <= right; x++)
			{
				if (x < 0 || x >= Main.maxTilesX) continue;
				var t = Main.tile[x, tileY];
				if (t.LiquidAmount > 0 && t.LiquidType == LiquidID.Water)
				{
					_hasWater = true;
					return;
				}
			}
		}
		_hasWater = false;
	}

	private void DoFishingRoll()
	{
		int waterCenterX = Position.X + 1;
		int waterY       = Position.Y + Size.Height;

		var rolled = FishingLootRoller.Roll(Tier, waterCenterX, waterY, _filters, out bool discarded);
		bool useBait = discarded;
		if (!discarded && !rolled.IsAir)
			useBait = TryFillCache(rolled);

		if (useBait)
		{
			int consume = BaitPerCatch;
			var slot = BaitHandler.Storage.GetStackInSlot(0);
			if (!slot.IsAir)
			{
				int take = Math.Min(slot.stack, consume);
				slot.stack -= take;
				if (slot.stack <= 0) BaitHandler.Storage.SetStackInSlot(0, new Item());
				BaitHandler.OnContentsChanged();
			}
		}
	}

	private bool TryFillCache(Item stack)
	{
		var storage = Cache.Storage;
		for (int i = 0; i < storage.SlotCount; i++)
		{
			var leftover = storage.Insert(i, stack, simulate: false);
			if (leftover.stack < stack.stack) return true;
		}
		return false;
	}

	private bool DrainEnergy(bool simulate)
	{
		long resultEnergy = EnergyContainer.EnergyStored - EnergyPerTick;
		if (resultEnergy >= 0L && resultEnergy <= EnergyContainer.EnergyCapacity)
		{
			if (!simulate) EnergyContainer.SetEnergyStored(resultEnergy);
			return true;
		}
		return false;
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		EnsureTraits();
		base.SaveMachineData(tag);
		tag["progress"]          = _progress;
		tag["active"]            = _active;
		tag["hasWater"]          = _hasWater;
		tag["isWorkingEnabled"]  = _isWorkingEnabled;
		tag["lootFilters"]       = (byte)_filters;
	}

	public override void LoadData(TagCompound tag)
	{
		EnsureTraits();
		base.LoadData(tag);
		_progress         = tag.GetInt("progress");
		_active           = tag.GetBool("active");
		_hasWater         = tag.GetBool("hasWater");
		_isWorkingEnabled = !tag.ContainsKey("isWorkingEnabled") || tag.GetBool("isWorkingEnabled");

		if (tag.ContainsKey("lootFilters"))
			_filters = (FisherFilter)tag.GetByte("lootFilters") & FisherFilter.All;
		else if (tag.ContainsKey("junkEnabled") && !tag.GetBool("junkEnabled"))
			_filters = FisherFilter.Junk;
		else
			_filters = FisherFilter.None;
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add($"Speed: {MaxProgress} ticks / catch");
		lines.Add("Water needed: below the machine");
		lines.Add($"Draw: {EnergyPerTick:N0} EU/t");
		lines.Add($"Fishing Power: {FishingLootRoller.FishingPower(Tier)}");
		lines.Add($"Luck: +{FishingLootRoller.SyntheticLuck(Tier):0.00}");
		lines.Add($"Bait: {BaitPerCatch} per catch");
		if (_filters != FisherFilter.None)
		{
			var filtered = new List<string>();
			if (IsFiltered(FisherFilter.Junk))  filtered.Add("junk");
			if (IsFiltered(FisherFilter.Fish))  filtered.Add("fish");
			if (IsFiltered(FisherFilter.Crate)) filtered.Add("treasures");
			lines.Add($"Filtered out: {string.Join(", ", filtered)}");
		}
		if (_active)
			lines.Add($"Progress: {_progress} / {MaxProgress}");
		else if (!_isWorkingEnabled)
			lines.Add("Disabled");
		else if (AllFiltered)
			lines.Add("Idle: every category filtered out");
		else if (!_hasWater)
			lines.Add("Idle: no water below");
		else if (BaitHandler.Storage.GetStackInSlot(0).IsAir)
			lines.Add("Idle: no bait (silk / worm / etc.)");
		else if (!DrainEnergy(simulate: true))
			lines.Add("Idle: not enough power");
	}
}
