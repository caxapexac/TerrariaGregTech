#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.OS;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

public sealed class UITextField : UIElement, ITextInput
{
	public static void UnfocusAll() => TextInputFocus.UnfocusAll();
	void ITextInput.CommitInput() => Commit();

	private const int InitialRepeatDelay = 25;
	private const int RepeatInterval = 3;
	private const float TextScale = 0.8f;
	private const int TextPadX = 5;

	private readonly Func<string> _current;
	private readonly Action<string> _onConfirm;
	private readonly Func<char, bool>? _filter;
	private readonly int _maxLength;
	private readonly string _placeholder;
	private readonly string? _tooltip;
	private readonly bool _forceUpper;
	private readonly Func<bool>? _isDisabled;
	private readonly string? _disabledTooltip;

	private bool Disabled => _isDisabled?.Invoke() ?? false;

	private string _buffer = "";
	private int _caret;
	private int _selAnchor = -1;
	private bool _mouseSelecting;
	private KeyboardState _prevKb;
	private Keys _heldKey = Keys.None;
	private int _heldTicks;
	private bool _wasComposing;

	public bool IsFocused => TextInputFocus.Current == (ITextInput)this;

	public UITextField(Func<string> current, Action<string> onConfirm,
		int maxLength = 32, Func<char, bool>? filter = null, string placeholder = "",
		string? tooltip = null, bool forceUpper = false,
		Func<bool>? isDisabled = null, string? disabledTooltip = null)
	{
		_current = current;
		_onConfirm = onConfirm;
		_maxLength = maxLength;
		_filter = filter;
		_placeholder = placeholder;
		_tooltip = tooltip;
		_forceUpper = forceUpper;
		_isDisabled = isDisabled;
		_disabledTooltip = disabledTooltip;
		OnLeftMouseDown += (_, _) => FocusFromMouse();
	}

	private void Focus()
	{
		if (IsFocused) return;
		TextInputFocus.Set(this);
		_buffer = _current() ?? "";
		_caret = _buffer.Length;
		_selAnchor = -1;
		_prevKb = Keyboard.GetState();
		_heldKey = Keys.None;
		_heldTicks = 0;
	}

	private void FocusFromMouse()
	{
		if (Disabled) return;
		Focus();
		_caret = CaretFromMouse();
		_selAnchor = _caret;
		_mouseSelecting = true;
	}

	private void Commit()
	{
		if (!IsFocused) return;
		TextInputFocus.Clear(this);
		_mouseSelecting = false;
		_onConfirm(_buffer);
	}

	private void Discard()
	{
		if (IsFocused) TextInputFocus.Clear(this);
		_mouseSelecting = false;
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		var bounds = GetDimensions().ToRectangle();
		bool over = bounds.Contains(ModalEscape.PollCursor());

		if (Disabled)
		{
			if (IsFocused) Discard();
			if (over)
			{
				Main.LocalPlayer.mouseInterface = true;
				string? tip = _disabledTooltip ?? _tooltip;
				if (tip != null) Main.instance.MouseText(tip);
			}
			return;
		}

		if (_mouseSelecting)
		{
			if (Main.mouseLeft) _caret = CaretFromMouse();
			else { _mouseSelecting = false; if (_selAnchor == _caret) _selAnchor = -1; }
		}
		else if (IsFocused && MouseClick.LeftPressed && !over)
		{
			Commit();
		}

		if (IsFocused)
		{
			PlayerInput.WritingText = true;
			Main.chatRelease = false;
			ProcessKeystrokes();
			var composed = ImeText.ConsumeComposed();
			if (composed.Length > 0) InsertText(composed);
		}
		if (over)
		{
			Main.LocalPlayer.mouseInterface = true;
			if (_tooltip != null) Main.instance.MouseText(_tooltip);
		}
	}

	private void ProcessKeystrokes()
	{
		var kb = Keyboard.GetState();
		Keys fired = FindFiringKey(kb);
		_prevKb = kb;
		bool composing = ImeText.Composing;
		bool suppress = composing || _wasComposing;
		_wasComposing = composing;
		if (suppress) return;
		if (fired == Keys.None) return;

		bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
		bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);

		if (ctrl)
		{
			switch (fired)
			{
				case Keys.A: _selAnchor = 0; _caret = _buffer.Length; return;
				case Keys.C: CopySelection(); return;
				case Keys.X: CopySelection(); DeleteSelection(); return;
				case Keys.V: InsertText(Platform.Get<IClipboard>().Value ?? ""); return;
			}
		}

		switch (fired)
		{
			case Keys.Enter: Main.chatRelease = false; Commit(); return;
			case Keys.Escape: Discard(); return;
			case Keys.Left: Move(_caret - 1, shift); return;
			case Keys.Right: Move(_caret + 1, shift); return;
			case Keys.Home: Move(0, shift); return;
			case Keys.End: Move(_buffer.Length, shift); return;
			case Keys.Back:
				if (HasSelection()) DeleteSelection();
				else if (_caret > 0) { _buffer = _buffer.Remove(_caret - 1, 1); _caret--; }
				return;
			case Keys.Delete:
				if (HasSelection()) DeleteSelection();
				else if (_caret < _buffer.Length) _buffer = _buffer.Remove(_caret, 1);
				return;
		}

