#nullable enable
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Content;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Config;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

public sealed class AssemblyLineMachine : WorkableElectricMultiblockMachine
{
	protected override string Label => "Assembly Line";

	private readonly List<NotifiableItemStackHandler> _orderedItemBuses = new();

	public AssemblyLineMachine() : base() { }

	private bool _allowCircuitSlots = false;
	public override bool AllowCircuitSlots() => _allowCircuitSlots;
	public void SetAllowCircuitSlots(bool value) => _allowCircuitSlots = value;

	public override System.Comparison<IMultiPart> GetPartSorter() => (a, b) =>
	{
		var pa = a.Self()?.Position; var pb = b.Self()?.Position;
		if (pa is null && pb is null) return 0;
		if (pa is null) return -1;
		if (pb is null) return  1;
		int dx = pa.Value.X.CompareTo(pb.Value.X);
		return dx != 0 ? dx : pa.Value.Y.CompareTo(pb.Value.Y);
	};

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["allowCircuitSlots"] = _allowCircuitSlots;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("allowCircuitSlots"))
			_allowCircuitSlots = tag.GetBool("allowCircuitSlots");
	}

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		RebuildBusOrdering();
	}

	public override void OnStructureInvalid()
	{
		base.OnStructureInvalid();
		_orderedItemBuses.Clear();
	}

	public override void OnPartUnload()
	{
		base.OnPartUnload();
		_orderedItemBuses.Clear();
	}

	private void RebuildBusOrdering()
	{
		_orderedItemBuses.Clear();
		foreach (var part in GetParts())
		{
			if (part is ItemBusPartMachine bus && bus.Io == IO.IN
				&& bus.Inventory != null && bus.Inventory.ShouldSearchContent)
				_orderedItemBuses.Add(bus.Inventory);
		}
	}

	public override ActionResult TryMatchInputContents(
		GTRecipe recipe,
		IReadOnlyList<Content> items,
		IReadOnlyList<Content> fluids)
	{
		var baseResult = base.TryMatchInputContents(recipe, items, fluids);
		if (!baseResult.IsSuccess) return baseResult;

		bool orderedItems = GTConfig.Instance?.OrderedAssemblyLineItems ?? true;
		if (!orderedItems) return ActionResult.SUCCESS;
		if (!CheckOrderedItems(items))
			return ActionResult.Fail(null, ItemRecipeCapability.CAP, IO.IN);
		return ActionResult.SUCCESS;
	}

	public override ActionResult TryConsumeInputContents(
		GTRecipe recipe,
		IReadOnlyList<Content> items,
		IReadOnlyList<Content> fluids)
	{
		bool orderedItems = GTConfig.Instance?.OrderedAssemblyLineItems ?? true;
		var none = System.Array.Empty<Content>();

		if (items.Count > 0)
		{
			var r = orderedItems
				? ConsumeOrderedItems(recipe, items)
				: base.TryConsumeInputContents(recipe, items, none);
			if (!r.IsSuccess) return r;
		}
		if (fluids.Count > 0)
		{
			var r = base.TryConsumeInputContents(recipe, none, fluids);
			if (!r.IsSuccess) return r;
		}
		return ActionResult.SUCCESS;
	}

	private bool CheckOrderedItems(IReadOnlyList<Content> items)
	{
		if (items.Count > _orderedItemBuses.Count) return false;
		for (int i = 0; i < items.Count; i++)
		{
			if (items[i].Payload is not Ingredient ing) return false;
			var bus = _orderedItemBuses[i];
			Item? firstStack = GetFirstNonEmptyStack(bus);
			if (firstStack == null || firstStack.IsAir) return false;
			if (!ing.Test(firstStack)) return false;
		}
		return true;
	}

	private ActionResult ConsumeOrderedItems(GTRecipe recipe, IReadOnlyList<Content> items)
	{
		if (items.Count > _orderedItemBuses.Count)
			return ActionResult.Fail(null, ItemRecipeCapability.CAP, IO.IN);

		for (int i = 0; i < items.Count; i++)
		{
			if (items[i].Payload is not Ingredient ing) return ActionResult.FAIL_NO_REASON;
			if (!SimulateBusConsume(_orderedItemBuses[i], recipe, ing))
				return ActionResult.Fail(null, ItemRecipeCapability.CAP, IO.IN);
		}
		for (int i = 0; i < items.Count; i++)
		{
			if (items[i].Payload is not Ingredient ing) return ActionResult.FAIL_NO_REASON;
			if (!RealBusConsume(_orderedItemBuses[i], recipe, ing))
				return ActionResult.FAIL_NO_REASON;
		}
		return ActionResult.SUCCESS;
	}

	private static bool SimulateBusConsume(NotifiableItemStackHandler bus, GTRecipe recipe, Ingredient ing)
	{
		var left = new List<Ingredient> { ing };
		var remaining = bus.HandleRecipeInner(IO.IN, recipe, left, simulate: true);
		return remaining is null || remaining.Count == 0;
	}

	private static bool RealBusConsume(NotifiableItemStackHandler bus, GTRecipe recipe, Ingredient ing)
	{
		var left = new List<Ingredient> { ing };
		var remaining = bus.HandleRecipeInner(IO.IN, recipe, left, simulate: false);
		return remaining is null || remaining.Count == 0;
	}

	private static Item? GetFirstNonEmptyStack(NotifiableItemStackHandler bus)
	{
		for (int s = 0; s < bus.GetSlots(); s++)
		{
			var stack = bus.Storage.GetStackInSlot(s);
			if (stack != null && !stack.IsAir) return stack;
		}
		return null;
	}
}
