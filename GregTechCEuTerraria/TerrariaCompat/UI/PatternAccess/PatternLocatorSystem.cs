#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.UI.PatternAccess;

public sealed class PatternLocatorSystem : ModSystem
{
	private static Point16 _target;
	private static long _expireTick = -1;
	private const int DurationTicks = 600;

	public static void Locate(Point16 providerPos)
	{
		_target = providerPos;
		_expireTick = UiFrame.Count + DurationTicks;
	}

	public override void ClearWorld()
	{
		_target = default;
		_expireTick = -1;
	}

	public override void PostDrawTiles()
	{
		if (Main.dedServ || UiFrame.Count > _expireTick) return;

		var start = Main.LocalPlayer.Center;
		var end = new Vector2(_target.X * 16f + 16f, _target.Y * 16f + 16f);

		var sb = Main.spriteBatch;
		sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
			DepthStencilState.None, RasterizerState.CullCounterClockwise, null,
			Main.GameViewMatrix.TransformationMatrix);

		float life = (_expireTick - UiFrame.Count) / (float)DurationTicks;
		float alpha = System.Math.Min(1f, life * 4f);
		DrawLine(sb, start - Main.screenPosition, end - Main.screenPosition,
			new Color(120, 200, 255) * (0.85f * alpha), 4f);
		var px = TextureAssets.MagicPixel.Value;
		var dot = (end - Main.screenPosition);
		sb.Draw(px, new Rectangle((int)dot.X - 4, (int)dot.Y - 4, 8, 8), new Color(120, 200, 255) * alpha);

		sb.End();
	}

	private static void DrawLine(SpriteBatch sb, Vector2 a, Vector2 b, Color color, float thickness)
	{
		var delta = b - a;
		float len = delta.Length();
		if (len < 1f) return;
		sb.Draw(TextureAssets.MagicPixel.Value, a, new Rectangle(0, 0, 1, 1), color,
			delta.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, thickness),
			SpriteEffects.None, 0f);
	}
}
