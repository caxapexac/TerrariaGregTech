#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public class CokeOvenHatch : MultiblockPartMachine
{
	protected override string Label => "Coke Oven Hatch";

	public override bool SupportsCovers => false;

	public ItemHandlerProxyTrait? InputInventory  { get; protected set; }
	public ItemHandlerProxyTrait? OutputInventory { get; protected set; }
	public FluidTankProxyTrait?   Tank            { get; protected set; }

	private IoFacadeHandler? _itemFacade;
	public override IItemHandler? ExposedItemHandler  => _itemFacade;
	public override IFluidHandler? ExposedFluidHandler => Tank;

	public CokeOvenHatch() : base() { }

	public void Configure()
	{
		EnsureTraits();
	}

	protected override void OnTick()
	{
		EnsureTraits();
		base.OnTick();
	}

	private void EnsureTraits()
	{
		if (InputInventory != null) return;
		InputInventory  = new ItemHandlerProxyTrait(IO.IN);
		OutputInventory = new ItemHandlerProxyTrait(IO.OUT);
		Tank            = new FluidTankProxyTrait(IO.BOTH);
		Traits.Attach(InputInventory);
		Traits.Attach(OutputInventory);
		Traits.Attach(Tank);
		_itemFacade = new IoFacadeHandler(InputInventory, OutputInventory);
	}

	public bool CanShared() => false;

	public override void AddedToController(MultiblockControllerMachine controller)
	{
		base.AddedToController(controller);
		EnsureTraits();
		if (controller is ICokeOvenController cokeOven)
		{
			InputInventory!.Proxy  = cokeOven.ImportItems;
			OutputInventory!.Proxy = cokeOven.ExportItems;
			Tank!.Proxy            = cokeOven.ExportFluids;
		}
	}

	public override void RemovedFromController(MultiblockControllerMachine controller)
	{
		base.RemovedFromController(controller);
		if (InputInventory  != null) InputInventory.Proxy  = null;
		if (OutputInventory != null) OutputInventory.Proxy = null;
		if (Tank            != null) Tank.Proxy            = null;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		EnsureTraits();
	}

	private sealed class IoFacadeHandler : IItemHandler
	{
		private readonly ItemHandlerProxyTrait _input;
		private readonly ItemHandlerProxyTrait _output;

		public IoFacadeHandler(ItemHandlerProxyTrait input, ItemHandlerProxyTrait output)
		{
			_input  = input;
			_output = output;
		}

		public int SlotCount => _input.SlotCount + _output.SlotCount;

		public Item GetSlot(int slot)
		{
			if (slot < _input.SlotCount) return _input.GetSlot(slot);
			return _output.GetSlot(slot - _input.SlotCount);
		}

		public Item Insert(int slot, Item item, bool simulate)
		{
			if (slot < _input.SlotCount) return _input.Insert(slot, item, simulate);
			return item;
		}

		public Item Extract(int slot, int maxAmount, bool simulate)
		{
			if (slot < _input.SlotCount) return new Item();
			return _output.Extract(slot - _input.SlotCount, maxAmount, simulate);
		}

		public int GetSlotLimit(int slot) =>
			slot < _input.SlotCount ? _input.GetSlotLimit(slot)
									: _output.GetSlotLimit(slot - _input.SlotCount);

		public bool IsItemValid(int slot, Item item) =>
			slot < _input.SlotCount && _input.IsItemValid(slot, item);
	}
}
