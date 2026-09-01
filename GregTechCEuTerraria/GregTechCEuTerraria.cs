#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Worldgen;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Items.Cables;
using GregTechCEuTerraria.TerrariaCompat.Items.Machines;
using GregTechCEuTerraria.TerrariaCompat.Tiles;
using GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;
using System;
using System.Reflection;
using Terraria.ModLoader;

namespace GregTechCEuTerraria;

public sealed class GregTechCEuTerraria : Mod
{
	private static readonly Action<string>? _setSubProgress = ResolveSubProgressSetter();

	private static Action<string>? ResolveSubProgressSetter()
	{
		try
		{
			var asm = typeof(Mod).Assembly;
			var interfaceType = asm.GetType("Terraria.ModLoader.UI.Interface");
			var field = interfaceType?.GetField("loadMods", BindingFlags.NonPublic | BindingFlags.Static);
			var loadMods = field?.GetValue(null);
			if (loadMods == null) return null;
			var prop = loadMods.GetType().GetProperty("SubProgressText", BindingFlags.Public | BindingFlags.Instance);
			if (prop == null) return null;
			return text => prop.SetValue(loadMods, text);
		}
		catch { return null; }
	}

	private void Stage(string text)
	{
		_setSubProgress?.Invoke("GregTechCEuTerraria: " + text);
		Logger.Info(text);
	}

	public override void Load()
	{
		Terraria.ModLoader.Logging.IgnoreExceptionContents(
			"GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting.CraftingTreeNode.Request");
		Terraria.ModLoader.Logging.IgnoreExceptionContents(
			"GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting.CraftingCalculation.HandlePausing");

		Api.TickScale.SimulationSpeedProvider = () => Config.GTConfig.Instance?.SimulationSpeed ?? 1.0f;

		TerrariaCompat.UI.GTShaders.Load(this);

		Stage("Loading materials");
		TerrariaCompat.Materials.MaterialJsonLoader.Load(this);
		Stage("Parsing registry dump");
		TerrariaCompat.Items.Registry.RegistryDump.Load(this);
		Stage("Registering fluids");
		TerrariaCompat.Loaders.FluidLoader.RegisterAll(this);
		Stage("Registering fluid buckets");
		TerrariaCompat.Items.Fluids.FluidBucketRegistry.Register(this);
		Stage("Registering material block tiles");
		TerrariaCompat.Tiles.MaterialBlockTileRegistry.Register(this);
		Stage("Registering material items");
		MaterialItemRegistry.Register(this);
		Stage("Registering ore tiles");
		OreTileRegistry.Register(this);
		Stage("Loading ore veins");
		VeinJsonLoader.Load(this);
		Stage("Registering wires & cables");
		WireItemRegistry.Register(this);
		TerrariaCompat.Items.Cables.SuperconductorWireLoader.Register(this);
		TerrariaCompat.Items.MeCables.MeCableItemRegistry.Register(this);
		Stage("Registering pipes");
		TerrariaCompat.Items.Pipes.PipeItemRegistry.Register(this);
		TerrariaCompat.Items.Pipes.SimplePipeRegistry.Register(this);
		Stage("Registering batteries");
		TerrariaCompat.Items.Batteries.BatteryItemLoader.Register(this);
		Stage("Registering item magnets");
		TerrariaCompat.Items.Magnets.MagnetItemLoader.Register(this);
		Stage("Registering ore scanners");
		TerrariaCompat.Items.Prospectors.ProspectorItemLoader.Register(this);
		Stage("Registering ME terminal upgrade cards");
		TerrariaCompat.Items.Terminal.MeTerminalUpgradeLoader.Register(this);
		Stage("Registering GregTech tools");
		TerrariaCompat.Items.Tools.ToolItemLoader.Register(this);
		Stage("Registering Gregith weapons");
		TerrariaCompat.Items.Tools.GregithItemLoader.Register(this);
		Stage("Registering power armor");
		TerrariaCompat.Items.Armor.ArmorItemLoader.Register(this);
		Stage("Registering wooden forms");
		TerrariaCompat.Items.WoodenFormItemLoader.Register(this);
		Stage("Registering inert components from dump");
		TerrariaCompat.Items.Registry.RegistryItemLoader.Load(this);
		Stage("Registering item tags");
		TerrariaCompat.Items.Registry.RegistryTagLoader.Load(this);
		Stage("Registering covers");
		TerrariaCompat.Cover.GTCovers.Register();
		TerrariaCompat.Items.Covers.CoverItemLoader.Register(this);
		TerrariaCompat.Cover.GTCovers.RegisterFilterItems();
		Stage("Registering fluid cells");
		TerrariaCompat.Items.Fluids.FluidCellRegistry.Register(this);
		Stage("Building machine definitions");
		MachineDefinitions.RegisterAll();
		TerrariaCompat.Machine.Multiblock.Electric.FusionReactorMachine.RegisterFusionTier(
			(int)Common.Energy.VoltageTier.LuV, " (MKI)");
		TerrariaCompat.Machine.Multiblock.Electric.FusionReactorMachine.RegisterFusionTier(
			(int)Common.Energy.VoltageTier.ZPM, " (MKII)");
		TerrariaCompat.Machine.Multiblock.Electric.FusionReactorMachine.RegisterFusionTier(
			(int)Common.Energy.VoltageTier.UV, " (MKIII)");
		TerrariaCompat.Pipelike.LongDistance.LongDistanceLocale.RegisterAll();
		TerrariaCompat.Machine.Multiblock.MultiblockLocale.RegisterAll();
		AppliedEnergistics.Api.Stacks.AEKeyTypes.RegisterBuiltins();
		AppliedEnergistics.Core.AELocale.RegisterAll();
		Stage("Registering machine tiles & items");
		TieredMachineFactory.RegisterAll(this);
		Stage("Registering wood-form items");
		TerrariaCompat.Items.WoodFormItemLoader.Register(this);
		Stage("Registering casing blocks");
		TerrariaCompat.Tiles.Casings.CasingRegistry.Register(this);
		Stage("Registering crafting stations");
		TerrariaCompat.Tiles.CraftingStations.CraftingStationRegistry.RegisterAll(this);
		Stage("Registering turbine rotors");
		TerrariaCompat.Items.TurbineRotorItemLoader.Register(this);
		TerrariaCompat.Items.ResearchDataGlobalItem.RegisterHooks();
		TerrariaCompat.Pipelike.PipeIntersection.InstallHook();

		var resolver = TerrariaCompat.Recipes.IngredientResolverImpl.Instance;
		IIngredientResolver.Default = resolver;
		Api.Recipe.Lookup.RecipeDB.Warn = msg => Logger.Warn(msg);
		Api.Machine.Trait.RecipeLogic.AmbientRecipeModifier = TerrariaCompat.Machine.GregtechToiletAura.PostModify;

		Stage("Loading recipes (~32k)");
		RecipeJsonLoader.Load(this, resolver);
		Stage("Synthesising biome world-I/O recipes");
		TerrariaCompat.Machine.Multiblock.Electric.BiomeWorldIORecipeSynth.Register(this);
		TerrariaCompat.Items.Tools.ToolWorldEffectRecipeSynth.Register(this);
		TerrariaCompat.Worldgen.OreVeinRecipeSynth.Register(this);
		Stage("Verifying recipe coverage");
		TerrariaCompat.Recipes.RecipeCoverageCheck.Verify(this);
		Stage("Registering multiblock bags");
		TerrariaCompat.BossDrops.MultiblockBag.MultiblockBagLoader.Register(this);
		Stage("Resolving boss drops");
		TerrariaCompat.BossDrops.BossDropRegistry.Resolve(this);

		Stage("Ready");
	}

