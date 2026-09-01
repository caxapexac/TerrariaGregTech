#nullable enable
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

public sealed class VanillaCraftingBridgeSystem : ModSystem
{
	public const string AnyWorkbench = "GregTechCEuTerraria:workbenches";

	public override void AddRecipeGroups()
	{
		ToolRecipeGroups.Register();
		RegisterRubberAsVanillaWood();
		RegisterAnyWorkbenchGroup();
	}

	private void RegisterAnyWorkbenchGroup()
	{
		var ids = new List<int> { ItemID.WorkBench };
		foreach (var (type, item) in ContentSamples.ItemsByType)
			if (type != ItemID.WorkBench && item.createTile == TileID.WorkBenches)
				ids.Add(type);

		Terraria.RecipeGroup.RegisterGroup(AnyWorkbench, new Terraria.RecipeGroup(
			() => $"{Terraria.Lang.misc[37].Value} {Terraria.Lang.GetItemNameValue(ItemID.WorkBench)}",
			ids.ToArray()));
	}

	private void RegisterRubberAsVanillaWood()
	{
		if (!Terraria.RecipeGroup.recipeGroups.TryGetValue(RecipeGroupID.Wood, out var wood)) return;
		if (Mod.TryFind<ModItem>("rubber_log", out var log))
			wood.ValidItems.Add(log.Type);
	}

	public override void AddRecipes()
	{
		VanillaCraftingBridge.Register(Mod);
		AddCompatHandRecipes();
		RecyclingShimmer.AddRecipes(Mod);
	}

	public override void PostAddRecipes()
	{
		NativeRecipeProxy.SynthesizeFromTerrariaRecipes();
		BlockBridgedRecipesFromMagicStorageRecursion();
		GateSubstitutedIngotShimmer();
		RecipeRegistry.RemoveStations("smelting", "blasting");
		AppliedEnergistics.Crafting.CraftingRecipeResolver.ClearResolvedCache();
	}

	private void GateSubstitutedIngotShimmer()
	{
		var bars = VanillaItemMap.SubstitutedVanillaIngotBars();
		if (bars.Count == 0) return;

		var desc = Terraria.Localization.Language.GetOrRegister(
			"Mods.GregTechCEuTerraria.Condition.ShimmerDupesAllowed",
			() => "Shimmer duplication allowed");

		int gated = 0;
		for (int i = 0; i < Terraria.Recipe.numRecipes; i++)
		{
			var r = Terraria.Main.recipe[i];
			if (r is null || r.createItem.type == ItemID.None || !bars.Contains(r.createItem.type)) continue;
			if (VanillaCraftingBridge.BridgeRegistered.Contains(r)) continue;
			r.AddDecraftCondition(desc, () => Config.GTConfig.Instance.AllowShimmerDupes);
			gated++;
		}
		Mod.Logger.Info($"[shimmer] gated {gated} vanilla bar recipe(s) behind AllowShimmerDupes");
	}

	private void BlockBridgedRecipesFromMagicStorageRecursion()
	{
		if (!ModLoader.TryGetMod("MagicStorage", out var ms)) return;
		int n = 0;
		try
		{
			foreach (var recipe in VanillaCraftingBridge.BridgeRegistered)
			{
				ms.Call("Block Recursion", recipe);
				n++;
			}
		}
		catch (System.Exception e)
		{
			Mod.Logger.Warn($"[magicstorage] Block Recursion call failed after {n} recipes (API mismatch?) - {e.Message}");
			return;
		}
		Mod.Logger.Info($"[magicstorage] blocked {n} bridged recipes from recursive crafting (load-time perf)");
	}

	private void AddCompatHandRecipes()
	{
		if (Mod.TryFind<ModItem>("wood_rod", out var rod))
			Terraria.Recipe.Create(rod.Type, 4)
				.AddRecipeGroup(RecipeGroupID.Wood, 1)
				.DisableDecraft()
				.Register();

		if (Mod.TryFind<ModItem>("wood_plate", out var plate))
			Terraria.Recipe.Create(plate.Type, 2)
				.AddRecipeGroup(RecipeGroupID.Wood, 1)
				.DisableDecraft()
				.Register();

		if (Items.MaterialItemRegistry.TryGetByUpstreamId("gtceu:clay_gem", out var clayBall))
			Terraria.Recipe.Create(clayBall, 4)
				.AddIngredient(ItemID.ClayBlock, 1)
				.DisableDecraft()
				.Register();

		if (Items.MaterialItemRegistry.TryGetByUpstreamId("gtceu:coal_gem", out var coalGem))
			Terraria.Recipe.Create(coalGem, 1)
				.AddIngredient(ItemID.Coal, 1)
				.AddTile(TileID.WorkBenches)
				.DisableDecraft()
				.Register();

		AddVanillaOreToRawOreRecipes();
	}

	private void AddVanillaOreToRawOreRecipes()
	{
		void Add(string materialId, string prefix, int vanillaItemId)
		{
			int? type = Items.MaterialItemRegistry.Get(materialId, prefix);
			if (type is null || type <= 0) return;
			Terraria.Recipe.Create(type.Value, Tiles.OreTileRegistry.RawOrePerBlock)
				.AddIngredient(vanillaItemId, 1)
				.DisableDecraft()
				.Register();
			Terraria.Recipe.Create(vanillaItemId, 1)
				.AddIngredient(type.Value, Tiles.OreTileRegistry.RawOrePerBlock)
				.DisableDecraft()
				.Register();
		}

		Add("iron",     "raw_ore", ItemID.IronOre);
		Add("lead",     "raw_ore", ItemID.LeadOre);
		Add("copper",   "raw_ore", ItemID.CopperOre);
		Add("tin",      "raw_ore", ItemID.TinOre);
		Add("gold",     "raw_ore", ItemID.GoldOre);
		Add("platinum", "raw_ore", ItemID.PlatinumOre);
		Add("silver",   "raw_ore", ItemID.SilverOre);
		Add("tungstate", "raw_ore", ItemID.TungstenOre);
	}
}
