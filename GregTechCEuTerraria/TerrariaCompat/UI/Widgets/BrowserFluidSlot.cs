#nullable enable
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

public static class BrowserFluidSlot
{
	private const int LabelHeight = 10;

	public static void Draw(SpriteBatch sb, Rectangle dest, FluidType? fluid,
		int amountMb = 0, string? fallbackLabel = null, Color? lightColor = null,
		int amountBottomInset = LabelHeight, float labelScale = 1f)
	{
		var tint = lightColor ?? Color.White;
		float alpha = tint.A / 255f;
		var px = TextureAssets.MagicPixel.Value;

		sb.Draw(px, dest, new Color(20, 25, 50) * alpha);

		var inner = new Rectangle(dest.X + 2, dest.Y + 2, dest.Width - 4, dest.Height - 4);
		if (fluid is null || !FluidIconRenderer.Draw(sb, fluid, inner, alpha))
		{
			Color fallback = new(80, 80, 200);
			if (fluid is not null)
			{
				uint c = fluid.Color;
				fallback = new Color((byte)((c >> 16) & 0xFF),
					(byte)((c >> 8) & 0xFF), (byte)(c & 0xFF));
			}
			sb.Draw(px, inner, fallback * alpha);
		}

		TankFrame.DrawBorder(sb, dest, TankFrame.BorderColor * alpha);

		string label = fluid?.DisplayName ?? fallbackLabel ?? "?";
		if (label.Length > 0)
		{
			Terraria.Utils.DrawBorderString(sb,
				label.Substring(0, System.Math.Min(2, label.Length)).ToUpperInvariant(),
				new Vector2(dest.X + 2f * labelScale, dest.Y + 2f * labelScale),
				tint, 0.6f * labelScale);
		}

		if (amountMb > 0)
		{
			Terraria.Utils.DrawBorderString(sb, UINumberFormat.Fluid(amountMb),
				new Vector2(dest.X + 2, dest.Bottom - amountBottomInset),
				tint, 0.6f);
		}
	}

	public static void EmitTooltip(FluidType? fluid, int amountMb = 0,
		string? fallbackLabel = null, string? extraLine = null)
	{
		string name = fluid?.DisplayName ?? fallbackLabel ?? "?";
		Color nameColor = Color.White;
		if (fluid is not null)
		{
			uint c = fluid.Color;
			nameColor = Readable(new Color((byte)((c >> 16) & 0xFF),
				(byte)((c >> 8) & 0xFF), (byte)(c & 0xFF)));
		}

		BrowserTooltipGlobal.Begin(replace: true);
		BrowserTooltipGlobal.Append(name, nameColor);
		if (amountMb > 0)
			BrowserTooltipGlobal.Append(amountMb.ToString("N0") + " mB", BrowserTooltipGlobal.Detail);
		if (fluid is not null)
			BrowserTooltipGlobal.Append(FluidTooltips.Temperature(fluid), FluidTooltips.TemperatureColor);
		if (!string.IsNullOrEmpty(extraLine))
			foreach (var line in extraLine!.Split('\n'))
				if (line.Length > 0)
					BrowserTooltipGlobal.Append(line, BrowserTooltipGlobal.Detail);

		if (fluid is not null) BrowserHover.SetFluid(fluid.Id, fluid.DisplayName);
		else BrowserHover.SetNone();

		Main.HoverItem = new Item();
		Main.HoverItem.SetDefaults(Terraria.ID.ItemID.WaterBucket);
		Main.LocalPlayer.cursorItemIconEnabled = false;
		Main.instance.MouseText("");
	}

	private static Color Readable(Color c)
	{
		int max = System.Math.Max(c.R, System.Math.Max(c.G, c.B));
		if (max == 0) return Color.White;
		if (max >= 140) return c;
		float s = 140f / max;
		return new Color(
			(byte)System.Math.Min(255, (int)(c.R * s)),
			(byte)System.Math.Min(255, (int)(c.G * s)),
			(byte)System.Math.Min(255, (int)(c.B * s)));
	}
}
