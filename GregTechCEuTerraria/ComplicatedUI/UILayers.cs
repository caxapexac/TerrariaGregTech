#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public static class UILayers
{
	private static readonly Dictionary<string, Func<bool>> _modalIsOpen = new();
	private static readonly Dictionary<string, Func<bool>> _modalCursorOver = new();
	private static readonly Dictionary<string, Func<IEnumerable<Rectangle>>> _modalBounds = new();
	private static readonly List<string> _modalStack = new();
	private static uint _stackSyncTick = uint.MaxValue;

	private static uint _lastPushTick = uint.MaxValue;
	public static bool PushedThisFrame => _lastPushTick == UiFrame.Count;

	public static void Push(string name)
	{
		_modalStack.Remove(name);
		_modalStack.Add(name);
		_lastPushTick = UiFrame.Count;
	}

	private static void SyncModalStack()
	{
		if (_stackSyncTick == UiFrame.Count) return;
		_stackSyncTick = UiFrame.Count;
		int before = _modalStack.Count;
		_modalStack.RemoveAll(n => !_modalIsOpen.TryGetValue(n, out var f) || !f());
		if (Main.mouseLeft && _modalStack.Count < before) _pressSpent = true;
	}

	public static void RegisterModal(string name, Func<bool> isOpen)
		=> _modalIsOpen[name] = isOpen;

	public static bool IsModalOpen(string name)
		=> _modalIsOpen.TryGetValue(name, out var f) && f();

	public static void RegisterModalCursorProbe(string name, Func<bool> cursorOver)
		=> _modalCursorOver[name] = cursorOver;

	public static void RegisterModalBoundsProbe(string name, Func<IEnumerable<Rectangle>> bounds)
		=> _modalBounds[name] = bounds;

	public static IEnumerable<Rectangle> OpenModalBounds(string? excludeName = null)
	{
		SyncModalStack();
		foreach (var name in _modalStack)
		{
			if (name == excludeName) continue;
			if (!_modalBounds.TryGetValue(name, out var f)) continue;
			foreach (var r in f())
				if (r.Width > 0 && r.Height > 0) yield return r;
		}
	}

	public static bool IsCursorOverAnyModal()
	{
		SyncModalStack();
		foreach (var name in _modalStack)
			if (_modalCursorOver.TryGetValue(name, out var f) && f())
				return true;
		return false;
	}

	public static string? TopmostModalAtCursor()
	{
		SyncModalStack();
		for (int i = _modalStack.Count - 1; i >= 0; i--)
			if (_modalCursorOver.TryGetValue(_modalStack[i], out var f) && f())
				return _modalStack[i];
		return null;
	}

	public static bool IsCursorOverHigherModal(string myName)
	{
		SyncModalStack();
		int i = _modalStack.IndexOf(myName);
		if (i < 0) return false;
		for (int j = i + 1; j < _modalStack.Count; j++)
			if (_modalCursorOver.TryGetValue(_modalStack[j], out var f) && f())
				return true;
		return false;
	}

	private static uint _claimTick = uint.MaxValue;
	private static string? _pressOwner;
	private static bool _pressSpent;

	private static void SyncClaim()
	{
		if (_claimTick == UiFrame.Count) return;
		_claimTick = UiFrame.Count;
		SyncModalStack();
		int count = _modalStack.Count;
		if (count == 0 || !Main.mouseLeft)
		{
			_pressOwner = null;
			_pressSpent = false;
		}
		else
		{
			if (MouseClick.LeftPressed)
			{
				_pressOwner = TopmostModalAtCursor();
				_pressSpent = false;
			}
			foreach (var n in _modalStack)
				if (!(_modalIsOpen.TryGetValue(n, out var f) && f())) { _pressSpent = true; break; }
		}
	}

	public static bool PressBelongsToAnotherModal(string myName)
	{
		SyncClaim();
		if (!Main.mouseLeft) return false;
		if (_pressSpent) return true;
		return _pressOwner != null && _pressOwner != myName;
	}

	public static string? TopModal
	{ get { SyncModalStack(); return _modalStack.Count > 0 ? _modalStack[^1] : null; } }

	public static bool IsTopModal(string name)
	{ SyncModalStack(); return _modalStack.Count > 0 && _modalStack[^1] == name; }

	private static int ModalDepth(string name) { SyncModalStack(); return _modalStack.IndexOf(name); }

	public static void InsertButton(
		List<GameInterfaceLayer> layers, string layerName, GameInterfaceDrawMethod drawFn)
		=> Insert(layers, layerName, drawFn, AnchorPlacement.AboveAccessoryBar);

	private const int BtnX = 580, BtnTop = 86, BtnStep = 36, BtnSize = 30;
	public static void DrawStackedButton(
		int slot, Color background, Action<SpriteBatch, Rectangle> drawIcon,
		string tooltip, Action onClick, Func<bool>? visible = null, ModKeybind? keybind = null)
	{
		if (Main.dedServ || !Main.playerInventory) return;
		if (visible != null && !visible()) return;

		var rect = new Rectangle(BtnX, BtnTop + slot * BtnStep, BtnSize, BtnSize);
		bool hover = rect.Contains(new Point(Main.mouseX, Main.mouseY))
			&& !PlayerInput.IgnoreMouseInterface;
		if (hover)
		{
			Main.LocalPlayer.mouseInterface = true;
			if (Main.mouseLeft && Main.mouseLeftRelease)
			{
				Main.mouseLeftRelease = false;
				SoundEngine.PlaySound(SoundID.MenuTick);
				onClick();
			}
		}

		var sb = Main.spriteBatch;
		var px = TextureAssets.MagicPixel.Value;
		sb.Draw(px, rect, background);
		drawIcon(sb, rect);

		if (hover)
		{
			sb.Draw(px, rect, Color.White * 0.16f);
			var b = new Color(255, 235, 140);
			sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), b);
			sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), b);
			sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), b);
			sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), b);
			DrawHotkeyTooltip(tooltip, keybind);
		}
	}

	private static void DrawHotkeyTooltip(string title, ModKeybind? keybind)
	{
		string text = title;
		if (keybind != null)
			text += $"\nHotkey: {HotkeyLabel(keybind)}";
		Main.instance.MouseText(text);
	}

	public static string HotkeyLabel(ModKeybind? keybind)
	{
		if (keybind != null)
		{
			try
			{
				var keys = keybind.GetAssignedKeys();
				if (keys.Count > 0) return string.Join(", ", keys);
			}
			catch { /* key not yet registered */ }
		}
		return "ASSIGN IN SETTINGS";
	}

	public static void InsertModal(
		List<GameInterfaceLayer> layers, string layerName, GameInterfaceDrawMethod drawFn)
		=> Insert(layers, layerName, drawFn, AnchorPlacement.BelowMouseText);

	private enum AnchorPlacement { AboveAccessoryBar, BelowMouseText }

	private static void Insert(
		List<GameInterfaceLayer> layers, string layerName, GameInterfaceDrawMethod drawFn,
		AnchorPlacement placement)
	{
		int idx;
		if (placement == AnchorPlacement.BelowMouseText)
		{
			idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
			if (idx >= 0)
			{
				int myDepth = ModalDepth(layerName);
				int insertAt = idx;
				for (int i = idx - 1; i >= 0; i--)
				{
					int d = ModalDepth(layers[i].Name);
					if (d < 0) continue;
					if (d > myDepth) insertAt = i;
					else break;
				}
				layers.Insert(insertAt, MakeLayer(layerName, drawFn));
				return;
			}
		}

		idx = layers.FindIndex(l => l.Name == "Vanilla: Info Accessories Bar");
		if (idx < 0) idx = layers.FindIndex(l => l.Name == "Vanilla: Inventory");
		if (idx < 0) idx = layers.Count - 1;
		layers.Insert(idx + 1, MakeLayer(layerName, drawFn));
	}

	private static LegacyGameInterfaceLayer MakeLayer(string name, GameInterfaceDrawMethod draw)
		=> new(name, draw, InterfaceScaleType.UI);
}
