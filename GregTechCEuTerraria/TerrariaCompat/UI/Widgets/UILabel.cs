#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

// Static text label using Terraria.Utils.DrawBorderString so it stays readable over
// the translucent Terraria panel chrome. For values that change every tick
// use UIDynamicLabel instead.
public sealed class UILabel : UIElement
{
	private readonly string _text;
	private readonly float _scale;
	private readonly Color _color;

	public UILabel(string text, float scale = 0.85f, Color? color = null)
	{
		_text = text;
		_scale = scale;
		_color = color ?? Color.White;
		Width = StyleDimension.FromPixels(120);
		Height = StyleDimension.FromPixels(16);
	}

	// Display-only - never capture the mouse (see UIDynamicLabel).
	public override bool ContainsPoint(Vector2 point) => false;

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		var b = GetDimensions();
		Terraria.Utils.DrawBorderString(spriteBatch, _text, new Vector2(b.X, b.Y), _color, _scale);
	}
}
