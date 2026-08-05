#nullable enable
using System;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public class RotorHolderPartMachine : TieredPartMachine
{
	public const int SPEED_INCREMENT = 1;
	public const int SPEED_DECREMENT = 3;

	protected override string Label => "Rotor Holder";

	public NotifiableItemStackHandler? Inventory { get; protected set; }
	public int MaxRotorHolderSpeed { get; protected set; }
	public int RotorSpeed          { get; protected set; }
	public Material? RotorMaterial { get; protected set; }

	public override Api.Capability.IItemHandler? ExposedItemHandler => Inventory;

	private TickableSubscription? _rotorSpeedSubs;
	private ISubscription? _rotorInvSubs;

	public RotorHolderPartMachine() : base() { }

	public void Configure(int tier)
	{
		Tier = tier;
		MaxRotorHolderSpeed = 2000 + 1000 * tier;
		EnsureInventory();
		UpdateRotorSubscription();
	}

	protected override void OnDefinitionBound()
	{
		base.OnDefinitionBound();
		Configure((int)((MetaMachine)this).Tier);
	}

	private void EnsureInventory()
	{
		if (Inventory != null) return;
		Inventory = new NotifiableItemStackHandler(1, IO.NONE, IO.BOTH);
		Traits.Attach(Inventory);
		Traits.RegisterPersistent("Inventory", Inventory);
		_rotorInvSubs = Inventory.AddChangedListener(OnRotorInventoryChanged);
	}

	private void OnRotorInventoryChanged()
	{
		RotorMaterial = RotorStats.Material(GetRotorStack());
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public override void OnKill()
	{
		base.OnKill();
		_rotorInvSubs?.Unsubscribe();
		_rotorInvSubs = null;
		_rotorSpeedSubs?.Unsubscribe();
		_rotorSpeedSubs = null;
	}

	public bool CanShared() => false;

	public bool HasRotor() => Inventory != null && !Inventory.GetSlot(0).IsAir;

	public override Item[]? GetSlotGroup(TerrariaCompat.Machine.SlotGroup group)
	{
		if (group == TerrariaCompat.Machine.SlotGroup.RotorSlot && Inventory != null)
			return Inventory.Storage.Stacks;
		return base.GetSlotGroup(group);
	}

	public override void NotifySlotGroupChanged(TerrariaCompat.Machine.SlotGroup group)
	{
		if (group == TerrariaCompat.Machine.SlotGroup.RotorSlot)
		{
			Inventory?.OnContentsChanged();
			return;
		}
		base.NotifySlotGroupChanged(group);
	}

	public Item GetRotorStack() => Inventory?.GetSlot(0) ?? new Item();

	public void SetRotorSpeed(int rotorSpeed)
	{
		RotorSpeed = rotorSpeed;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	protected void UpdateRotorSubscription()
	{
		if (RotorSpeed > 0)
			_rotorSpeedSubs ??= SubscribeServerTick(UpdateRotorSpeedTick);
		else if (_rotorSpeedSubs != null)
		{
			_rotorSpeedSubs.Unsubscribe();
			_rotorSpeedSubs = null;
		}
	}

	private void UpdateRotorSpeedTick()
	{
		foreach (var c in GetControllers())
		{
			if (c is IWorkableMultiController wmc && wmc.GetRecipeLogic().IsWorking())
				return;
			break;
		}
		if (!HasRotor()) SetRotorSpeed(0);
		else if (RotorSpeed > 0) SetRotorSpeed(Math.Max(0, RotorSpeed - SPEED_DECREMENT));
		UpdateRotorSubscription();
	}

	public override bool OnWorking(IWorkableMultiController controller)
	{
		if (RotorSpeed < MaxRotorHolderSpeed)
		{
			SetRotorSpeed(RotorSpeed + SPEED_INCREMENT);
			UpdateRotorSubscription();
		}
		if (GetMcOffsetTimer() % 20 == 0)
		{
			int numMaintenance = 0;
			foreach (var c in GetControllers())
			{
				if (c is IMaintenanceMachine maint) numMaintenance = maint.GetNumMaintenanceProblems();
				break;
			}
			DamageRotor(1 + numMaintenance);
		}
		return true;
	}

	public override GTRecipe? ModifyRecipe(GTRecipe recipe)
	{
		if (!HasRotor()) return null;
		return recipe;
	}

	public int GetRotorEfficiency()        => RotorStats.Efficiency(GetRotorStack());
	public int GetRotorPower()             => RotorStats.Power(GetRotorStack());
	public int GetRotorDurabilityPercent() => RotorStats.DurabilityPercent(GetRotorStack());
	public void DamageRotor(int amount)    => RotorStats.ApplyDamage(GetRotorStack(), amount);

	public int GetTierDifference()
	{
		foreach (var c in GetControllers())
		{
			if (c is Api.Machine.Feature.ITieredMachine tm) return Tier - tm.GetTier();
			return -1;
		}
		return -1;
	}

	public int GetHolderEfficiency()
	{
		int diff = GetTierDifference();
		return diff == -1 ? -1 : 100 + 10 * diff;
	}

	public int GetHolderPowerMultiplier()
	{
		int diff = GetTierDifference();
		return diff == -1 ? -1 : (int)Math.Pow(2, diff);
	}

	public int GetTotalEfficiency()
	{
		int rotorEff  = GetRotorEfficiency();   if (rotorEff  == -1) return -1;
		int holderEff = GetHolderEfficiency();  if (holderEff == -1) return -1;
		return Math.Max(100, rotorEff * holderEff / 100);
	}

	public int GetTotalPower() => GetHolderPowerMultiplier() * GetRotorPower();

	public bool IsRotorSpinning() => RotorSpeed > 0;

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["rotorSpeed"] = RotorSpeed;
		tag["rotorMaterial"] = RotorMaterial?.Id ?? "";
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		MaxRotorHolderSpeed = 2000 + 1000 * Tier;
		RotorSpeed          = tag.GetInt("rotorSpeed");
		var matId           = tag.GetString("rotorMaterial");
		RotorMaterial       = string.IsNullOrEmpty(matId) ? null : MaterialRegistry.Get(matId);
		EnsureInventory();
		Traits.Load(tag);
		UpdateRotorSubscription();
	}
}

internal static class RotorStats
{
	public static int Efficiency(Item stack) =>
		stack.ModItem is TurbineRotorItem r ? r.Efficiency : -1;

	public static int Power(Item stack) =>
		stack.ModItem is TurbineRotorItem r ? r.Power : -1;

	public static int DurabilityPercent(Item _) => -1;

	public static void ApplyDamage(Item _, int _amount) { }

	public static Material? Material(Item stack) =>
		stack.ModItem is TurbineRotorItem r ? r.Material : null;
}
