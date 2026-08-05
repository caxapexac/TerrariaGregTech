#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Steam;

public class LargeBoilerMachine : WorkableMultiblockMachine
{
	protected override string Label => Definition?.Label ?? "Large Boiler";

	private const int TicksPerSteamGeneration = 5;

	public const int SteamPerWater = 160;

	public int CurrentTemperature { get; private set; }
	public int Throttle           { get; private set; } = 100;
	public int SteamGenerated     { get; private set; }

	public int MaxTemperature => Definition?.BoilerMaxTemperature ?? 800;
	public int HeatSpeed      => Definition?.BoilerHeatSpeed      ?? 4;

	public LargeBoilerMachine() : base() { }

	protected override RecipeLogic CreateRecipeLogic() => new LargeBoilerRecipeLogic();

	protected override void OnTick()
	{
		base.OnTick();
		if (!IsServer || !IsFormed) return;
		long t = GetMcOffsetTimer();
		if (Recipe.IsWorking())
		{
			if (t % 10 == 0 && CurrentTemperature < MaxTemperature)
				CurrentTemperature = System.Math.Min(MaxTemperature, CurrentTemperature + HeatSpeed * 10);
		}
		else if (CurrentTemperature > 0)
		{
			CurrentTemperature -= GetCoolDownRate();
			if (CurrentTemperature < 0) CurrentTemperature = 0;
		}

		if (t % TicksPerSteamGeneration != 0) return;

		int maxDrain = CurrentTemperature * Throttle * TicksPerSteamGeneration / (SteamPerWater * 100);
		if (CurrentTemperature < 100)
		{
			SteamGenerated = 0;
			return;
		}
		if (maxDrain <= 0) return;

		int waterRemaining = maxDrain;
		int tanksSeen = 0;
		foreach (var tank in CollectFluidTanks(IO.IN, IO.BOTH))
		{
			tanksSeen++;
			if (waterRemaining <= 0) break;
			var probe = new FluidStack(FluidRegistry.Water, waterRemaining);
			var drained = (tank is NotifiableFluidTank nft)
				? nft.DrainInternal(probe, simulate: false)
				: tank.Drain(probe, simulate: false);
			if (drained.IsEmpty) continue;
			waterRemaining -= drained.Amount;
		}
		int waterDrained = maxDrain - waterRemaining;
		SteamGenerated = waterDrained * SteamPerWater;

		if (tanksSeen == 0) return;

		if (waterDrained > 0)
		{
			int steamRemaining = SteamGenerated;
			foreach (var tank in CollectFluidTanks(IO.OUT, IO.BOTH))
			{
				if (steamRemaining <= 0) break;
				var probe = new FluidStack(FluidRegistry.Steam, steamRemaining);
				int filled = (tank is NotifiableFluidTank nft)
					? nft.FillInternal(probe, simulate: false)
					: tank.Fill(probe, simulate: false);
				if (filled <= 0) continue;
				steamRemaining -= filled;
			}
		}

		if (waterDrained < maxDrain)
			ExplodeBoiler();
	}

	private IEnumerable<IFluidHandler> CollectFluidTanks(params IO[] directions)
	{
		foreach (var direction in directions)
		{
			if (!CapabilitiesFlat.TryGetValue(direction, out var byCap)) continue;
			if (!byCap.TryGetValue(FluidRecipeCapability.CAP, out var handlers)) continue;
			foreach (var h in handlers)
				if (h is IFluidHandler tank)
					yield return tank;
		}
	}

	protected virtual int GetCoolDownRate() => 1;

	public void SetThrottle(int newThrottle)
	{
		newThrottle = System.Math.Clamp(newThrottle, 25, 100);
		if (newThrottle == Throttle) return;
		if (Recipe is LargeBoilerRecipeLogic lbrl)
			lbrl.OnThrottleChanged(Throttle, newThrottle);
		Throttle = newThrottle;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["lb_temp"] = CurrentTemperature;
		tag["lb_throttle"] = Throttle;
		tag["lb_steam"] = SteamGenerated;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("lb_temp"))     CurrentTemperature = tag.GetInt("lb_temp");
		if (tag.ContainsKey("lb_throttle")) Throttle           = tag.GetInt("lb_throttle");
		if (tag.ContainsKey("lb_steam"))    SteamGenerated     = tag.GetInt("lb_steam");
		if (Throttle < 25 || Throttle > 100) Throttle = 100;
	}

	public override void NetSend(System.IO.BinaryWriter writer)
	{
		base.NetSend(writer);
		writer.Write((short)CurrentTemperature);
		writer.Write((byte)Throttle);
		writer.Write((short)SteamGenerated);
	}

	public override void NetReceive(System.IO.BinaryReader reader)
	{
		base.NetReceive(reader);
		CurrentTemperature = reader.ReadInt16();
		Throttle           = reader.ReadByte();
		SteamGenerated     = reader.ReadInt16();
	}

	private void ExplodeBoiler()
	{
		if (!IsServer) return;
		Common.Machine.Trait.EnvironmentalExplosionTrait.DoExplosionAt(this, 2.0f);
	}

	public class LargeBoilerRecipeLogic : RecipeLogic
	{
		private int _currentThrottle = 100;

		public override void SetupRecipe(Api.Recipe.GTRecipe recipe)
		{
			base.SetupRecipe(recipe);
			if (_lastRecipe != null && Machine is LargeBoilerMachine boiler)
			{
				_currentThrottle = boiler.Throttle;
				_duration = (int)System.Math.Round(_lastRecipe.Duration / (_currentThrottle / 100.0));
			}
		}

		public void OnThrottleChanged(int oldThrottle, int newThrottle)
		{
			if (_lastRecipe == null) return;
			double mult = (double)oldThrottle / newThrottle;
			_duration = (int)System.Math.Round(_lastRecipe.Duration / (newThrottle / 100.0));
			_progress = (int)System.Math.Round(_progress * mult);
			_currentThrottle = newThrottle;
		}

		public override void Save(TagCompound tag)
		{
			base.Save(tag);
			tag["currentThrottle"] = _currentThrottle;
		}

		public override void Load(TagCompound tag)
		{
			base.Load(tag);
			if (tag.ContainsKey("currentThrottle")) _currentThrottle = tag.GetInt("currentThrottle");
		}
	}
}
