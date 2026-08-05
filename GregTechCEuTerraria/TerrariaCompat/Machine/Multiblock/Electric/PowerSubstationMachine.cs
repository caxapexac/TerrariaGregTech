#nullable enable
using System.Collections.Generic;
using System.Numerics;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Multiblock;
using GregTechCEuTerraria.Api.Misc;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

public class PowerSubstationMachine : WorkableMultiblockMachine
{
	public const int  MaxBatteryLayers = 18;
	public const int  MinCasings        = 14;
	public const long PassiveDrainDivisor = 20L * 60 * 60 * 24 * 100;
	public const long PassiveDrainMaxPerStorage = 100_000L;

	private PowerStationEnergyBank? _energyBank;
	private EnergyContainerList?    _inputHatches;
	private EnergyContainerList?    _outputHatches;
	private long _passiveDrain;

	private long _netInLastSec;
	private long _netOutLastSec;
	private long _inputPerSec;
	private long _outputPerSec;

	protected override string Label => Definition?.Label ?? "Power Substation";

	public PowerSubstationMachine() : base() { }

	public PowerStationEnergyBank EnergyBank => _energyBank ??= EnsureEnergyBank();

	private PowerStationEnergyBank EnsureEnergyBank()
	{
		BindDefinition();
		var bank = new PowerStationEnergyBank(System.Array.Empty<IBatteryData>());
		Traits.Attach(bank);
		Traits.RegisterPersistent("substation_bank", bank);
		return bank;
	}

	protected override void OnTick()
	{
		_energyBank ??= EnsureEnergyBank();
		base.OnTick();
		if (!IsServer || !IsFormed) return;
		TransferEnergyTick();
	}

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		_energyBank ??= EnsureEnergyBank();

		var inputs  = new List<IEnergyContainer>();
		var outputs = new List<IEnergyContainer>();
		foreach (var part in GetParts())
		{
			foreach (var rhl in part.GetRecipeHandlers())
			{
				if (!rhl.IsValid(IO.BOTH)) continue;
				List<IEnergyContainer>? containers = null;
				foreach (var h in rhl.GetCapability(EURecipeCapability.CAP))
					if (h is IEnergyContainer ec)
						(containers ??= new List<IEnergyContainer>()).Add(ec);
				if (containers is null) continue;
				if (rhl.HandlerIO.Supports(IO.IN))       inputs.AddRange(containers);
				else if (rhl.HandlerIO.Supports(IO.OUT)) outputs.AddRange(containers);
			}
		}
		_inputHatches  = new EnergyContainerList(inputs);
		_outputHatches = new EnergyContainerList(outputs);

		var batteries = new List<IBatteryData>();
		foreach (var (x, y) in EnumerateFootprintCells())
		{
			var tile = Terraria.Main.tile[x, y];
			if (!tile.HasTile) continue;
			if (Terraria.ModLoader.TileLoader.GetTile(tile.TileType) is not
			    Tiles.Casings.CasingTile casingTile) continue;
			var data = PssBatteryData.Get(casingTile.Name);
			if (data is null) continue;
			if (data.Tier == -1 || data.Capacity <= 0) continue;
			batteries.Add(data);
		}

		if (batteries.Count == 0)
		{
			SetUnformedReason("No filled battery blocks installed");
			OnStructureInvalid();
			return;
		}

