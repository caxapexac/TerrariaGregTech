#nullable enable
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Ammo;

// GregTech `round` material item is used as Bullet ammo
public sealed class RoundProjectile : ModProjectile
{
	public override string Texture =>
		"GregTechCEuTerraria/Content/Textures/item/material_sets/metallic/round";

	private int _tintRgb;

	public override void SetStaticDefaults()
	{
		ProjectileID.Sets.TrailCacheLength[Type] = 5;
		ProjectileID.Sets.TrailingMode[Type] = 0;
	}

	public override void SetDefaults()
	{
		Projectile.width = 8;
		Projectile.height = 8;
		Projectile.aiStyle = ProjAIStyleID.Arrow;
		Projectile.friendly = true;
		Projectile.hostile = false;
		Projectile.DamageType = DamageClass.Ranged;
		Projectile.penetrate = 1;
		Projectile.timeLeft = 600;
		Projectile.alpha = 255;
		Projectile.light = 0.4f;
		Projectile.ignoreWater = true;
		Projectile.tileCollide = true;
		Projectile.extraUpdates = 1;
		AIType = ProjectileID.Bullet;
	}

	public override void OnSpawn(IEntitySource source)
	{
		if (source is EntitySource_ItemUse_WithAmmo ammo
			&& ContentSamples.ItemsByType.TryGetValue(ammo.AmmoItemIdUsed, out var sample)
			&& sample.ModItem is MaterialItem mi
			&& mi.MaterialColorRgb is int rgb)
		{
			_tintRgb = rgb;
		}
	}

	public override void SendExtraAI(BinaryWriter writer) => writer.Write(_tintRgb);

	public override void ReceiveExtraAI(BinaryReader reader) => _tintRgb = reader.ReadInt32();

	public override bool PreDraw(ref Color lightColor)
	{
		var tex = TextureAssets.Projectile[Type].Value;
		Vector2 origin = tex.Size() * 0.5f;
		Color tint = _tintRgb > 0
			? new Color((byte)((_tintRgb >> 16) & 0xFF), (byte)((_tintRgb >> 8) & 0xFF), (byte)(_tintRgb & 0xFF))
			: Color.White;

		for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
		{
			Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + origin
				+ new Vector2(0f, Projectile.gfxOffY);
			Color trail = Projectile.GetAlpha(Multiply(lightColor, tint))
				* ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
			Main.EntitySpriteDraw(tex, drawPos, null, trail, Projectile.rotation, origin,
				Projectile.scale, SpriteEffects.None, 0);
		}

		Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
			Projectile.GetAlpha(Multiply(lightColor, tint)), Projectile.rotation, origin,
			Projectile.scale, SpriteEffects.None, 0);
		return false;
	}

	private static Color Multiply(Color a, Color b) => new(
		(byte)(a.R * b.R / 255),
		(byte)(a.G * b.G / 255),
		(byte)(a.B * b.B / 255),
		(byte)(a.A * b.A / 255));
}
