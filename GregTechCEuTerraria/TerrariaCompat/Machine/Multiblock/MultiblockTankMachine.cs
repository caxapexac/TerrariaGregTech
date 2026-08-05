#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Materials;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

public class MultiblockTankMachine : MultiblockControllerMachine,
	IMultiblockTankController, IFluidHandler
{
	protected override string Label => "Multiblock Tank";

	public override bool SupportsCovers => false;

	private NotifiableFluidTank? _tank;

	public NotifiableFluidTank GetTank()
	{
		EnsureTankTrait();
		return _tank!;
	}

	public int Capacity => Definition?.Capacity ?? 250_000;

	public MultiblockTankMachine() : base() { }

	protected void EnsureTankTrait()
	{
		if (_tank != null) return;
		BindDefinition();

		_tank = new NotifiableFluidTank(1, Capacity, IO.BOTH);
		string? matId = Definition?.MaterialId;
		if (matId != null && MaterialRegistry.Get(matId)?.FluidPipe is IPropertyFluidFilter filter)
			_tank.SetFilter(filter.Test);
		Traits.Attach(_tank);
		Traits.RegisterPersistent("Tank", _tank);
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		EnsureTankTrait();
		base.SaveMachineData(tag);
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		EnsureTankTrait();
		base.LoadData(tag);
	}

	protected override void OnTick()
	{
		EnsureTankTrait();
		base.OnTick();
	}

	public int        TankCount             { get { EnsureTankTrait(); return 1; } }
	public FluidStack GetTank(int tank)     { EnsureTankTrait(); return _tank!.GetFluidInTank(0); }
	public int        GetCapacity(int tank) { EnsureTankTrait(); return _tank!.GetTankCapacity(0); }
	public bool       IsFluidValid(int tank, FluidStack fluid)
		{ EnsureTankTrait(); return _tank!.IsFluidValid(0, fluid); }

	public int Fill(FluidStack fluid, bool simulate)
		{ EnsureTankTrait(); return _tank!.Fill(fluid, simulate); }
	public FluidStack Drain(int maxAmount, bool simulate)
		{ EnsureTankTrait(); return _tank!.Drain(maxAmount, simulate); }
	public FluidStack Drain(FluidStack fluidStack, bool simulate)
		{ EnsureTankTrait(); return _tank!.Drain(fluidStack, simulate); }

	public IFluidHandler GetTankAccess(int tank) { EnsureTankTrait(); return _tank!.Storages[0]; }
	public override int  ResolveFluidTank(IO direction, int localIndex) => localIndex;

	public (bool AllowFill, bool AllowDrain) GetTankClickCaps(int tank) => (true, true);
}
