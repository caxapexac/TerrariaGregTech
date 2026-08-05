#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public sealed class BlockBreakerMachine : TieredEnergyMachine, IControllable
{
	public BlockBreakerMachine() { }
	public BlockBreakerMachine(VoltageTier tier) : base(tier) { }

	public enum BreakerMode : byte { MineDown, CutTrees }

	protected override string Label => Definition?.Label ?? "Block Breaker";

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

	public int Range
	{
		get
		{
			double frac = (int)Tier switch
			{
				1 => 0.20,
				2 => 0.35,
				3 => 0.50,
				4 => 0.65,
				5 => 0.80,
				6 => 0.95,
				7 => 1.00,
				_ => 0.20 + 0.15 * ((int)Tier - 1),
			};
			int h = Main.maxTilesY > 0 ? Main.maxTilesY : 1200;
			return (int)(h * frac);
		}
	}

	private int TicksPerTile
	{
		get
		{
			int t = (int)Tier;
			return Math.Max(3, 45 - t * 8);
		}
	}

	private int TicksPerTreeTile => TicksPerTile * 2;

	private int TierRank => (int)Tier - (int)VoltageTier.LV + 1;
	private int CutHalfSpan => 8 * TierRank;
	public int CutWidth => Size.Width + 2 * CutHalfSpan;
	private const int CutBandAbove = 2;
	private const int MaxTreeScanHeight = 60;

	protected override bool HasChargerSlot => true;

	private EnvironmentalExplosionTrait? _explosion;
	private void EnsureTraits()
	{
		if (_explosion is not null) return;
		BindDefinition();
		EnsureEnergyContainer();
		_explosion = Traits.GetTrait<EnvironmentalExplosionTrait>(EnvironmentalExplosionTrait.TYPE);
		_explosion?.SetEnableEnvironmentalExplosions(false);
	}

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		EnsureTraits();
	}

	private bool _isWorkingEnabled = true;
	public bool IsWorkingEnabled() => _isWorkingEnabled;
	public void SetWorkingEnabled(bool enabled) => _isWorkingEnabled = enabled;

	private BreakerMode _mode = BreakerMode.MineDown;
	public BreakerMode Mode => _mode;
	public void SetMode(BreakerMode mode)
	{
		if ((byte)mode > (byte)BreakerMode.CutTrees) mode = BreakerMode.MineDown;
		if (_mode == mode) return;
		_mode = mode;
		_hasTarget = false;
		_progress = 0;
		_active = false;
	}

	private bool _replant = true;
	public bool ReplantEnabled => _replant;
	public void SetReplant(bool enabled) => _replant = enabled;

	private bool _active;
	public override bool IsActive => _active;

	private int _progress;
	private int _targetX;
	private int _targetY;
	private bool _hasTarget;
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

		if (_mode == BreakerMode.CutTrees)
			CutTreesTick();
		else
			MineDownTick();
	}

	private void MineDownTick()
	{
		if (!_hasTarget || !TargetStillValid())
		{
			if (!FindTarget(out _targetX, out _targetY))
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
		if (_progress >= TicksPerTile)
		{
			BreakTarget();
			_progress = 0;
			_hasTarget = false;
		}
	}

	private void CutTreesTick()
	{
		if (!_hasTarget || !TreeTargetStillValid())
		{
			if (!FindTreeTarget(out _targetX, out _targetY))
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
		if (_progress >= TicksPerTreeTile)
		{
			_progress = 0;
			BreakHighestTreeTile(_targetX, _targetY);
			if (!TreeHasTiles(_targetX, _targetY))
			{
				if (_replant)
					ReplantTreeAt(_targetX, _targetY);
				_hasTarget = false;
			}
		}
	}

	private bool TargetStillValid()
	{
		if (_targetX < 0 || _targetX >= Main.maxTilesX || _targetY < 0 || _targetY >= Main.maxTilesY)
			return false;
		var t = Main.tile[_targetX, _targetY];
		return t.HasTile && !IsProtected(t.TileType);
	}

	private bool FindTarget(out int outX, out int outY)
	{
		int startY = Position.Y + Size.Height;
		int endY   = Math.Min(Main.maxTilesY - 1, startY + Range - 1);
		int bestY  = int.MaxValue;
		int bestX  = -1;

		for (int dx = 0; dx < Size.Width; dx++)
		{
			int x = Position.X + dx;
			if (x < 0 || x >= Main.maxTilesX) continue;
			for (int y = startY; y <= endY; y++)
			{
				var t = Main.tile[x, y];
				if (!t.HasTile) continue;
				if (IsProtected(t.TileType)) continue;
				if (y < bestY) { bestY = y; bestX = x; }
				break;
			}
		}

		if (bestX < 0) { outX = outY = 0; return false; }
		outX = bestX; outY = bestY;
		return true;
	}

	private static bool IsProtected(ushort tileType)
	{
		if (Main.tileDungeon[tileType]) return true;
		if (tileType == TileID.LihzahrdBrick) return true;
		if (tileType == TileID.ShimmerBlock) return true;
		if (TileID.Sets.IsAContainer[tileType]) return true;
		return false;
	}

	private void BreakTarget()
	{
		if (_targetX < 0 || _targetX >= Main.maxTilesX || _targetY < 0 || _targetY >= Main.maxTilesY) return;
		var t = Main.tile[_targetX, _targetY];
		if (!t.HasTile) return;

		WorldGen.KillTile(_targetX, _targetY, fail: false, effectOnly: false, noItem: false);
		if (Main.netMode == NetmodeID.Server && !Main.tile[_targetX, _targetY].HasTile)
			NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, _targetX, _targetY);

		_lastDepth = _targetY - (Position.Y + Size.Height) + 1;
	}

	private static bool IsTreeTrunk(ushort type)
		=> TileID.Sets.IsATreeTrunk[type] || type == TileID.PalmTree;

	private bool TreeTargetStillValid() => TreeHasTiles(_targetX, _targetY);

	private bool FindTreeTarget(out int centerX, out int baseY)
	{
		int topRow = Position.Y - CutBandAbove;
		int botRow = Position.Y + Size.Height - 1;
		int span = CutHalfSpan;
		for (int d = 0; d < span; d++)
		{
			if (TryResolveTreeCenter(Position.X - 1 - d, topRow, botRow, out centerX, out baseY)) return true;
			if (TryResolveTreeCenter(Position.X + Size.Width + d, topRow, botRow, out centerX, out baseY)) return true;
		}
		centerX = baseY = 0;
		return false;
	}

	private static bool TryResolveTreeCenter(int col, int topRow, int botRow, out int centerX, out int baseY)
	{
		centerX = baseY = 0;
		if (col < 0 || col >= Main.maxTilesX) return false;
		for (int y = topRow; y <= botRow; y++)
		{
			if (y < 0 || y >= Main.maxTilesY) continue;
			var t = Main.tile[col, y];
			if (!t.HasTile || !IsTreeTrunk(t.TileType)) continue;
			WorldGen.GetTreeBottom(col, y, out int gx, out int gy);
			if (gy - 1 < 0 || !Main.tile[gx, gy].HasTile) return false;
			centerX = gx;
			baseY = gy - 1;
			return true;
		}
		return false;
	}

	private static bool HasTrunkInRow(int centerX, int y)
	{
		if (y < 0 || y >= Main.maxTilesY) return false;
		for (int x = centerX - 1; x <= centerX + 1; x++)
		{
			if (x < 0 || x >= Main.maxTilesX) continue;
			var t = Main.tile[x, y];
			if (t.HasTile && IsTreeTrunk(t.TileType)) return true;
		}
		return false;
	}

	private static int TreeTopRow(int centerX, int baseY)
	{
		int limit = Math.Max(0, baseY - MaxTreeScanHeight);
		int top = baseY + 1;
		for (int y = baseY; y >= limit; y--)
		{
			if (!HasTrunkInRow(centerX, y)) break;
			top = y;
		}
		return top;
	}

	private void BreakHighestTreeTile(int centerX, int baseY)
	{
		int top = TreeTopRow(centerX, baseY);
		if (top > baseY) return;
		for (int x = centerX - 1; x <= centerX + 1; x++)
		{
			if (x < 0 || x >= Main.maxTilesX) continue;
			var t = Main.tile[x, top];
			if (!t.HasTile || !IsTreeTrunk(t.TileType)) continue;
			KillTreeTileAt(x, top);
			return;
		}
	}

	private static bool TreeHasTiles(int centerX, int baseY) => TreeTopRow(centerX, baseY) <= baseY;

	private static void ReplantTreeAt(int centerX, int baseY)
	{
		WorldGen.PlaceTile(centerX, baseY, TileID.Saplings, mute: true, forced: false, plr: -1, style: 0);
		if (Main.netMode == NetmodeID.Server && Main.tile[centerX, baseY].HasTile && Main.tile[centerX, baseY].TileType == TileID.Saplings)
			NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 1, centerX, baseY, TileID.Saplings);
	}

	private static void KillTreeTileAt(int x, int ty)
	{
		if (x < 0 || x >= Main.maxTilesX || ty < 0 || ty >= Main.maxTilesY) return;
		var t = Main.tile[x, ty];
		if (!t.HasTile || !IsTreeTrunk(t.TileType)) return;
		WorldGen.KillTile(x, ty, fail: false, effectOnly: false, noItem: false);
		if (Main.netMode == NetmodeID.Server && !Main.tile[x, ty].HasTile)
			NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, ty);
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
		tag["hasTarget"]        = _hasTarget;
		tag["lastDepth"]        = _lastDepth;
		tag["mode"]             = (byte)_mode;
		tag["replant"]          = _replant;
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
		_hasTarget        = tag.GetBool("hasTarget");
		_lastDepth        = tag.GetInt("lastDepth");
		_mode             = (BreakerMode)tag.GetByte("mode");
		_replant          = !tag.ContainsKey("replant") || tag.GetBool("replant");
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		if (_mode == BreakerMode.CutTrees)
		{
			lines.Add("Mode: Cut Trees");
			lines.Add($"Area: {CutWidth} wide x {Size.Height + CutBandAbove} tall band");
			lines.Add($"Draw: {EnergyPerTick:N0} EU/t");
			if (_active)
				lines.Add($"Cutting trees ({RecipeStatusText.FormatProgressSeconds(_progress, TicksPerTreeTile)})");
			else if (!_isWorkingEnabled)
				lines.Add("Disabled");
			else
				lines.Add("Idle: no trees in range");
			return;
		}

		lines.Add("Mode: Mine Down");
		lines.Add($"Range: {Range} tiles below");
		lines.Add($"Speed: {TicksPerTile} ticks / tile");
		lines.Add($"Draw: {EnergyPerTick:N0} EU/t");
		if (_active)
			lines.Add($"Drilling at depth {_lastDepth} ({RecipeStatusText.FormatProgressSeconds(_progress, TicksPerTile)})");
		else if (!_isWorkingEnabled)
			lines.Add("Disabled");
		else
			lines.Add("Idle: nothing to drill");
	}
}
