#nullable enable
using System.IO;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

namespace GregTechCEuTerraria.TerrariaCompat.Net.Actions;

public sealed class FluidLockSetAction : IMachineAction
{
	public enum Op : byte
	{
		Toggle  = 0,
		Phantom = 1,
	}

	public PacketType Type => PacketType.FluidLockSet;

	private Op _op;
	private bool _locked;
	private string _fluidId = "";

	public FluidLockSetAction() { }

	public static FluidLockSetAction Toggle(bool locked) =>
		new() { _op = Op.Toggle, _locked = locked };

	public static FluidLockSetAction Phantom(FluidType? type) =>
		new() { _op = Op.Phantom, _fluidId = type?.Id ?? "" };

	public void Write(BinaryWriter w)
	{
		w.Write((byte)_op);
		w.Write(_locked);
		w.Write(_fluidId ?? "");
	}

	public void Read(BinaryReader r)
	{
		_op      = (Op)r.ReadByte();
		_locked  = r.ReadBoolean();
		_fluidId = r.ReadString();
	}

	public void Apply(MetaMachine entity, int byWhoAmI)
	{
		if (entity is not FluidHatchPartMachine hatch) return;
		var tank = hatch.Tank;
		if (tank is null || tank.Storages.Length == 0) return;

		switch (_op)
		{
			case Op.Toggle:
				tank.SetLocked(_locked);
				break;

			case Op.Phantom:
				if (!tank.GetFluidInTank(0).IsEmpty) return;
				if (_fluidId.Length == 0)
				{
					tank.SetLocked(false);
				}
				else if (FluidRegistry.TryGet(_fluidId, out var type))
				{
					tank.SetLocked(true, new FluidStack(type, 1));
				}
				break;
		}
	}
}
