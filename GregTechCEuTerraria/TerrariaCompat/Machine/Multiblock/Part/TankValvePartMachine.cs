#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public class TankValvePartMachine : MultiblockPartMachine
{
	protected override string Label => "Tank Valve";

	public override bool SupportsCovers => false;

	public FluidTankProxyTrait? TankProxy { get; protected set; }

	public override Api.Capability.IFluidHandler? ExposedFluidHandler => TankProxy;

	public TankValvePartMachine() : base() { }

	protected override void OnTick()
	{
		EnsureProxy();
		base.OnTick();
	}

	private void EnsureProxy()
	{
		if (TankProxy != null) return;
		TankProxy = new FluidTankProxyTrait(IO.BOTH);
		Traits.Attach(TankProxy);
	}

	public bool CanShared() => false;

	public override void AddedToController(MultiblockControllerMachine controller)
	{
		base.AddedToController(controller);
		EnsureProxy();
		if (controller is IMultiblockTankController tankController)
			TankProxy!.Proxy = tankController.GetTank();
	}

	public override void RemovedFromController(MultiblockControllerMachine controller)
	{
		base.RemovedFromController(controller);
		if (TankProxy != null) TankProxy.Proxy = null;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		EnsureProxy();
	}
}
