#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Net;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public class TankValvePartMachine : MultiblockPartMachine
{
	protected override string Label => "Tank Valve";

	public override bool SupportsCovers => false;

	public FluidTankProxyTrait? TankProxy { get; protected set; }

	public override Api.Capability.IFluidHandler? ExposedFluidHandler => TankProxy;

	public IODirection IoDirection { get; protected set; } = IODirection.Up;

	public void SetIoDirection(IODirection direction)
	{
		if (IoDirection == direction) return;
		IoDirection = direction;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	private TickableSubscription? _autoIOSubs;

	public TankValvePartMachine() : base() { }

	protected override void OnTick()
	{
		EnsureProxy();
		EnsureAutoIOSubscription();
		base.OnTick();
	}

	private void EnsureProxy()
	{
		if (TankProxy != null) return;
		TankProxy = new FluidTankProxyTrait(IO.BOTH);
		Traits.Attach(TankProxy);
	}

	private void EnsureAutoIOSubscription()
	{
		_autoIOSubs ??= SubscribeServerTick(AutoIOTick);
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

	private void AutoIOTick()
	{
		if (GetOffsetTimer() % global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(5) != 0) return;
		if (!WorkingEnabled || TankProxy == null || TankProxy.IsEmpty()) return;
		TankProxy.ExportToNearby(IoDirection);
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["ioDirection"] = (byte)IoDirection;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("ioDirection")) IoDirection = (IODirection)tag.GetByte("ioDirection");
		EnsureProxy();
		EnsureAutoIOSubscription();
	}
}
