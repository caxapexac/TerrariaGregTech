#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.OS;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

public sealed class UITextArea : UIElement, ITextInput
{
	private const int InitialRepeatDelay = 25;
	private const int RepeatInterval = 3;
	private const float TextScale = 0.78f;
	private const int PadX = 6;
	private const int PadY = 5;

	private readonly Func<string> _current;
	private readonly Action<string> _onConfirm;
	private readonly int _maxLength;
	private readonly string _placeholder;
	private readonly bool _commitOnEscape;

	private string _buffer = "";
	private int _caret;
	private int _selAnchor = -1;
	private bool _mouseSelecting;
	private int _scrollLine;
	private KeyboardState _prevKb;
	private Keys _heldKey = Keys.None;
	private int _heldTicks;
	private bool _wasComposing;

	private readonly List<(int Start, int Len)> _lines = new();
	private bool _layoutDirty = true;
	private float _layoutWidth = -1;
	private float _innerW;

	public bool IsFocused => TextInputFocus.Current == (ITextInput)this;
	void ITextInput.CommitInput() => Commit();

	public UITextArea(Func<string> current, Action<string> onConfirm, int maxLength = 1024,
		string placeholder = "", bool commitOnEscape = false)
	{
		_current = current;
		_onConfirm = onConfirm;
		_maxLength = maxLength;
		_placeholder = placeholder;
		_commitOnEscape = commitOnEscape;
		OnLeftMouseDown += (_, _) => FocusFromMouse();
	}

	private static float LineHeight => FontAssets.MouseText.Value.LineSpacing * TextScale;

	private void Focus()
	{
		if (IsFocused) return;
		TextInputFocus.Set(this);
		_buffer = _current() ?? "";
		_caret = _buffer.Length;
		_selAnchor = -1;
		_scrollLine = 0;
		_layoutDirty = true;
		_prevKb = Keyboard.GetState();
		_heldKey = Keys.None;
		_heldTicks = 0;
	}

