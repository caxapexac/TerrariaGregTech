#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Items.Pipes;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal static class PipeUnits
{
	internal static readonly (PipeSize size, int units)[] Item =
		{ (PipeSize.Small, 2), (PipeSize.Normal, 6), (PipeSize.Large, 12), (PipeSize.Huge, 24) };

	internal static readonly (PipeSize size, int units)[] Fluid =
	{
		(PipeSize.Tiny, 1), (PipeSize.Small, 2), (PipeSize.Normal, 6), (PipeSize.Large, 12),
		(PipeSize.Huge, 24), (PipeSize.Quadruple, 8), (PipeSize.Nonuple, 18),
	};

	internal static int UnitsOf((PipeSize, int)[] t, PipeSize s)
	{
		foreach (var (size, u) in t) if (size == s) return u;
		return 0;
	}

	internal static PipeSize SizeOf((PipeSize, int)[] t, int units)
	{
		foreach (var (size, u) in t) if (u == units) return size;
		return t[0].Item1;
	}

	internal static int[] Options((PipeSize, int)[] t)
	{
		var a = new int[t.Length];
		for (int i = 0; i < t.Length; i++) a[i] = t[i].Item2;
		return a;
	}

	internal static string Cap(string s) =>
		string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

internal sealed class ItemPipeMultitoolLayer : MultitoolLayer
{
	private static readonly (PipeSize, int)[] T = PipeUnits.Item;

	private const string SimpleMat = "simple_item";
	private static readonly string SimpleKey = EncodeKey(SimpleMat, false);

	public override string Id => "item_pipe";
	public override string Name => "Item Pipe";
	public override IReadOnlyList<int> WidthOptions => PipeUnits.Options(T);
	public override string WidthLabel(int units) => PipeUnits.Cap(PipeSizes.Word(PipeUnits.SizeOf(T, units)));

	private PipeSize SelSize => PipeUnits.SizeOf(T, MultitoolState.WidthFor(Id));

	public override int IconItem(Player p)
	{
		int icon = ArmedOrFirstIcon(p, Variants(p));
		return icon != 0 ? icon : ResolveId("copper", false, PipeSize.Small) ?? 0;
	}

	public override List<MultitoolVariant> Variants(Player p)
	{
		var groups = new Dictionary<(string mat, bool restr), (PipeSize repSize, int units, PipeItem rep)>();
		foreach (var it in MultitoolInventory.Slots(p))
		{
			if (it.ModItem is not PipeItem pi || pi.Kind != PipeKind.Item || pi.MaterialId is not { } mat) continue;

			var key = (mat, pi.Restrictive);
			int u = PipeUnits.UnitsOf(T, pi.Size) * it.stack;
			if (groups.TryGetValue(key, out var g))
			{
				g.units += u;
				if (pi.Size < g.repSize) { g.repSize = pi.Size; g.rep = pi; }
				groups[key] = g;
			}
			else { groups[key] = (pi.Size, u, pi); }
		}

		PipeSize size = SelSize;
		var rows = new List<(float rate, string mat, MultitoolVariant v)>(groups.Count);
		foreach (var ((mat, restr), g) in groups)
		{
			int icon = ResolveId(mat, restr, size) ?? g.rep.Type;
			float rate = g.rep.BuildItemCellForSize(size).TransferRate;
			string label = ((int)(rate * 64 + 0.5f)).ToString();
			float sortRate = g.rep.BuildItemCellForSize(PipeSize.Normal).TransferRate;
			rows.Add((sortRate, mat, new MultitoolVariant(EncodeKey(mat, restr), icon, label, g.units)));
		}

		int simpleUnits = MultitoolBudget.CountUnits(p, SimpleUnitOf);
		if (simpleUnits > 0)
		{
			var scell = SimpleItemPipeItem.BuildCell(size);
			int sicon = SimpleItemPipeItem.TypeFor(size);
			if (sicon <= 0) sicon = SimpleItemPipeItem.TypeFor(PipeSize.Normal);
			string slabel = ((int)(scell.TransferRate * 64 + 0.5f)).ToString();
			float ssort = SimpleItemPipeItem.BuildCell(PipeSize.Normal).TransferRate;
			rows.Add((ssort, SimpleMat, new MultitoolVariant(SimpleKey, sicon, slabel, simpleUnits)));
		}

		rows.Sort((a, b) =>
		{
			int c = a.rate.CompareTo(b.rate);
			return c != 0 ? c : string.CompareOrdinal(a.mat, b.mat);
		});

		var list = new List<MultitoolVariant>(rows.Count);
		foreach (var r in rows) list.Add(r.v);
		return list;
	}

	public override bool HasCellAt(int x, int y) => ItemPipeLayerSystem.Pipes.Has(x, y);

	public override bool TryPick(int x, int y, out string variantKey, out int width)
	{
		variantKey = ""; width = 0;
		if (ItemPipeLayerSystem.Pipes.CellAt(x, y) is not { } cell) return false;
		variantKey = EncodeKey(cell.MaterialId, cell.Restrictive);
		width = PipeUnits.UnitsOf(T, cell.Size);
		return true;
	}

	public override bool TryBuildVariant(Player p, string key, out MultitoolVariant variant)
	{
		variant = default;
		if (!DecodeKey(key, out string mat, out bool restr)) return false;
		if (mat == SimpleMat)
		{
			PipeSize ssize = SelSize;
			int sicon = SimpleItemPipeItem.TypeFor(ssize);
			if (sicon <= 0) return false;
			var scell = SimpleItemPipeItem.BuildCell(ssize);
			variant = new MultitoolVariant(key, sicon,
				((int)(scell.TransferRate * 64 + 0.5f)).ToString(),
				MultitoolBudget.CountUnits(p, SimpleUnitOf));
			return true;
		}
		var rep = ResolvePipeItem(mat, restr);
		if (rep is null) return false;
		PipeSize size = SelSize;
		int icon = ResolveId(mat, restr, size) ?? rep.Type;
		string label = ((int)(rep.BuildItemCellForSize(size).TransferRate * 64 + 0.5f)).ToString();
		variant = new MultitoolVariant(key, icon, label,
			MultitoolBudget.CountUnits(p, it => UnitOf(it, mat, restr)));
		return true;
	}

	public override int CommitPlace(Player p, IReadOnlyList<Point> path, in MultitoolVariant v, int width)
	{
		if (!DecodeKey(v.Key, out string mat, out bool restr)) return 0;
		bool simple = mat == SimpleMat;
		PipeSize size = PipeUnits.SizeOf(T, width);
		int unit = PipeUnits.UnitsOf(T, size);

		ItemPipeCell cell;
		System.Func<Item, int> unitOf;
		int refundType, refundUnit;
		if (simple)
		{
			cell = SimpleItemPipeItem.BuildCell(size);
			unitOf = SimpleUnitOf;
			refundType = SimpleItemPipeItem.TypeFor(PipeSize.Small);
			refundUnit = PipeUnits.UnitsOf(T, PipeSize.Small);
		}
		else
		{
			var rep = ResolvePipeItem(mat, restr);
			if (rep is null) return 0;
			cell = rep.BuildItemCellForSize(size);
			unitOf = it => UnitOf(it, mat, restr);
			refundType = ResolveId(mat, restr, PipeSize.Small) ?? 0;
			refundUnit = PipeUnits.UnitsOf(T, PipeSize.Small);
		}

		MultitoolIntersect.ResetWarning();
		int budget = MultitoolBudget.CountUnits(p, unitOf);
		int spent = 0, placed = 0;
		bool acted = false;
		foreach (var pt in path)
		{
			if (budget - spent < unit) break;
			var existing = ItemPipeLayerSystem.Pipes.CellAt(pt.X, pt.Y);
			if (existing.HasValue && existing.Value.Equals(cell)) continue;

			if (existing.HasValue && existing.Value.MaterialId != mat)
			{
				var r = MultitoolIntersect.HandleCrossing(p, pt.X, pt.Y);
				if (r == MultitoolIntersect.Result.Crossed) { acted = true; continue; }
				if (r == MultitoolIntersect.Result.NoItem) { MultitoolIntersect.WarnMissingOnce(); continue; }
			}

			if (ItemPipeLayerHandle.Instance.TryPlace(cell, pt.X, pt.Y, p, refundOverwrite: false))
			{
				spent += unit;
				placed++;
				acted = true;
				if (existing.HasValue) RefundCellAsSmallest(p, existing.Value);
				if (simple) SimpleItemPipeItem.AutoInsertOnAdjacentStorage(PipeKind.Item, pt.X, pt.Y);
			}
		}
		if (spent > 0)
			MultitoolBudget.SpendUnits(p, unitOf, spent, refundType, refundUnit);
		if (acted)
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50, new Vector2(path[0].X * 16f, path[0].Y * 16f));
		return placed;
	}

	public override int CommitCut(Player p, IReadOnlyList<Point> path)
	{
		int cut = 0;
		foreach (var pt in path)
		{
			var existing = ItemPipeLayerSystem.Pipes.CellAt(pt.X, pt.Y);
			if (existing is null) continue;
			RefundCellAsSmallest(p, existing.Value);
			if (ItemPipeLayerHandle.Instance.CutAt(pt.X, pt.Y, p, refund: false)) cut++;
		}
		return cut;
	}

	private static int UnitOf(Item it, string mat, bool restr) =>
		it.ModItem is PipeItem pi && pi.Kind == PipeKind.Item && pi.MaterialId == mat && pi.Restrictive == restr
			? PipeUnits.UnitsOf(T, pi.Size) : 0;

	private static int SimpleUnitOf(Item it) =>
		it.ModItem is SimpleItemPipeItem && SimpleItemPipeItem.TryGetSize(it.type, out var s)
			? PipeUnits.UnitsOf(T, s) : 0;

	private static void RefundCellAsSmallest(Player p, ItemPipeCell cell)
	{
		if (cell.IsSimple)
		{
			int simpleSmall = SimpleItemPipeItem.TypeFor(PipeSize.Small);
			if (simpleSmall <= 0) return;
			int sn = PipeUnits.UnitsOf(T, cell.Size) / PipeUnits.UnitsOf(T, PipeSize.Small);
			if (sn > 0) PlayerGive.Give(p, p.GetSource_ItemUse(p.HeldItem), simpleSmall, sn);
			return;
		}
		int? small = ResolveId(cell.MaterialId, cell.Restrictive, PipeSize.Small);
		if (small is null) return;
		int n = PipeUnits.UnitsOf(T, cell.Size) / PipeUnits.UnitsOf(T, PipeSize.Small);
		if (n > 0) PlayerGive.Give(p, p.GetSource_ItemUse(p.HeldItem), small.Value, n);
	}

	private static int? ResolveId(string mat, bool restr, PipeSize size)
	{
		string id = mat + "_" + PipeSizes.Word(size) + (restr ? "_restrictive_item_pipe" : "_item_pipe");
		return PipeItemRegistry.Get(id);
	}

	private static PipeItem? ResolvePipeItem(string mat, bool restr)
	{
		foreach (var (size, _) in T)
		{
			int? t = ResolveId(mat, restr, size);
			if (t is not null && ContentSamples.ItemsByType[t.Value].ModItem is PipeItem pi) return pi;
		}
		return null;
	}

	private static string EncodeKey(string mat, bool restr) => $"{mat}|{(restr ? 1 : 0)}";

	private static bool DecodeKey(string? key, out string mat, out bool restr)
	{
		mat = ""; restr = false;
		if (key is null) return false;
		int bar = key.LastIndexOf('|');
		if (bar < 0) return false;
		mat = key[..bar];
		restr = key[(bar + 1)..] == "1";
		return mat.Length > 0;
	}
}

