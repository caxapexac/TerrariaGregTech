#nullable enable
using System.Collections.Generic;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools.Multitool;

internal static class MultitoolInventory
{
	internal static IEnumerable<Item> Slots(Player p)
	{
		for (int i = 0; i < 58; i++)
		{
			var it = p.inventory[i];
			if (it is not null && !it.IsAir) yield return it;
		}

		foreach (var bank in Banks(p))
		{
			if (bank?.item is null) continue;
			foreach (var it in bank.item)
				if (it is not null && !it.IsAir) yield return it;
		}
	}

	private static IEnumerable<Chest> Banks(Player p)
	{
		yield return p.bank;
		yield return p.bank2;
		yield return p.bank3;
		yield return p.bank4;
	}
}
