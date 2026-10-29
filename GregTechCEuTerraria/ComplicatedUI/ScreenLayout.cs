#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public enum ScreenEdge { Left, Right, Top, Bottom }

public static class ScreenLayout
{
	private static readonly Dictionary<string, Func<IEnumerable<Rectangle>>> _providers = new();
	private static readonly List<Rectangle> _occupied = new();
	private static uint _snapTick = uint.MaxValue;

	public static void RegisterProvider(string key, Func<IEnumerable<Rectangle>> occupied)
		=> _providers[key] = occupied;

	public static void RemoveProvider(string key) => _providers.Remove(key);

	public static Rectangle Screen
	{
		get
		{
			float s = Main.UIScale <= 0f ? 1f : Main.UIScale;
			var size = Terraria.GameInput.PlayerInput.OriginalScreenSize;
			return new Rectangle(0, 0, (int)MathF.Round(size.X / s), (int)MathF.Round(size.Y / s));
		}
	}

	public static IReadOnlyList<Rectangle> Occupied { get { Snapshot(); return _occupied; } }

	private static void Snapshot()
	{
		if (_snapTick == UiFrame.Count) return;
		_snapTick = UiFrame.Count;
		_occupied.Clear();
		foreach (var provider in _providers.Values)
		{
			IEnumerable<Rectangle>? rects;
			try { rects = provider(); }
			catch { continue; }
			if (rects is null) continue;
			foreach (var r in rects)
				if (r.Width > 0 && r.Height > 0) _occupied.Add(r);
		}
	}

	public static bool IsFree(Rectangle region, IEnumerable<Rectangle>? extra = null)
	{
		Snapshot();
		foreach (var o in _occupied)
			if (o.Intersects(region)) return false;
		if (extra != null)
			foreach (var o in extra)
				if (o.Width > 0 && o.Height > 0 && o.Intersects(region)) return false;
		return true;
	}

	public static bool Overlaps(Rectangle region, IEnumerable<Rectangle>? extra = null)
		=> !IsFree(region, extra);

	public static Rectangle FreeRectAnchored(ScreenEdge edge)
		=> FreeRectAnchored(edge, Screen, null);

	public static Rectangle FreeRectAnchored(ScreenEdge edge, Rectangle within,
		IEnumerable<Rectangle>? extra = null)
	{
		Snapshot();
		int left = within.Left, right = within.Right, top = within.Top, bottom = within.Bottom;

		Push(_occupied);
		if (extra != null) Push(extra);

		if (right < left) right = left;
		if (bottom < top) bottom = top;
		return new Rectangle(left, top, right - left, bottom - top);

		void Push(IEnumerable<Rectangle> obstacles)
		{
			foreach (var o in obstacles)
			{
				if (o.Width <= 0 || o.Height <= 0) continue;
				if (o.Right <= within.Left || o.Left >= within.Right) continue;
				if (o.Bottom <= within.Top || o.Top >= within.Bottom) continue;

				switch (edge)
				{
					case ScreenEdge.Right:  left   = Math.Max(left,   Math.Min(o.Right,  within.Right));  break;
					case ScreenEdge.Left:   right  = Math.Min(right,  Math.Max(o.Left,   within.Left));   break;
					case ScreenEdge.Bottom: top    = Math.Max(top,    Math.Min(o.Bottom, within.Bottom)); break;
					case ScreenEdge.Top:    bottom = Math.Min(bottom, Math.Max(o.Top,    within.Top));    break;
				}
			}
		}
	}
}
