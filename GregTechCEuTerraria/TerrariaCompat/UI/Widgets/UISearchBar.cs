#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

// JEI-style text input. LMB=focus, RMB=clear, Esc/Enter=unfocus, key-repeat
// after InitialRepeatDelay every RepeatInterval.
public sealed class UISearchBar : UIElement
{
	private static UISearchBar? _focusedInstance;

	public static void UnfocusAll()
	{
		_focusedInstance?.Unsubscribe();
		_focusedInstance = null;
	}

	// Backspace key-repeat (KeyboardState has no auto-repeat).
	private const int BackRepeatDelay    = 25;   // ticks before repeat kicks in
	private const int BackRepeatInterval = 3;

	private string _text = "";
	private readonly string _placeholder;
	private readonly Action<string> _onChanged;
	private bool _subscribed;
	private KeyboardState _prevKb;
	private int _backHeld;

	public string Text => _text;
	public bool IsFocused => _focusedInstance == this;

	public UISearchBar(string placeholder, Action<string> onChanged)
	{
		_placeholder = placeholder;
		_onChanged = onChanged;
		OnLeftMouseDown  += (_, _) => Focus();
		OnRightMouseDown += (_, _) => { SetText(""); Focus(); };
	}

	private void Focus()
	{
		// At most one search bar is focused (and thus subscribed) at a time.
		if (_focusedInstance != null && _focusedInstance != this)
			_focusedInstance.Unsubscribe();
		_focusedInstance = this;
		Subscribe();
		_prevKb = Keyboard.GetState();
		_backHeld = 0;
	}

	public void Unfocus()
	{
		if (IsFocused) _focusedInstance = null;
		Unsubscribe();
	}

	private void Subscribe()
	{
		if (_subscribed) return;
		TextInputEXT.TextInput += OnTextInput;
		_subscribed = true;
	}

	private void Unsubscribe()
	{
		if (!_subscribed) return;
		TextInputEXT.TextInput -= OnTextInput;
		_subscribed = false;
	}

	private void OnTextInput(char c)
	{
		if (IsFocused && c >= ' ' && c != (char)127)
			SetText(_text + c);
	}

	public void SetText(string text)
	{
		if (_text == text) return;
		_text = text;
		_onChanged(_text);
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);

		var bounds = GetDimensions().ToRectangle();
		bool over = bounds.Contains((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y);

		if (IsFocused && Main.mouseLeft && !over) Unfocus();

		if (IsFocused)
		{
			PlayerInput.WritingText = true;

			var kb = Keyboard.GetState();
			if (Edge(kb, Keys.Enter) || Edge(kb, Keys.Escape))
			{
				Unfocus();
			}
			else if (kb.IsKeyDown(Keys.Back))
			{
				_backHeld++;
				bool fire = _backHeld == 1 ||
					(_backHeld >= BackRepeatDelay && (_backHeld - BackRepeatDelay) % BackRepeatInterval == 0);
				if (fire && _text.Length > 0) SetText(_text.Substring(0, _text.Length - 1));
			}
			else
			{
				_backHeld = 0;
			}
			_prevKb = kb;
		}

		if (over) Main.LocalPlayer.mouseInterface = true;
	}

	private bool Edge(KeyboardState kb, Keys k) => kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k);

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		var bounds = GetDimensions().ToRectangle();
		var px = TextureAssets.MagicPixel.Value;

		spriteBatch.Draw(px, bounds, new Color(8, 10, 28) * 0.85f);
		DrawBorder(spriteBatch, px, bounds, IsFocused ? new Color(255, 235, 140) : new Color(60, 70, 100));

		var font = FontAssets.MouseText.Value;
		const float scale = 0.85f;
		float textY = bounds.Y + (bounds.Height - font.LineSpacing * scale) / 2f - 1;
		bool empty = _text.Length == 0;

		Terraria.Utils.DrawBorderString(spriteBatch,
			empty ? _placeholder : _text,
			new Vector2(bounds.X + 6, textY),
			empty ? new Color(140, 140, 160) : Color.White,
			scale);

		if (IsFocused && (Main.GameUpdateCount % 30) < 15)
		{
			float w = empty ? 0f : font.MeasureString(_text).X * scale;
			Terraria.Utils.DrawBorderString(spriteBatch, "|",
				new Vector2(bounds.X + 6 + w, textY),
				Color.LightYellow, scale);
		}
	}

	private static void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle b, Color c)
	{
		sb.Draw(px, new Rectangle(b.X, b.Y, b.Width, 1), c);
		sb.Draw(px, new Rectangle(b.X, b.Bottom - 1, b.Width, 1), c);
		sb.Draw(px, new Rectangle(b.X, b.Y, 1, b.Height), c);
		sb.Draw(px, new Rectangle(b.Right - 1, b.Y, 1, b.Height), c);
	}
}
