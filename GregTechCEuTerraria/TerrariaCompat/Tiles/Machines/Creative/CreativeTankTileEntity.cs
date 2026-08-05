#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Fluids;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines.Creative;

public sealed class CreativeTankTileEntity : SuperTankTileEntity
{
	public CreativeTankTileEntity() { }

	protected override string Label => Definition?.Label ?? "Creative Tank";

	private int _mBPerCycle    = 1000;
	private int _ticksPerCycle = 1;

	public int MBPerCycle
	{
		get => _mBPerCycle;
		set => _mBPerCycle = Math.Max(1, value);
	}

	public int TicksPerCycle
	{
		get => _ticksPerCycle;
		set
		{
			_ticksPerCycle = Math.Max(1, value);
			if (AutoOutput is not null) AutoOutput.TicksPerCycle = _ticksPerCycle;
		}
	}

	public void SetSourceFluid(FluidType? type)
	{
		if (type is null)
		{
			_stored = FluidStack.Empty;
			_storedAmount = 0;
		}
		else
		{
			_stored = new FluidStack(type, 1);
			_storedAmount = 1;
		}
	}

	public override FluidStack GetTank(int tank) =>
		_stored.IsEmpty ? FluidStack.Empty : _stored.WithAmount(_mBPerCycle);

	public override int GetCapacity(int tank) => 1000;

	public override bool IsFluidValid(int tank, FluidStack stack) => true;

	public override int Fill(FluidStack resource, bool simulate)
	{
		if (resource.IsEmpty) return 0;
		if (!_stored.IsEmpty && _stored.SameTypeAs(resource)) return resource.Amount;
		return 0;
	}

	public override FluidStack Drain(int maxAmount, bool simulate)
	{
		if (_stored.IsEmpty) return FluidStack.Empty;
		return _stored.WithAmount(Math.Min(maxAmount, _mBPerCycle));
	}

	public override FluidStack Drain(FluidStack fluid, bool simulate)
	{
		if (fluid.IsEmpty || !_stored.SameTypeAs(fluid)) return FluidStack.Empty;
		return fluid.WithAmount(Math.Min(fluid.Amount, _mBPerCycle));
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["mBPerCycle"]    = _mBPerCycle;
		tag["ticksPerCycle"] = _ticksPerCycle;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		_mBPerCycle    = tag.ContainsKey("mBPerCycle")    ? Math.Max(1, tag.GetInt("mBPerCycle"))    : 1000;
		_ticksPerCycle = tag.ContainsKey("ticksPerCycle") ? Math.Max(1, tag.GetInt("ticksPerCycle")) : 1;
		if (AutoOutput is not null) AutoOutput.TicksPerCycle = _ticksPerCycle;
	}

	public override void WritePortableData(TagCompound tag)
	{
		if (!_stored.IsEmpty) tag["fluidId"] = _stored.Type!.Id;
		tag["mBPerCycle"]    = _mBPerCycle;
		tag["ticksPerCycle"] = _ticksPerCycle;
	}

	public override void ReadPortableData(TagCompound tag)
	{
		if (tag.ContainsKey("fluidId") && FluidRegistry.TryGet(tag.GetString("fluidId"), out var t))
		{
			_stored = new FluidStack(t, 1);
			_storedAmount = 1;
		}
		if (tag.ContainsKey("mBPerCycle"))    _mBPerCycle    = Math.Max(1, tag.GetInt("mBPerCycle"));
		if (tag.ContainsKey("ticksPerCycle")) _ticksPerCycle = Math.Max(1, tag.GetInt("ticksPerCycle"));
		if (AutoOutput is not null) AutoOutput.TicksPerCycle = _ticksPerCycle;
	}

	public override void AppendTooltip(List<string> lines)
	{
		lines.Add(DisplayName);
		lines.Add(_stored.IsEmpty ? "Source: (unset)" : $"Source: {_stored.Type!.DisplayName}");
		lines.Add($"Rate: {_mBPerCycle:N0} mB / {_ticksPerCycle}t");
		if (!IsAutoOutput) lines.Add("Auto-output: disabled");
	}
}
