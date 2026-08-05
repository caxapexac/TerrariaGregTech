#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using Terraria;
using Status = GregTechCEuTerraria.Api.Machine.Feature.RecipeLogicStatus;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

public class FluidDrillingRigMachine : WorkableElectricMultiblockMachine
{
	private BiomeProbe.Biome _cachedBiome;
	private bool _biomeCached;
	private string _lastFluidId = "";

	private NotifiableFluidTank? _fluidOut;

	public FluidDrillingRigMachine() : base() { }

	protected override RecipeLogic CreateRecipeLogic() => new FluidDrillingRigLogic();
	public new FluidDrillingRigLogic Recipe => (FluidDrillingRigLogic)base.Recipe;

	private const int CycleTicks = 20;

	private int RigMultiplier => Tier switch
	{
		VoltageTier.MV => 1,
		VoltageTier.HV => 16,
		VoltageTier.EV => 64,
		_              => 1,
	};

	private int BaseProduction => 100;

	private int ProductionPerCycle => BaseProduction * RigMultiplier;

	private long EuPerTick => VoltageTiers.VA((int)Tier);

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		RebindIoParts();
		Recipe.SetDuration(CycleTicks);
	}

	public override void OnStructureInvalid()
	{
		base.OnStructureInvalid();
		_fluidOut = null;
	}

	public override void OnPartUnload()
	{
		base.OnPartUnload();
		_fluidOut = null;
	}

	private void RebindIoParts()
	{
		_fluidOut = null;
		foreach (var part in GetParts())
		{
			if (part is FluidHatchPartMachine fh && fh.Io == IO.OUT && fh.Tank is not null)
			{
				_fluidOut = fh.Tank;
				return;
			}
		}
	}

	public bool PrepareTick(out string reason)
	{
		reason = "";
		if (!IsFormed || GetMultiblockState().HasError()) { reason = "Structure not formed"; return false; }
		if (!WorkingEnabled) { reason = "Disabled by player"; return false; }

		if (_fluidOut == null) RebindIoParts();
		if (_fluidOut == null) { reason = "Need an output fluid hatch"; return false; }

		if (!_biomeCached)
		{
			_cachedBiome = BiomeProbe.GetForTile(Position.X, Position.Y);
			_biomeCached = true;
		}
		var fluid = BiomeWorldIOTables.GetFluid(_cachedBiome);
		if (fluid == null) { reason = "No drillable fluid in biome"; return false; }
		_lastFluidId = fluid.Id;

		var simFill = _fluidOut.FillInternal(new FluidStack(fluid, ProductionPerCycle), simulate: true);
		if (simFill <= 0) { reason = "Output hatch full"; return false; }

		if (_energyContainer is null) _energyContainer = GetEnergyContainer();
		if (_energyContainer.EnergyStored < EuPerTick) { reason = "Out of power"; return false; }

		_energyContainer.ChangeEnergy(-EuPerTick);
		return true;
	}

	public void ProduceCycle()
	{
		_cachedBiome = BiomeProbe.GetForTile(Position.X, Position.Y);
		var fluid = BiomeWorldIOTables.GetFluid(_cachedBiome);
		if (fluid == null || _fluidOut == null) return;
		_fluidOut.FillInternal(new FluidStack(fluid, ProductionPerCycle), simulate: false);
	}

	public override void AppendTooltip(List<string> lines)
	{
		lines.Add(DisplayName);
		OnAddFancyInformationTooltip(lines);

		if (!IsFormed)
		{
			AppendUnformedStructureBlock(lines);
			return;
		}

		if (Recipe.IsWorking())
		{
			string biome = _biomeCached ? _cachedBiome.ToString() : "scanning";
			string fluidName = string.IsNullOrEmpty(_lastFluidId) ? "?" : _lastFluidId;
			lines.Add($"[c/55FF55:Drilling ({biome}):] {fluidName} {ProductionPerCycle}mB / {CycleTicks / 20.0:0.0}s");
		}
		else
		{
			lines.Add(RecipeStatusText.StatusLineForMulti(this, Recipe));
		}
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["fdr_lastFluid"] = _lastFluidId;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		_lastFluidId = tag.GetString("fdr_lastFluid");
	}

	public sealed class FluidDrillingRigLogic : RecipeLogic
	{
		public FluidDrillingRigLogic() : base() { }

		public new FluidDrillingRigMachine Machine => (FluidDrillingRigMachine)base.Machine;

		protected override IReadOnlyList<Type> ValidMachineClasses() =>
			new[] { typeof(FluidDrillingRigMachine) };

		public override void ServerTick()
		{
			if (_duration <= 0) return;
			if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) != 0) return;

			var m = Machine;
			if (!m.PrepareTick(out string reason))
			{
				_progress = 0;
				SetWaiting(reason);
				return;
			}

			SetStatus(Status.WORKING);
			if (_progress++ < _duration)
			{
				if (!m.OnWorking()) InterruptRecipe();
				return;
			}
			_progress = 0;
			m.ProduceCycle();
		}

		public override void SaveForSync(Terraria.ModLoader.IO.TagCompound tag) => Save(tag);

		public void SetDuration(int max) { _duration = max; }
	}
}