internal sealed class FluidPipeMultitoolLayer : MultitoolLayer
{
	private static readonly (PipeSize, int)[] T = PipeUnits.Fluid;

	private const string SimpleMat = "simple_fluid";

	private static readonly int[] SimpleWidthOptions = BuildSimpleWidthOptions();

	public override string Id => "fluid_pipe";
	public override string Name => "Fluid Pipe";
	public override IReadOnlyList<int> WidthOptions
	{
		get
		{
			var armed = MultitoolState.ArmedFor(Id);
			if (armed == SimpleMat) return SimpleWidthOptions;
			if (armed != null) return AvailableWidths(armed);
			return PipeUnits.Options(T);
		}
	}
	public override string WidthLabel(int units) => PipeUnits.Cap(PipeSizes.Word(PipeUnits.SizeOf(T, units)));

	private static int[] BuildSimpleWidthOptions()
	{
		var a = new int[SimpleFluidPipeItem.Sizes.Length];
		for (int i = 0; i < a.Length; i++) a[i] = PipeUnits.UnitsOf(T, SimpleFluidPipeItem.Sizes[i]);
		return a;
	}

	private static int[] AvailableWidths(string mat)
	{
		var list = new List<int>(T.Length);
		foreach (var (size, units) in T)
			if (ResolveId(mat, size) != null) list.Add(units);
		return list.Count > 0 ? list.ToArray() : PipeUnits.Options(T);
	}