	private void FocusFromMouse()
	{
		Focus();
		_innerW = GetDimensions().ToRectangle().Width - PadX * 2;
		EnsureLayout();
		_caret = CaretFromPoint(ModalEscape.PollCursorScreen());
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

	public override void ScrollWheel(UIScrollWheelEvent evt)
	{
		base.ScrollWheel(evt);
		if (!IsFocused || !ContainsPoint(ModalEscape.PollCursorScreen())) return;
		_scrollLine = Math.Clamp(_scrollLine - Math.Sign(evt.ScrollWheelValue), 0, MaxScroll());
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		var bounds = GetDimensions().ToRectangle();
		_innerW = bounds.Width - PadX * 2;
		bool over = bounds.Contains(ModalEscape.PollCursor());

		if (_mouseSelecting)
		{
			if (Main.mouseLeft) { EnsureLayout(); _caret = CaretFromPoint(ModalEscape.PollCursorScreen()); }
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
			EnsureLayout();
			ProcessKeystrokes();
			var composed = ImeText.ConsumeComposed();
			if (composed.Length > 0) InsertText(composed);
			EnsureCaretVisible();
		}
		if (over)
			Main.LocalPlayer.mouseInterface = true;
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

		int line = CaretLine();
		switch (fired)
		{
			case Keys.Enter: Main.chatRelease = false; InsertText("\n"); return;
			case Keys.Escape: if (_commitOnEscape) Commit(); else Discard(); return;
			case Keys.Left: MoveCaret(_caret - 1, shift); return;
			case Keys.Right: MoveCaret(_caret + 1, shift); return;
			case Keys.Up: MoveVertical(-1, shift); return;
			case Keys.Down: MoveVertical(1, shift); return;
			case Keys.Home: MoveCaret(_lines[line].Start, shift); return;
			case Keys.End: MoveCaret(_lines[line].Start + _lines[line].Len, shift); return;
			case Keys.Back:
				if (HasSelection()) DeleteSelection();
				else if (_caret > 0) { _buffer = _buffer.Remove(_caret - 1, 1); _caret--; _layoutDirty = true; }
				return;
			case Keys.Delete:
				if (HasSelection()) DeleteSelection();
				else if (_caret < _buffer.Length) { _buffer = _buffer.Remove(_caret, 1); _layoutDirty = true; }
				return;
		}

		if (ctrl) return;
		char? ch = TextInputUtil.CharFor(fired, shift, forceUpper: false);
		if (ch is { } c) InsertText(c.ToString());
	}

	private bool HasSelection() => _selAnchor >= 0 && _selAnchor != _caret;
	private int SelMin() => Math.Min(_selAnchor, _caret);
	private int SelMax() => Math.Max(_selAnchor, _caret);

	private void MoveCaret(int target, bool shift)
	{
		target = Math.Clamp(target, 0, _buffer.Length);
		if (shift) { if (_selAnchor < 0) _selAnchor = _caret; _caret = target; }
		else { _caret = target; _selAnchor = -1; }
	}

	private void MoveVertical(int dir, bool shift)
	{
		int line = CaretLine();
		int target = line + dir;
		if (target < 0 || target >= _lines.Count) return;
		float x = CaretX(line);
		int col = ColNearestX(target, x);
		MoveCaret(_lines[target].Start + col, shift);
	}

	private void DeleteSelection()
	{
		if (!HasSelection()) return;
		int a = SelMin(), b = SelMax();
		_buffer = _buffer.Remove(a, b - a);
		_caret = a;
		_selAnchor = -1;
		_layoutDirty = true;
	}

	private void InsertText(string s)
	{
		if (string.IsNullOrEmpty(s)) return;
		var sb = new System.Text.StringBuilder(s.Length);
		foreach (char c in s.Replace("\r\n", "\n").Replace('\r', '\n'))
			if (c == '\n' || (c >= ' ' && c != (char)127)) sb.Append(c);
		string clean = sb.ToString();
		if (clean.Length == 0) return;

		if (HasSelection()) DeleteSelection();
		int room = _maxLength - _buffer.Length;
		if (room <= 0) return;
		if (clean.Length > room) clean = clean.Substring(0, room);
		_buffer = _buffer.Insert(_caret, clean);
		_caret += clean.Length;
		_layoutDirty = true;
	}

	private void CopySelection()
	{
		string text = HasSelection() ? _buffer.Substring(SelMin(), SelMax() - SelMin()) : _buffer;
		if (text.Length > 0) Platform.Get<IClipboard>().Value = text;
	}

	private void EnsureLayout()
	{
		if (!_layoutDirty && Math.Abs(_layoutWidth - _innerW) < 0.5f) return;
		RebuildLayout(Math.Max(20f, _innerW));
		_layoutWidth = _innerW;
		_layoutDirty = false;
	}

	private void RebuildLayout(float maxWidth)
	{
		_lines.Clear();
		var font = FontAssets.MouseText.Value;
		string b = _buffer;
		int n = b.Length;
		int lineStart = 0, lastSpace = -1;
		float width = 0f;
		for (int i = 0; i < n; i++)
		{
			char ch = b[i];
			if (ch == '\n')
			{
				_lines.Add((lineStart, i - lineStart));
				lineStart = i + 1; lastSpace = -1; width = 0f;
				continue;
			}
			float cw = font.MeasureString(ch.ToString()).X * TextScale;
			if (ch == ' ') lastSpace = i;
			if (width + cw > maxWidth && i > lineStart)
			{
				int breakAt = lastSpace >= lineStart ? lastSpace + 1 : i;
				_lines.Add((lineStart, breakAt - lineStart));
				lineStart = breakAt; lastSpace = -1;
				width = font.MeasureString(b.Substring(lineStart, i - lineStart + 1)).X * TextScale;
			}
			else
			{
				width += cw;
			}
		}
		_lines.Add((lineStart, n - lineStart));
	}

	private int CaretLine()
	{
		for (int li = 0; li < _lines.Count; li++)
		{
			var (s, len) = _lines[li];
			int end = s + len;
			if (_caret < end) return li;
			if (_caret == end)
			{
				bool last = li == _lines.Count - 1;
				bool hardEnd = end < _buffer.Length && _buffer[end] == '\n';
				if (last || hardEnd) return li;
			}
		}
		return Math.Max(0, _lines.Count - 1);
	}

	private float Measure(int start, int count)
		=> FontAssets.MouseText.Value.MeasureString(_buffer.Substring(start, count)).X * TextScale;

	private float CaretX(int line)
	{
		var (s, _) = _lines[line];
		return Measure(s, _caret - s);
	}

	private int ColNearestX(int line, float x)
	{
		var (s, len) = _lines[line];
		int best = 0;
		float bestDist = Math.Abs(x);
		for (int c = 1; c <= len; c++)
		{
			float w = Measure(s, c);
			float d = Math.Abs(x - w);
			if (d < bestDist) { bestDist = d; best = c; }
		}
		return best;
	}

	private int CaretFromPoint(Vector2 mouse)
	{
		var b = GetDimensions().ToRectangle();
		float relY = mouse.Y - (b.Y + PadY);
		int li = Math.Clamp(_scrollLine + (int)(relY / LineHeight), 0, Math.Max(0, _lines.Count - 1));
		float mx = mouse.X - (b.X + PadX);
		var (s, _) = _lines[li];
		return s + ColNearestX(li, mx);
	}

	private int VisibleLines()
	{
		var b = GetDimensions().ToRectangle();
		return Math.Max(1, (int)((b.Height - PadY * 2) / LineHeight));
	}

	private int MaxScroll() => Math.Max(0, _lines.Count - VisibleLines());

	private void EnsureCaretVisible()
	{
		int vis = VisibleLines();
		int cl = CaretLine();
		if (cl < _scrollLine) _scrollLine = cl;
		else if (cl >= _scrollLine + vis) _scrollLine = cl - vis + 1;
		_scrollLine = Math.Clamp(_scrollLine, 0, MaxScroll());
	}

	private Keys FindFiringKey(KeyboardState kb)
	{
		foreach (var key in kb.GetPressedKeys())
		{
			if (!_prevKb.IsKeyDown(key)) { _heldKey = key; _heldTicks = 0; return key; }
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
		_innerW = bounds.Width - PadX * 2;
		var px = TextureAssets.MagicPixel.Value;
		sb.Draw(px, bounds, new Color(8, 10, 28) * 0.85f);
		Color border = IsFocused ? new Color(255, 235, 140) : new Color(60, 70, 100);
		sb.Draw(px, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
		sb.Draw(px, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
		sb.Draw(px, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
		sb.Draw(px, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);

		string comp = IsFocused ? ImeText.Composition : "";
		string savedBuffer = _buffer;
		int savedCaret = _caret, savedSel = _selAnchor;
		if (comp.Length > 0)
		{
			int at = Math.Min(_caret, _buffer.Length);
			_buffer = _buffer.Insert(at, comp);
			_caret = at + comp.Length;
			_selAnchor = -1;
			_layoutDirty = true;
		}

		if (!IsFocused) { _buffer = _current() ?? ""; _layoutDirty = true; }
		EnsureLayout();

		var font = FontAssets.MouseText.Value;
		float lineH = LineHeight;
		float textX = bounds.X + PadX;
		float textY = bounds.Y + PadY;

		if (_buffer.Length == 0 && !IsFocused)
		{
			Terraria.Utils.DrawBorderString(sb, _placeholder, new Vector2(textX, textY),
				new Color(140, 140, 160), TextScale);
			return;
		}

		int vis = VisibleLines();
		int selMin = HasSelection() ? SelMin() : -1, selMax = HasSelection() ? SelMax() : -1;

		for (int row = 0; row < vis; row++)
		{
			int li = _scrollLine + row;
			if (li >= _lines.Count) break;
			var (s, len) = _lines[li];
			float y = textY + row * lineH;

			if (selMin >= 0 && selMin <= s + len && selMax > s)
			{
				int a = Math.Clamp(selMin, s, s + len);
				int b = Math.Clamp(selMax, s, s + len);
				float x1 = Measure(s, a - s);
				float x2 = Measure(s, b - s);
				if (selMax > s + len) x2 = Math.Max(x2, x1 + 4f);
				if (x2 - x1 >= 1f)
					sb.Draw(px, new Rectangle((int)(textX + x1), (int)y,
						(int)(x2 - x1), (int)lineH), new Color(70, 110, 200) * 0.6f);
			}

			if (len > 0)
				Terraria.Utils.DrawBorderString(sb, _buffer.Substring(s, len),
					new Vector2(textX, y), Color.White, TextScale);
		}

		if (IsFocused && UiFrame.Count % 30 < 15)
		{
			int cl = CaretLine();
			if (cl >= _scrollLine && cl < _scrollLine + vis)
			{
				float cx = textX + CaretX(cl);
				float cy = textY + (cl - _scrollLine) * lineH;
				Terraria.Utils.DrawBorderString(sb, "|", new Vector2(cx - 1, cy), Color.LightYellow, TextScale);
			}
		}

		if (IsFocused) ImeText.DrawPanel(sb, bounds);

		if (comp.Length > 0)
		{
			_buffer = savedBuffer;
			_caret = savedCaret;
			_selAnchor = savedSel;
			_layoutDirty = true;
		}
	}
}
