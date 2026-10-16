#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Machine;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Optical;

public sealed class OpticalNetHandler : IDataAccessHatch, IOpticalComputationProvider
{
	private OpticalPipeNet _net;
	private readonly (int x, int y) _pipePos;
	private readonly IODirection    _facing;

	public OpticalNetHandler(OpticalPipeNet net, (int x, int y) pipePos, IODirection facing)
	{
		_net     = net;
		_pipePos = pipePos;
		_facing  = facing;
	}

	public void UpdateNetwork(OpticalPipeNet net) => _net = net;
	public OpticalPipeNet GetNet() => _net;

	private void SetPipesActive()
	{
		foreach (var pos in _net.AllNodes.Keys)
			OpticalPipeLayerSystem.SetActive(pos.x, pos.y, ticks: 100);
	}

	private bool IsNetInvalidForTraversal() =>
		_net == null || _facing == IODirection.None || !OpticalPipeLayerSystem.Pipes.Has(_pipePos.x, _pipePos.y);

	public bool IsRecipeAvailable(GTRecipe recipe, ICollection<IDataAccessHatch> seen)
	{
		bool available = TraverseRecipeAvailable(recipe, seen);
		if (available) SetPipesActive();
		return available;
	}

	public bool IsCreative() => false;

	public GTRecipe? ModifyRecipe(GTRecipe recipe)
	{
		if (IsCreative()) return recipe;
		if (IsRecipeAvailable(recipe, NewDataSeen())) return recipe;
		return null;
	}

	private bool TraverseRecipeAvailable(GTRecipe recipe, ICollection<IDataAccessHatch> seen)
	{
		if (IsNetInvalidForTraversal()) return false;
		var inv = _net.GetNetData(_pipePos, _facing, OpticalRouteKind.Data);
		if (inv == null) return false;
		var hatch = inv.GetDataHatch();
		if (hatch == null || seen.Contains(hatch)) return false;
		if (hatch.IsTransmitter())
			return hatch.IsRecipeAvailable(recipe, seen);
		return false;
	}

	public IOpticalDataAccessHatch? ResolveRemoteDataHatch()
	{
		if (IsNetInvalidForTraversal()) return null;
		return _net.GetNetData(_pipePos, _facing, OpticalRouteKind.Data)?.GetDataHatch();
	}

	public IOpticalComputationProvider? ResolveRemoteComputationProvider()
	{
		if (IsNetInvalidForTraversal()) return null;
		return _net.GetNetData(_pipePos, _facing, OpticalRouteKind.Computation)?.GetComputationHatch();
	}

	public int CountVisibleResearch(ICollection<IDataAccessHatch> seen)
	{
		if (IsNetInvalidForTraversal()) return 0;
		var inv = _net.GetNetData(_pipePos, _facing, OpticalRouteKind.Data);
		var hatch = inv?.GetDataHatch();
		if (hatch == null || seen.Contains(hatch)) return 0;
		return hatch.IsTransmitter() ? hatch.CountVisibleResearch(seen) : 0;
	}

	public int RequestCWUt(int cwut, bool simulate, ICollection<IOpticalComputationProvider> seen)
	{
		if (cwut == 0) return 0;
		var provider = GetComputationProvider(seen);
		if (provider == null) return 0;
		int provided = provider.RequestCWUt(cwut, simulate, seen);
		if (provided > 0) SetPipesActive();
		return provided;
	}

	public int RequestCWUt(int cwut, bool simulate) => RequestCWUt(cwut, simulate, NewCompSeen());

	public int GetMaxCWUt(ICollection<IOpticalComputationProvider> seen)
	{
		var provider = GetComputationProvider(seen);
		return provider?.GetMaxCWUt(seen) ?? 0;
	}

	public int GetMaxCWUt() => GetMaxCWUt(NewCompSeen());

	public bool CanBridge(ICollection<IOpticalComputationProvider> seen)
	{
		var provider = GetComputationProvider(seen);
		return provider == null || provider.CanBridge(seen);
	}

	public bool CanBridge() => CanBridge(NewCompSeen());

	private IOpticalComputationProvider? GetComputationProvider(ICollection<IOpticalComputationProvider> seen)
	{
		if (IsNetInvalidForTraversal()) return null;
		var inv = _net.GetNetData(_pipePos, _facing, OpticalRouteKind.Computation);
		if (inv == null) return null;
		var hatch = inv.GetComputationHatch();
		if (hatch == null || seen.Contains(hatch)) return null;
		return hatch;
	}

	private static ICollection<IDataAccessHatch> NewDataSeen() => new HashSet<IDataAccessHatch>();
	private static ICollection<IOpticalComputationProvider> NewCompSeen() => new HashSet<IOpticalComputationProvider>();
}
