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

public class LargeMinerMachine : WorkableElectricMultiblockMachine
{
	private BiomeWorldIOTables.MinerBucket _cachedBucket;
	private bool   _biomeCached;
	private string _lastOreId = "";

	private NotifiableFluidTank?         _drillingFluidIn;
	private NotifiableItemStackHandler?  _oreOut;

	private Random? _rng;
	private Random Rng => _rng ??= new Random(unchecked(Position.X * 73856093 ^ Position.Y * 19349663));

	public LargeMinerMachine() : base() { }

	protected override RecipeLogic CreateRecipeLogic() => new LargeMinerLogic();
	public new LargeMinerLogic Recipe => (LargeMinerLogic)base.Recipe;

	private int CycleTicks => Tier switch
	{
		VoltageTier.EV  => 200,
		VoltageTier.IV  => 150,
		VoltageTier.LuV => 100,
		_               => 200,
	};

	private int OutputCount => Tier switch
	{
		VoltageTier.EV  => 1,
		VoltageTier.IV  => 2,
		VoltageTier.LuV => 4,
		_               => 1,
	};

	private int DrillingFluidPerCycle => Tier switch
	{
		VoltageTier.EV  => 4,
		VoltageTier.IV  => 3,
		VoltageTier.LuV => 2,
		_               => 4,
	};

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
		_drillingFluidIn = null;
		_oreOut = null;
	}

	public override void OnPartUnload()
	{
		base.OnPartUnload();
		_drillingFluidIn = null;
		_oreOut = null;
	}

	private void RebindIoParts()
	{
		_drillingFluidIn = null;
		_oreOut = null;
		foreach (var part in GetParts())
		{
			if (_drillingFluidIn == null && part is FluidHatchPartMachine fh
				&& fh.Io == IO.IN && fh.Tank is not null)
				_drillingFluidIn = fh.Tank;
			if (_oreOut == null && part is ItemBusPartMachine ib
				&& ib.Io == IO.OUT && ib.Inventory is not null)
				_oreOut = ib.Inventory;
			if (_drillingFluidIn != null && _oreOut != null) return;
		}
	}

	public bool PrepareTick(out string reason)
	{
		reason = "";
		if (!IsFormed || GetMultiblockState().HasError()) { reason = "Structure not formed"; return false; }
		if (!WorkingEnabled) { reason = "Disabled by player"; return false; }

		if (_drillingFluidIn == null || _oreOut == null) RebindIoParts();
		if (_oreOut == null) { reason = "Need an output bus"; return false; }

		if (_energyContainer is null) _energyContainer = GetEnergyContainer();
		if (_energyContainer.EnergyStored < EuPerTick) { reason = "Out of power"; return false; }

		if (_drillingFluidIn == null || GetDrillingFluidStored() < DrillingFluidPerCycle)
		{
			reason = "Need drilling fluid";
			return false;
		}

		_energyContainer.ChangeEnergy(-EuPerTick);
		return true;
	}

	public void ProduceCycle()
	{
		_cachedBucket = BiomeWorldIOTables.Classify(Position.X, Position.Y);
		_biomeCached = true;

		if (_drillingFluidIn == null) return;
		var drillingFluid = FluidRegistry.Get("drilling_fluid");
		if (drillingFluid == null) return;
		var drained = _drillingFluidIn.DrainInternal(
			new FluidStack(drillingFluid, DrillingFluidPerCycle), simulate: false);
		if (drained.IsEmpty || drained.Amount < DrillingFluidPerCycle) return;

		var (itemType, matId) = BiomeWorldIOTables.RollFromBucket(_cachedBucket, Rng);
		if (itemType <= 0) return;
		_lastOreId = matId;

		if (_oreOut == null) return;
		var leftover = OutputCount;
		for (int slot = 0; slot < _oreOut.SlotCount && leftover > 0; slot++)
		{
			var stack = new Item();
			stack.SetDefaults(itemType);
			stack.stack = leftover;
			var rem = _oreOut.InsertInternal(slot, stack, simulate: false);
			leftover = rem.stack;
		}
	}

	private int GetDrillingFluidStored()
	{
		if (_drillingFluidIn == null) return 0;
		var stack = _drillingFluidIn.Storages[0].Fluid;
		var df = FluidRegistry.Get("drilling_fluid");
		if (df == null || stack.IsEmpty || stack.Type?.Id != df.Id) return 0;
		return stack.Amount;
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
			string biome = _biomeCached ? BiomeWorldIOTables.Label(_cachedBucket) : "scanning";
			if (!string.IsNullOrEmpty(_lastOreId))
				lines.Add($"[c/55FF55:Mining ({biome}):] {_lastOreId} x{OutputCount} / {CycleTicks / 20.0:0.0}s");
			else
				lines.Add($"[c/55FF55:Mining ({biome}):] warming up");
		}
		else
		{
			lines.Add(RecipeStatusText.StatusLineForMulti(this, Recipe));
		}
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["lm_lastOre"] = _lastOreId;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		_lastOreId = tag.GetString("lm_lastOre");
	}

	public sealed class LargeMinerLogic : RecipeLogic
	{
		public LargeMinerLogic() : base() { }

		public new LargeMinerMachine Machine => (LargeMinerMachine)base.Machine;

		protected override IReadOnlyList<Type> ValidMachineClasses() =>
			new[] { typeof(LargeMinerMachine) };

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
