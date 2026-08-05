#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public class SuperChestTileEntity : MetaMachine, IItemHandler, IControllable
{
	public SuperChestTileEntity() { }
	public SuperChestTileEntity(VoltageTier tier) : base(tier) { }

	protected override string  Label       => Definition?.Label ?? "Super Chest";

	internal static long MaxAmountForTier(VoltageTier tier)
	{
		int t = (int)tier;
		if (t >= (int)VoltageTier.MAX) return long.MaxValue;
		return t == 0 ? 2_000_000L : 4_000_000L << (t - 1);
	}

	private long _maxAmount = -1;
	public long MaxAmount
	{
		get
		{
			if (_maxAmount < 0) _maxAmount = MaxAmountForTier(Tier);
			return _maxAmount;
		}
	}

	protected Item _stored = new();
	protected long _storedAmount;
	private Item _lockedItem = new();

	public bool IsVoiding { get; set; }
	public bool IsLocked => !_lockedItem.IsAir;
	public Item StoredItem => _stored;
	public long StoredAmount => _storedAmount;

	private static bool SameItem(Item a, Item b) =>
		!a.IsAir && !b.IsAir && a.type == b.type;

	private bool Accepts(Item stack) => !IsLocked || SameItem(stack, _lockedItem);

	public int SlotCount => 1;

	public virtual Item GetSlot(int slot)
	{
		if (_stored.IsAir || _storedAmount <= 0) return new Item();
		var view = _stored.Clone();
		view.stack = (int)Math.Min(_storedAmount, _stored.maxStack);
		return view;
	}

	public virtual Item Insert(int slot, Item item, bool simulate)
	{
		if (item is null || item.IsAir) return new Item();
		long free = IsVoiding ? long.MaxValue : MaxAmount - _storedAmount;
		long canStore = 0;
		if ((_stored.IsAir || SameItem(_stored, item)) && Accepts(item))
			canStore = Math.Min(item.stack, free);

		if (!simulate && canStore > 0)
		{
			if (_stored.IsAir)
			{
				_stored = item.Clone();
				_stored.stack = 1;
			}
			_storedAmount = Math.Min(MaxAmount, _storedAmount + canStore);
		}

		long leftoverCount = item.stack - canStore;
		if (leftoverCount <= 0) return new Item();
		var leftover = item.Clone();
		leftover.stack = (int)leftoverCount;
		return leftover;
	}

	public virtual Item Extract(int slot, int amount, bool simulate)
	{
		if (_stored.IsAir || _storedAmount <= 0 || amount <= 0) return new Item();
		long toExtract = Math.Min(_storedAmount, amount);
		var copy = _stored.Clone();
		copy.stack = (int)toExtract;
		if (!simulate && toExtract > 0)
		{
			_storedAmount -= toExtract;
			if (_storedAmount == 0) _stored = new Item();
		}
		return copy;
	}

	public virtual bool IsItemValid(int slot, Item item) => Accepts(item);

	private AutoOutputTrait? _autoOutput;
	public override AutoOutputTrait? AutoOutput { get { EnsureAutoOutput(); return _autoOutput; } }

	private void EnsureAutoOutput()
	{
		if (_autoOutput is not null) return;
		_autoOutput = AutoOutputTrait.OfItems(slotStart: 0, slotCount: 1);
		Traits.Attach(_autoOutput);
		Traits.RegisterPersistent("AutoOutput", _autoOutput);
	}

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		EnsureAutoOutput();
	}

	public override bool SupportsAutoOutputItems  => true;
	public override bool SupportsAutoOutputFluids => false;

	public bool IsAutoOutput
	{
		get => AutoOutput!.IsAutoOutputItems;
		set => AutoOutput!.SetAllowAutoOutputItems(value);
	}

	bool IControllable.IsWorkingEnabled() => _autoOutput?.IsAutoOutputItems ?? false;
	void IControllable.SetWorkingEnabled(bool enabled) => AutoOutput!.SetAllowAutoOutputItems(enabled);

	public override bool SupportsWorkingEnabledToggle => false;

	public void SetLocked(bool locked)
	{
		if (locked && !_stored.IsAir)
		{
			_lockedItem = _stored.Clone();
			_lockedItem.stack = 1;
		}
		else if (!locked)
		{
			_lockedItem = new Item();
		}
	}

	public void DumpStackTo(Player player)
	{
		if (_stored.IsAir || _storedAmount <= 0) return;
		int amount = (int)Math.Min(_storedAmount, _stored.maxStack);
		var taken = Extract(0, amount, simulate: false);
		if (taken.IsAir) return;
		global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(player, player.GetSource_OpenItem(taken.type), taken);
	}

	public override void WritePortableData(TagCompound tag)
	{
		if (_stored.IsAir || _storedAmount <= 0) return;
		tag["stored"]       = ItemIO.Save(_stored);
		tag["storedAmount"] = _storedAmount;
	}

	public override void ReadPortableData(TagCompound tag)
	{
		if (tag.ContainsKey("stored"))
		{
			_stored = ItemIO.Load(tag.GetCompound("stored"));
			_storedAmount = tag.GetLong("storedAmount");
		}
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		EnsureAutoOutput();
		base.SaveMachineData(tag);
		if (!_stored.IsAir) tag["stored"] = ItemIO.Save(_stored);
		tag["storedAmount"] = _storedAmount;
		tag["voiding"] = IsVoiding;
		if (!_lockedItem.IsAir) tag["locked"] = ItemIO.Save(_lockedItem);
	}

	public override void LoadData(TagCompound tag)
	{
		EnsureAutoOutput();
		base.LoadData(tag);
		_stored = tag.ContainsKey("stored") ? ItemIO.Load(tag.GetCompound("stored")) : new Item();
		_storedAmount = tag.GetLong("storedAmount");
		IsVoiding = tag.GetBool("voiding");
		_lockedItem = tag.ContainsKey("locked") ? ItemIO.Load(tag.GetCompound("locked")) : new Item();
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add(_stored.IsAir
			? $"Empty  (0 / {MaxAmount:N0})"
			: $"{_stored.Name}: {_storedAmount:N0} / {MaxAmount:N0}");
		if (IsLocked) lines.Add($"Locked: {_lockedItem.Name}");
		if (IsVoiding) lines.Add("Voiding overflow");
		lines.Add("Right-click to open. Deposit through the slot inside the UI");
	}
}
