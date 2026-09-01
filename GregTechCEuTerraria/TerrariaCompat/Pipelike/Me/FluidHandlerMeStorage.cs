#nullable enable
using System;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class FluidHandlerMeStorage : MEStorage
{
	private readonly Func<IFluidHandler?> _resolver;
	private readonly bool _extractableOnly;

	public FluidHandlerMeStorage(Func<IFluidHandler?> resolver, bool extractableOnly)
	{
		_resolver = resolver;
		_extractableOnly = extractableOnly;
	}

	public string GetDescription() => "External Fluid Tank";

	public long Insert(AEKey what, long amount, Actionable mode, IActionSource source)
	{
		if (what is not AEFluidKey fk) return 0;
		var h = _resolver();
		if (h is null) return 0;

		int amt = (int)Math.Min(amount, int.MaxValue);
		if (amt <= 0) return 0;
		return h.Fill(fk.ToStack(amt), mode == Actionable.SIMULATE);
	}

	public long Extract(AEKey what, long amount, Actionable mode, IActionSource source)
	{
		if (what is not AEFluidKey fk) return 0;
		var h = _resolver();
		if (h is null) return 0;

		int amt = (int)Math.Min(amount, int.MaxValue);
		if (amt <= 0) return 0;
		var drained = h.Drain(fk.ToStack(amt), mode == Actionable.SIMULATE);
		return drained.IsEmpty ? 0 : drained.Amount;
	}

	public void GetAvailableStacks(KeyCounter @out)
	{
		var h = _resolver();
		if (h is null) return;

		for (int t = 0; t < h.TankCount; t++)
		{
			var stack = h.GetTank(t);
			if (stack.IsEmpty) continue;
			var key = AEFluidKey.Of(stack);
			if (key is null) continue;
			if (_extractableOnly && h.Drain(stack, simulate: true).IsEmpty) continue;
			@out.Add(key, stack.Amount);
		}
	}
}
