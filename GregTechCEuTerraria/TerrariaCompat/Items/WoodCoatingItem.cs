using System;
using System.Collections.Generic;
using System.Text;

using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items
{
	public sealed class WoodCoatingItem : ModItem
	{

		public override string Name => "wood_coating";
		public override string Texture => "GregTechCEuTerraria/Content/TerrariaCompat/wood_coating";

		public override void SetStaticDefaults()
		=> Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName",
			() => "Wood Coating");

		public override void SetDefaults()
		{
			Item.width = 30;
			Item.height = 30;
			Item.maxStack = 9999;
			Item.rare = ItemRarityID.Green;
		}


	}
}
