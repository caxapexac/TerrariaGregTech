#nullable enable
using System;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Transfer;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public class ObjectHolderMachine : MultiblockPartMachine
{
	protected override string Label => "Object Holder";

	public ObjectHolderHandler? HeldItems { get; protected set; }
	public bool IsLocked { get; protected set; }

	public ObjectHolderMachine() : base() { }

	protected override void OnDefinitionBound()
	{
		base.OnDefinitionBound();
		Configure();
	}

	public void Configure()
	{
		EnsureTraits();
	}

	public override Item[]? GetSlotGroup(TerrariaCompat.Machine.SlotGroup group)
	{
		if (HeldItems == null) return base.GetSlotGroup(group);
		return group is TerrariaCompat.Machine.SlotGroup.InventoryInput
			or TerrariaCompat.Machine.SlotGroup.Inventory
			? HeldItems.Storage.Stacks : base.GetSlotGroup(group);
	}

	public override bool IsSlotBlocked(TerrariaCompat.Machine.SlotGroup group, int index) => IsLocked;

	private void EnsureTraits()
	{
		if (HeldItems != null) return;
		HeldItems = new ObjectHolderHandler(this);
		Traits.Attach(HeldItems);
		Traits.RegisterPersistent("HeldItems", HeldItems);
	}

	public void SetLocked(bool locked)
	{
		if (IsLocked == locked) return;
		IsLocked = locked;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public Item GetHeldItem(bool remove) => GetSlotImpl(0, remove);
	public void SetHeldItem(Item heldItem) => HeldItems?.Storage.SetSlot(0, heldItem);

	public Item GetDataItem(bool remove) => GetSlotImpl(1, remove);
	public void SetDataItem(Item dataItem) => HeldItems?.Storage.SetSlot(1, dataItem);

	private Item GetSlotImpl(int slot, bool remove)
	{
		if (HeldItems == null) return new Item();
		var stack = HeldItems.GetSlot(slot);
		if (remove && stack != null && !stack.IsAir)
			HeldItems.Storage.SetSlot(slot, new Item());
		return stack ?? new Item();
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["isLocked"] = IsLocked;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		IsLocked = tag.GetBool("isLocked");
	}

	public sealed class ObjectHolderHandler : NotifiableItemStackHandler
	{
		private readonly ObjectHolderMachine _owner;

		public ObjectHolderHandler(ObjectHolderMachine owner)
			: base(2, IO.IN, IO.BOTH, n => new LimitedStorage(n))
		{
			_owner = owner;
		}

		public override int GetSlotLimit(int slot) => 1;

		public override Item Extract(int slot, int amount, bool simulate)
		{
			if (_owner.IsLocked) return new Item();
			return base.Extract(slot, amount, simulate);
		}

		public override bool IsItemValid(int slot, Item stack)
		{
			if (stack == null || stack.IsAir) return true;
			bool isDataItem = TerrariaCompat.Items.ResearchDataGlobalItem.IsDataItemType(stack.type);
			if (slot == 0 && !isDataItem) return true;
			return slot == 1 && isDataItem;
		}

		private sealed class LimitedStorage : CustomItemStackHandler
		{
			public LimitedStorage(int slots) : base(slots) { }
			public override int GetSlotLimit(int slot) => 1;
		}
	}
}
