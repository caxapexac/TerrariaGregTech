#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Category;
using GregTechCEuTerraria.Common.Recipe.Condition;
using GregTechCEuTerraria.Api.Recipe.Chance.Logic;
using GregTechCEuTerraria.Api.Recipe.Content;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Primitive;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

public static class BiomeWorldIORecipeSynth
{
	public static void Register(Mod mod)
	{
		var per = new Dictionary<string, List<GTRecipe>>();
		int miner = 0, rig = 0, pump = 0;

		var minerList = new List<GTRecipe>();
		foreach (BiomeWorldIOTables.MinerBucket bucket in
			System.Enum.GetValues(typeof(BiomeWorldIOTables.MinerBucket)))
		{
			var recipe = BuildMinerRecipe(bucket);
			if (recipe is null) continue;
			minerList.Add(recipe);
			miner++;
		}
		if (minerList.Count > 0) per[GTRecipeTypes.LARGE_MINER.RegistryName] = minerList;

		var rigList = new List<GTRecipe>();
		foreach (BiomeProbe.Biome biome in System.Enum.GetValues(typeof(BiomeProbe.Biome)))
		{
			var recipe = BuildRigRecipe(biome);
			if (recipe is null) continue;
			rigList.Add(recipe);
			rig++;
		}
		if (rigList.Count > 0) per[GTRecipeTypes.FLUID_DRILLING_RIG.RegistryName] = rigList;

		var pumpList = new List<GTRecipe>();
		foreach (BiomeProbe.Biome biome in System.Enum.GetValues(typeof(BiomeProbe.Biome)))
		{
			var recipe = BuildPumpRecipe(biome);
			if (recipe is null) continue;
			pumpList.Add(recipe);
			pump++;
		}
		if (pumpList.Count > 0) per[GTRecipeTypes.PRIMITIVE_PUMP.RegistryName] = pumpList;

		RecipeRegistry.AppendAll(per);
		mod.Logger.Info(
			$"BiomeWorldIORecipeSynth: synthesized {miner} large_miner + {rig} fluid_drilling_rig + {pump} primitive_pump browser recipes.");
	}

	private static GTRecipe? BuildPumpRecipe(BiomeProbe.Biome biome)
	{
		int mb = PumpBiomeModifier.ForBiome(biome);
		if (mb <= 0) return null;
		var outputs = new Dictionary<object, List<Content>>();
		outputs[FluidRecipeCapability.CAP] = new List<Content>
		{
			new(new FluidIngredient(FluidRegistry.Water, mb),
				ChanceLogic.GetMaxChancedValue(), ChanceLogic.GetMaxChancedValue(), 0),
		};

		return BuildRecipe(
			GTRecipeTypes.PRIMITIVE_PUMP,
			id:             $"primitive_pump/{biome.ToString().ToLowerInvariant()}",
			inputs:         new Dictionary<object, List<Content>>(),
			outputs:        outputs,
			eutInput:       0,
			duration:       20,
			conditionLabel: biome == BiomeProbe.Biome.Underworld
				? biome.ToString()
				: $"{biome} (+50% in rain)");
	}

	private static GTRecipe? BuildMinerRecipe(BiomeWorldIOTables.MinerBucket bucket)
	{
		var pool = BiomeWorldIOTables.GetPool(bucket);
		int totalWeight = 0;
		foreach (var e in pool) totalWeight += e.Weight;
		if (pool.Count == 0 || totalWeight == 0) return null;

		var outputs = new Dictionary<object, List<Content>>();
		var outList = new List<Content>(pool.Count);
		foreach (var e in pool)
			outList.Add(new Content(new ItemStackIngredient(e.ItemType), e.Weight, totalWeight, 0));
		outputs[ItemRecipeCapability.CAP] = outList;

		var inputs = new Dictionary<object, List<Content>>();
		var drillingFluid = FluidRegistry.Get("drilling_fluid");
		if (drillingFluid is not null)
		{
			inputs[FluidRecipeCapability.CAP] = new List<Content>
			{
				new(new FluidIngredient(drillingFluid, 4),
					ChanceLogic.GetMaxChancedValue(), ChanceLogic.GetMaxChancedValue(), 0),
			};
		}

		return BuildRecipe(
			GTRecipeTypes.LARGE_MINER,
			id:             $"large_miner/{bucket.ToString().ToLowerInvariant()}",
			inputs:         inputs,
			outputs:        outputs,
			eutInput:       VoltageTiers.V((int)VoltageTier.EV),
			duration:       200,
			conditionLabel: BiomeWorldIOTables.Label(bucket));
	}

	private static GTRecipe? BuildRigRecipe(BiomeProbe.Biome biome)
	{
		var fluid = BiomeWorldIOTables.GetFluid(biome);
		if (fluid is null) return null;

		const int baseProductionMb = 100;
		const int cycleTicks = 20;

		var outputs = new Dictionary<object, List<Content>>();
		outputs[FluidRecipeCapability.CAP] = new List<Content>
		{
			new(new FluidIngredient(fluid, baseProductionMb),
				ChanceLogic.GetMaxChancedValue(), ChanceLogic.GetMaxChancedValue(), 0),
		};

		return BuildRecipe(
			GTRecipeTypes.FLUID_DRILLING_RIG,
			id:         $"fluid_drilling_rig/{biome.ToString().ToLowerInvariant()}",
			inputs:     new Dictionary<object, List<Content>>(),
			outputs:    outputs,
			eutInput:       VoltageTiers.V((int)VoltageTier.MV),
			duration:       cycleTicks,
			conditionLabel: biome.ToString());
	}

	private static GTRecipe BuildRecipe(GTRecipeType type, string id,
		Dictionary<object, List<Content>> inputs,
		Dictionary<object, List<Content>> outputs,
		long eutInput, int duration, string conditionLabel)
	{
		var tickInputs = new Dictionary<object, List<Content>>();
		if (eutInput > 0)
			EURecipeCapability.PutEUContent(tickInputs, new EnergyStack(eutInput, 1));

		var conditions = new List<RecipeCondition>
		{
			new BiomeCondition(conditionLabel),
		};

		return new GTRecipe(
			recipeType:              type,
			id:                      id,
			inputs:                  inputs,
			outputs:                 outputs,
			tickInputs:              tickInputs,
			tickOutputs:             new Dictionary<object, List<Content>>(),
			inputChanceLogics:       new Dictionary<object, ChanceLogic>(),
			outputChanceLogics:      new Dictionary<object, ChanceLogic>(),
			tickInputChanceLogics:   new Dictionary<object, ChanceLogic>(),
			tickOutputChanceLogics:  new Dictionary<object, ChanceLogic>(),
			conditions:              conditions,
			ingredientActions:       System.Array.Empty<object>(),
			data:                    new TagCompound(),
			duration:                duration,
			recipeCategory:          GTRecipeCategory.DEFAULT,
			groupColor:              -1);
	}
}