		_energyBank.Rebuild(batteries);
		_passiveDrain = _energyBank.GetPassiveDrainPerTick();
	}

	public override void OnStructureInvalid()
	{
		_inputHatches  = null;
		_outputHatches = null;
		_passiveDrain  = 0;
		_netInLastSec  = 0;
		_inputPerSec   = 0;
		_netOutLastSec = 0;
		_outputPerSec  = 0;
		base.OnStructureInvalid();
	}

	private IEnumerable<(int X, int Y)> EnumerateFootprintCells() =>
		GetMultiblockState().GetCache();

	private void TransferEnergyTick()
	{
		if (Terraria.Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(20) == 0)
		{
			Recipe.SetStatus(_energyBank!.HasEnergy()
				? Api.Machine.Feature.RecipeLogicStatus.WORKING
				: Api.Machine.Feature.RecipeLogicStatus.IDLE);
			_inputPerSec  = _netInLastSec;
			_outputPerSec = _netOutLastSec;
			_netInLastSec  = 0;
			_netOutLastSec = 0;
		}

		if (!Recipe.IsWorkingEnabled() || _inputHatches is null || _outputHatches is null) return;

		long banked = _energyBank!.Fill(_inputHatches.EnergyStored);
		_inputHatches.ChangeEnergy(-banked);
		_netInLastSec += banked;

		long passiveDrained = _energyBank.Drain(GetPassiveDrain());
		_netOutLastSec += passiveDrained;

		long debanked = _energyBank.Drain(_outputHatches.EnergyCapacity - _outputHatches.EnergyStored);
		_outputHatches.ChangeEnergy(debanked);
		_netOutLastSec += debanked;
	}

	public long GetPassiveDrain() => _passiveDrain;

	public override void AppendTooltip(List<string> lines)
	{
		lines.Add(DisplayName);
		OnAddFancyInformationTooltip(lines);

		if (!IsFormed)
		{
			AppendUnformedStructureBlock(lines);
			return;
		}

		if (_energyBank is null) return;
		AppendLiveStats(lines);
	}

	private void AppendLiveStats(List<string> lines)
	{
		var bank = _energyBank!;

		if (!Recipe.IsWorkingEnabled())
			lines.Add("[c/FFFF55:Paused]");
		else if (bank.HasEnergy())
			lines.Add("[c/55FF55:Running]");
		else
			lines.Add("Idle");

		BigInteger stored   = bank.GetStored();
		BigInteger capacity = bank.Capacity;
		lines.Add($"Stored: {stored:N0} / {capacity:N0} EU");
		lines.Add($"Passive drain: [c/FF8888:{GetPassiveDrain():N0} EU/t]");
		lines.Add($"Avg in: [c/55FF55:{_inputPerSec / 20:N0} EU/t]");
		lines.Add($"Avg out: [c/FF8888:{System.Math.Abs(_outputPerSec) / 20:N0} EU/t]");

		long net = _inputPerSec - _outputPerSec;
		if (net > 0)
		{
			BigInteger seconds = (capacity - stored) / new BigInteger(net);
			lines.Add($"Time to full: [c/55FF55:~{FormatDuration(seconds)}]");
		}
		else if (net < 0)
		{
			BigInteger seconds = stored / new BigInteger(-net);
			lines.Add($"Time to empty: [c/FF8888:~{FormatDuration(seconds)}]");
		}
	}

	public IReadOnlyList<string> BuildPanelLines()
	{
		var lines = new List<string>();
		if (!IsFormed)
		{
			lines.Add(RecipeStatusText.StatusLineForMulti(this, Recipe));
			return lines;
		}
		if (_energyBank is not null) AppendLiveStats(lines);
		return lines;
	}

	private static string FormatDuration(BigInteger seconds)
	{
		if (seconds > new BigInteger(long.MaxValue)) return "forever";
		long s = (long)seconds;
		if (s <= 180) return $"{s} s";
		if (s <= 180 * 60) return $"{s / 60} min";
		if (s <= 72 * 3600) return $"{s / 3600} h";
		if (s <= 730L * 86400) return $"{s / 86400} d";
		return $"{s / (86400L * 365)} yr";
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["pss_inPerSec"]   = _inputPerSec;
		tag["pss_outPerSec"]  = _outputPerSec;
		tag["pss_passive"]    = _passiveDrain;
	}

	public override void LoadData(TagCompound tag)
	{
		_energyBank ??= EnsureEnergyBank();
		base.LoadData(tag);
		if (tag.ContainsKey("pss_inPerSec"))  _inputPerSec  = tag.GetLong("pss_inPerSec");
		if (tag.ContainsKey("pss_outPerSec")) _outputPerSec = tag.GetLong("pss_outPerSec");
		if (tag.ContainsKey("pss_passive"))   _passiveDrain = tag.GetLong("pss_passive");
	}
}
