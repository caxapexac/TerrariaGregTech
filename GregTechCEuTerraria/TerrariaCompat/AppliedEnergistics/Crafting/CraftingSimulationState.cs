// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (appeng.crafting.inv.CraftingSimulationState + ChildCraftingSimulationState +
// NetworkCraftingSimulationState), Forge 1.20.1. LGPL-3.0-only. See AE2 LICENSE.
#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using GregTechCEuTerraria.Config;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;

public abstract class CraftingSimulationState : ICraftingSimulationState
{
	private readonly KeyCounter _unmodifiedCache = new();
	private readonly KeyCounter _modifiableCache = new();
	private readonly KeyCounter _emittedItems = new();
	private readonly HashSet<AEKey> _cached = new();
	private double _bytes = 0;
	private readonly Dictionary<MePattern, long> _crafts = new();
	private readonly KeyCounter _requiredExtract = new();

	protected abstract long SimulateExtractParent(AEKey what, long amount);

	private void CacheKey(AEKey what)
	{
		if (_cached.Add(what))
		{
			var extracted = SimulateExtractParent(what, long.MaxValue);
			_modifiableCache.Add(what, extracted);
			_unmodifiedCache.Add(what, extracted);
		}
	}

	public void Insert(AEKey what, long amount, Actionable mode)
	{
		CacheKey(what);
		if (mode == Actionable.MODULATE)
			_modifiableCache.Add(what, amount);
	}

	private void UpdateRequiredExtract(AEKey key, long delta)
	{
		if (delta > 0)
		{
			long max = Math.Max(delta, _requiredExtract.Get(key));
			_requiredExtract.Set(key, max);
		}
	}

	public long Extract(AEKey what, long amount, Actionable mode)
	{
		CacheKey(what);

		var cachedAmount = _modifiableCache.Get(what);
		if (cachedAmount == 0)
			return 0;

		long extracted = Math.Min(cachedAmount, amount);
		if (mode == Actionable.MODULATE)
			_modifiableCache.Remove(what, extracted);

		UpdateRequiredExtract(what, _unmodifiedCache.Get(what) - _modifiableCache.Get(what));

		return extracted;
	}

	public void EmitItems(AEKey what, long amount) => _emittedItems.Add(what, amount);

	public void AddBytes(double bytes) => _bytes += bytes;

	public void AddStackBytes(AEKey key, long amount, long multiplier)
		=> AddBytes((double)amount * multiplier / key.GetAmountPerByte() * 8);

	public void AddCrafting(MePattern details, long crafts)
		=> _crafts[details] = (_crafts.TryGetValue(details, out var c) ? c : 0) + crafts;

	public void Ignore(AEKey stack)
	{
		CacheKey(stack);
		_unmodifiedCache.Set(stack, 0);
		_modifiableCache.Set(stack, 0);
	}

	public void ApplyDiff(CraftingSimulationState parent)
	{
		foreach (var entry in _requiredExtract)
		{
			var key = entry.Key;
			long delta = parent._unmodifiedCache.Get(key) - parent._modifiableCache.Get(key) + entry.Value;
			parent.UpdateRequiredExtract(key, delta);
		}

		foreach (var entry in _modifiableCache)
		{
			var unmodified = _unmodifiedCache.Get(entry.Key);
			long sizeDelta = entry.Value - unmodified;

			if (sizeDelta > 0)
			{
				parent.Insert(entry.Key, sizeDelta, Actionable.MODULATE);
			}
			else if (sizeDelta < 0)
			{
				long newStackSize = -sizeDelta;
				var reallyExtracted = parent.Extract(entry.Key, newStackSize, Actionable.MODULATE);
				if (reallyExtracted != -sizeDelta)
					throw new InvalidOperationException("Failed to extract from parent. This is a bug!");
			}
		}

		foreach (var toEmit in _emittedItems)
			parent.EmitItems(toEmit.Key, toEmit.Value);

		parent.AddBytes(_bytes);

		foreach (var entry in _crafts)
			parent.AddCrafting(entry.Key, entry.Value);
	}

	public static CraftingPlan BuildCraftingPlan(CraftingSimulationState state, CraftingCalculation calculation,
		long calculatedAmount)
	{
		return new CraftingPlan(
			new GenericStack(calculation.Output, calculatedAmount),
			(long)Math.Ceiling(state._bytes),
			calculation.IsSimulation,
			calculation.HasMultiplePaths,
			state._requiredExtract,
			state._emittedItems,
			calculation.GetMissingItems(),
			state._crafts);
	}
}

public sealed class ChildCraftingSimulationState : CraftingSimulationState
{
	private readonly ICraftingInventory _parent;

	public ChildCraftingSimulationState(ICraftingInventory parent) => _parent = parent;

	protected override long SimulateExtractParent(AEKey what, long amount)
		=> _parent.Extract(what, amount, Actionable.SIMULATE);
}

public sealed class NetworkCraftingSimulationState : CraftingSimulationState
{
	private readonly KeyCounter _list = new();

	public NetworkCraftingSimulationState(MeNetwork net, IActionSource? src)
	{
		if (src == null)
			return;

		foreach (var stack in net.GetCachedInventory())
		{
			long networkAmount = GTConfig.Instance.CraftingSimulatedExtraction
				? net.GetStorage().Extract(stack.Key, stack.Value, Actionable.SIMULATE, src)
				: stack.Value;
			if (networkAmount > 0)
				_list.Add(stack.Key, networkAmount);
		}
	}

	protected override long SimulateExtractParent(AEKey what, long amount)
		=> Math.Min(_list.Get(what), amount);
}
