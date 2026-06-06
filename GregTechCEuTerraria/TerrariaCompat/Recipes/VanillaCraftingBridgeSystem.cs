#nullable enable
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

// Bridges RecipeRegistry into Terraria's vanilla crafting
public sealed class VanillaCraftingBridgeSystem : ModSystem
{
	public override void AddRecipeGroups()
	{
		ToolRecipeGroups.Register();
	}

	public override void AddRecipes()
	{
		VanillaCraftingBridge.Register(Mod);
		AddCompatHandRecipes();
	}

	public override void PostAddRecipes()
	{
		NativeRecipeProxy.SynthesizeFromTerrariaRecipes();
		BlockBridgedRecipesFromMagicStorageRecursion();
	}

	// Magic Storage builds a recursive crafting tree per enabled recipe at load, we block it
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

	// Early-game progression fix
	private void AddCompatHandRecipes()
	{
		if (Mod.TryFind<ModItem>("wood_rod", out var rod))
			Terraria.Recipe.Create(rod.Type, 4)
				.AddRecipeGroup(RecipeGroupID.Wood, 1)
				.Register();

		if (Mod.TryFind<ModItem>("wood_plate", out var plate))
			Terraria.Recipe.Create(plate.Type, 2)
				.AddRecipeGroup(RecipeGroupID.Wood, 1)
				.Register();

		if (Items.MaterialItemRegistry.TryGetByUpstreamId("gtceu:clay_gem", out var clayBall))
			Terraria.Recipe.Create(clayBall, 4)
				.AddIngredient(ItemID.ClayBlock, 1)
				.Register();

		// ItemID.Coal is the Christmas Lump of Coal (maxStack=1, gag item)
		if (Items.MaterialItemRegistry.TryGetByUpstreamId("gtceu:coal_gem", out var coalGem))
			Terraria.Recipe.Create(coalGem, 1)
				.AddIngredient(ItemID.Coal, 1)
				.AddTile(TileID.WorkBenches)
				.Register();

		AddVanillaOreToRawOreRecipes();
	}

	// TODO better ore substitution
	private void AddVanillaOreToRawOreRecipes()
	{
		void Add(string materialId, string prefix, int vanillaItemId)
		{
			int? type = Items.MaterialItemRegistry.Get(materialId, prefix);
			if (type is null || type <= 0) return;
			Terraria.Recipe.Create(type.Value, Tiles.OreTileRegistry.RawOrePerBlock)
				.AddIngredient(vanillaItemId, 1)
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
