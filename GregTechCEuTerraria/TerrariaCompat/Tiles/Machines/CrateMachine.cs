#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public sealed class CrateMachine : MetaMachine, IItemHandler
{
	public CrateMachine() { }
	public CrateMachine(VoltageTier tier) : base(tier) { }

	protected override string Label => Definition?.Label ?? "Crate";

	public int InventorySize => Definition?.Capacity ?? 27;

	private sealed class CrateStorage : Api.Transfer.CustomItemStackHandler
	{
		public CrateStorage(int size) : base(size) { }
		public override int GetSlotLimit(int slot) => Item.CommonMaxStack;
	}

	private NotifiableItemStackHandler? _inventory;
	public NotifiableItemStackHandler Inventory { get { EnsureTraits(); return _inventory!; } }

	private void EnsureTraits()
	{
		if (_inventory is not null) return;
		BindDefinition();
		_inventory = new NotifiableItemStackHandler(InventorySize, Api.Capability.Recipe.IO.BOTH,
			Api.Capability.Recipe.IO.BOTH, n => new CrateStorage(n));
		Traits.Attach(_inventory);
		Traits.RegisterPersistent("inventory", _inventory);
	}

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		EnsureTraits();
	}

	public override Item[]? GetSlotGroup(SlotGroup group) => group switch
	{
		SlotGroup.Inventory => Inventory.Storage.Stacks,
		_ => base.GetSlotGroup(group),
	};

	public override void NotifySlotGroupChanged(SlotGroup group)
	{
		if (group == SlotGroup.Inventory) Inventory.OnContentsChanged();
	}

	public int SlotCount => Inventory.SlotCount;
	public Item GetSlot(int slot) => Inventory.GetSlot(slot);
	public Item Insert(int slot, Item item, bool simulate) => Inventory.Insert(slot, item, simulate);
	public Item Extract(int slot, int amount, bool simulate) => Inventory.Extract(slot, amount, simulate);
	public bool IsItemValid(int slot, Item item) => true;

	public override void OnKill()
	{
		base.OnKill();
		if (!IsServer) return;
		var src  = new EntitySource_TileBreak(Position.X, Position.Y);
		var rect = new Rectangle(Position.X * 16, Position.Y * 16, Size.Width * 16, Size.Height * 16);
		foreach (var stack in Inventory.Storage.Stacks)
		{
			if (stack.IsAir) continue;
			Item.NewItem(src, rect, stack.Clone());
		}
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		EnsureTraits();
		base.SaveMachineData(tag);
	}

	public override void LoadData(TagCompound tag)
	{
		EnsureTraits();
		base.LoadData(tag);
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		int used = 0;
		foreach (var s in Inventory.Storage.Stacks) if (!s.IsAir) used++;
		lines.Add($"Storage: {used} / {InventorySize} slots used");
	}
}
