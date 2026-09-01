#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.CraftingStations;

public sealed class CraftingStationItem : ModItem, ITextureWarmUp
{
	private readonly string? _id;
	private readonly string? _displayName;
	private readonly string? _gtTag;
	private bool _aliased;

	public CraftingStationItem() { }
	public CraftingStationItem(string id, string displayName, string? gtTag)
	{
		_id = id;
		_displayName = displayName;
		_gtTag = gtTag;
	}

	public override bool IsLoadingEnabled(Mod mod) => _id != null;
	public override string Name => _id ?? nameof(CraftingStationItem);
	public override string Texture => "GregTechCEuTerraria/Content/TerrariaCompat/TooManyItemsItem";
	protected override bool CloneNewInstances => true;

	public override void SetStaticDefaults()
	{
		if (_id != null && !string.IsNullOrEmpty(_displayName))
			Language.GetOrRegister(
				$"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName", () => _displayName!);
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		tooltips.Add(new TooltipLine(Mod, "GTStationWIP", "Visuals are WIP, looking for artist!")
			{ OverrideColor = new Color(255, 70, 70) });
	}

	public override void SetDefaults()
	{
		if (_id is null) return;
		Item.DefaultToPlaceableTile(Mod.Find<ModTile>(Name).Type);
		Item.width   = 32;
		Item.height  = 32;
		Item.maxStack = 99;
		Item.rare    = ItemRarityID.Green;
		Item.value   = Item.sellPrice(silver: 50);
	}

	public override void AddRecipes()
	{
		if (_gtTag is null) return;
		if (!VanillaItemMap.TryGetGroup(_gtTag, out int groupId)) return;
		CreateRecipe()
			.AddRecipeGroup(groupId, 1)
			.AddRecipeGroup(VanillaCraftingBridgeSystem.AnyWorkbench, 1)
			.DisableDecraft()
			.Register();
	}

	private int _overlayItem = -1;
	private int OverlayItem()
	{
		if (_overlayItem < 0)
			_overlayItem = (_id != null && CraftingStationRegistry.TryGetOverlayTool(_id, out int it)) ? it : 0;
		return _overlayItem;
	}

	private static Texture2D? OverlayImage(int ov)
	{
		if (ov == 0) return null;
		Items.Tools.ToolItemLoader.EnsureBaked(ov);
		return TextureAssets.Item[ov].Value;
	}

	private static Rectangle OverlayFrame(int ov, Texture2D img) =>
		Main.itemAnimations[ov]?.GetFrame(img) ?? img.Frame();

	public override void PostDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame,
		Color drawColor, Color itemColor, Vector2 origin, float scale)
	{
		int ov = OverlayItem();
		var img = OverlayImage(ov);
		if (img is null) return;
		var src = OverlayFrame(ov, img);
		sb.Draw(img, position, src, drawColor, 0f, src.Size() * 0.5f, scale, SpriteEffects.None, 0f);
	}

	public override void PostDrawInWorld(SpriteBatch sb, Color lightColor, Color alphaColor,
		float rotation, float scale, int whoAmI)
	{
		int ov = OverlayItem();
		var img = OverlayImage(ov);
		if (img is null) return;
		var src = OverlayFrame(ov, img);
		sb.Draw(img, Item.Center - Main.screenPosition, src, lightColor, rotation,
			src.Size() * 0.5f, scale * 0.7f, SpriteEffects.None, 0f);
	}

	public void WarmUpTexture()
	{
		if (_id is null || _aliased || Main.dedServ) return;
		if (Item.type < TextureAssets.Item.Length && ItemID.Autohammer < TextureAssets.Item.Length)
		{
			Main.instance.LoadItem(ItemID.Autohammer);
			TextureAssets.Item[Item.type] = TextureAssets.Item[ItemID.Autohammer];
			_aliased = true;
		}
	}
}