	private PipeSize SelSize => PipeUnits.SizeOf(T, MultitoolState.WidthFor(Id));

	public override int IconItem(Player p)
	{
		int icon = ArmedOrFirstIcon(p, Variants(p));
		return icon != 0 ? icon : ResolveId("bronze", PipeSize.Tiny) ?? 0;
	}

	public override List<MultitoolVariant> Variants(Player p)
	{
		var groups = new Dictionary<string, (PipeSize repSize, int units, PipeItem rep)>();
		foreach (var it in MultitoolInventory.Slots(p))
		{
			if (it.ModItem is not PipeItem pi || pi.Kind != PipeKind.Fluid || pi.MaterialId is not { } mat) continue;

			int u = PipeUnits.UnitsOf(T, pi.Size) * it.stack;
			if (groups.TryGetValue(mat, out var g))
			{
				g.units += u;
				if (pi.Size < g.repSize) { g.repSize = pi.Size; g.rep = pi; }
				groups[mat] = g;
			}
			else { groups[mat] = (pi.Size, u, pi); }
		}

		PipeSize size = SelSize;
		var rows = new List<(int through, string mat, MultitoolVariant v)>(groups.Count);
		foreach (var (mat, g) in groups)
		{
			int icon = ResolveId(mat, size) ?? g.rep.Type;
			var cell = g.rep.BuildFluidCellForSize(size);
			int through = cell?.Throughput ?? 0;
			string label = through.ToString();
			int sort = g.rep.BuildFluidCellForSize(PipeSize.Normal)?.Throughput ?? 0;
			rows.Add((sort, mat, new MultitoolVariant(mat, icon, label, g.units)));
		}

		int simpleUnits = MultitoolBudget.CountUnits(p, SimpleUnitOf);
		if (simpleUnits > 0)
		{
			PipeSize ssize = MultitoolState.ArmedFor(Id) == SimpleMat ? size : PipeSize.Normal;
			var scell = SimpleFluidPipeItem.BuildCell(ssize);
			int sicon = SimpleFluidPipeItem.TypeFor(ssize);
			if (sicon <= 0) sicon = SimpleFluidPipeItem.TypeFor(PipeSize.Normal);
			int ssort = SimpleFluidPipeItem.BuildCell(PipeSize.Normal).Throughput;
			rows.Add((ssort, SimpleMat, new MultitoolVariant(SimpleMat, sicon, scell.Throughput.ToString(), simpleUnits)));
		}

		rows.Sort((a, b) =>
		{
			int c = a.through.CompareTo(b.through);
			return c != 0 ? c : string.CompareOrdinal(a.mat, b.mat);
		});

		var list = new List<MultitoolVariant>(rows.Count);
		foreach (var r in rows) list.Add(r.v);
		return list;
	}

