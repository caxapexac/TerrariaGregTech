#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

// Item counterpart of AdjacentFluidPush
public static class AdjacentItemPush
{
	public static int Push(MetaMachine source, int sourceSlotStart, int sourceSlotCount,
		int maxPerSlot = 1, IODirection side = IODirection.None)
	{
		if (source is not IItemHandler outHandler) return 0;
		return Push(source, outHandler, sourceSlotStart, sourceSlotCount, maxPerSlot, side);
	}

	public static int Push(MetaMachine source, IItemHandler outHandler,
		int sourceSlotStart, int sourceSlotCount,
		int maxPerSlot = 1, IODirection side = IODirection.None)
	{
		int transferred = 0;

		for (int s = sourceSlotStart; s < sourceSlotStart + sourceSlotCount; s++)
		{
			var peek = outHandler.GetSlot(s);
			if (peek.IsAir) continue;

			foreach (var (x, y, srcSide) in AdjacentFluidPush.EnumerateAdjacentCells(source, side))
			{
				if (!source.GetItemCapFilter(srcSide, IO.OUT)(peek))
					continue;
				var dest = WorldCapability.ItemHandlerAt(x, y, srcSide.Opposite());
				if (dest is null || ReferenceEquals(dest, outHandler)) continue;

				var available = outHandler.Extract(s, maxPerSlot, simulate: true);
				if (available.IsAir || available.stack <= 0) break;

				bool insertedAny = false;
				for (int ds = 0; ds < dest.SlotCount; ds++)
				{
					if (!dest.IsItemValid(ds, available)) continue;
					var leftover = dest.Insert(ds, available, simulate: true);
					int wouldInsert = available.stack - (leftover?.stack ?? 0);
					if (wouldInsert <= 0) continue;

					var actuallyExtracted = outHandler.Extract(s, wouldInsert, simulate: false);
					if (actuallyExtracted.IsAir) break;
					dest.Insert(ds, actuallyExtracted, simulate: false);
					transferred += actuallyExtracted.stack;
					insertedAny = true;
					break;
				}
				if (insertedAny) break;
			}
		}
		return transferred;
	}
}
