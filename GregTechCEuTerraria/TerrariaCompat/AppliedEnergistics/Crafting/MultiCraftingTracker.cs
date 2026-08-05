// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (appeng.helpers.MultiCraftingTracker), Forge 1.20.1. LGPL-3.0-only. See AE2 LICENSE.

#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;

public sealed class MultiCraftingTracker
{
	private readonly int _size;
	private readonly IMeCraftingRequester _owner;
	private readonly CraftingLink?[] _links;
	private readonly CraftingJob?[] _jobs;

	public MultiCraftingTracker(IMeCraftingRequester owner, int size)
	{
		_owner = owner;
		_size = size;
		_links = new CraftingLink?[size];
		_jobs = new CraftingJob?[size];
	}

	public bool HandleCrafting(int x, AEKey what, long amount, MeNetwork? net, IActionSource mySrc)
	{
		var craftingJob = GetJob(x);
		if (GetLink(x) != null) return false;
		if (net == null) return false;

		if (craftingJob != null)
		{
			CraftingPlan? job = null;
			if (craftingJob.IsDone)
				job = craftingJob.Get();

			if (job != null)
			{
				var result = MeCraftingService.Submit(net, job, _owner, null, mySrc);

				SetJob(x, null);

				if (result.IsSuccess && result.Link != null)
				{
					SetLink(x, result.Link);

					return true;
				}
			}
		}
		else if (GetLink(x) == null)
		{
			SetJob(x, MeCraftingService.BeginCraftingCalculation(
				net, what, amount, CalculationStrategy.CraftLess, mySrc));
		}
		return false;
	}

	public IReadOnlyCollection<CraftingLink> GetRequestedJobs()
	{
		var list = new List<CraftingLink>();
		foreach (var l in _links)
			if (l != null) list.Add(l);
		return list;
	}

	public void JobStateChange(CraftingLink link)
	{
		for (int x = 0; x < _links.Length; x++)
			if (_links[x] == link) { SetLink(x, null); return; }
	}

	public int GetSlot(CraftingLink link)
	{
		for (int x = 0; x < _links.Length; x++)
			if (_links[x] == link) return x;
		return -1;
	}

	public void Cancel()
	{
		for (int x = 0; x < _links.Length; x++)
		{
			_links[x]?.Cancel();
			_links[x] = null;
		}

		for (int x = 0; x < _jobs.Length; x++)
		{
			_jobs[x]?.Cancel();
			_jobs[x] = null;
		}
	}

	public bool IsBusy(int slot) => GetLink(slot) != null || GetJob(slot) != null;

	private CraftingJob? GetJob(int slot) => _jobs[slot];

	private void SetJob(int slot, CraftingJob? job) => _jobs[slot] = job;

	private CraftingLink? GetLink(int slot) => _links[slot];

	private void SetLink(int slot, CraftingLink? link)
	{
		_links[slot] = link;
		for (int x = 0; x < _links.Length; x++)
		{
			var g = _links[x];
			if (g != null && (g.IsCanceled || g.IsDone)) _links[x] = null;
		}
	}

	public void WriteToNBT(TagCompound tag)
	{
		for (int x = 0; x < _size; x++)
		{
			var link = GetLink(x);
			if (link == null) continue;
			var ln = new TagCompound();
			link.WriteToNBT(ln);
			tag["links-" + x] = ln;
		}
	}

	public void ReadFromNBT(TagCompound tag)
	{
		for (int x = 0; x < _size; x++)
		{
			string key = "links-" + x;
			if (!tag.ContainsKey(key)) continue;
			var link = new CraftingLink(tag.GetCompound(key), _owner);
			CraftingLinkManager.AddLink(link);
			SetLink(x, link);
		}
	}
}
