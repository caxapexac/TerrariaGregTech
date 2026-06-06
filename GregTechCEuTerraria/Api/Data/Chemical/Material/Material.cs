#nullable enable
using GregTechCEuTerraria.Api.Data.Chemical.Material.Properties;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Fluids.Store;
using GregTechCEuTerraria.Api.Fluids;
using System.Collections.Generic;

namespace GregTechCEuTerraria.Api.Data.Chemical.Material;

// Mirrors GTCEu's com.gregtechceu.gtceu.api.data.chemical.material.Material.
// Populated from JSON under Data/Materials/*.json by MaterialLoader.
public sealed class Material
{
	public required string Id { get; init; }              // "iron", "annealed_copper"
	public string? Name { get; init; }                    // localization key; defaults to Mods.GregTechCEuTerraria.Materials.<id>
	public uint? Color { get; init; }                     // 0xRRGGBB
	public uint? SecondaryColor { get; init; }
	public string? IconSet { get; init; }                 // upstream MaterialIconSet name: "METALLIC", "SHINY", "RUBY", ...
	public string? Element { get; init; }                 // periodic symbol: "Fe", "Cu", "Al"
	public string? Formula { get; init; }

	public List<string> Forms { get; init; } = new();

	// Upstream MaterialFlag names. NOT YET PORTED - the materials registry dump
	// does not emit flags, so this list is always empty. The field is kept
	// because the verbatim FluidBuilder port references it (PHOSPHORESCENT /
	// STICKY) in its luminosity/viscosity inference - that path is currently
	// inert since Round-2 dumps fluid stats directly. To finish the port, emit
	// the flag set from the material DataProvider.
	public List<string> Flags { get; init; } = new();

	public List<MaterialComponent> Components { get; init; } = new();

	// Dust/ingot/gem harvest level = upstream DustProperty.harvestLevel (default 2 = "iron")
	public int? HarvestLevel { get; init; }

	public int? MeltingPointK { get; init; }              // from .liquid(temp) / .liquid(new FluidBuilder().temperature(t))
	public int? BlastTemperatureK { get; init; }          // from .blast(temp, ...) / .blastTemp(temp, ...)
	public string? BlastGasTier { get; init; }            // "LOW" | "MID" | "HIGH" | "HIGHER" | "HIGHEST"

	public string? CableTier { get; init; }
	public int? CableAmperage { get; init; }
	public int? CableLoss { get; init; }
	public bool? CableIsSuperconductor { get; init; }
	public int? CableCriticalTempK { get; init; }

	public ToolProperty? Tool { get; init; }

	public bool HasTool() => Tool != null;

	// Fluid-pipe property from upstream .fluidPipeProperties(...). Drives the
	// drum fluid filter (a material's drum may only hold fluids its pipe
	// property permits). Deserialized off the `fluidPipe` block; null for
	// materials with no FLUID_PIPE property. == upstream hasProperty(FLUID_PIPE).
	public FluidPipeProperties? FluidPipe { get; init; }

	public bool HasFluidPipe() => FluidPipe != null;

	// Item-pipe property from upstream `.itemPipeProperties(priority, rate)`.
	// Base routing priority + per-second transfer rate; the per-pipe-size
	// multipliers (ItemPipeSizeModifier) get applied on top at placement
	// time inside PipeItem.BuildItemCell.
	// TODO: not yet emitted by DataGenerators.java - extend the dump to
	// write per-material itemPipe blocks, then load here. For now this
	// stays null for every material and PipeItem.BuildItemCell falls back
	// to upstream's parameterless default `(1, 0.25f)`.
	public ItemPipeProperties? ItemPipe { get; init; }

	public bool HasItemPipe() => ItemPipe != null;

	// Rotor property from upstream .rotorStats(power, efficiency, damage, durability).
	// Drives turbine rotor stats (TurbineRotorItem + RotorHolderPartMachine).
	// Deserialized off the `rotor` block in materials.json. Power + efficiency
	// only - damage + durability dropped (no item durability in Terraria, rotor
	// isn't a melee weapon). Null for materials with no ROTOR property.
	public RotorProperty? Rotor { get; init; }

	public bool HasRotor() => Rotor != null;

	public List<string> Unported { get; init; } = new();

	public FluidProperty? FluidProperty { get; internal set; }

	public bool HasFluid() => FluidProperty?.Get() != null;

	public FluidType? GetFluid() => FluidProperty?.Get();

	public FluidType? GetFluid(FluidStorageKey key) =>
		FluidProperty?.Get(key);

	public int GetFluidTemperature(FluidStorageKey? key = null)
	{
		var fluid = key is null ? GetFluid() : GetFluid(key);
		return fluid?.Temperature ?? 0;
	}

	public FluidBuilder? GetFluidBuilder(FluidStorageKey key) =>
		FluidProperty?.GetQueuedBuilder(key);

	public FluidBuilder? GetFluidBuilder()
	{
		var prop = FluidProperty;
		if (prop is null) return null;
		var b = prop.PrimaryKey is { } key ? prop.GetQueuedBuilder(key) : null;
		if (b is not null) return b;
		b = prop.GetQueuedBuilder(FluidStorageKey.LIQUID);
		if (b is not null) return b;
		return prop.GetQueuedBuilder(FluidStorageKey.GAS);
	}
}

public sealed record MaterialComponent(string MaterialId, int Amount);
