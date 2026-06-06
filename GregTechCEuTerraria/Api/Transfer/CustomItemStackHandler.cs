#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.Api.Transfer;

// port of com.gregtechceu.gtceu.api.transfer.item.CustomItemStackHandler.
//
// Slot-backed item store with content-change callback + per-slot filter +
// drop-on-destroy helper
//
// Documented adaptations:
//   - Forge ItemStack / NBT -> Terraria Item / TagCompound. Item.IsAir replaces
//     ItemStack.isEmpty; Item.Clone() replaces ItemStack.copy.
//   - INBTSerializable<CompoundTag> -> SaveData/LoadData(TagCompound) pair.
//   - dropInventoryInWorld uses Item.NewItem instead of Block.popResource.
public class CustomItemStackHandler : IItemHandlerModifiable
{
	public Item[] Stacks;

	// Shared "empty" sentinel - the Terraria analog of Forge's ItemStack.EMPTY.
	// must treat an IsAir return as read-only
	public static readonly Item Empty = new();

	public Action OnContentsChangedAction { get; set; } = () => { };

	public Predicate<Item> Filter { get; set; } = _ => true;

	public CustomItemStackHandler() { Stacks = System.Array.Empty<Item>(); }

	public CustomItemStackHandler(int size)
	{
		Stacks = new Item[size];
		for (int i = 0; i < size; i++) Stacks[i] = new Item();
	}

	public CustomItemStackHandler(Item itemStack) : this(1)
	{
		Stacks[0] = itemStack ?? new Item();
	}

	public CustomItemStackHandler(IList<Item> stacks)
	{
		Stacks = new Item[stacks.Count];
		for (int i = 0; i < Stacks.Length; i++) Stacks[i] = stacks[i] ?? new Item();
	}

	// === IItemHandlerModifiable surface =====================================

	public int SlotCount => Stacks.Length;
	public int GetSlots() => Stacks.Length;

	public Item GetSlot(int slot) => Stacks[slot];
	public Item GetStackInSlot(int slot) => Stacks[slot];

	public void SetSlot(int slot, Item item) => SetStackInSlot(slot, item);

	public virtual void SetStackInSlot(int slot, Item stack)
	{
		Stacks[slot] = stack ?? new Item();
		OnContentsChanged(slot);
	}

	public virtual bool IsItemValid(int slot, Item stack) => Filter(stack);

	public virtual int GetSlotLimit(int slot) => Item.CommonMaxStack;

	public virtual Item Insert(int slot, Item item, bool simulate)
	{
		if (item is null || item.IsAir) return Empty;
		if (!IsItemValid(slot, item)) return item.Clone();

		var existing = Stacks[slot];
		int limit = GetStackLimit(slot, item);

		if (existing.IsAir)
		{
			int accept = Math.Min(item.stack, limit);
			if (!simulate)
			{
				var placed = item.Clone();
				placed.stack = accept;
				Stacks[slot] = placed;
				OnContentsChanged(slot);
			}
			int rem = item.stack - accept;
			if (rem <= 0) return Empty;
			var leftover = item.Clone();
			leftover.stack = rem;
			return leftover;
		}

		// Same-type stack - top up
		if (existing.type == item.type &&
		    Terraria.ModLoader.ItemLoader.CanStack(existing, item))
		{
			int room = limit - existing.stack;
			if (room <= 0) return item.Clone();
			int accept = Math.Min(item.stack, room);
			if (!simulate)
			{
				existing.stack += accept;
				OnContentsChanged(slot);
			}
			int rem = item.stack - accept;
			if (rem <= 0) return Empty;
			var leftover = item.Clone();
			leftover.stack = rem;
			return leftover;
		}

		return item.Clone();
	}

	public virtual Item Extract(int slot, int maxAmount, bool simulate)
	{
		if (maxAmount <= 0) return Empty;
		var existing = Stacks[slot];
		if (existing.IsAir) return Empty;
		int take = Math.Min(existing.stack, maxAmount);
		var out_ = existing.Clone();
		out_.stack = take;
		if (!simulate)
		{
			int rem = existing.stack - take;
			if (rem <= 0)
			{
				Stacks[slot] = new Item();
			}
			else
			{
				existing.stack = rem;
			}
			OnContentsChanged(slot);
		}
		return out_;
	}

	protected virtual int GetStackLimit(int slot, Item stack) =>
		Math.Min(GetSlotLimit(slot), stack.maxStack);

	protected virtual void OnContentsChanged(int slot) => OnContentsChangedAction();

	public virtual void Clear()
	{
		for (int i = 0; i < Stacks.Length; i++) Stacks[i] = new Item();
		OnContentsChangedAction();
	}

	public void DropInventoryInWorld(int tileX, int tileY)
	{
		int wx = tileX * 16 + 8;
		int wy = tileY * 16 + 8;
		foreach (var stack in Stacks)
		{
			if (stack.IsAir) continue;
			Terraria.Item.NewItem(null, wx, wy, 16, 16, stack.type, stack.stack);
		}
		Clear();
	}

	public TagCompound SerializeNBT()
	{
		var tag = new TagCompound();
		var items = new List<TagCompound>(Stacks.Length);
		foreach (var s in Stacks) items.Add(ItemIO.Save(s));
		tag["items"] = items;
		tag["size"]  = Stacks.Length;
		return tag;
	}

	public void DeserializeNBT(TagCompound tag)
	{
		int size = tag.ContainsKey("size") ? tag.GetInt("size") : Stacks.Length;
		if (Stacks.Length != size)
		{
			Stacks = new Item[size];
			for (int i = 0; i < size; i++) Stacks[i] = new Item();
		}
		if (tag.ContainsKey("items"))
		{
			var items = tag.GetList<TagCompound>("items");
			for (int i = 0; i < items.Count && i < Stacks.Length; i++)
				Stacks[i] = ItemIO.Load(items[i]);
		}
	}
}
