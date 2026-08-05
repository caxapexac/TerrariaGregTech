#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines.BatteryBuffers;

public class BatteryBufferMachine : TieredEnergyMachine, IControllable
{
	public BatteryBufferMachine() { }
	public BatteryBufferMachine(VoltageTier tier) : base(tier) { }

	protected override string Label => Definition?.Label ?? "Battery Buffer";

	public const long AMPS_PER_BATTERY_NORMAL  = 2L;
	public const long AMPS_PER_BATTERY_CHARGER = 4L;

	public enum State { IDLE, RUNNING, FINISHED }

	public virtual int  SlotCount        => Definition?.BatterySlotCount ?? 0;
	public virtual long InputAmpsPerItem => Definition?.InputAmpsPerItem  ?? AMPS_PER_BATTERY_NORMAL;
	public virtual long OutputAmps       => Definition?.OutputAmps        ?? 0;

	public const IODirection OutputFace = IODirection.Up;
	public const IODirection InputFace  = IODirection.Down;

	public override IODirection EnergyFaceForCell(int cx, int cy) =>
		cy == Position.Y ? OutputFace : InputFace;

	private bool   _isWorkingEnabled = true;
	private State  _state            = State.IDLE;

	bool IControllable.IsWorkingEnabled() => _isWorkingEnabled;
	void IControllable.SetWorkingEnabled(bool enabled)
	{
		_isWorkingEnabled = enabled;
		(EnergyContainer as EnergyBatteryTrait)?.CheckOutputSubscription();
	}

	public State CurrentState => _state;

	internal void ChangeState(State newState)
	{
		if (_state == newState) return;
		_state = newState;
	}

	private Item[]? _batteryInv;
	public Item[] BatteryInv
	{
		get
		{
			if (_batteryInv is null)
			{
				_batteryInv = new Item[SlotCount];
				for (int i = 0; i < SlotCount; i++) _batteryInv[i] = new Item();
			}
			return _batteryInv;
		}
	}

	public override Item[]? GetSlotGroup(SlotGroup group) => group switch
	{
		SlotGroup.Inventory => BatteryInv,
		_ => base.GetSlotGroup(group),
	};

	internal List<IElectricItem> GetNonFullBatteries()
	{
		var result = new List<IElectricItem>();
		var inv = BatteryInv;
		for (int i = 0; i < inv.Length; i++)
		{
			if (inv[i].IsAir) continue;
			if (inv[i].ModItem is not IElectricItem e) continue;
			if (e.GetCharge() < e.GetMaxCharge()) result.Add(e);
		}
		return result;
	}

	internal List<IElectricItem> GetNonEmptyBatteries()
	{
		var result = new List<IElectricItem>();
		var inv = BatteryInv;
		for (int i = 0; i < inv.Length; i++)
		{
			if (inv[i].IsAir) continue;
			if (inv[i].ModItem is not IElectricItem e) continue;
			if (e.CanProvideChargeExternally() && e.GetCharge() > 0) result.Add(e);
		}
		return result;
	}

	internal List<IElectricItem> GetAllBatteries()
	{
		var result = new List<IElectricItem>();
		var inv = BatteryInv;
		for (int i = 0; i < inv.Length; i++)
		{
			if (inv[i].IsAir) continue;
			if (inv[i].ModItem is IElectricItem e) result.Add(e);
		}
		return result;
	}

	public override bool CanAccept  => true;
	public override bool CanExtract => OutputAmps > 0;

	public override long EnergyCapacity => EnergyContainer.EnergyCapacity;

	protected override NotifiableEnergyContainer CreateEnergyContainer()
		=> new EnergyBatteryTrait(SlotCount, InputAmpsPerItem, OutputAmps, Tier);

	public override bool HasSyncEnergy => false;

	public override long AcceptEnergy(long amount, VoltageTier sourceTier)
	{
		var trait = EnergyContainer as EnergyBatteryTrait;
		if (trait is null) return 0;
		long voltage  = VoltageTiers.Voltage(sourceTier);
		long amperage = voltage > 0 ? Math.Max(1, amount / voltage) : 0;
		if (amperage <= 0) return 0;
		long usedAmps = trait.AcceptEnergyFromNetwork(IODirection.Up, voltage, amperage);
		return usedAmps * voltage;
	}

