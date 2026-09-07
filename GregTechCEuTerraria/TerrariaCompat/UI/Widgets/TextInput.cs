#nullable enable
using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Localization.IME;
using ReLogic.OS;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

internal interface ITextInput
{
	void CommitInput();
}

internal static class ImeText
{
	internal static bool AnyFieldFocused => TextInputFocus.Current != null || UISearchBar.AnyFocused;

	internal static bool Composing =>
		!string.IsNullOrEmpty(Platform.Get<IImeService>().CompositionString);

	internal static string Composition => Platform.Get<IImeService>().CompositionString ?? "";

	internal static readonly Color CompositionColor = new(255, 240, 20);

	internal static string ConsumeComposed()
	{
		int n = Main.keyCount;
		if (n <= 0) return "";
		var sb = new StringBuilder(n);
		int len = Main.keyString.Length;
		for (int i = 0; i < n && i < len; i++)
			if (Main.keyInt[i] > 127)
				sb.Append(Main.keyString[i]);
		Main.keyCount = 0;
		return sb.ToString();
	}

	internal static void DrawPanel(SpriteBatch sb, Rectangle field)
	{
		var ime = Platform.Get<IImeService>();
		if (!ime.IsCandidateListVisible) return;
		uint count = ime.CandidateCount;
		if (count == 0) return;

		var font = FontAssets.MouseText.Value;
		const float scale = 0.85f;
		const float gap = 14f;
		const int voff = 32;
		const string fmt = "{0,2}: {1}";
		const string sep = "  ";

		float width = gap;
		for (uint i = 0; i < count; i++)
		{
			string t = string.Format(i < count - 1 ? fmt + sep : fmt, i + 1, ime.GetCandidate(i));
			width += font.MeasureString(t).X * scale + gap;
		}

		Vector2 position = new Vector2(field.X, field.Y);
		position.X = Math.Min(position.X, Main.screenWidth / Main.UIScale - width);
		position.X = Math.Max(position.X, 0f);
		Terraria.Utils.DrawSettings2Panel(sb, position + new Vector2(0f, -voff), width,
			new Color(63, 65, 151, 255) * 0.785f);

		position += new Vector2(10f, -voff / 2f);
		uint sel = ime.SelectedCandidate;
		for (uint i = 0; i < count; i++)
		{
			Color color = i == sel ? Color.White : Color.Gray;
			string t = string.Format(i < count - 1 ? fmt + sep : fmt, i + 1, ime.GetCandidate(i));
			Vector2 m = font.MeasureString(t) * scale;
			Terraria.Utils.DrawBorderString(sb, t, position, color, scale, 0f, 0.4f);
			position.X += m.X + gap;
		}
	}
}

internal sealed class TextInputImeSystem : ModSystem
{
	public override void PostUpdateInput()
	{
		if (ImeText.AnyFieldFocused)
			PlayerInput.WritingText = true;
	}
}

internal static class TextInputFocus
{
	private static ITextInput? _current;
	internal static ITextInput? Current => _current;

	internal static void Set(ITextInput input)
	{
		if (_current == input) return;
		_current?.CommitInput();
		_current = input;
	}

	internal static void Clear(ITextInput input)
	{
		if (_current == input) _current = null;
	}

	internal static void UnfocusAll()
	{
		_current?.CommitInput();
		_current = null;
	}
}

internal static class TextInputUtil
{
	internal static char? CharFor(Keys k, bool shift, bool forceUpper)
	{
		if (k >= Keys.A && k <= Keys.Z)
		{
			char c = (char)('a' + (k - Keys.A));
			return (shift || forceUpper) ? char.ToUpperInvariant(c) : c;
		}
		if (k >= Keys.D0 && k <= Keys.D9)
		{
			if (!shift) return (char)('0' + (k - Keys.D0));
			return k switch
			{
				Keys.D1 => '!', Keys.D2 => '@', Keys.D3 => '#', Keys.D4 => '$', Keys.D5 => '%',
				Keys.D6 => '^', Keys.D7 => '&', Keys.D8 => '*', Keys.D9 => '(', Keys.D0 => ')',
				_ => (char?)null,
			};
		}
		if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return (char)('0' + (k - Keys.NumPad0));
		return k switch
		{
			Keys.Space        => ' ',
			Keys.Multiply     => '*',
			Keys.Divide       => '/',
			Keys.Add          => '+',
			Keys.Subtract     => '-',
			Keys.Decimal      => '.',
			Keys.OemSemicolon => shift ? ':' : ';',
			Keys.OemQuestion  => shift ? '?' : '/',
			Keys.OemPipe      => shift ? '|' : '\\',
			Keys.OemMinus     => shift ? '_' : '-',
			Keys.OemPlus      => shift ? '+' : '=',
			Keys.OemPeriod    => shift ? '>' : '.',
			Keys.OemComma     => shift ? '<' : ',',
			Keys.OemQuotes    => shift ? '"' : '\'',
			Keys.OemOpenBrackets  => shift ? '{' : '[',
			Keys.OemCloseBrackets => shift ? '}' : ']',
			_                 => (char?)null,
		};
	}
}