		if (ctrl) return;
		char? ch = TextInputUtil.CharFor(fired, shift, _forceUpper);
		if (ch is { } c && (_filter is null || _filter(c)))
			InsertText(c.ToString());
	}

	private bool HasSelection() => _selAnchor >= 0 && _selAnchor != _caret;
	private int SelMin() => Math.Min(_selAnchor, _caret);
	private int SelMax() => Math.Max(_selAnchor, _caret);

	private void Move(int target, bool shift)
	{
		target = Math.Clamp(target, 0, _buffer.Length);
		if (shift)
		{
			if (_selAnchor < 0) _selAnchor = _caret;
			_caret = target;
		}
		else
		{
			_caret = target;
			_selAnchor = -1;
		}
	}

	private void DeleteSelection()
	{
		if (!HasSelection()) return;
		int a = SelMin(), b = SelMax();
		_buffer = _buffer.Remove(a, b - a);
		_caret = a;
		_selAnchor = -1;
	}

	private void InsertText(string s)
	{
		if (string.IsNullOrEmpty(s)) return;
		var sb = new System.Text.StringBuilder(s.Length);
		foreach (char c in s)
			if (c >= ' ' && c != (char)127 && (_filter is null || _filter(c)))
				sb.Append(c);
		string clean = sb.ToString();
		if (clean.Length == 0) return;

		if (HasSelection()) DeleteSelection();
		int room = _maxLength - _buffer.Length;
		if (room <= 0) return;
		if (clean.Length > room) clean = clean.Substring(0, room);
		_buffer = _buffer.Insert(_caret, clean);
		_caret += clean.Length;
	}

	private void CopySelection()
	{
		string text = HasSelection() ? _buffer.Substring(SelMin(), SelMax() - SelMin()) : _buffer;
		if (text.Length > 0) Platform.Get<IClipboard>().Value = text;
	}

	private static string Flat(string s) => s.Replace('\r', ' ').Replace('\n', ' ');

	private int CaretFromMouse()
	{
		var b = GetDimensions().ToRectangle();
		float mx = ModalEscape.PollCursorScreen().X - (b.X + TextPadX);
		var font = FontAssets.MouseText.Value;
		string s = Flat(_buffer);
		int best = 0;
		float bestDist = Math.Abs(mx);
		for (int i = 1; i <= s.Length; i++)
		{
			float w = font.MeasureString(s.Substring(0, i)).X * TextScale;
			float dist = Math.Abs(mx - w);
			if (dist < bestDist) { bestDist = dist; best = i; }
		}
		return best;
	}

	private Keys FindFiringKey(KeyboardState kb)
	{
		foreach (var key in kb.GetPressedKeys())
		{
			if (!_prevKb.IsKeyDown(key))
			{
				_heldKey = key;
				_heldTicks = 0;
				return key;
			}
		}
		if (_heldKey != Keys.None && kb.IsKeyDown(_heldKey))
		{
			_heldTicks++;
			int since = _heldTicks - InitialRepeatDelay;
			return since >= 0 && since % RepeatInterval == 0 ? _heldKey : Keys.None;
		}
		_heldKey = Keys.None;
		return Keys.None;
	}

	protected override void DrawSelf(SpriteBatch sb)
	{
		var bounds = GetDimensions().ToRectangle();
		var px = TextureAssets.MagicPixel.Value;
		bool dis = Disabled;
		sb.Draw(px, bounds, new Color(8, 10, 28) * (dis ? 0.5f : 0.85f));
		var border = dis ? new Color(40, 45, 60)
			: IsFocused ? new Color(255, 235, 140) : new Color(60, 70, 100);
		sb.Draw(px, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
		sb.Draw(px, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
		sb.Draw(px, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
		sb.Draw(px, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);

		string shown = Flat(IsFocused ? _buffer : (_current() ?? ""));
		string comp = IsFocused ? ImeText.Composition : "";
		bool empty = shown.Length == 0 && comp.Length == 0;
		var font = FontAssets.MouseText.Value;
		float tx = bounds.X + TextPadX;
		float ty = bounds.Y + (bounds.Height - font.LineSpacing * TextScale) / 2f - 1;

		if (IsFocused && HasSelection())
		{
			float x1 = font.MeasureString(shown.Substring(0, Math.Min(SelMin(), shown.Length))).X * TextScale;
			float x2 = font.MeasureString(shown.Substring(0, Math.Min(SelMax(), shown.Length))).X * TextScale;
			sb.Draw(px, new Rectangle((int)(tx + x1), (int)ty + 1,
				(int)(x2 - x1), (int)(font.LineSpacing * TextScale)), new Color(70, 110, 200) * 0.6f);
		}

		if (empty)
		{
			Terraria.Utils.DrawBorderString(sb, _placeholder, new Vector2(tx, ty), new Color(140, 140, 160), TextScale);
		}
		else
		{
			int caret = Math.Min(_caret, shown.Length);
			string left = shown.Substring(0, caret);
			string right = shown.Substring(caret);
			var textColor = dis ? new Color(110, 110, 125) : Color.White;
			float x = tx;
			Terraria.Utils.DrawBorderString(sb, left, new Vector2(x, ty), textColor, TextScale);
			x += font.MeasureString(left).X * TextScale;
			if (comp.Length > 0)
			{
				Terraria.Utils.DrawBorderString(sb, comp, new Vector2(x, ty), ImeText.CompositionColor, TextScale);
				x += font.MeasureString(comp).X * TextScale;
			}
			if (right.Length > 0)
				Terraria.Utils.DrawBorderString(sb, right, new Vector2(x, ty), textColor, TextScale);
			if (IsFocused && UiFrame.Count % 30 < 15)
				Terraria.Utils.DrawBorderString(sb, "|", new Vector2(x, ty), Color.LightYellow, TextScale);
		}

		if (IsFocused) ImeText.DrawPanel(sb, bounds);
	}
}
