#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Items.Cables;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal static class MultitoolWires
{
	internal static int CountUnits(Player p, string materialId, bool insulated)
	{
		int u = 0;
		foreach (var it in MultitoolInventory.Slots(p))
			if (it.ModItem is WireItem w && w.Insulated == insulated && w.MaterialId == materialId)
				u += w.WireSize * it.stack;
		return u;
	}

	internal static CableCell? BuildCell(string materialId, byte width, bool insulated)
	{
		for (byte s = 1; s <= 16; s = (byte)(s << 1))
		{
			int? type = WireItemRegistry.Get(materialId, s, insulated);
			if (type is null) continue;
			if (ContentSamples.ItemsByType[type.Value].ModItem is WireItem w)
				return w.BuildCellWithSize(width);
		}
		return null;
	}

	internal static void SpendUnits(Player p, string materialId, bool insulated, int units)
	{
		if (units <= 0) return;

		var slots = new List<(Item it, byte size, int order)>();
		foreach (var it in MultitoolInventory.Slots(p))
			if (it.ModItem is WireItem w && w.Insulated == insulated && w.MaterialId == materialId)
				slots.Add((it, w.WireSize, slots.Count));
		slots.Sort((a, b) =>
		{
			int c = a.size.CompareTo(b.size);
			return c != 0 ? c : a.order.CompareTo(b.order);
		});

		int removed = 0;
		foreach (var (it, size, _) in slots)
		{
			while (it.stack > 0 && removed < units)
			{
				it.stack--;
				removed += size;
			}
			if (it.stack <= 0) it.TurnToAir();
			if (removed >= units) break;
		}

		int overshoot = removed - units;
		if (overshoot > 0)
		{
			int? single = WireItemRegistry.Get(materialId, 1, insulated);
			if (single is not null)
				PlayerGive.Give(p, p.GetSource_ItemUse(p.HeldItem), single.Value, overshoot);
		}
	}
}
