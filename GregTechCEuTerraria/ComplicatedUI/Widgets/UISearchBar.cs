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

public sealed class UISearchBar : UIElement
{
	private static UISearchBar? _focusedInstance;

	public static bool AnyFocused => _focusedInstance != null;

	public static void UnfocusAll()
	{
		_focusedInstance?.Unsubscribe();
		_focusedInstance = null;
	}

	private const int BackRepeatDelay    = 25;
	private const int BackRepeatInterval = 3;

	private string _text = "";
	private readonly string _placeholder;
	private readonly Action<string> _onChanged;
	private bool _subscribed;
	private KeyboardState _prevKb;
	private int _backHeld;
	private bool _wasComposing;

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
		if (IsFocused && c >= ' ' && c < (char)127)
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
		bool over = bounds.Contains(ModalEscape.PollCursor());

		if (IsFocused && MouseClick.LeftPressed && !over) Unfocus();

		if (IsFocused)
		{
			PlayerInput.WritingText = true;
			Main.chatRelease = false;

			var kb = Keyboard.GetState();
			bool composing = ImeText.Composing;
			bool suppress = composing || _wasComposing;
			_wasComposing = composing;
			if (!suppress)
			{
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
			}
			_prevKb = kb;

			var composed = ImeText.ConsumeComposed();
			if (composed.Length > 0) SetText(_text + composed);
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
		string comp = IsFocused ? ImeText.Composition : "";
		bool empty = _text.Length == 0 && comp.Length == 0;

		float x = bounds.X + 6;
		if (empty)
		{
			Terraria.Utils.DrawBorderString(spriteBatch, _placeholder,
				new Vector2(x, textY), new Color(140, 140, 160), scale);
		}
		else
		{
			Terraria.Utils.DrawBorderString(spriteBatch, _text, new Vector2(x, textY), Color.White, scale);
			x += font.MeasureString(_text).X * scale;
			if (comp.Length > 0)
			{
				Terraria.Utils.DrawBorderString(spriteBatch, comp, new Vector2(x, textY), ImeText.CompositionColor, scale);
				x += font.MeasureString(comp).X * scale;
			}
		}

		if (IsFocused && (UiFrame.Count % 30) < 15)
			Terraria.Utils.DrawBorderString(spriteBatch, "|", new Vector2(x, textY), Color.LightYellow, scale);

		if (IsFocused) ImeText.DrawPanel(spriteBatch, bounds);
	}

	private static void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle b, Color c)
	{
		sb.Draw(px, new Rectangle(b.X, b.Y, b.Width, 1), c);
		sb.Draw(px, new Rectangle(b.X, b.Bottom - 1, b.Width, 1), c);
		sb.Draw(px, new Rectangle(b.X, b.Y, 1, b.Height), c);
		sb.Draw(px, new Rectangle(b.Right - 1, b.Y, 1, b.Height), c);
	}
}
