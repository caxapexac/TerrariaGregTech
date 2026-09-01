#nullable enable
using System;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class ItemHandlerMeStorage : MEStorage
{
	private readonly Func<IItemHandler?> _resolver;
	private readonly bool _extractableOnly;

	public ItemHandlerMeStorage(Func<IItemHandler?> resolver, bool extractableOnly)
	{
		_resolver = resolver;
		_extractableOnly = extractableOnly;
	}

	public string GetDescription() => "External Inventory";

	public long Insert(AEKey what, long amount, Actionable mode, IActionSource source)
	{
		if (what is not AEItemKey ik) return 0;
		var h = _resolver();
		if (h is null) return 0;

		bool simulate = mode == Actionable.SIMULATE;
		long inserted = 0;
		long remaining = amount;
		for (int s = 0; s < h.SlotCount && remaining > 0; s++)
		{
			int chunk = (int)Math.Min(remaining, ik.GetMaxStackSize());
			var stack = ik.ToStack(chunk);
			if (!h.IsItemValid(s, stack)) continue;
			var leftover = h.Insert(s, stack, simulate);
			int moved = chunk - (leftover.IsAir ? 0 : leftover.stack);
			if (moved <= 0) continue;
			inserted += moved;
			remaining -= moved;
		}
		return inserted;
	}

	public long Extract(AEKey what, long amount, Actionable mode, IActionSource source)
	{
		if (what is not AEItemKey ik) return 0;
		var h = _resolver();
		if (h is null) return 0;

		bool simulate = mode == Actionable.SIMULATE;
		long extracted = 0;
		long remaining = amount;
		for (int s = 0; s < h.SlotCount && remaining > 0; s++)
		{
			var slot = h.GetSlot(s);
			if (slot.IsAir || !ik.Matches(slot)) continue;
			int want = (int)Math.Min(remaining, int.MaxValue);
			var got = h.Extract(s, want, simulate);
			if (got.IsAir) continue;
			long take = Math.Min(got.stack, want);
			extracted += take;
			remaining -= take;
		}
		return extracted;
	}

	public void GetAvailableStacks(KeyCounter @out)
	{
		var h = _resolver();
		if (h is null) return;

		for (int s = 0; s < h.SlotCount; s++)
		{
			var slot = h.GetSlot(s);
			if (slot.IsAir) continue;
			var key = AEItemKey.Of(slot);
			if (key is null) continue;
			if (_extractableOnly
				&& h.Extract(s, 1, true).IsAir
				&& h.Extract(s, slot.stack, true).IsAir) continue;
			@out.Add(key, slot.stack);
		}
	}
}
