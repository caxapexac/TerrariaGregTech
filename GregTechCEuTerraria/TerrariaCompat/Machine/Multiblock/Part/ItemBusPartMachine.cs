#nullable enable
using GregTechCEuTerraria.Api.Capability;
using System;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Cover.Filter;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

// Port of ItemBusPartMachine. Tier-sized item inventory ((1+min(9,tier))^2 slots)
//
// Adaptations:
//   ctor -> Configure(tier, io)
//   paint dropped
//   frontFacing -> TieredIOPartMachine.IoDirection
//   onLoad TickTask deferred init -> kicked from Configure
//   swapIO deferred
public class ItemBusPartMachine : TieredIOPartMachine, IDistinctPart, IHasCircuitSlot
{
	protected override string Label => "Item Bus";

	public NotifiableItemStackHandler? Inventory { get; protected set; }
	public NotifiableItemStackHandler? CircuitInventory { get; protected set; }
	public ItemFilterHandler? FilterHandler { get; protected set; }

	public override Api.Capability.IItemHandler? ExposedItemHandler => Inventory;

	protected bool HasCircuitSlot { get; private set; } = true;
	public bool CircuitSlotEnabled { get; protected set; } = true;
	private bool _isDistinct;

	private TickableSubscription? _autoIOSubs;

	public ItemBusPartMachine() : base() { }

	public void Configure(int tier, IO io)
	{
		Tier = tier;
		Io   = io;
		EnsureTraits();
		EnsureAutoIOSubscription();
	}

	protected override void OnDefinitionBound()
	{
		base.OnDefinitionBound();
		var def = Definition;
		if (def?.PartIo == null) return;
		Configure((int)((MetaMachine)this).Tier, def.PartIo.Value);
	}

	protected virtual int GetInventorySize()
	{
		int sizeRoot = 1 + System.Math.Min(9, Tier);
		return sizeRoot * sizeRoot;
	}

	protected virtual NotifiableItemStackHandler CreateInventory() =>
		new(GetInventorySize(), Io);

	public override Item[]? GetSlotGroup(TerrariaCompat.Machine.SlotGroup group)
	{
		if (Inventory == null) return base.GetSlotGroup(group);
		bool isInputBus  = Io == IO.IN  && group == TerrariaCompat.Machine.SlotGroup.InventoryInput;
		bool isOutputBus = Io == IO.OUT && group == TerrariaCompat.Machine.SlotGroup.InventoryOutput;
		return (isInputBus || isOutputBus) ? Inventory.Storage.Stacks : base.GetSlotGroup(group);
	}

	protected virtual NotifiableItemStackHandler CreateCircuitItemHandler(IO io)
	{
		if (io == IO.IN)
		{
			var circuit = new NotifiableItemStackHandler(1, IO.IN, IO.NONE).SetFilter(IsProgrammedCircuit);
			circuit.ShouldDropInventoryInWorld = false;
			circuit.ShouldSearchContent = false;
			return circuit;
		}
		HasCircuitSlot     = false;
		CircuitSlotEnabled = false;
		return new NotifiableItemStackHandler(0, IO.NONE) { ShouldSearchContent = false };
	}

	private void EnsureTraits()
	{
		if (Inventory != null) return;
		Inventory = CreateInventory();
		Inventory.SetFilter(MatchesFilter);
		Traits.Attach(Inventory);
		Traits.RegisterPersistent("Inventory", Inventory);

		CircuitInventory = CreateCircuitItemHandler(Io);
		Traits.Attach(CircuitInventory);
		Traits.RegisterPersistent("CircuitInventory", CircuitInventory);

		FilterHandler = new ItemFilterHandler(this);
	}

	// === Filter ============================================================

	protected bool MatchesFilter(Item stack)
	{
		var fh = FilterHandler;
		if (fh != null && fh.IsFilterPresent)
			return fh.GetFilter().Test(stack);
		return true;
	}

