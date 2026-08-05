#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Content;
using GregTechCEuTerraria.Api.Recipe.Modifier;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using GregTechCEuTerraria.TerrariaCompat.Machine.Steam;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Steam;

public sealed class SteamParallelMultiblockMachine : WorkableMultiblockMachine
{
	public const double CONVERSION_RATE = 2.0;

	protected override string Label => Definition?.Label ?? "Steam Parallel";

	public int MaxParallels { get; private set; } = 8;
	public void SetMaxParallels(int value) => MaxParallels = value;

	private SteamEnergyRecipeHandler? _steamEnergy;

	public long SteamStored   { get; private set; }
	public long SteamCapacity { get; private set; }

	public SteamParallelMultiblockMachine() : base() { }

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		foreach (var part in GetParts())
		{
			if (part is SteamHatchPartMachine sh && sh.Tank is { } tank)
			{
				_steamEnergy = new SteamEnergyRecipeHandler(tank, CONVERSION_RATE);
				RefreshSteamSnapshot();
				return;
			}
		}
		SetUnformedReason("No steam input hatch bound");
		OnStructureInvalid();
	}

	public override void OnStructureInvalid()
	{
		base.OnStructureInvalid();
		_steamEnergy = null;
		SteamStored = 0; SteamCapacity = 0;
	}

	public override void OnPartUnload()
	{
		base.OnPartUnload();
		_steamEnergy = null;
		SteamStored = 0; SteamCapacity = 0;
	}

	private void RefreshSteamSnapshot()
	{
		if (_steamEnergy == null) { SteamStored = 0; SteamCapacity = 0; return; }
		SteamStored   = _steamEnergy.Stored;
		SteamCapacity = _steamEnergy.Capacity;
	}

	protected override void OnTick()
	{
		base.OnTick();
		if (IsServer && IsFormed && (Terraria.Main.GameUpdateCount & 0x7) == 0)
			RefreshSteamSnapshot();
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["spm_steam"] = SteamStored;
		tag["spm_cap"]   = SteamCapacity;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		SteamStored   = tag.GetLong("spm_steam");
		SteamCapacity = tag.GetLong("spm_cap");
	}

	public override long EnergyStored
	{
		get => _steamEnergy?.StoredEu ?? 0;
		set { }
	}

	public override ActionResult TryDrainEU(GTRecipe recipe, long voltage)
	{
		if (voltage <= 0) return ActionResult.SUCCESS;
		if (_steamEnergy == null)
			return ActionResult.Fail("gtceu.recipe.insufficient_eu", EURecipeCapability.CAP, IO.IN);
		return _steamEnergy.TryDrainEnergy(voltage, simulate: false)
			? ActionResult.SUCCESS
			: ActionResult.Fail("gtceu.recipe.insufficient_eu", EURecipeCapability.CAP, IO.IN);
	}

	protected override void AppendDrawingLine(System.Collections.Generic.List<string> lines)
	{
		if (ActiveEut > 0)
		{
			long steam = (long)System.Math.Ceiling(ActiveEut * CONVERSION_RATE);
			lines.Add($"Consuming: {steam:N0} mB/t Steam");
		}
	}

	public override RecipeModifier GetRecipeModifier() => SteamParallelModifier;

	private static readonly RecipeModifier SteamParallelModifier = new((machine, recipe) =>
	{
		if (machine is not SteamParallelMultiblockMachine steam)
			return ModifierFunction.NULL;
		if (RecipeHelper.GetRecipeEUtTier(recipe) > (int)VoltageTier.LV)
			return ModifierFunction.NULL;

		long eut = recipe.InputEUt.GetTotalEU();
		int parallels = ParallelLogic.GetParallelAmount(machine, recipe, steam.MaxParallels);
		double scale = 8.0 / 9.0;
		double eutMultiplier = (eut * scale * parallels <= 32)
			? (scale * parallels)
			: (32.0 / eut);

		return ModifierFunction.Builder()
			.InputModifier (ContentModifier.Multiplier_(parallels))
			.OutputModifier(ContentModifier.Multiplier_(parallels))
			.DurationMultiplier(1.5)
			.EutMultiplier(eutMultiplier)
			.Parallels(parallels)
			.Build();
	});
}