	public override void PostSetupContent()
	{
		TerrariaCompat.Machine.Multiblock.MultiblockStructureRecipeSynth.Register(this);

		if (TryFind<Terraria.ModLoader.ModItem>("lv_rock_crusher", out var rockCrusher))
			TerrariaCompat.UI.Widgets.StationIcon.RegisterExplicit("rock_breaker", rockCrusher.Type);
	}

	public override void HandlePacket(BinaryReader reader, int whoAmI)
	{
		NetRouter.Handle(reader, whoAmI);
	}

	public override void Unload()
	{
		RuntimeTextureRegistry.DisposeAll();
		TerrariaCompat.UI.GTShaders.Unload();

		TerrariaCompat.Pipelike.PipeIntersection.UninstallHook();
		IIngredientResolver.Default = null;
		Api.Recipe.Lookup.RecipeDB.Warn = null;
		Api.Machine.Trait.RecipeLogic.AmbientRecipeModifier = null;

		TerrariaCompat.BossDrops.BossDropRegistry.Unload();
		TerrariaCompat.BossDrops.MultiblockBag.MultiblockBagLoader.Unload();
		RecipeRegistry.Clear();
		TerrariaCompat.Recipes.RecyclingShimmer.Unload();
		TerrariaCompat.Items.Tools.ToolItemLoader.Unload();
		TerrariaCompat.Items.Tools.GregithItemLoader.Unload();
		TerrariaCompat.Items.Armor.ArmorItemLoader.Unload();
		TerrariaCompat.Items.TurbineRotorItemLoader.Unload();
		TerrariaCompat.Items.ResearchDataGlobalItem.UnloadHooks();
		TerrariaCompat.Items.Registry.RegistryItemLoader.Unload();
		TerrariaCompat.Items.Registry.RegistryTagLoader.Unload();
		TerrariaCompat.Items.Covers.CoverItemLoader.Unload();
		TerrariaCompat.Items.Registry.TagMembership.Clear();
		Api.Cover.Filter.FilterItemRegistry.Clear();
		Api.Cover.CoverRegistry.Clear();
		TerrariaCompat.Items.Registry.RegistryDump.Unload();
		WireItemRegistry.Unload();
		TerrariaCompat.Items.MeCables.MeCableItemRegistry.Unload();
		TerrariaCompat.Items.Pipes.PipeItemRegistry.Unload();
		VeinRegistry.Clear();
		OreTileRegistry.Unload();
		TerrariaCompat.Tiles.MaterialBlockTileRegistry.Unload();
		MaterialItemRegistry.Unload();
		MaterialRegistry.Clear();
		MachineRegistry.Clear();
	}
}
