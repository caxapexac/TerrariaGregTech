#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using GregTechCEuTerraria.Api.Machine.Multiblock;
using GregTechCEuTerraria.Api.Pattern.Error;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Tiles;
using Terraria;

namespace GregTechCEuTerraria.Api.Pattern;

public static class Predicates
{
	public static TraceabilityPredicate Controller(TraceabilityPredicate predicate) =>
		predicate.SetController();

	public static TraceabilityPredicate Controller(MachineDefinition def) =>
		Machines(def).SetController();

	public static TraceabilityPredicate Blocks(params ushort[] tileTypes) =>
		new(new PredicateBlocks(tileTypes));

	public static TraceabilityPredicate Blocks(params string[] tileNames)
	{
		var types = new List<ushort>(tileNames.Length);
		var mod = Terraria.ModLoader.ModLoader.GetMod("GregTechCEuTerraria");
		foreach (var name in tileNames)
		{
			if (string.IsNullOrEmpty(name)) continue;
			if (mod.TryFind<Terraria.ModLoader.ModTile>(name, out var tile))
				types.Add((ushort)tile.Type);
			else
				mod.Logger.Warn($"Predicates.Blocks: tile name '{name}' did not resolve - pattern will not match it.");
		}
		return new(new PredicateBlocks(types.ToArray()));
	}

	public static TraceabilityPredicate Machines(params MachineDefinition[] definitions)
	{
		var types = new List<ushort>();
		foreach (var def in definitions)
		{
			if (def is null) continue;
			foreach (var t in MachineRegistry.TilesForId(def.Id)) types.Add((ushort)t);
		}
		return Blocks(types.ToArray());
	}

	public static TraceabilityPredicate Custom(Func<MultiblockState, bool> predicate, Func<Item[]> candidates) =>
		new(predicate, candidates);

	public static TraceabilityPredicate Any() => new(SimplePredicate.ANY);

	public static TraceabilityPredicate Air() => new(SimplePredicate.AIR);

	public static TraceabilityPredicate Abilities(params PartAbility[] abilities)
	{
		var types = abilities.SelectMany(a => a.GetAllTiles()).Distinct().ToArray();
		return Blocks(types);
	}

	public static TraceabilityPredicate Ability(PartAbility ability, params int[] tiers)
	{
		var types = (tiers.Length == 0 ? ability.GetAllTiles() : ability.GetTiles(tiers)).ToArray();
		return Blocks(types);
	}

	public static TraceabilityPredicate AutoAbilities(params GTRecipeType[] recipeType) =>
		AutoAbilities(recipeType, true, true, true, true, true, true);

	public static TraceabilityPredicate AutoAbilities(
		GTRecipeType[] recipeType,
		bool checkEnergyIn,
		bool checkEnergyOut,
		bool checkItemIn,
		bool checkItemOut,
		bool checkFluidIn,
		bool checkFluidOut)
	{
		TraceabilityPredicate predicate = new();

		bool AnyHasInput(object cap)
		{
			foreach (var t in recipeType) if (t.HasInput(cap)) return true;
			return false;
		}
		bool AnyHasOutput(object cap)
		{
			foreach (var t in recipeType) if (t.HasOutput(cap)) return true;
			return false;
		}

		if (checkEnergyIn  && AnyHasInput(Api.Capability.Recipe.EURecipeCapability.CAP))
			predicate = predicate.Or(Abilities(PartAbility.INPUT_ENERGY).SetMinGlobalLimited(1).SetMaxGlobalLimited(2).SetPreviewCount(1));
		if (checkEnergyOut && AnyHasOutput(Api.Capability.Recipe.EURecipeCapability.CAP))
			predicate = predicate.Or(Abilities(PartAbility.OUTPUT_ENERGY).SetMinGlobalLimited(1).SetMaxGlobalLimited(2).SetPreviewCount(1));
		if (checkItemIn    && AnyHasInput(Api.Capability.Recipe.ItemRecipeCapability.CAP))
			predicate = predicate.Or(Abilities(PartAbility.IMPORT_ITEMS).SetPreviewCount(1));
		if (checkItemOut   && AnyHasOutput(Api.Capability.Recipe.ItemRecipeCapability.CAP))
			predicate = predicate.Or(Abilities(PartAbility.EXPORT_ITEMS).SetPreviewCount(1));
		if (checkFluidIn   && AnyHasInput(Api.Capability.Recipe.FluidRecipeCapability.CAP))
			predicate = predicate.Or(Abilities(PartAbility.IMPORT_FLUIDS).SetPreviewCount(1));
		if (checkFluidOut  && AnyHasOutput(Api.Capability.Recipe.FluidRecipeCapability.CAP))
			predicate = predicate.Or(Abilities(PartAbility.EXPORT_FLUIDS).SetPreviewCount(1));
		return predicate;
	}

