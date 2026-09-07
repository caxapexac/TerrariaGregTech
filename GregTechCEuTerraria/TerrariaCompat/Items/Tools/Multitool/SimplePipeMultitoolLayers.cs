#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Laser;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.LongDistance;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Optical;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal abstract class SimplePipeMultitoolLayer : MultitoolLayer
{
	protected readonly record struct Kind(
		string Key, string ItemName, string Label,
		Func<int, int, Player, bool> Place,
		Func<int, int, Player, bool> Cut,
		Func<int, int, bool> Has);

	protected abstract Kind[] Kinds { get; }

	public override int IconItem(Player p)
	{
		int icon = ArmedOrFirstIcon(p, Variants(p));
		return icon != 0 ? icon : (Kinds.Length > 0 ? Resolve(Kinds[0].ItemName) : 0);
	}

	public override List<MultitoolVariant> Variants(Player p)
	{
		var list = new List<MultitoolVariant>();
		foreach (var k in Kinds)
		{
			int type = Resolve(k.ItemName);
			if (type <= 0) continue;
			int count = CountItem(p, type);
			if (count <= 0) continue;
			list.Add(new MultitoolVariant(k.Key, type, k.Label, count));
		}
		return list;
	}

	public override bool HasCellAt(int x, int y)
	{
		foreach (var k in Kinds) if (k.Has(x, y)) return true;
		return false;
	}

	public override bool TryPick(int x, int y, out string variantKey, out int width)
	{
		width = 0; variantKey = "";
		foreach (var k in Kinds)
			if (k.Has(x, y)) { variantKey = k.Key; return true; }
		return false;
	}

	public override bool TryBuildVariant(Player p, string key, out MultitoolVariant variant)
	{
		variant = default;
		foreach (var k in Kinds)
		{
			if (k.Key != key) continue;
			int type = Resolve(k.ItemName);
			if (type <= 0) return false;
			variant = new MultitoolVariant(k.Key, type, k.Label, CountItem(p, type));
			return true;
		}
		return false;
	}

	public override int CommitPlace(Player p, IReadOnlyList<Point> path, in MultitoolVariant v, int width)
	{
		if (!TryKind(v.Key, out var k)) return 0;
		int type = Resolve(k.ItemName);
		if (type <= 0) return 0;

		int budget = CountItem(p, type);
		int placed = 0;
		foreach (var pt in path)
		{
			if (placed >= budget) break;
			if (k.Place(pt.X, pt.Y, p)) placed++;
		}
		if (placed > 0)
		{
			ConsumeItem(p, type, placed);
			Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50, new Vector2(path[0].X * 16f, path[0].Y * 16f));
		}
		return placed;
	}

	public override int CommitCut(Player p, IReadOnlyList<Point> path)
	{
		int cut = 0;
		foreach (var pt in path)
			foreach (var k in Kinds)
				if (k.Has(pt.X, pt.Y)) { if (k.Cut(pt.X, pt.Y, p)) cut++; break; }
		return cut;
	}

	private bool TryKind(string? key, out Kind kind)
	{
		foreach (var k in Kinds) if (k.Key == key) { kind = k; return true; }
		kind = default;
		return false;
	}

	private static int Resolve(string name) =>
		ModContent.TryFind<ModItem>("GregTechCEuTerraria", name, out var mi) ? mi.Type : 0;

	private static int CountItem(Player p, int type)
	{
		int n = 0;
		foreach (var it in MultitoolInventory.Slots(p))
			if (it.type == type) n += it.stack;
		return n;
	}

	private static void ConsumeItem(Player p, int type, int count)
	{
		foreach (var it in MultitoolInventory.Slots(p))
		{
			if (count <= 0) break;
			if (it.type != type) continue;
			int take = Math.Min(count, it.stack);
			it.stack -= take;
			count -= take;
			if (it.stack <= 0) it.TurnToAir();
		}
	}
}

internal sealed class LaserMultitoolLayer : SimplePipeMultitoolLayer
{
	public override string Id => "laser";
	public override string Name => "Laser Pipe";
	protected override Kind[] Kinds { get; } =
	{
		new("laser", "normal_laser_pipe", "Energy",
			(x, y, pl) => LaserPipeLayerHandle.Instance.TryPlace(x, y, pl),
			(x, y, pl) => LaserPipeLayerHandle.Instance.CutAt(x, y, pl),
			(x, y)     => LaserPipeLayerHandle.Instance.Has(x, y)),
	};
}

internal sealed class OpticalMultitoolLayer : SimplePipeMultitoolLayer
{
	public override string Id => "optical";
	public override string Name => "Optical Pipe";
	protected override Kind[] Kinds { get; } =
	{
		new("optical", "normal_optical_pipe", "Data",
			(x, y, pl) => OpticalPipeLayerHandle.Instance.TryPlace(x, y, pl),
			(x, y, pl) => OpticalPipeLayerHandle.Instance.CutAt(x, y, pl),
			(x, y)     => OpticalPipeLayerHandle.Instance.Has(x, y)),
	};
}

internal sealed class LongDistanceMultitoolLayer : SimplePipeMultitoolLayer
{
	public override string Id => "long_distance";
	public override string Name => "LD Pipe";
	protected override Kind[] Kinds { get; } =
	{
		new("ld_item", "long_distance_item_pipeline", "Item",
			(x, y, pl) => LongDistancePipeLayerHandle.Item.TryPlace(x, y, pl),
			(x, y, pl) => LongDistancePipeLayerHandle.Item.CutAt(x, y, pl),
			(x, y)     => LongDistancePipeLayerHandle.Item.Has(x, y)),
		new("ld_fluid", "long_distance_fluid_pipeline", "Fluid",
			(x, y, pl) => LongDistancePipeLayerHandle.Fluid.TryPlace(x, y, pl),
			(x, y, pl) => LongDistancePipeLayerHandle.Fluid.CutAt(x, y, pl),
			(x, y)     => LongDistancePipeLayerHandle.Fluid.Has(x, y)),
	};
}
