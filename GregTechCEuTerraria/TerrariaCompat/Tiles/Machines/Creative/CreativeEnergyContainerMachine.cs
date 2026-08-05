#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines.Creative;

public sealed class CreativeEnergyContainerMachine : MetaMachine, IEnergyContainer
{
	public CreativeEnergyContainerMachine() { }

	protected override string Label => Definition?.Label ?? "Creative Energy Container";

	private long _voltage      = 0;
	private int  _amps         = 1;
	private bool _active       = false;
	private bool _source       = true;
	private long _energyIOPerSec;
	private long _lastAverageEnergyIOPerTick;
	private long _ampsReceived;

	public long Voltage
	{
		get => _voltage;
		set => _voltage = Math.Max(0, value);
	}
	public int Amps
	{
		get => _amps;
		set => _amps = Math.Max(0, value);
	}
	public bool Active
	{
		get => _active;
		set
		{
			if (_active == value) return;
			_active = value;
			TerrariaCompat.Pipelike.Cable.EnergyNetSystem.MarkEndpointsDirty();
		}
	}
	public bool Source
	{
		get => _source;
		set
		{
			if (_source == value) return;
			_source = value;
			if (_source)
			{
				_voltage = 0;
				_amps    = 0;
			}
			else
			{
				_voltage = VoltageTiers.Voltage(VoltageTier.MAX);
				_amps    = 1;
			}
			TerrariaCompat.Pipelike.Cable.EnergyNetSystem.MarkEndpointsDirty();
		}
	}
	public long LastAverageEnergyIOPerTick => _lastAverageEnergyIOPerTick;

	protected override void OnTick()
	{
		if (GetMcOffsetTimer() % 20 == 0)
		{
			_lastAverageEnergyIOPerTick = _energyIOPerSec / 20;
			_energyIOPerSec = 0;
		}
		_ampsReceived = 0;
		if (!_active || !_source || _voltage <= 0 || _amps <= 0) return;
		long ampsUsed = 0;
		foreach (var (side, neighbor) in MachineCellResolver.PerimeterNeighbors(this))
		{
			if (neighbor is not IEnergyContainer container) continue;
			var opposite = side.Opposite();
			if (container.InputsEnergy(opposite) && container.GetEnergyCanBeInserted() > 0)
			{
				ampsUsed += container.AcceptEnergyFromNetwork(opposite, _voltage, _amps - ampsUsed);
				if (ampsUsed >= _amps) break;
			}
		}
		_energyIOPerSec += ampsUsed * _voltage;
	}

	public long AcceptEnergyFromNetwork(IODirection side, long voltage, long amperage)
	{
		if (_source || !_active || _ampsReceived >= _amps) return 0;
		if (voltage > _voltage)
		{
			return Math.Min(amperage, _amps - _ampsReceived);
		}
		long accepted = Math.Min(amperage, _amps - _ampsReceived);
		if (accepted > 0)
		{
			_ampsReceived   += accepted;
			_energyIOPerSec += accepted * voltage;
			return accepted;
		}
		return 0;
	}

	public bool InputsEnergy(IODirection side) => !_source && _active;
	public bool OutputsEnergy(IODirection side) => _source && _active;

	public long ChangeEnergy(long differenceAmount)
	{
		if (_source || !_active) return 0;
		_energyIOPerSec += differenceAmount;
		return differenceAmount;
	}

	public long EnergyStored
	{
		get => (_source && _active && _voltage > 0) ? long.MaxValue : 69;
	}
	public long EnergyCapacity => 420;

	public long InputAmperage  => (!_source && _active) ? _amps    : 0;
	public long InputVoltage   => (!_source && _active) ? _voltage : 0;
	public long OutputVoltage  => ( _source && _active) ? _voltage : 0;
	public long OutputAmperage => ( _source && _active) ? _amps    : 0;

	void IEnergyContainer.OnEnergyPushedToNetwork(long amps, long voltage)
	{
		_energyIOPerSec += amps * voltage;
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["voltage"] = _voltage;
		tag["amps"]    = _amps;
		tag["active"]  = _active;
		tag["source"]  = _source;
		tag["lastAvg"] = _lastAverageEnergyIOPerTick;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		_voltage = tag.GetLong("voltage");
		_amps    = tag.GetInt("amps");
		_active  = tag.GetBool("active");
		_source  = tag.ContainsKey("source") ? tag.GetBool("source") : true;
		_lastAverageEnergyIOPerTick = tag.GetLong("lastAvg");
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		if (_active)
		{
			string tier = _voltage <= 0
				? "(0)"
				: VoltageTiers.ShortName((VoltageTier)System.Math.Clamp(
					VoltageTiers.FloorTierByVoltage(_voltage), 0, (int)VoltageTier.MAX));
			lines.Add($"{(_source ? "Source" : "Sink")}: {tier} ({_voltage:N0} EU/t) x {_amps:N0} A");
		}
		else
		{
			lines.Add("Inactive");
		}
		lines.Add($"Avg I/O: {_lastAverageEnergyIOPerTick:N0} EU/t");
	}
}
