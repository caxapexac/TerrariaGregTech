#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public abstract class MultiblockPartMachine : MetaMachine, IMultiPart, IItemHandler, IFluidHandler
{
	public virtual IItemHandler?  ExposedItemHandler  => null;
	public virtual IFluidHandler? ExposedFluidHandler => null;

	int  IItemHandler.SlotCount                            => ExposedItemHandler?.SlotCount ?? 0;
	Item IItemHandler.GetSlot(int slot)                    => ExposedItemHandler?.GetSlot(slot) ?? new Item();
	Item IItemHandler.Insert(int slot, Item item, bool sim)=> ExposedItemHandler?.Insert(slot, item, sim) ?? item;
	Item IItemHandler.Extract(int slot, int max, bool sim) => ExposedItemHandler?.Extract(slot, max, sim) ?? new Item();
	int  IItemHandler.GetSlotLimit(int slot)               => ExposedItemHandler?.GetSlotLimit(slot) ?? 0;
	bool IItemHandler.IsItemValid(int slot, Item item)     => ExposedItemHandler?.IsItemValid(slot, item) ?? false;

	int         IFluidHandler.TankCount                          => ExposedFluidHandler?.TankCount ?? 0;
	FluidStack  IFluidHandler.GetTank(int tank)                  => ExposedFluidHandler?.GetTank(tank) ?? FluidStack.Empty;
	int         IFluidHandler.GetCapacity(int tank)              => ExposedFluidHandler?.GetCapacity(tank) ?? 0;
	bool        IFluidHandler.IsFluidValid(int tank, FluidStack f)=> ExposedFluidHandler?.IsFluidValid(tank, f) ?? false;
	int         IFluidHandler.Fill(FluidStack f, bool sim)       => ExposedFluidHandler?.Fill(f, sim) ?? 0;
	FluidStack  IFluidHandler.Drain(int max, bool sim)           => ExposedFluidHandler?.Drain(max, sim) ?? FluidStack.Empty;
	FluidStack  IFluidHandler.Drain(FluidStack f, bool sim)      => ExposedFluidHandler?.Drain(f, sim) ?? FluidStack.Empty;
	IFluidHandler IFluidHandler.GetTankAccess(int tank)          => ExposedFluidHandler?.GetTankAccess(tank) ?? this;
	(bool AllowFill, bool AllowDrain) IFluidHandler.GetTankClickCaps(int tank) => ExposedFluidHandler?.GetTankClickCaps(tank) ?? (true, true);


	private readonly HashSet<Point16> _controllerPositions = new();

	private readonly List<MultiblockControllerMachine> _controllers = new();
	private readonly HashSet<MultiblockControllerMachine> _controllerSet = new();

	private RecipeHandlerList? _handlerList;

	protected MultiblockPartMachine() : base() { }

	public bool HasController(int controllerPosX, int controllerPosY) =>
		_controllerPositions.Contains(new Point16(controllerPosX, controllerPosY));

	public bool IsFormed() => _controllerPositions.Count > 0;

	public void OnControllersUpdated()
	{
		_controllers.Clear();
		_controllerSet.Clear();
		foreach (var pos in _controllerPositions)
		{
			if (MetaMachine.GetMachineAt(pos.X, pos.Y) is MultiblockControllerMachine controller)
			{
				if (_controllerSet.Add(controller))
					_controllers.Add(controller);
			}
		}
	}

	public IReadOnlyCollection<MultiblockControllerMachine> GetControllers()
	{
		if (_controllers.Count != _controllerPositions.Count)
			OnControllersUpdated();
		return _controllers;
	}

	public List<RecipeHandlerList> GetRecipeHandlers() => new() { GetHandlerList() };

	public virtual bool OnWorking   (IWorkableMultiController controller) => true;
	public virtual bool OnWaiting   (IWorkableMultiController controller) => true;
	public virtual bool OnPaused    (IWorkableMultiController controller) => true;
	public virtual bool AfterWorking(IWorkableMultiController controller) => true;
	public virtual bool BeforeWorking(IWorkableMultiController controller) => true;
	public virtual GTRecipe? ModifyRecipe(GTRecipe recipe) => recipe;

	protected RecipeHandlerList GetHandlerList()
	{
		if (_handlerList is null)
		{
			var handlers = new List<object>();
			IO handlerIO = IO.NONE;
			bool ioFound = false;
			foreach (var trait in Traits.AllTraits)
			{
				if (trait is IRecipeHandlerTrait rht)
				{
					if (!ioFound)
					{
						handlerIO = rht.GetHandlerIO();
						ioFound = true;
					}
					handlers.Add(rht);
				}
			}
			_handlerList = handlers.Count == 0
				? RecipeHandlerList.NO_DATA
				: RecipeHandlerList.Of(handlerIO, handlers);
		}
		return _handlerList;
	}

	public override void OnKill()
	{
		base.OnKill();
		if (!IsServer) return;

		var snapshot = new List<MultiblockControllerMachine>(_controllers);
		foreach (var controller in snapshot)
		{
			RemovedFromController(controller);
			controller.OnPartUnload();
		}
		_controllerPositions.Clear();
		_controllers.Clear();
		_controllerSet.Clear();
	}

	public virtual void RemovedFromController(MultiblockControllerMachine controller)
	{
		_controllerPositions.Remove(controller.Position);
		if (_controllerSet.Remove(controller))
			_controllers.Remove(controller);
	}

	public virtual void AddedToController(MultiblockControllerMachine controller)
	{
		_controllerPositions.Add(controller.Position);
		if (_controllerSet.Add(controller))
			_controllers.Add(controller);
	}

	public virtual bool ReplacePartModelWhenFormed() => IsFormed();

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		if (_controllerPositions.Count == 0) return;
		var xs = new int[_controllerPositions.Count];
		var ys = new int[_controllerPositions.Count];
		int i = 0;
		foreach (var pos in _controllerPositions) { xs[i] = pos.X; ys[i] = pos.Y; i++; }
		tag["partCtrlX"] = xs;
		tag["partCtrlY"] = ys;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		_controllerPositions.Clear();
		_controllers.Clear();
		_controllerSet.Clear();
		if (!tag.ContainsKey("partCtrlX") || !tag.ContainsKey("partCtrlY")) return;
		var xs = tag.GetIntArray("partCtrlX");
		var ys = tag.GetIntArray("partCtrlY");
		int n = System.Math.Min(xs.Length, ys.Length);
		for (int i = 0; i < n; i++)
			_controllerPositions.Add(new Point16(xs[i], ys[i]));
	}
}