	public static TraceabilityPredicate StandardWall(
		string casingTileName,
		GTRecipeType[] recipeTypes,
		bool maintenance = true,
		bool muffler     = false,
		bool parallel    = false,
		int  minGlobal   = 0)
	{
		var casing = Blocks(casingTileName);
		if (minGlobal > 0) casing = casing.SetMinGlobalLimited(minGlobal);
		return casing
		    .Or(AutoAbilities(recipeTypes))
		    .Or(AutoAbilities(maintenance, muffler, parallel));
	}

	public static TraceabilityPredicate AutoAbilities(
		bool checkMaintenance, bool checkMuffler, bool checkParallel)
	{
		TraceabilityPredicate predicate = new();
		if (checkMaintenance)
			predicate = predicate.Or(Abilities(PartAbility.MAINTENANCE).SetMinGlobalLimited(1).SetMaxGlobalLimited(1));
		if (checkMuffler)
			predicate = predicate.Or(Abilities(PartAbility.MUFFLER).SetMinGlobalLimited(1).SetMaxGlobalLimited(1));
		if (checkParallel)
			predicate = predicate.Or(Abilities(PartAbility.PARALLEL_HATCH).SetMaxGlobalLimited(1).SetPreviewCount(1));
		return predicate;
	}

	public static TraceabilityPredicate DataHatchPredicate(TraceabilityPredicate def) =>
		Abilities(PartAbility.DATA_ACCESS, PartAbility.OPTICAL_DATA_RECEPTION).SetExactLimit(1).Or(def);

	public static TraceabilityPredicate Frames(params Material[] frameMaterials)
	{
		var types = new List<ushort>();
		foreach (var m in frameMaterials)
		{
			if (m is null) continue;
			var t = MaterialItemRegistry.Get(m.Id, MaterialPrefixes.Frame.Id);
			if (t is null) continue;
			var tileType = MaterialBlockTileRegistry.Get($"{m.Id}_frame");
			if (tileType.HasValue) types.Add((ushort)tileType.Value);
		}
		return Blocks(types.ToArray());
	}

	public static TraceabilityPredicate HeatingCoils()
	{
		(ushort tileType, Api.Block.CoilType coil)[]? entries = null;

		bool MatchCell(MultiblockState state)
		{
			entries ??= ResolveCoilEntries();
			var tile = Main.tile[state.PosX, state.PosY];
			if (!tile.HasTile) return false;
			foreach (var (tileType, coil) in entries)
			{
				if (tile.TileType != tileType) continue;
				var existing = state.MatchContext.Get<Api.Block.CoilType>("CoilType");
				if (existing is null)
					state.MatchContext.Set("CoilType", coil);
				else if (!ReferenceEquals(existing, coil))
					return false;
				return true;
			}
			return false;
		}

		entries ??= ResolveCoilEntries();
		var tileTypes = entries.Length > 0
			? System.Array.ConvertAll(entries, e => e.tileType)
			: System.Array.Empty<ushort>();
		return new TraceabilityPredicate(MatchCell, PredicateBlocks.CandidatesForTiles(tileTypes));
	}

	private static (ushort tileType, Api.Block.CoilType coil)[] ResolveCoilEntries()
	{
		var mod = Terraria.ModLoader.ModLoader.GetMod("GregTechCEuTerraria");
		var list = new List<(ushort, Api.Block.CoilType)>();
		foreach (var c in Api.Block.CoilType.All)
		{
			if (mod.TryFind<Terraria.ModLoader.ModTile>(c.TileName, out var t))
				list.Add(((ushort)t.Type, c));
		}
		return list.ToArray();
	}

	public static TraceabilityPredicate CleanroomFilters()
	{
		(ushort tileType, Common.Block.CleanroomFilterType filter)[]? entries = null;

		bool MatchCell(MultiblockState state)
		{
			entries ??= ResolveFilterEntries();
			var tile = Main.tile[state.PosX, state.PosY];
			if (!tile.HasTile) return false;
			foreach (var (tileType, filter) in entries)
			{
				if (tile.TileType != tileType) continue;
				var existing = state.MatchContext.Get<Common.Block.CleanroomFilterType>("FilterType");
				if (existing is null)
					state.MatchContext.Set("FilterType", filter);
				else if (!ReferenceEquals(existing, filter))
					return false;
				return true;
			}
			return false;
		}

		entries ??= ResolveFilterEntries();
		var tileTypes = entries.Length > 0
			? System.Array.ConvertAll(entries, e => e.tileType)
			: System.Array.Empty<ushort>();
		return new TraceabilityPredicate(MatchCell, PredicateBlocks.CandidatesForTiles(tileTypes));
	}

	private static (ushort tileType, Common.Block.CleanroomFilterType filter)[] ResolveFilterEntries()
	{
		var mod = Terraria.ModLoader.ModLoader.GetMod("GregTechCEuTerraria");
		var list = new List<(ushort, Common.Block.CleanroomFilterType)>();
		foreach (var f in Common.Block.CleanroomFilterType.All)
		{
			if (mod.TryFind<Terraria.ModLoader.ModTile>(f.TileName, out var t))
				list.Add(((ushort)t.Type, f));
		}
		return list.ToArray();
	}
}
