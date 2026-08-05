#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Primitive;

public class PrimitivePumpMachine : MultiblockControllerMachine
{
	protected override string Label => "Primitive Water Pump";

	public override bool SupportsCovers => false;

	private int _biomeModifier;
	private int _hatchModifier;
	private NotifiableFluidTank? _fluidTank;

	public PrimitivePumpMachine() : base() { }

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		InitializeTank();
	}

	public override void OnStructureInvalid()
	{
		base.OnStructureInvalid();
		ResetState();
	}

	public override void OnPartUnload()
	{
		base.OnPartUnload();
		ResetState();
	}

	private void ResetState()
	{
		_hatchModifier = 0;
		_fluidTank = null;
	}

	private void InitializeTank()
	{
		foreach (var part in GetParts())
		{
			if (part is not PumpHatchPartMachine hatch) continue;
			if (hatch.Tank is not { } tank) continue;
			int cap = tank.GetTankCapacity(0);
			_hatchModifier = cap switch
			{
				PumpBiomeModifier.BUCKET_VOLUME              => 1,
				PumpBiomeModifier.BUCKET_VOLUME * 8          => 2,
				_                                            => 4,
			};
			_fluidTank = tank;
			return;
		}
	}

	protected override void OnTick()
	{
		base.OnTick();
		if (!IsServer) return;
		if (!IsFormed) return;
		if (GetMultiblockState().HasError()) return;
		if ((GetMcOffsetTimer() % 20) != 0) return;

		if (_biomeModifier == 0)
		{
			_biomeModifier = PumpBiomeModifier.GetForTile(Position.X, Position.Y);
			return;
		}
		if (_biomeModifier < 0) return;
		if (_fluidTank == null) InitializeTank();
		if (_fluidTank == null) return;

		int amount = GetFluidProduction();
		if (amount <= 0) return;
		var water = new FluidStack(FluidRegistry.Water, amount);
		_fluidTank.Storages[0].Fill(water, simulate: false);
	}

	public int GetFluidProduction()
	{
		int value = _biomeModifier * _hatchModifier;
		if (IsRainingHere()) value = value * 3 / 2;
		return value;
	}

	private bool IsRainingHere() => Main.raining && Position.Y <= Main.UnderworldLayer;

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["pp_biomeMod"] = _biomeModifier;
		tag["pp_hatchMod"] = _hatchModifier;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		_biomeModifier = tag.GetInt("pp_biomeMod");
		_hatchModifier = tag.GetInt("pp_hatchMod");
	}
}
