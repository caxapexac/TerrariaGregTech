#nullable enable
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace GregTechCEuTerraria.TerrariaCompat.UI.WorldInteract;

public static class WorldCursor
{
	public static void RawWorld(out float wx, out float wy)
	{
		var zoom = Main.GameViewMatrix.ZoomMatrix;
		float zx = zoom.M11 <= 0 ? 1f : zoom.M11;
		float zy = zoom.M22 <= 0 ? 1f : zoom.M22;
		float halfW = Main.screenWidth * 0.5f, halfH = Main.screenHeight * 0.5f;
		wx = Main.screenPosition.X + halfW + (Main.mouseX - halfW) / zx;
		wy = Main.screenPosition.Y + halfH + (Main.mouseY - halfH) / zy;
	}

	public static void RawCell(out int x, out int y)
	{
		RawWorld(out float wx, out float wy);
		x = (int)System.Math.Floor(wx / 16f);
		y = (int)System.Math.Floor(wy / 16f);
	}

	public static void WorldToUi(float worldX, float worldY, out float uiX, out float uiY)
	{
		var screen = Vector2.Transform(new Vector2(worldX, worldY) - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
		uiX = screen.X / Main.UIScale;
		uiY = screen.Y / Main.UIScale;
	}

	public static void UiScreenSize(out float uiW, out float uiH)
	{
		var size = PlayerInput.OriginalScreenSize;
		uiW = size.X / Main.UIScale;
		uiH = size.Y / Main.UIScale;
	}
}