	public override bool HasCellAt(int x, int y) => FluidPipeLayerSystem.Pipes.Has(x, y);

	public override bool TryPick(int x, int y, out string variantKey, out int width)
	{
		variantKey = ""; width = 0;
		if (FluidPipeLayerSystem.Pipes.CellAt(x, y) is not { } cell) return false;
		variantKey = cell.MaterialId;
		width = PipeUnits.UnitsOf(T, cell.Size);
		return true;
	}

	public override bool TryBuildVariant(Player p, string key, out MultitoolVariant variant)
	{
		variant = default;
		if (key == SimpleMat)
		{
			PipeSize ssize = SelSize;
			int sicon = SimpleFluidPipeItem.TypeFor(ssize);
			if (sicon <= 0) return false;
			var scell = SimpleFluidPipeItem.BuildCell(ssize);
			variant = new MultitoolVariant(key, sicon, scell.Throughput.ToString(),
				MultitoolBudget.CountUnits(p, SimpleUnitOf));
			return true;
		}
		var rep = ResolvePipeItem(key);
		if (rep is null) return false;
		PipeSize size = SelSize;
		int icon = ResolveId(key, size) ?? rep.Type;
		int through = rep.BuildFluidCellForSize(size)?.Throughput ?? 0;
		variant = new MultitoolVariant(key, icon, through.ToString(),
			MultitoolBudget.CountUnits(p, it => UnitOf(it, key)));
		return true;
	}