	internal override void SystemTick()
	{
		base.SystemTick();
		if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) == 0)
			(EnergyContainer as EnergyBatteryTrait)?.RepayInternalStorageDebt();
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["isWorkingEnabled"] = _isWorkingEnabled;
		var inv = BatteryInv;
		var list = new List<TagCompound>(inv.Length);
		for (int i = 0; i < inv.Length; i++)
			list.Add(ItemIO.Save(inv[i]));
		tag["BatteryInv"] = list;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		_isWorkingEnabled = !tag.ContainsKey("isWorkingEnabled") || tag.GetBool("isWorkingEnabled");

		string? invKey = tag.ContainsKey("BatteryInv") ? "BatteryInv"
		             : (tag.ContainsKey("chargerInventory") ? "chargerInventory" : null);
		if (invKey != null)
		{
			var list = tag.GetList<TagCompound>(invKey);
			var inv = BatteryInv;
			for (int i = 0; i < inv.Length && i < list.Count; i++)
				inv[i] = ItemIO.Load(list[i]);
		}
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add($"Battery slots: {SlotCount}");
		if (OutputAmps > 0)
		{
			lines.Add($"Input (bottom): {EnergyContainer.InputAmperage}A at {EnergyContainer.InputVoltage:N0} EU/t");
			lines.Add($"Output (top): {OutputAmps}A at {EnergyContainer.OutputVoltage:N0} EU/t");
			lines.Add("Output needs a wire (won't power a touching machine)");
		}
		else
		{
			lines.Add($"Input (any side): {EnergyContainer.InputAmperage}A at {EnergyContainer.InputVoltage:N0} EU/t");
		}
		lines.Add($"State: {_state}");
	}

	public sealed class EnergyBatteryTrait : NotifiableEnergyContainer
	{
		private readonly VoltageTier _tier;
		private readonly long _inputAmpsPerItem;

		public EnergyBatteryTrait(int inventorySize, long inputAmpsPerItem, long outputAmps, VoltageTier tier)
			: base(VoltageTiers.Voltage(tier) * inventorySize * 32L,         // maxCapacity
			       VoltageTiers.Voltage(tier),                                // maxInputVoltage
			       inventorySize * inputAmpsPerItem,                          // maxInputAmperage
			       outputAmps == 0 ? 0 : VoltageTiers.Voltage(tier),          // maxOutputVoltage
			       outputAmps)                                                // maxOutputAmperage
		{
			_tier = tier;
			_inputAmpsPerItem = inputAmpsPerItem;
			SideInputCondition  = s => Buffer.WorkingEnabled &&
			                           (Buffer.OutputAmps <= 0 || s == BatteryBufferMachine.InputFace);
			SideOutputCondition = s => Buffer.WorkingEnabled && s == BatteryBufferMachine.OutputFace;
		}

		public override void Save(TagCompound tag) { }
		public override void Load(TagCompound tag) => _energyStored = 0;

		private BatteryBufferMachine Buffer => (BatteryBufferMachine)Machine;

		protected override IReadOnlyList<Type> ValidMachineClasses() =>
			new[] { typeof(BatteryBufferMachine) };

		public override void CheckOutputSubscription()
		{
			if (Buffer.WorkingEnabled)
			{
				base.CheckOutputSubscription();
			}
			else if (_outputSubs is not null)
			{
				_outputSubs.Unsubscribe();
				_outputSubs = null;
			}
		}

		protected override void ServerTick()
		{
			if (MetaMachine.IsClient) return;
			if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) != 0) return;

			long voltage = OutputVoltage;
			if (voltage <= 0 || OutputAmperage <= 0) return;
			if (!Buffer.WorkingEnabled) return;

			var batteries = Buffer.GetNonEmptyBatteries();
			if (batteries.Count == 0) return;

			long internalAmps = Math.Abs(Math.Min(0, _energyStored / voltage));
			long genAmps      = Math.Max(0, batteries.Count - internalAmps);
			long outAmps      = 0L;

			if (genAmps > 0)
			{
				long remaining = genAmps;
				foreach (var (side, neighbor) in MachineCellResolver.PerimeterNeighbors(Machine))
				{
					if (!OutputsEnergy(side)) continue;
					if (remaining <= 0) break;
					var opposite = side.Opposite();
					var nc = neighbor.Traits.GetTrait<NotifiableEnergyContainer>(TYPE);
					if (nc is null || !nc.InputsEnergy(opposite)) continue;
					long accepted = nc.AcceptEnergyFromNetwork(opposite, voltage, remaining);
					outAmps   += accepted;
					remaining -= accepted;
				}
				if (outAmps == 0 && internalAmps == 0) return;
			}

			long energy      = (outAmps + internalAmps) * voltage;
			long distributed = energy / batteries.Count;

			bool changed = false;
			foreach (var b in batteries)
			{
				long charged = b.Discharge(distributed, (int)_tier,
					ignoreTransferLimit: false, externally: true, simulate: false);
				if (charged > 0) changed = true;
				energy -= charged;
				_energyOutputPerSec += charged;
			}

			if (changed)
			{
				Buffer.ChangeState(State.RUNNING);
				CheckOutputSubscription();
			}

			SetEnergyStored(_energyStored + internalAmps * voltage - energy);
		}

		public override long AcceptEnergyFromNetwork(IODirection side, long voltage, long amperage)
		{
			long latestTimeStamp = Main.GameUpdateCount;
			if (_lastTimeStamp < latestTimeStamp)
			{
				_amps = 0;
				_lastTimeStamp = latestTimeStamp;
			}
			if (amperage <= 0 || voltage <= 0)
			{
				Buffer.ChangeState(State.IDLE);
				return 0;
			}

			var batteries = Buffer.GetNonFullBatteries();
			long leftAmps = batteries.Count * _inputAmpsPerItem - _amps;
			long usedAmps = Math.Min(leftAmps, amperage);
			if (leftAmps <= 0) return 0;

			if (SideInputCondition == null || SideInputCondition(side))
			{
				if (voltage > InputVoltage)
				{
					ExplodeOnOvervoltage(voltage);
					return usedAmps;
				}

				long internalAmps = Math.Min(leftAmps, Math.Max(0, _energyStored / voltage));
				usedAmps          = Math.Min(usedAmps, leftAmps - internalAmps);
				_amps            += usedAmps;

				long energy      = (usedAmps + internalAmps) * voltage;
				long distributed = batteries.Count > 0 ? energy / batteries.Count : 0;

				bool changed = false;
				foreach (var b in batteries)
				{
					long cap     = VoltageTiers.Voltage((VoltageTier)b.GetTier()) * _inputAmpsPerItem;
					long charged = b.Charge(Math.Min(distributed, cap), (int)_tier,
						ignoreTransferLimit: true, simulate: false);
					if (charged > 0) changed = true;
					energy -= charged;
					_energyInputPerSec += charged;
				}

				if (changed)
				{
					Buffer.ChangeState(State.RUNNING);
					CheckOutputSubscription();
				}

				SetEnergyStored(_energyStored - internalAmps * voltage + energy);
				return usedAmps;
			}
			return 0;
		}

		public override long EnergyCapacity
		{
			get
			{
				long cap = 0;
				foreach (var b in Buffer.GetAllBatteries()) cap += b.GetMaxCharge();
				if (cap == 0) Buffer.ChangeState(State.IDLE);
				return cap;
			}
		}

		public override long EnergyStored
		{
			get
			{
				long stored = 0;
				long cap    = 0;
				foreach (var b in Buffer.GetAllBatteries())
				{
					stored += b.GetCharge();
					cap    += b.GetMaxCharge();
				}
				if (cap != 0 && cap == stored) Buffer.ChangeState(State.FINISHED);
				return stored;
			}
		}

		internal long ComputeAvailableOutputAmps()
		{
			if (!Buffer.WorkingEnabled || OutputVoltage <= 0) return 0;
			long voltage = OutputVoltage;
			long internalAmps = Math.Abs(Math.Min(0, _energyStored / voltage));
			long nonEmpty     = Buffer.GetNonEmptyBatteries().Count;
			return Math.Min(OutputAmperage, Math.Max(0, nonEmpty - internalAmps));
		}

		public override long GetPushAmperage() => ComputeAvailableOutputAmps();

		public override void OnEnergyPushedToNetwork(long amps, long voltage)
			=> DistributeEnergyOut(amps, voltage);

		internal void DistributeEnergyOut(long amps, long voltage)
		{
			if (amps <= 0 || voltage <= 0) return;
			var batteries = Buffer.GetNonEmptyBatteries();
			if (batteries.Count == 0) return;

			long internalAmps = Math.Abs(Math.Min(0, _energyStored / voltage));
			long energy      = (amps + internalAmps) * voltage;
			long distributed = energy / batteries.Count;

			bool changed = false;
			foreach (var b in batteries)
			{
				long charged = b.Discharge(distributed, (int)_tier,
					ignoreTransferLimit: false, externally: true, simulate: false);
				if (charged > 0) changed = true;
				energy -= charged;
				_energyOutputPerSec += charged;
			}
			SetEnergyStored(_energyStored + internalAmps * voltage - energy);
			if (changed) Buffer.ChangeState(State.RUNNING);
		}

		internal void RepayInternalStorageDebt()
		{
			if (_energyStored >= 0) return;
			long voltage = OutputVoltage;
			if (voltage <= 0) return;
			var batteries = Buffer.GetNonEmptyBatteries();
			if (batteries.Count == 0) return;

			long internalAmps = Math.Abs(Math.Min(0, _energyStored / voltage));
			if (internalAmps <= 0) return;

			long energy      = internalAmps * voltage;
			long distributed = energy / batteries.Count;
			foreach (var b in batteries)
			{
				long charged = b.Discharge(distributed, (int)_tier,
					ignoreTransferLimit: false, externally: true, simulate: false);
				energy -= charged;
				_energyOutputPerSec += charged;
			}
			SetEnergyStored(_energyStored + internalAmps * voltage - energy);
		}

		private void ExplodeOnOvervoltage(long voltage) =>
			EnvironmentalExplosionTrait.DoExplosionAt(Machine,
				EnvironmentalExplosionTrait.GetExplosionPower(voltage));
	}
}