	private static bool IsProgrammedCircuit(Item stack) =>
		stack != null && stack.ModItem is IntCircuitItem;

	public bool IsDistinct() => _isDistinct;

	public void SetDistinct(bool distinct)
	{
		_isDistinct = (Io != IO.OUT && distinct);
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
		GetHandlerList().SetDistinctAndNotify(_isDistinct);
	}

	public bool IsCircuitSlotEnabled() => CircuitSlotEnabled;

	public void SetCircuitSlotEnabled(bool enabled)
	{
		CircuitSlotEnabled = enabled;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public override void AddedToController(MultiblockControllerMachine controller)
	{
		if (HasCircuitSlot && !controller.AllowCircuitSlots())
		{
			ClearCircuit();
			SetCircuitSlotEnabled(false);
		}
		base.AddedToController(controller);
	}

	public override void RemovedFromController(MultiblockControllerMachine controller)
	{
		base.RemovedFromController(controller);
		if (!HasCircuitSlot) return;
		foreach (var c in GetControllers())
		{
			if (!c.AllowCircuitSlots()) return;
		}
		SetCircuitSlotEnabled(true);
	}

	private void ClearCircuit()
	{
		if (CircuitInventory == null) return;
		for (int i = 0; i < CircuitInventory.SlotCount; i++)
			CircuitInventory.SetSlot(i, new Item());
	}

	// TODO check if its optimal
	private void EnsureAutoIOSubscription()
	{
		_autoIOSubs ??= SubscribeServerTick(AutoIOTick);
	}

	protected virtual void AutoIOTick()
	{
		if (GetOffsetTimer() % global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(5) != 0) return;
		if (!WorkingEnabled || Inventory == null) return;
		if (IoDirection == IODirection.None) return;
		if (Io == IO.OUT && !Inventory.IsEmpty())
		{
			AdjacentItemPush.Push(this, 0, Inventory.SlotCount, maxPerSlot: int.MaxValue, side: IoDirection);
		}
		else if (Io == IO.IN)
		{
			AdjacentItemPull.Pull(this, 0, Inventory.SlotCount, maxPerSlot: int.MaxValue, side: IoDirection);
		}
		else if (Io == IO.BOTH)
		{
			if (!Inventory.IsEmpty())
				AdjacentItemPush.Push(this, 0, Inventory.SlotCount, maxPerSlot: int.MaxValue, side: IoDirection);
			AdjacentItemPull.Pull(this, 0, Inventory.SlotCount, maxPerSlot: int.MaxValue, side: IoDirection.Opposite());
		}
	}

	public override void AppendTooltip(System.Collections.Generic.List<string> lines)
	{
		base.AppendTooltip(lines);
		int slots = Inventory?.SlotCount ?? GetInventorySize();
		lines.Add($"Capacity: {slots} slots");
		lines.Add($"Side: {IoDirection}");

		if (Inventory is null) return;
		var stacks = Inventory.Storage.Stacks;
		int shown = 0;
		int more = 0;
		bool anyShown = false;
		for (int i = 0; i < stacks.Length; i++)
		{
			var s = stacks[i];
			if (s is null || s.IsAir) continue;
			if (shown >= 8) { more++; continue; }
			if (!anyShown) { lines.Add("Contents:"); anyShown = true; }
			lines.Add($"  {s.stack}x {Terraria.Lang.GetItemName(s.type).Value}");
			shown++;
		}
		if (more > 0) lines.Add($"  + {more} more");
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["isDistinct"]         = _isDistinct;
		tag["circuitSlotEnabled"] = CircuitSlotEnabled;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		_isDistinct        = tag.GetBool("isDistinct");
		CircuitSlotEnabled = !tag.ContainsKey("circuitSlotEnabled") || tag.GetBool("circuitSlotEnabled");
		EnsureTraits();
		Traits.Load(tag);
		EnsureAutoIOSubscription();
		GetHandlerList().SetDistinctAndNotify(_isDistinct);
	}
}