	public override int CommitPlace(Player p, IReadOnlyList<Point> path, in MultitoolVariant v, int width)
	{
		string mat = v.Key;
		bool simple = mat == SimpleMat;
		PipeSize size = PipeUnits.SizeOf(T, width);
		int unit = PipeUnits.UnitsOf(T, size);

		FluidPipeCell fcell;
		System.Func<Item, int> unitOf;
		int refundType, refundUnit;
		if (simple)
		{
			fcell = SimpleFluidPipeItem.BuildCell(size);
			unitOf = SimpleUnitOf;
			refundType = SimpleFluidPipeItem.TypeFor(PipeSize.Tiny);
			refundUnit = PipeUnits.UnitsOf(T, PipeSize.Tiny);
		}
		else
		{
			var rep = ResolvePipeItem(mat);
			var cell = rep?.BuildFluidCellForSize(size);
			if (cell is not { } mcell) return 0;
			fcell = mcell;
			unitOf = it => UnitOf(it, mat);
			(refundType, refundUnit) = SmallestRefund(mat);
		}

		MultitoolIntersect.ResetWarning();
		int budget = MultitoolBudget.CountUnits(p, unitOf);
		int spent = 0, placed = 0;
		bool acted = false;
		foreach (var pt in path)
		{
			if (budget - spent < unit) break;
			var existing = FluidPipeLayerSystem.Pipes.CellAt(pt.X, pt.Y);
			if (existing.HasValue && existing.Value.Equals(fcell)) continue;

			if (existing.HasValue && existing.Value.MaterialId != mat)
			{
				var r = MultitoolIntersect.HandleCrossing(p, pt.X, pt.Y);
				if (r == MultitoolIntersect.Result.Crossed) { acted = true; continue; }
				if (r == MultitoolIntersect.Result.NoItem) { MultitoolIntersect.WarnMissingOnce(); continue; }
			}

			if (FluidPipeLayerHandle.Instance.TryPlace(fcell, pt.X, pt.Y, p, refundOverwrite: false))
			{
				spent += unit;
				placed++;
				acted = true;
				if (existing.HasValue) RefundCellAsSmallest(p, existing.Value);
				if (simple) SimpleItemPipeItem.AutoInsertOnAdjacentStorage(PipeKind.Fluid, pt.X, pt.Y);
			}
		}
		if (spent > 0)
			MultitoolBudget.SpendUnits(p, unitOf, spent, refundType, refundUnit);
		if (acted)
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50, new Vector2(path[0].X * 16f, path[0].Y * 16f));
		return placed;
	}

	public override int CommitCut(Player p, IReadOnlyList<Point> path)
	{
		int cut = 0;
		foreach (var pt in path)
		{
			var existing = FluidPipeLayerSystem.Pipes.CellAt(pt.X, pt.Y);
			if (existing is null) continue;
			RefundCellAsSmallest(p, existing.Value);
			if (FluidPipeLayerHandle.Instance.CutAt(pt.X, pt.Y, p, refund: false)) cut++;
		}
		return cut;
	}

	private static int UnitOf(Item it, string mat) =>
		it.ModItem is PipeItem pi && pi.Kind == PipeKind.Fluid && pi.MaterialId == mat
			? PipeUnits.UnitsOf(T, pi.Size) : 0;

	private static int SimpleUnitOf(Item it) =>
		it.ModItem is SimpleFluidPipeItem && SimpleFluidPipeItem.TryGetSize(it.type, out var s)
			? PipeUnits.UnitsOf(T, s) : 0;

	private static void RefundCellAsSmallest(Player p, FluidPipeCell cell)
	{
		if (cell.IsSimple)
		{
			int simpleTiny = SimpleFluidPipeItem.TypeFor(PipeSize.Tiny);
			if (simpleTiny <= 0) return;
			int sn = PipeUnits.UnitsOf(T, cell.Size) / PipeUnits.UnitsOf(T, PipeSize.Tiny);
			if (sn > 0) PlayerGive.Give(p, p.GetSource_ItemUse(p.HeldItem), simpleTiny, sn);
			return;
		}
		int cellUnits = PipeUnits.UnitsOf(T, cell.Size);
		int bestUnits = int.MaxValue, bestType = 0;
		foreach (var (size, units) in T)
		{
			if (units <= 0 || units > cellUnits || cellUnits % units != 0) continue;
			int? id = ResolveId(cell.MaterialId, size);
			if (id is null) continue;
			if (units < bestUnits) { bestUnits = units; bestType = id.Value; }
		}
		if (bestType == 0) return;
		PlayerGive.Give(p, p.GetSource_ItemUse(p.HeldItem), bestType, cellUnits / bestUnits);
	}

	private static (int type, int unit) SmallestRefund(string mat)
	{
		foreach (var (size, units) in T)
			if (ResolveId(mat, size) is int t) return (t, units);
		return (0, 1);
	}

	private static int? ResolveId(string mat, PipeSize size) =>
		PipeItemRegistry.Get(mat + "_" + PipeSizes.Word(size) + "_fluid_pipe");

	private static PipeItem? ResolvePipeItem(string mat)
	{
		foreach (var (size, _) in T)
		{
			int? t = ResolveId(mat, size);
			if (t is not null && ContentSamples.ItemsByType[t.Value].ModItem is PipeItem pi) return pi;
		}
		return null;
	}
}
