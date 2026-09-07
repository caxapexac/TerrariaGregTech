#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Util;
using GregTechCEuTerraria.TerrariaCompat.Items.MeCables;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal sealed class MeCableMultitoolLayer : MultitoolLayer
{
	public override string Id => "me_cable";
	public override string Name => "ME Cable";

	public override int IconItem(Player p)
	{
		int icon = ArmedOrFirstIcon(p, Variants(p));
		return icon != 0 ? icon : MeCableItemRegistry.Get(AEColor.TRANSPARENT) ?? 0;
	}

	public override List<MultitoolVariant> Variants(Player p)
	{
		var counts = new Dictionary<AEColor, int>();
		foreach (var it in MultitoolInventory.Slots(p))
		{
			if (it.ModItem is not MeCableItem m) continue;
			counts.TryGetValue(m.Color, out int c);
			counts[m.Color] = c + it.stack;
		}

		var rows = new List<(AEColor color, MultitoolVariant v)>(counts.Count);
		foreach (var (color, units) in counts)
		{
			int icon = MeCableItemRegistry.Get(color) ?? 0;
			if (icon == 0) continue;
			rows.Add((color, new MultitoolVariant(color.RegistryPrefix(), icon, color.EnglishName(), units)));
		}
		rows.Sort((a, b) => ((int)a.color).CompareTo((int)b.color));

		var list = new List<MultitoolVariant>(rows.Count);
		foreach (var r in rows) list.Add(r.v);
		return list;
	}

	public override bool HasCellAt(int x, int y) => MeCableLayerSystem.Cables.Has(x, y);

	public override bool TryPick(int x, int y, out string variantKey, out int width)
	{
		variantKey = ""; width = 0;
		if (MeCableLayerSystem.Cables.CellAt(x, y) is not { } cell) return false;
		variantKey = cell.Color.RegistryPrefix();
		return true;
	}

	public override bool TryBuildVariant(Player p, string key, out MultitoolVariant variant)
	{
		variant = default;
		if (!TryDecodeColor(key, out var color)) return false;
		int icon = MeCableItemRegistry.Get(color) ?? 0;
		if (icon == 0) return false;
		variant = new MultitoolVariant(key, icon, color.EnglishName(), CountColor(p, color));
		return true;
	}

	public override int CommitPlace(Player p, IReadOnlyList<Point> path, in MultitoolVariant v, int width)
	{
		if (!TryDecodeColor(v.Key, out var color)) return 0;
		var cell = new MeCableCell(color);

		MultitoolIntersect.ResetWarning();
		int budget = CountColor(p, color);
		int spent = 0, placed = 0;
		bool acted = false;
		foreach (var pt in path)
		{
			if (budget - spent < 1) break;

			var ex = MeCableLayerSystem.Cables.CellAt(pt.X, pt.Y);
			if (ex.HasValue && ex.Value.Color != color)
			{
				var r = MultitoolIntersect.HandleCrossing(p, pt.X, pt.Y);
				if (r == MultitoolIntersect.Result.Crossed) { acted = true; continue; }
				if (r == MultitoolIntersect.Result.NoItem) { MultitoolIntersect.WarnMissingOnce(); continue; }
			}

			if (MeCableLayerHandle.Instance.TryPlace(cell, pt.X, pt.Y, p))
			{
				spent++;
				placed++;
				acted = true;
			}
		}
		if (spent > 0) MultitoolBudget.SpendUnits(p, it => UnitOf(it, color), spent, 0, 0);
		if (acted)
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50, new Vector2(path[0].X * 16f, path[0].Y * 16f));
		return placed;
	}

	public override int CommitCut(Player p, IReadOnlyList<Point> path)
	{
		int cut = 0;
		foreach (var pt in path)
			if (MeCableLayerHandle.Instance.CutAt(pt.X, pt.Y, p))
				cut++;
		return cut;
	}

	private static int UnitOf(Item it, AEColor color) =>
		it.ModItem is MeCableItem m && m.Color == color ? 1 : 0;

	private static int CountColor(Player p, AEColor color) =>
		MultitoolBudget.CountUnits(p, it => UnitOf(it, color));

	private static bool TryDecodeColor(string? key, out AEColor color)
	{
		color = default;
		if (string.IsNullOrEmpty(key)) return false;
		foreach (AEColor c in System.Enum.GetValues<AEColor>())
			if (c.RegistryPrefix() == key) { color = c; return true; }
		return false;
	}
}
