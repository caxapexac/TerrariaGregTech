#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using Terraria.ModLoader.IO;
using RLStatus = GregTechCEuTerraria.Api.Machine.Feature.RecipeLogicStatus;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric.Research;

public class DataBankMachine : WorkableElectricMultiblockMachine, IDataBankController, IControllable,
	Multiblock.IPowerDiagnostics
{
	public static readonly long EUT_PER_HATCH         = VoltageTiers.VA((int)VoltageTier.EV);
	public static readonly long EUT_PER_HATCH_CHAINED = VoltageTiers.VA((int)VoltageTier.LuV);

	protected long _energyUsage;
	public long EnergyUsage => _energyUsage;

	protected long _syncUpkeep, _syncStored, _syncCapacity, _syncMaxInput;
	long Multiblock.IPowerDiagnostics.PowerUpkeep   => _syncUpkeep;
	long Multiblock.IPowerDiagnostics.PowerCapacity => _syncCapacity;
	long Multiblock.IPowerDiagnostics.PowerStored   => _syncStored;
	long Multiblock.IPowerDiagnostics.PowerMaxInput => _syncMaxInput;

	protected IMaintenanceMachine? _maintenance;

	public DataBankMachine() : base() { }

	public override bool IsRecipeLogicAvailable() => false;

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		_energyContainer = GetEnergyContainer();
		_maintenance = null;
		foreach (var part in GetParts())
			if (part is IMaintenanceMachine mm) { _maintenance = mm; break; }
		_energyUsage = CalculateEnergyUsage();
	}

	public override void OnStructureInvalid()
	{
		base.OnStructureInvalid();
		_energyUsage = 0;
		_maintenance = null;
	}

	protected virtual long CalculateEnergyUsage()
	{
		int receivers = 0, transmitters = 0, regulars = 0;
		foreach (var part in GetParts())
		{
			switch (part)
			{
				case OpticalDataHatchMachine od when !od.TransmitterFlag: receivers++;    break;
				case OpticalDataHatchMachine od when  od.TransmitterFlag: transmitters++; break;
				case DataAccessHatchMachine:                              regulars++;     break;
			}
		}
		int dataHatches = receivers + transmitters + regulars;
		long eutPerHatch = receivers > 0 ? EUT_PER_HATCH_CHAINED : EUT_PER_HATCH;
		return eutPerHatch * dataHatches;
	}

	protected override void OnTick()
	{
		base.OnTick();
		if (!IsServer) return;
		if (!IsFormed) return;
		Tick();
	}

	// Verbatim DataBankMachine.tick().
	protected void Tick()
	{
		long energyToConsume = GetEnergyUsage();
		if (_maintenance != null)
			energyToConsume += _maintenance.GetNumMaintenanceProblems() * energyToConsume / 10;
		var logic = GetRecipeLogic();

		if (_energyContainer is null) _energyContainer = GetEnergyContainer();

		if (logic.IsWaiting() && _energyContainer.GetInputPerSec() > 19L * energyToConsume)
			logic.SetStatus(RLStatus.IDLE);

		if (_energyContainer.EnergyStored >= energyToConsume)
		{
			if (!logic.IsWaiting())
			{
				long before = _energyContainer.EnergyStored;
				_energyContainer.ChangeEnergy(-energyToConsume);
				if (before - _energyContainer.EnergyStored == energyToConsume)
					logic.SetStatus(RLStatus.WORKING);
				else
					logic.SetWaiting("insufficient_in");
			}
		}
		else
		{
			logic.SetWaiting("insufficient_in");
		}

		_syncUpkeep   = energyToConsume;
		_syncStored   = _energyContainer.EnergyStored;
		_syncCapacity = _energyContainer.EnergyCapacity;
		_syncMaxInput = _energyContainer.InputVoltage * _energyContainer.InputAmperage;
	}

	public virtual long GetEnergyUsage() => _energyUsage;

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["db_upkeep"] = _syncUpkeep;
		tag["db_stored"] = _syncStored;
		tag["db_cap"]    = _syncCapacity;
		tag["db_maxin"]  = _syncMaxInput;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("db_upkeep")) _syncUpkeep   = tag.GetLong("db_upkeep");
		if (tag.ContainsKey("db_stored")) _syncStored   = tag.GetLong("db_stored");
		if (tag.ContainsKey("db_cap"))    _syncCapacity = tag.GetLong("db_cap");
		if (tag.ContainsKey("db_maxin"))  _syncMaxInput = tag.GetLong("db_maxin");
	}
}
