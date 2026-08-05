#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public abstract class TieredIOPartMachine : TieredPartMachine, IControllable
{
	public IO Io { get; protected set; }

	public IODirection IoDirection { get; protected set; } = IODirection.None;

	public void SetIoDirection(IODirection direction)
	{
		if (IoDirection == direction) return;
		IoDirection = direction;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public virtual UI.Widgets.UIDirectionSelector.Mode PartIoConfigMode =>
		UI.Widgets.UIDirectionSelector.Mode.Items;

	private bool _workingEnabled = true;

	protected TieredIOPartMachine() : base() { }

	public bool IsWorkingEnabled() => _workingEnabled;

	public void SetWorkingEnabled(bool workingEnabled)
	{
		if (_workingEnabled == workingEnabled) return;
		_workingEnabled = workingEnabled;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["io"]             = (byte)Io;
		tag["ioDirection"]    = (byte)IoDirection;
		tag["workingEnabled"] = _workingEnabled;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("io"))          Io          = (IO)tag.GetByte("io");
		if (tag.ContainsKey("ioDirection")) IoDirection = (IODirection)tag.GetByte("ioDirection");
		_workingEnabled = !tag.ContainsKey("workingEnabled") || tag.GetBool("workingEnabled");
	}
}
