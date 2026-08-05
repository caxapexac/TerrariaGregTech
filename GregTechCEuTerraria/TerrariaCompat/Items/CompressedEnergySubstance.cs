#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items;

public sealed class CompressedEnergySubstance : ModItem, ITextureWarmUp
{
	private const string OrbTex = "GregTechCEuTerraria/Content/Textures/item/energy_cluster/8";
	private const float WhitenAmount = 0.55f;

	public override string Name => "compressed_energy_substance";
	public override string Texture => OrbTex;

	public override void SetStaticDefaults()
	{
		Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName",
			() => "Compressed Energy Substance");
		Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.Tooltip",
			() => "16 Energy Clusters pressed into one stackable unit");

		if (Main.dedServ) return;

		var tex = ModContent.Request<Texture2D>(Texture, AssetRequestMode.ImmediateLoad).Value;
		if (tex.Width > 0 && tex.Height > tex.Width && tex.Height % tex.Width == 0)
		{
			int frames = tex.Height / tex.Width;
			Main.RegisterItemAnimation(Type, new DrawAnimationVertical(
				MachineRenderer.AnimationTicksPerFrame, frames));
		}
	}

	public override void SetDefaults()
	{
		Item.width = 32;
		Item.height = 32;
		Item.maxStack = 9999;
		Item.rare = ItemRarityID.Purple;
		Item.value = 0;
	}

	public override void HoldItem(Player player)
	{
		base.HoldItem(player);
		ItemIconBaker.Install(Item.type, new IconLayer(OrbTex, Color.White, 1f, WhitenAmount));
	}

	void ITextureWarmUp.WarmUpTexture() =>
		ItemIconBaker.Install(Item.type, new IconLayer(OrbTex, Color.White, 1f, WhitenAmount));
}
