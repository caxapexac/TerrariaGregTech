#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Trait;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Steam;

public abstract class SteamMachine : MetaMachine, IFluidHandler
{
	public bool IsHighPressure => Definition?.IsHighPressure ?? false;

	private NotifiableFluidTank? _steamTank;
	public NotifiableFluidTank SteamTank
	{
		get { EnsureSteamTraits(); return _steamTank!; }
	}

	public int SteamTier => IsHighPressure ? 1 : 0;

	protected SteamMachine() : base() { }
	protected SteamMachine(bool isHighPressure) : base() { }

	protected virtual int SteamTankCapacity => 16_000;

	protected virtual void EnsureSteamTraits()
	{
		if (_steamTank is not null) return;
		BindDefinition();

		_steamTank = CreateSteamTank();
		_steamTank.SetFilter(fluid => !fluid.IsEmpty && fluid.Type!.Id == FluidRegistry.Steam.Id);
		Traits.Attach(_steamTank);
		Traits.RegisterPersistent("SteamTank", _steamTank);
	}

	protected virtual NotifiableFluidTank CreateSteamTank() =>
		new(1, SteamTankCapacity, Api.Capability.Recipe.IO.OUT);

	public virtual int TankCount { get { EnsureSteamTraits(); return _steamTank!.GetTanks(); } }

	public virtual FluidStack GetTank(int tank) { EnsureSteamTraits(); return _steamTank!.GetFluidInTank(tank); }

	public virtual int GetCapacity(int tank) => SteamTankCapacity;

	public virtual bool IsFluidValid(int tank, FluidStack fluid)
	{
		EnsureSteamTraits();
		if (_steamTank!.HandlerIO != Api.Capability.Recipe.IO.IN) return false;
		return !fluid.IsEmpty && fluid.Type!.Id == FluidRegistry.Steam.Id;
	}

	public virtual int Fill(FluidStack fluid, bool simulate)
	{
		EnsureSteamTraits();
		if (_steamTank!.HandlerIO != Api.Capability.Recipe.IO.IN) return 0;
		return _steamTank.Fill(fluid, simulate);
	}

	public virtual FluidStack Drain(int maxAmount, bool simulate)
	{
		if (maxAmount <= 0) return FluidStack.Empty;
		EnsureSteamTraits();
		return _steamTank!.Drain(maxAmount, simulate);
	}

	public virtual FluidStack Drain(FluidStack fluid, bool simulate)
	{
		if (fluid.IsEmpty) return FluidStack.Empty;
		EnsureSteamTraits();
		return _steamTank!.Drain(fluid, simulate);
	}

	public virtual IFluidHandler GetTankAccess(int tank)
	{
		EnsureSteamTraits();
		return _steamTank!.Storages[0];
	}

	public virtual (bool AllowFill, bool AllowDrain) GetTankClickCaps(int tank)
	{
		EnsureSteamTraits();
		return _steamTank!.HandlerIO == Api.Capability.Recipe.IO.IN
			? (true,  false)
			: (false, true);
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		EnsureSteamTraits();
		base.SaveMachineData(tag);
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		EnsureSteamTraits();
		base.LoadData(tag);
	}
}
