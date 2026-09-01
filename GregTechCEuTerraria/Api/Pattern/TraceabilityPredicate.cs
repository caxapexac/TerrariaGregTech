#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Capability.Recipe;

using Terraria;

namespace GregTechCEuTerraria.Api.Pattern;

public class TraceabilityPredicate
{
	public List<SimplePredicate> Common = new();
	public List<SimplePredicate> Limited = new();
	public List<SimplePredicate> Chain = new();
	public bool IsController;

	public TraceabilityPredicate() { }

	public TraceabilityPredicate(TraceabilityPredicate predicate)
	{
		Common.AddRange(predicate.Common);
		Limited.AddRange(predicate.Limited);
		Chain.AddRange(predicate.Chain);
		IsController = predicate.IsController;
	}

	public TraceabilityPredicate(Func<MultiblockState, bool> predicate, Func<Item[]>? candidates)
	{
		var sp = new SimplePredicate(predicate, candidates);
		Common.Add(sp);
		Chain.Add(sp);
	}

	public TraceabilityPredicate(SimplePredicate simplePredicate)
	{
		if (simplePredicate.MinCount != -1 || simplePredicate.MaxCount != -1)
			Limited.Add(simplePredicate);
		else
			Common.Add(simplePredicate);
		Chain.Add(simplePredicate);
	}

	public TraceabilityPredicate SetController()
	{
		IsController = true;
		return this;
	}

	public TraceabilityPredicate Sort()
	{
		Limited.Sort((a, b) => a.MinCount.CompareTo(b.MinCount));
		return this;
	}

	public TraceabilityPredicate AddTooltips(params string[] tips)
	{
		if (tips.Length == 0) return this;
		foreach (var p in Common)
		{
			if (p.Candidates is null) continue;
			p.ToolTips ??= new List<string>();
			p.ToolTips.AddRange(tips);
		}
		foreach (var p in Limited)
		{
			if (p.Candidates is null) continue;
			p.ToolTips ??= new List<string>();
			p.ToolTips.AddRange(tips);
		}
		return this;
	}

	public TraceabilityPredicate SetMinGlobalLimited(int min)
	{
		Limited.AddRange(Common);
		Common.Clear();
		foreach (var p in Limited) p.MinCount = min;
		return this;
	}

	public TraceabilityPredicate SetMinGlobalLimited(int min, int previewCount) =>
		SetMinGlobalLimited(min).SetPreviewCount(previewCount);

	public TraceabilityPredicate SetMaxGlobalLimited(int max)
	{
		Limited.AddRange(Common);
		Common.Clear();
		foreach (var p in Limited) p.MaxCount = max;
		return this;
	}

	public TraceabilityPredicate SetMaxGlobalLimited(int max, int previewCount) =>
		SetMaxGlobalLimited(max).SetPreviewCount(previewCount);

	public TraceabilityPredicate SetMinLayerLimited(int min)
	{
		Limited.AddRange(Common);
		Common.Clear();
		foreach (var p in Limited) p.MinLayerCount = min;
		return this;
	}

	public TraceabilityPredicate SetMinLayerLimited(int min, int previewCount) =>
		SetMinLayerLimited(min).SetPreviewCount(previewCount);

	public TraceabilityPredicate SetMaxLayerLimited(int max)
	{
		Limited.AddRange(Common);
		Common.Clear();
		foreach (var p in Limited) p.MaxLayerCount = max;
		return this;
	}

	public TraceabilityPredicate SetMaxLayerLimited(int max, int previewCount) =>
		SetMaxLayerLimited(max).SetPreviewCount(previewCount);

	public TraceabilityPredicate SetExactLimit(int limit) =>
		SetMinGlobalLimited(limit).SetMaxGlobalLimited(limit);

	public TraceabilityPredicate SetPreviewCount(int count)
	{
		foreach (var p in Common)  p.PreviewCount = count;
		foreach (var p in Limited) p.PreviewCount = count;
		return this;
	}

	public TraceabilityPredicate DisableRenderFormed()
	{
		foreach (var p in Common)  p.DisableRenderFormed = true;
		foreach (var p in Limited) p.DisableRenderFormed = true;
		return this;
	}

	public TraceabilityPredicate SetIO(IO io)
	{
		foreach (var p in Common)  p.Io = io;
		foreach (var p in Limited) p.Io = io;
		return this;
	}

	public TraceabilityPredicate SetSlotName(string slotName)
	{
		foreach (var p in Common)  p.SlotName = slotName;
		foreach (var p in Limited) p.SlotName = slotName;
		return this;
	}

	public bool Test(MultiblockState state)
	{
		state.Io = IO.BOTH;
		bool flag = false;
		foreach (var p in Limited)
		{
			if (p.TestLimited(state)) flag = true;
		}
		flag = flag || Common.Any(p => p.Test(state));
		if (flag) state.SetError(null);
		return flag;
	}

	public TraceabilityPredicate Or(TraceabilityPredicate? other)
	{
		if (other is null) return this;
		var result = new TraceabilityPredicate(this);
		result.Common.AddRange(other.Common);
		result.Limited.AddRange(other.Limited);
		result.Chain.AddRange(other.Chain);
		return result;
	}

	public Item? PreviewItem()
	{
		foreach (var p in Chain)
		{
			var items = p.Candidates?.Invoke();
			if (items != null && items.Length > 0 && items[0] != null) return items[0];
		}
		return null;
	}

	public virtual bool IsAny()  => Common.Count == 1 && Limited.Count == 0 && Common[0] == SimplePredicate.ANY;
	public bool IsAir()  => Common.Count == 1 && Limited.Count == 0 && Common[0] == SimplePredicate.AIR;
	public bool IsSingle() => !IsAny() && !IsAir() && Common.Count + Limited.Count == 1;
	public bool HasAir() => Common.Contains(SimplePredicate.AIR);
	public virtual bool AddCache() => !IsAny();
}
