#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class MeBusExportRequester : IMeCraftingRequester
{
	public readonly int X, Y;
	public readonly IODirection Side;
	public readonly MultiCraftingTracker Tracker;

	public MeBusExportRequester(int x, int y, IODirection side, int slots)
	{
		X = x;
		Y = y;
		Side = side;
		Tracker = new MultiCraftingTracker(this, slots);
	}

	public MeNetwork? RequesterNetwork => MeNetworkSystem.NetAt(X, Y);

	public IReadOnlyCollection<CraftingLink> GetRequestedJobs() => Tracker.GetRequestedJobs();

	public void JobStateChange(CraftingLink link) => Tracker.JobStateChange(link);

	public long InsertCraftedItems(CraftingLink link, AEKey what, long amount, Actionable mode)
	{
		if (RequesterNetwork == null) return 0;
		var (dx, dy) = Side.Offset();
		int nx = X + dx, ny = Y + dy;
		var arrival = Side.Opposite();
		MEStorage to = what is AEFluidKey
			? new FluidHandlerMeStorage(() => WorldCapability.FluidHandlerAt(nx, ny, arrival), false)
			: new ItemHandlerMeStorage(() => WorldCapability.ItemHandlerAt(nx, ny, arrival), false);
		return to.Insert(what, amount, mode, IActionSource.Empty());
	}
}
