#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Items.Cables;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal sealed class CableMultitoolLayer : MultitoolLayer
{
	public override string Id => "cable";
	public override string Name => "Cable";

	private static readonly int[] _widths = { 1, 2, 4, 8, 16 };
	public override IReadOnlyList<int> WidthOptions => _widths;

	public override int IconItem(Player p)
	{
		int icon = ArmedOrFirstIcon(p, Variants(p));
		return icon != 0 ? icon : WireItemRegistry.Get("copper", 1, false) ?? 0;
	}

	public override List<MultitoolVariant> Variants(Player p)
	{
		var groups = new Dictionary<(string mat, bool ins), (byte repSize, int units, WireItem rep)>();
		foreach (var it in MultitoolInventory.Slots(p))
		{
			if (it.ModItem is not WireItem w || w.MaterialId is not { } mat) continue;

			var key = (mat, w.Insulated);
			if (groups.TryGetValue(key, out var g))
			{
				g.units += w.WireSize * it.stack;
				if (w.WireSize < g.repSize) { g.repSize = w.WireSize; g.rep = w; }
				groups[key] = g;
			}
			else
			{
				groups[key] = (w.WireSize, w.WireSize * it.stack, w);
			}
		}

		byte width = (byte)MultitoolState.WidthFor(Id);
		var rows = new List<(int tier, bool ins, string mat, MultitoolVariant v)>(groups.Count);
		foreach (var ((mat, ins), g) in groups)
		{
			var cell = g.rep.BuildCell();
			int icon = WireItemRegistry.Get(mat, width, ins) ?? g.rep.Type;
			string label = VoltageTiers.ShortName(cell.Voltage);
			rows.Add(((int)cell.Voltage, ins, mat, new MultitoolVariant(EncodeKey(mat, ins), icon, label, g.units)));
		}
		rows.Sort((a, b) =>
		{
			int c = a.tier.CompareTo(b.tier);
			if (c != 0) return c;
			c = a.ins.CompareTo(b.ins);
			return c != 0 ? c : string.CompareOrdinal(a.mat, b.mat);
		});

		var list = new List<MultitoolVariant>(rows.Count);
		foreach (var r in rows) list.Add(r.v);
		return list;
	}

	public override bool HasCellAt(int x, int y) => CableLayerSystem.Cables.Has(x, y);

	public override bool TryPick(int x, int y, out string variantKey, out int width)
	{
		variantKey = ""; width = 0;
		if (CableLayerSystem.Cables.CellAt(x, y) is not { } cell) return false;
		variantKey = EncodeKey(cell.MaterialId, cell.Insulated);
		width = cell.WireSize;
		return true;
	}

	public override bool TryBuildVariant(Player p, string key, out MultitoolVariant variant)
	{
		variant = default;
		if (!DecodeKey(key, out string mat, out bool ins)) return false;
		byte width = (byte)MultitoolState.WidthFor(Id);
		if (width == 0) width = 1;
		if (MultitoolWires.BuildCell(mat, width, ins) is not { } cell) return false;
		int icon = WireItemRegistry.Get(mat, width, ins) ?? WireItemRegistry.Get(mat, 1, ins) ?? 0;
		if (icon == 0) return false;
		variant = new MultitoolVariant(key, icon, VoltageTiers.ShortName(cell.Voltage),
			MultitoolWires.CountUnits(p, mat, ins));
		return true;
	}

	public override int CommitPlace(Player p, IReadOnlyList<Point> path, in MultitoolVariant v, int width)
	{
		if (!DecodeKey(v.Key, out string mat, out bool ins)) return 0;
		byte w = (byte)width;

		var template = MultitoolWires.BuildCell(mat, w, ins);
		if (template is not { } cell) return 0;

		MultitoolIntersect.ResetWarning();
		int budget = MultitoolWires.CountUnits(p, mat, ins);
		int spent = 0, placed = 0;
		bool acted = false;
		foreach (var pt in path)
		{
			if (budget - spent < w) break;

			var ex = CableLayerSystem.Cables.CellAt(pt.X, pt.Y);
			if (ex.HasValue && ex.Value.MaterialId != mat)
			{
				var r = MultitoolIntersect.HandleCrossing(p, pt.X, pt.Y);
				if (r == MultitoolIntersect.Result.Crossed) { acted = true; continue; }
				if (r == MultitoolIntersect.Result.NoItem) { MultitoolIntersect.WarnMissingOnce(); continue; }
			}

			if (CableLayerHandle.Instance.TryPlaceRefundSingles(cell, pt.X, pt.Y, p))
			{
				spent += w;
				placed++;
				acted = true;
			}
		}
		if (spent > 0) MultitoolWires.SpendUnits(p, mat, ins, spent);
		if (acted)
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50,
				new Vector2(path[0].X * 16f, path[0].Y * 16f));
		return placed;
	}

	public override int CommitCut(Player p, IReadOnlyList<Point> path)
	{
		int cut = 0;
		foreach (var pt in path)
			if (CableLayerHandle.Instance.CutAsSingles(pt.X, pt.Y, p))
				cut++;
		return cut;
	}

	private static string EncodeKey(string mat, bool insulated) => $"{mat}|{(insulated ? 1 : 0)}";

	private static bool DecodeKey(string? key, out string mat, out bool insulated)
	{
		mat = ""; insulated = false;
		if (key is null) return false;
		int bar = key.LastIndexOf('|');
		if (bar < 0) return false;
		mat = key[..bar];
		insulated = key[(bar + 1)..] == "1";
		return mat.Length > 0;
	}
}
