#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Transfer;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public sealed class PumpMachine : TieredEnergyMachine, IControllable, IFluidHandler
{
	public PumpMachine() { }
	public PumpMachine(VoltageTier tier) : base(tier) { }

	protected override string Label => Definition?.Label ?? "Pump";

	public override bool CanAccept => true;
	public override long EnergyCapacity => VoltageTiers.Voltage(Tier) * 64L;

	private long EnergyPerTick
	{
		get
		{
			int idx = Math.Max(0, (int)Tier - 1);
			return VoltageTiers.Voltage((VoltageTier)idx);
		}
	}

	public int Width => 16 * (int)Tier;
	public int Depth
	{
		get
		{
			double frac = (int)Tier switch
			{
				1 => 0.35,
				2 => 0.55,
				3 => 0.75,
				4 => 0.95,
				_ => 0.35 + 0.20 * ((int)Tier - 1),
			};
			int h = Main.maxTilesY > 0 ? Main.maxTilesY : 1200;
			return (int)(h * frac);
		}
	}
	public int TankCapacity => 16_000 * (int)Tier;

	private int TicksPerPump
	{
		get
		{
			int t = (int)Tier;
			return Math.Max(5, 45 - t * 8);
		}
	}

	protected override bool HasChargerSlot => true;

	private const int WaterTankIndex = 0;
	private const int LavaTankIndex  = 1;

	private NotifiableFluidTank? _tanks;
	private AutoOutputTrait? _autoOutput;
	private EnvironmentalExplosionTrait? _explosion;

	public NotifiableFluidTank Tanks { get { EnsureTraits(); return _tanks!; } }

	public override AutoOutputTrait? AutoOutput { get { EnsureTraits(); return _autoOutput; } }

	private void EnsureTraits()
	{
		if (_tanks is not null) return;
		BindDefinition();
		EnsureEnergyContainer();

		int cap = TankCapacity;
		var waterTank = new CustomFluidTank(cap, f => f.IsEmpty || f.SameTypeAs(new FluidStack(FluidRegistry.Water, 1)));
		var lavaTank  = new CustomFluidTank(cap, f => f.IsEmpty || f.SameTypeAs(new FluidStack(FluidRegistry.Lava, 1)));

		_tanks = new NotifiableFluidTank(new[] { waterTank, lavaTank }, IO.BOTH, IO.OUT);
		Traits.Attach(_tanks);
		Traits.RegisterPersistent("Tanks", _tanks);

		_autoOutput = AutoOutputTrait.OfFluids(_tanks);
		Traits.Attach(_autoOutput);
		Traits.RegisterPersistent("AutoOutput", _autoOutput);

		_explosion = Traits.GetTrait<EnvironmentalExplosionTrait>(EnvironmentalExplosionTrait.TYPE);
		_explosion?.SetEnableEnvironmentalExplosions(false);
	}

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		EnsureTraits();
	}

	public override bool SupportsAutoOutputFluids => true;

	int IFluidHandler.TankCount => Tanks.Storages.Length;
	FluidStack IFluidHandler.GetTank(int tank) => Tanks.Storages[tank].Fluid;
	int IFluidHandler.GetCapacity(int tank) => Tanks.Storages[tank].Capacity;
	bool IFluidHandler.IsFluidValid(int tank, FluidStack fluid) => Tanks.Storages[tank].IsFluidValid(fluid);
	int IFluidHandler.Fill(FluidStack resource, bool simulate)
	{
		int filled = 0;
		var copy = resource;
		for (int i = 0; i < Tanks.Storages.Length && copy.Amount > 0; i++)
		{
			int n = Tanks.Storages[i].Fill(copy, simulate);
			filled += n;
			if (n > 0) copy = copy.WithAmount(copy.Amount - n);
		}
		return filled;
	}
	FluidStack IFluidHandler.Drain(int maxAmount, bool simulate)
	{
		for (int i = 0; i < Tanks.Storages.Length; i++)
		{
			var d = Tanks.Storages[i].Drain(maxAmount, simulate);
			if (!d.IsEmpty) return d;
		}
		return FluidStack.Empty;
	}
	FluidStack IFluidHandler.Drain(FluidStack fluid, bool simulate)
	{
		for (int i = 0; i < Tanks.Storages.Length; i++)
		{
			var d = Tanks.Storages[i].Drain(fluid, simulate);
			if (!d.IsEmpty) return d;
		}
		return FluidStack.Empty;
	}
	IFluidHandler IFluidHandler.GetTankAccess(int tank)
		=> tank >= 0 && tank < Tanks.Storages.Length ? Tanks.Storages[tank] : this;

	private bool _isWorkingEnabled = true;
	public bool IsWorkingEnabled() => _isWorkingEnabled;
	public void SetWorkingEnabled(bool enabled) => _isWorkingEnabled = enabled;

	private bool _active;
	public override bool IsActive => _active;

	private int _progress;
	private int _targetX;
	private int _targetY;
	private int _targetLiquidType;
	private bool _hasTarget;
	private int _scanCursor;
	private int _lastDepth;

	protected override void OnTick()
	{
		EnsureTraits();

		bool canWork = _isWorkingEnabled && DrainEnergy(simulate: true);
		if (!canWork)
		{
			if (_active || _progress != 0 || _hasTarget)
			{
				_active = false;
				_progress = 0;
				_hasTarget = false;
			}
			return;
		}

		if (!_hasTarget || !TargetStillValid())
		{
			if (!FindTarget(out _targetX, out _targetY, out _targetLiquidType))
			{
				_active = false;
				_progress = 0;
				_hasTarget = false;
				return;
			}
			_hasTarget = true;
			_progress = 0;
		}

		_active = true;
		DrainEnergy(simulate: false);

		_progress++;
		if (_progress >= TicksPerPump)
		{
			PumpTarget();
			_progress = 0;
			_hasTarget = false;
		}
	}

	private bool TargetStillValid()
	{
		if (_targetX < 0 || _targetX >= Main.maxTilesX || _targetY < 0 || _targetY >= Main.maxTilesY)
			return false;
		var t = Main.tile[_targetX, _targetY];
		return t.LiquidAmount > 0 && t.LiquidType == _targetLiquidType && BucketFillGate(_targetX, _targetY);
	}

	private static bool BucketFillGate(int x, int y)
	{
		int type = Main.tile[x, y].LiquidType;
		int sum = 0;
		for (int i = x - 1; i <= x + 1; i++)
		{
			for (int j = y - 1; j <= y + 1; j++)
			{
				if (i < 0 || i >= Main.maxTilesX || j < 0 || j >= Main.maxTilesY) continue;
				if (Main.tile[i, j].LiquidType == type)
					sum += Main.tile[i, j].LiquidAmount;
			}
		}
		return sum > 100;
	}

	private bool FindTarget(out int outX, out int outY, out int outLiquidType)
	{
		int leftX  = Position.X + Size.Width / 2 - Width / 2;
		int startY = Position.Y + Size.Height;
		int endY   = Math.Min(Main.maxTilesY - 1, startY + Depth - 1);

		// Skip tanks that are full - no point pumping their fluid.
		bool waterRoom = Tanks.Storages[WaterTankIndex].FluidAmount < Tanks.Storages[WaterTankIndex].Capacity;
		bool lavaRoom  = Tanks.Storages[LavaTankIndex].FluidAmount  < Tanks.Storages[LavaTankIndex].Capacity;

		for (int i = 0; i < Width; i++)
		{
			int x = leftX + ((_scanCursor + i) % Width);
			_scanCursor = (_scanCursor + 1) % Width;
			if (x < 0 || x >= Main.maxTilesX) continue;
			for (int y = startY; y <= endY; y++)
			{
				var t = Main.tile[x, y];
				if (t.LiquidAmount == 0) continue;
				int lt = t.LiquidType;
				if (lt == LiquidID.Water && !waterRoom) continue;
				if (lt == LiquidID.Lava  && !lavaRoom)  continue;
				if (lt != LiquidID.Water && lt != LiquidID.Lava) continue;
				if (!BucketFillGate(x, y)) continue;
				outX = x; outY = y; outLiquidType = lt;
				return true;
			}
		}

		outX = outY = 0;
		outLiquidType = -1;
		return false;
	}

	private void PumpTarget()
	{
		if (_targetX < 0 || _targetX >= Main.maxTilesX || _targetY < 0 || _targetY >= Main.maxTilesY) return;
		var t = Main.tile[_targetX, _targetY];
		if (t.LiquidAmount == 0 || t.LiquidType != _targetLiquidType) return;

		int liquidType = t.LiquidType;
		int collected = t.LiquidAmount;
		var target = Main.tile[_targetX, _targetY];
		target.LiquidAmount = 0;
		target.LiquidType   = LiquidID.Water;
		WorldGen.SquareTileFrame(_targetX, _targetY, resetFrame: false);
		SyncLiquidChange(_targetX, _targetY);

		if (collected < 255)
		{
			for (int k = _targetX - 1; k <= _targetX + 1 && collected < 255; k++)
			{
				for (int l = _targetY - 1; l <= _targetY + 1 && collected < 255; l++)
				{
					if (k == _targetX && l == _targetY) continue;
					if (k < 0 || k >= Main.maxTilesX || l < 0 || l >= Main.maxTilesY) continue;
					var n = Main.tile[k, l];
					if (n.LiquidAmount <= 0 || n.LiquidType != liquidType) continue;

					int take = n.LiquidAmount;
					if (take + collected > 255) take = 255 - collected;
					collected += take;
					var neighbour = Main.tile[k, l];
					neighbour.LiquidAmount = (byte)(n.LiquidAmount - take);
					if (neighbour.LiquidAmount == 0)
						neighbour.LiquidType = LiquidID.Water;
					WorldGen.SquareTileFrame(k, l, resetFrame: false);
					SyncLiquidChange(k, l);
				}
			}
		}

		int mB = collected * 1000 / 255;
		var stack = new FluidStack(
			liquidType == LiquidID.Water ? FluidRegistry.Water : FluidRegistry.Lava,
			mB);
		int tankIdx = liquidType == LiquidID.Water ? WaterTankIndex : LavaTankIndex;
		Tanks.Storages[tankIdx].Fill(stack, simulate: false);

		_lastDepth = _targetY - (Position.Y + Size.Height) + 1;
	}

	private static void SyncLiquidChange(int x, int y)
	{
		if (Main.netMode == NetmodeID.Server)
			NetMessage.sendWater(x, y);
		else
			Liquid.AddWater(x, y);
	}

	private bool DrainEnergy(bool simulate)
	{
		long resultEnergy = EnergyContainer.EnergyStored - EnergyPerTick;
		if (resultEnergy >= 0L && resultEnergy <= EnergyContainer.EnergyCapacity)
		{
			if (!simulate) EnergyContainer.SetEnergyStored(resultEnergy);
			return true;
		}
		return false;
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		EnsureTraits();
		base.SaveMachineData(tag);
		tag["progress"]         = _progress;
		tag["active"]           = _active;
		tag["isWorkingEnabled"] = _isWorkingEnabled;
		tag["targetX"]          = _targetX;
		tag["targetY"]          = _targetY;
		tag["targetLiquid"]     = _targetLiquidType;
		tag["hasTarget"]        = _hasTarget;
		tag["scanCursor"]       = _scanCursor;
		tag["lastDepth"]        = _lastDepth;
	}

	public override void LoadData(TagCompound tag)
	{
		EnsureTraits();
		base.LoadData(tag);
		_progress         = tag.GetInt("progress");
		_active           = tag.GetBool("active");
		_isWorkingEnabled = !tag.ContainsKey("isWorkingEnabled") || tag.GetBool("isWorkingEnabled");
		_targetX          = tag.GetInt("targetX");
		_targetY          = tag.GetInt("targetY");
		_targetLiquidType = tag.ContainsKey("targetLiquid") ? tag.GetInt("targetLiquid") : -1;
		_hasTarget        = tag.GetBool("hasTarget");
		_scanCursor       = tag.GetInt("scanCursor");
		_lastDepth        = tag.GetInt("lastDepth");
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add($"Area: {Width} wide x {Depth} deep");
		lines.Add($"Speed: {TicksPerPump} ticks / bucket");
		lines.Add($"Draw: {EnergyPerTick:N0} EU/t");
		lines.Add($"Tanks: {Tanks.Storages[WaterTankIndex].FluidAmount}/{TankCapacity} mB water, " +
		          $"{Tanks.Storages[LavaTankIndex].FluidAmount}/{TankCapacity} mB lava");
		if (_active)
			lines.Add($"Pumping at depth {_lastDepth} ({RecipeStatusText.FormatProgressSeconds(_progress, TicksPerPump)})");
		else if (!_isWorkingEnabled)
			lines.Add("Disabled");
		else
			lines.Add("Idle: no source liquid in range");
	}
}
