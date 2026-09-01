#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public sealed class DrumMachine : MetaMachine, IFluidHandler, IControllable
{
	public DrumMachine() { }
	public DrumMachine(VoltageTier tier) : base(tier) { }

	protected override string Label => Definition?.Label ?? "Drum";

	public int Capacity => Definition?.Capacity ?? 16_000;

	private NotifiableFluidTank? _cache;
	private AutoOutputTrait? _autoOutput;

	public NotifiableFluidTank Cache { get { EnsureTraits(); return _cache!; } }
	public override AutoOutputTrait? AutoOutput { get { EnsureTraits(); return _autoOutput; } }

	private void EnsureTraits()
	{
		if (_cache is not null) return;
		BindDefinition();

		_cache = new NotifiableFluidTank(1, Capacity, Api.Capability.Recipe.IO.BOTH);
		Traits.Attach(_cache);
		Traits.RegisterPersistent("cache", _cache);

		_autoOutput = AutoOutputTrait.OfFluids(tankStart: 0, tankCount: 1);
		Traits.Attach(_autoOutput);
		Traits.RegisterPersistent("AutoOutput", _autoOutput);
	}

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		EnsureTraits();
	}

	public int TankCount => 1;
	public FluidStack GetTank(int tank) => Cache.GetFluidInTank(tank);
	public int GetCapacity(int tank) => Capacity;
	public bool IsFluidValid(int tank, FluidStack fluid) => Cache.IsFluidValid(tank, fluid);
	public int Fill(FluidStack resource, bool simulate) => Cache.Fill(resource, simulate);
	public FluidStack Drain(int maxAmount, bool simulate) => Cache.Drain(maxAmount, simulate);
	public FluidStack Drain(FluidStack fluid, bool simulate) => Cache.Drain(fluid, simulate);

	public IFluidHandler GetTankAccess(int tank) => Cache.Storages[tank];

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

	public override void WritePortableData(TagCompound tag)
	{
		var stored = Cache.Storages[0].Fluid;
		if (stored.IsEmpty) return;
		tag["fluidId"]     = stored.Type!.Id;
		tag["fluidAmount"] = stored.Amount;
		if (stored.Nbt != null) tag["fluidNbt"] = stored.Nbt;
	}

	public override void ReadPortableData(TagCompound tag)
	{
		if (tag.ContainsKey("fluidId") && FluidRegistry.TryGet(tag.GetString("fluidId"), out var type))
			Cache.Storages[0].SetFluid(new FluidStack(type, tag.GetInt("fluidAmount"),
				tag.ContainsKey("fluidNbt") ? tag.GetCompound("fluidNbt") : null));
	}

	protected override void SaveMachineData(TagCompound tag) { EnsureTraits(); base.SaveMachineData(tag); }
	public override void LoadData(TagCompound tag)
	{
		EnsureTraits();
		base.LoadData(tag);
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		var stored = Cache.Storages[0].Fluid;
		lines.Add(stored.IsEmpty
			? $"Empty  (0 / {Capacity:N0} mB)"
			: $"{stored.Type!.DisplayName}: {stored.Amount:N0} / {Capacity:N0} mB");
		lines.Add("Right-click to open. Fill/drain through the fluid slot inside the UI");
	}
}
