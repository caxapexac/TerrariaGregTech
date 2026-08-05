#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public class SuperTankTileEntity : MetaMachine, IFluidHandler, IControllable
{
	public SuperTankTileEntity() { }
	public SuperTankTileEntity(VoltageTier tier) : base(tier) { }

	protected override string Label => Definition?.Label ?? "Super Tank";

	internal static long MaxAmountForTier(VoltageTier tier)
	{
		int t = (int)tier;
		return t == 0 ? 2_000_000L : 4_000_000L << (t - 1);
	}

	private long _maxAmount = -1;
	public long MaxAmount
	{
		get
		{
			if (_maxAmount < 0) _maxAmount = MaxAmountForTier(Tier);
			return _maxAmount;
		}
	}

	protected FluidStack _stored = FluidStack.Empty;
	protected long       _storedAmount;
	private FluidStack _lockedFluid = FluidStack.Empty;

	public bool IsVoiding { get; set; }
	public bool IsLocked => !_lockedFluid.IsEmpty;
	public long StoredAmount => _storedAmount;
	public FluidType? StoredType => _stored.Type;

	private bool Accepts(FluidStack fluid) => !IsLocked || _lockedFluid.SameTypeAs(fluid);

	public int TankCount => 1;

	public virtual FluidStack GetTank(int tank) =>
		_stored.IsEmpty
			? FluidStack.Empty
			: _stored.WithAmount((int)Math.Min(_storedAmount, int.MaxValue));

	public virtual int GetCapacity(int tank) => (int)Math.Min(MaxAmount, int.MaxValue);

	public virtual bool IsFluidValid(int tank, FluidStack fluid) => Accepts(fluid);

	public virtual int Fill(FluidStack resource, bool simulate)
	{
		if (resource.IsEmpty) return 0;
		long free = IsVoiding ? long.MaxValue : MaxAmount - _storedAmount;
		long canFill = 0;
		if ((_stored.IsEmpty || _stored.SameTypeAs(resource)) && Accepts(resource))
			canFill = Math.Min(resource.Amount, free);
		if (!simulate && canFill > 0)
		{
			if (_stored.IsEmpty) _stored = resource.WithAmount(1);
			_storedAmount = Math.Min(MaxAmount, _storedAmount + canFill);
		}
		return (int)Math.Min(canFill, int.MaxValue);
	}

	public virtual FluidStack Drain(int maxAmount, bool simulate)
	{
		if (_stored.IsEmpty || maxAmount <= 0) return FluidStack.Empty;
		long toDrain = Math.Min(_storedAmount, maxAmount);
		var copy = _stored.WithAmount((int)toDrain);
		if (!simulate && toDrain > 0)
		{
			_storedAmount -= toDrain;
			if (_storedAmount == 0) _stored = FluidStack.Empty;
		}
		return copy;
	}

	public virtual FluidStack Drain(FluidStack fluid, bool simulate)
	{
		if (fluid.IsEmpty || !fluid.SameTypeAs(_stored)) return FluidStack.Empty;
		return Drain(fluid.Amount, simulate);
	}

	private AutoOutputTrait? _autoOutput;
	public override AutoOutputTrait? AutoOutput { get { EnsureAutoOutput(); return _autoOutput; } }

	private void EnsureAutoOutput()
	{
		if (_autoOutput is not null) return;
		_autoOutput = AutoOutputTrait.OfFluids(tankStart: 0, tankCount: 1);
		Traits.Attach(_autoOutput);
		Traits.RegisterPersistent("AutoOutput", _autoOutput);
	}

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		EnsureAutoOutput();
	}

	public override bool SupportsAutoOutputItems  => false;
	public override bool SupportsAutoOutputFluids => true;

	public bool IsAutoOutput
	{
		get => AutoOutput!.IsAutoOutputFluids;
		set => AutoOutput!.SetAllowAutoOutputFluids(value);
	}

	bool IControllable.IsWorkingEnabled() => _autoOutput?.IsAutoOutputFluids ?? false;
	void IControllable.SetWorkingEnabled(bool enabled) => AutoOutput!.SetAllowAutoOutputFluids(enabled);

	public override bool SupportsWorkingEnabledToggle => false;

	public void SetLocked(bool locked)
	{
		if (locked && !_stored.IsEmpty)
			_lockedFluid = _stored.WithAmount(1);
		else if (!locked)
			_lockedFluid = FluidStack.Empty;
	}

	public override void WritePortableData(TagCompound tag)
	{
		if (_stored.IsEmpty || _storedAmount <= 0) return;
		tag["fluidId"]     = _stored.Type!.Id;
		tag["fluidAmount"] = _storedAmount;
		if (_stored.Nbt != null) tag["fluidNbt"] = _stored.Nbt;
	}

	public override void ReadPortableData(TagCompound tag)
	{
		if (tag.ContainsKey("fluidId") && FluidRegistry.TryGet(tag.GetString("fluidId"), out var type))
		{
			_stored = new FluidStack(type, 1,
				tag.ContainsKey("fluidNbt") ? tag.GetCompound("fluidNbt") : null);
			_storedAmount = tag.GetLong("fluidAmount");
		}
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		EnsureAutoOutput();
		base.SaveMachineData(tag);
		if (!_stored.IsEmpty)
		{
			tag["storedType"] = _stored.Type!.Id;
			if (_stored.Nbt != null) tag["storedNbt"] = _stored.Nbt;
		}
		tag["storedAmount"] = _storedAmount;
		tag["voiding"] = IsVoiding;
		if (!_lockedFluid.IsEmpty)
		{
			tag["lockType"] = _lockedFluid.Type!.Id;
			if (_lockedFluid.Nbt != null) tag["lockNbt"] = _lockedFluid.Nbt;
		}
	}

	public override void LoadData(TagCompound tag)
	{
		EnsureAutoOutput();
		base.LoadData(tag);
		_stored = tag.ContainsKey("storedType")
		          && FluidRegistry.TryGet(tag.GetString("storedType"), out var st)
			? new FluidStack(st, 1, tag.ContainsKey("storedNbt") ? tag.GetCompound("storedNbt") : null)
			: FluidStack.Empty;
		_storedAmount = tag.GetLong("storedAmount");
		IsVoiding = tag.GetBool("voiding");
		_lockedFluid = tag.ContainsKey("lockType")
		               && FluidRegistry.TryGet(tag.GetString("lockType"), out var lt)
			? new FluidStack(lt, 1, tag.ContainsKey("lockNbt") ? tag.GetCompound("lockNbt") : null)
			: FluidStack.Empty;
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add(_stored.IsEmpty
			? $"Empty  (0 / {MaxAmount:N0} mB)"
			: $"{_stored.Type!.DisplayName}: {_storedAmount:N0} / {MaxAmount:N0} mB");
		if (IsLocked) lines.Add($"Locked: {_lockedFluid.Type!.DisplayName}");
		if (IsVoiding) lines.Add("Voiding overflow");
		lines.Add("Right-click to open. Fill/drain through the fluid slot inside the UI");
	}
}
