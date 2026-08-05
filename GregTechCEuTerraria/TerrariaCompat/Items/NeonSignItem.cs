#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Tiles.NeonSign;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items;

public sealed class NeonSignItem : ModItem
{
	public override string Name => "neon_sign";
	public override string Texture => "GregTechCEuTerraria/Content/TerrariaCompat/NeonSignItem";

	public override void SetStaticDefaults()
		=> Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName",
			() => "Neon Sign");

	public override void SetDefaults()
	{
		Item.DefaultToPlaceableTile(ModContent.TileType<NeonSignTile>());
		Item.width    = 24;
		Item.height   = 24;
		Item.maxStack = 9999;
		Item.rare     = ItemRarityID.Green;
		Item.value    = Item.buyPrice(silver: 2);
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		tooltips.Add(new TooltipLine(Mod, "Neon", "Right-click to edit the glowing text"));
		tooltips.Add(new TooltipLine(Mod, "NeonNote", "[c/A0A0A0:Pick a color and size; text renders in the world]"));
	}
}
