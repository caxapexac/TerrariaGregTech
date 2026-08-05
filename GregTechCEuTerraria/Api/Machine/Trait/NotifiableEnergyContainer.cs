#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Machine;
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Capability.Recipe;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.Api.Machine.Trait;

public class NotifiableEnergyContainer
	: NotifiableRecipeHandlerTrait<Api.Recipe.Ingredient.EnergyStack>, IEnergyContainer
{
	public static readonly MachineTraitType<NotifiableEnergyContainer> TYPE = new(allowMultipleInstances: true);

	public override MachineTraitType TraitType => TYPE;

	public IO HandlerIO { get; protected set; }

	protected long _energyStored;
	public virtual long EnergyStored => _energyStored;

	protected long _energyCapacity;
	public virtual long EnergyCapacity { get => _energyCapacity; protected set => _energyCapacity = value; }
	public long InputVoltage   { get; private set; }
	public long InputAmperage  { get; private set; }
	public long OutputVoltage  { get; private set; }
	public long OutputAmperage { get; private set; }

	public Predicate<IODirection>? SideInputCondition  { get; set; }
	public Predicate<IODirection>? SideOutputCondition { get; set; }

	protected long _amps;
	protected long _lastTimeStamp;

	protected TickableSubscription? _outputSubs;
	protected TickableSubscription? _updateSubs;

	protected long _lastEnergyInputPerSec  = 0;
	protected long _lastEnergyOutputPerSec = 0;
	protected long _energyInputPerSec      = 0;
	protected long _energyOutputPerSec     = 0;

	public NotifiableEnergyContainer(long maxCapacity,
	                                 long maxInputVoltage, long maxInputAmperage,
	                                 long maxOutputVoltage, long maxOutputAmperage)
	{
		_lastTimeStamp  = long.MinValue;
		EnergyCapacity  = maxCapacity;
		InputVoltage    = maxInputVoltage;
		InputAmperage   = maxInputAmperage;
		OutputVoltage   = maxOutputVoltage;
		OutputAmperage  = maxOutputAmperage;
		bool isIn  = (InputVoltage  != 0 && InputAmperage  != 0);
		bool isOut = (OutputVoltage != 0 && OutputAmperage != 0);
		HandlerIO = (isIn && isOut) ? IO.BOTH : isIn ? IO.IN : isOut ? IO.OUT : IO.NONE;
	}

	public static NotifiableEnergyContainer EmitterContainer(long maxCapacity,
	                                                          long maxOutputVoltage, long maxOutputAmperage)
		=> new(maxCapacity, 0L, 0L, maxOutputVoltage, maxOutputAmperage);

	public static NotifiableEnergyContainer ReceiverContainer(long maxCapacity,
	                                                           long maxInputVoltage, long maxInputAmperage)
		=> new(maxCapacity, maxInputVoltage, maxInputAmperage, 0L, 0L);

	public void ResetBasicInfo(long maxCapacity, long maxInputVoltage, long maxInputAmperage,
	                           long maxOutputVoltage, long maxOutputAmperage)
	{
		EnergyCapacity = maxCapacity;
		InputVoltage   = maxInputVoltage;
		InputAmperage  = maxInputAmperage;
		OutputVoltage  = maxOutputVoltage;
		OutputAmperage = maxOutputAmperage;
		bool isIn  = (InputVoltage  != 0 && InputAmperage  != 0);
		bool isOut = (OutputVoltage != 0 && OutputAmperage != 0);
		HandlerIO = (isIn && isOut) ? IO.BOTH : isIn ? IO.IN : isOut ? IO.OUT : IO.NONE;
		CheckOutputSubscription();
	}

	public long GetInputPerSec()  => _lastEnergyInputPerSec;
	public long GetOutputPerSec() => _lastEnergyOutputPerSec;

	public void SetEnergyStored(long energyStored)
	{
		if (_energyStored == energyStored) return;
		if (energyStored > _energyStored)
			_energyInputPerSec  += energyStored - _energyStored;
		else
			_energyOutputPerSec += _energyStored - energyStored;
		_energyStored = energyStored;
		CheckOutputSubscription();
		NotifyListeners();
	}

	public override void OnMachineLoad()
	{
		base.OnMachineLoad();
		CheckOutputSubscription();
		_updateSubs = SubscribeServerTick(_updateSubs, UpdateTick);
	}

	public override void OnMachineUnload()
	{
		base.OnMachineUnload();
		if (_updateSubs is not null)
		{
			_updateSubs.Unsubscribe();
			_updateSubs = null;
		}
	}

	public virtual void CheckOutputSubscription()
	{
		if (OutputVoltage > 0 && OutputAmperage > 0)
		{
			if (_energyStored >= OutputVoltage)
				_outputSubs = SubscribeServerTick(_outputSubs, ServerTick);
			else if (_outputSubs is not null)
			{
				_outputSubs.Unsubscribe();
				_outputSubs = null;
			}
		}
	}

	private void UpdateTick()
	{
		if (Main.GameUpdateCount % global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(20) == 0)
		{
			_lastEnergyOutputPerSec = _energyOutputPerSec;
			_lastEnergyInputPerSec  = _energyInputPerSec;
			_energyOutputPerSec     = 0;
			_energyInputPerSec      = 0;
		}
	}

	protected virtual void ServerTick()
	{
		if (MetaMachine.IsClient) return;
		if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) != 0) return;

		if (_energyStored >= OutputVoltage && OutputVoltage > 0 && OutputAmperage > 0)
		{
			long outputVoltage  = OutputVoltage;
			long outputAmperes  = Math.Min(_energyStored / outputVoltage, OutputAmperage);
			if (outputAmperes == 0) return;
			long amperesUsed = 0;
			foreach (var (side, neighbor) in MachineCellResolver.PerimeterNeighbors(Machine))
			{
				if (!OutputsEnergy(side)) continue;
				var oppositeSide = side.Opposite();
				var energyContainer = neighbor.Traits.GetTrait<NotifiableEnergyContainer>(TYPE);
				if (energyContainer != null && energyContainer.InputsEnergy(oppositeSide))
				{
					amperesUsed += energyContainer.AcceptEnergyFromNetwork(
						oppositeSide, outputVoltage, outputAmperes - amperesUsed);
					if (amperesUsed >= outputAmperes) break;
				}
			}
			if (amperesUsed > 0)
				SetEnergyStored(_energyStored - amperesUsed * outputVoltage);
		}
	}

	public virtual long AcceptEnergyFromNetwork(IODirection side, long voltage, long amperage)
	{
		long latestTimeStamp = Main.GameUpdateCount;
		if (_lastTimeStamp < latestTimeStamp)
		{
			_amps = 0;
			_lastTimeStamp = latestTimeStamp;
		}
		if (_amps >= InputAmperage) return 0;
		long canAccept = EnergyCapacity - _energyStored;
		if (voltage > 0L && InputsEnergy(side))
		{
			if (voltage > InputVoltage)
			{
				var explodable = Machine.Traits.GetTrait(
					Common.Machine.Trait.EnvironmentalExplosionTrait.TYPE);
				if (explodable != null)
				{
					Terraria.ModLoader.ModContent.GetInstance<GregTechCEuTerraria>()
						?.Logger?.Warn(
							$"[overvoltage] {Machine?.GetType().Name ?? "?"} at " +
							$"({Machine?.Position.X},{Machine?.Position.Y}) - pushed {voltage} V " +
							$"vs InputVoltage {InputVoltage} V (side {side}, amperage {amperage}) - EXPLODING");
					explodable.DoExplosion(
						Common.Machine.Trait.EnvironmentalExplosionTrait.GetExplosionPower(voltage));
				}
				return Math.Min(amperage, InputAmperage - _amps);
			}
			if (canAccept >= voltage)
			{
				long amperesAccepted = Math.Min(canAccept / voltage,
				                                 Math.Min(amperage, InputAmperage - _amps));
				if (amperesAccepted > 0)
				{
					SetEnergyStored(_energyStored + voltage * amperesAccepted);
					_amps += amperesAccepted;
					return amperesAccepted;
				}
			}
		}
		return 0;
	}

	public virtual bool InputsEnergy(IODirection side) =>
		!OutputsEnergy(side) && InputVoltage > 0 &&
		(SideInputCondition == null || SideInputCondition(side));

	public virtual bool OutputsEnergy(IODirection side) =>
		OutputVoltage > 0 && (SideOutputCondition == null || SideOutputCondition(side));

	public virtual long GetPushAmperage()
	{
		long v = OutputVoltage;
		if (v <= 0) return 0;
		return System.Math.Min(OutputAmperage, _energyStored / v);
	}

	public virtual void OnEnergyPushedToNetwork(long amps, long voltage)
	{
		long drained = amps * voltage;
		if (drained <= 0) return;
		ChangeEnergy(-drained);
	}

	public long ChangeEnergy(long energyToAdd)
	{
		long oldEnergyStored = _energyStored;
		long newEnergyStored = (EnergyCapacity - oldEnergyStored < energyToAdd)
			? EnergyCapacity
			: (oldEnergyStored + energyToAdd);
		if (newEnergyStored < 0) newEnergyStored = 0;
		SetEnergyStored(newEnergyStored);
		return newEnergyStored - oldEnergyStored;
	}

	public long AddEnergy(long energyToAdd)       => ChangeEnergy(energyToAdd);
	public long RemoveEnergy(long energyToRemove) => -ChangeEnergy(-energyToRemove);
	public long GetEnergyCanBeInserted()          => EnergyCapacity - _energyStored;


	public bool DischargeOrRechargeEnergyContainers(Item[] slots, int slotIndex, bool simulate)
	{
		if (slotIndex < 0 || slotIndex >= slots.Length) return false;
		var stackInSlot = slots[slotIndex];
		if (stackInSlot is null || stackInSlot.IsAir) return false;

		var electricItem = stackInSlot.ModItem as IElectricItem;
		if (electricItem != null)
		{
			if (HandleElectricItem(electricItem, simulate))
				return true;
		}
		return false;
	}

	private bool HandleElectricItem(IElectricItem electricItem, bool simulate)
	{
		int machineTier   = (int)VoltageTiers.MinTierForVoltage(Math.Max(InputVoltage, OutputVoltage));
		int chargeTier    = Math.Min(machineTier, electricItem.GetTier());
		double chargePct  = EnergyCapacity > 0 ? (double)_energyStored / EnergyCapacity : 0.0;

		if (electricItem.CanProvideChargeExternally() && GetEnergyCanBeInserted() > 0)
		{
			if (chargePct <= 0.33 && chargeTier == machineTier)
			{
				long dischargedBy = electricItem.Discharge(GetEnergyCanBeInserted(),
					machineTier, ignoreTransferLimit: false, externally: true, simulate);
				if (!simulate)
					AddEnergy(dischargedBy);
				return dischargedBy > 0L;
			}
		}

		if (chargePct > 0.66)
		{
			long chargedBy = electricItem.Charge(_energyStored, chargeTier,
				ignoreTransferLimit: false, simulate: false);
			if (!simulate)
				RemoveEnergy(chargedBy);
			return chargedBy > 0;
		}
		return false;
	}

	public override IO GetHandlerIO() => HandlerIO;

	public override List<Api.Recipe.Ingredient.EnergyStack>? HandleRecipeInner(
		IO io, GTRecipe recipe, List<Api.Recipe.Ingredient.EnergyStack> left, bool simulate)
	{
		for (int i = left.Count - 1; i >= 0; i--)
		{
			var stack = left[i];
			if (stack.IsEmpty()) { left.RemoveAt(i); continue; }

			long totalEU = stack.GetTotalEU();
			long canTransfer = Math.Min(totalEU,
				io == IO.IN ? EnergyStored : EnergyCapacity - EnergyStored);
			if (!simulate)
			{
				ChangeEnergy(io == IO.IN ? -canTransfer : canTransfer);
			}

			totalEU -= canTransfer;
			if (totalEU <= 0)
				left.RemoveAt(i);
			else
				left[i] = new Api.Recipe.Ingredient.EnergyStack(totalEU);
		}
		return left.Count == 0 ? null : left;
	}

	public override IReadOnlyList<object> GetContents()
	{
		long amperage = Math.Max(InputAmperage, OutputAmperage);
		return new object[] { new Api.Recipe.Ingredient.EnergyStack(EnergyStored, Math.Max(1, amperage)) };
	}

	public override double GetTotalContentAmount() => _energyStored;

	public override Api.Capability.Recipe.RecipeCapability<Api.Recipe.Ingredient.EnergyStack>
		GetCapability() => Api.Capability.Recipe.EURecipeCapability.CAP;

	public override void Save(TagCompound tag)
	{
		tag["energyStored"] = _energyStored;
	}

	public override void Load(TagCompound tag)
	{
		if (tag.ContainsKey("energyStored"))
			_energyStored = tag.GetLong("energyStored");
	}

	public override void SaveForSync(TagCompound tag) { }

	public void SetStoredFromSync(long energy) => _energyStored = energy;

}
