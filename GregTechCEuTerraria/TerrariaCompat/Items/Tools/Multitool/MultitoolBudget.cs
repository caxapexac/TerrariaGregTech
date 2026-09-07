#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal static class MultitoolBudget
{
	internal static int CountUnits(Player p, Func<Item, int> unitOf)
	{
		int u = 0;
		foreach (var it in MultitoolInventory.Slots(p))
		{
			int pu = unitOf(it);
			if (pu > 0) u += pu * it.stack;
		}
		return u;
	}

	internal static void SpendUnits(Player p, Func<Item, int> unitOf, int units, int refundType, int refundUnit)
	{
		if (units <= 0) return;

		var slots = new List<(Item it, int u, int order)>();
		foreach (var it in MultitoolInventory.Slots(p))
		{
			int pu = unitOf(it);
			if (pu > 0) slots.Add((it, pu, slots.Count));
		}
		slots.Sort((a, b) =>
		{
			int c = a.u.CompareTo(b.u);
			return c != 0 ? c : a.order.CompareTo(b.order);
		});

		int removed = 0;
		foreach (var (it, pu, _) in slots)
		{
			while (it.stack > 0 && removed < units)
			{
				it.stack--;
				removed += pu;
			}
			if (it.stack <= 0) it.TurnToAir();
			if (removed >= units) break;
		}

		int overshoot = removed - units;
		if (overshoot > 0 && refundType > 0 && refundUnit > 0)
		{
			int give = overshoot / refundUnit;
			if (give > 0)
				PlayerGive.Give(p, p.GetSource_ItemUse(p.HeldItem), refundType, give);
		}
	}
}
