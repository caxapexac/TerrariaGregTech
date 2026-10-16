#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Machine;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Optical;

public sealed class OpticalNetWalker : PipeNetWalker<OpticalPipeCell, OpticalPipeProperties, OpticalPipeNet>
{
	public static readonly OpticalRoutePath FAILED_MARKER = new((0, 0), IODirection.None, 0);

	public OpticalRoutePath? RoutePath { get; private set; }

	private (int x, int y) _sourcePipe;
	private IODirection    _facingToHandler;
	private OpticalRouteKind _kind;
	private MetaMachine?   _origin;

	private OpticalNetWalker(OpticalPipeNet net, (int x, int y) sourcePipe, int distance)
		: base(net, sourcePipe, distance) { }

	public static OpticalRoutePath? CreateNetData(OpticalPipeNet world, (int x, int y) sourcePipe,
		IODirection faceToSourceHandler, OpticalRouteKind kind)
	{
		try
		{
			var walker = new OpticalNetWalker(world, sourcePipe, 1);
			walker._sourcePipe      = sourcePipe;
			walker._facingToHandler = faceToSourceHandler;
			walker._kind            = kind;
			var (ox, oy) = faceToSourceHandler.Offset();
			walker._origin = MachineCellResolver.TryFindMachineAt(sourcePipe.x + ox, sourcePipe.y + oy, out var origin)
				? origin : null;
			walker.TraversePipeNet();
			return walker.RoutePath;
		}
		catch
		{
			return FAILED_MARKER;
		}
	}

	protected override PipeNetWalker<OpticalPipeCell, OpticalPipeProperties, OpticalPipeNet> CreateSubWalker(
		OpticalPipeNet pipeNet, IODirection facingToNextPos, (int x, int y) nextPos, int walkedBlocks)
	{
		var walker = new OpticalNetWalker(pipeNet, nextPos, walkedBlocks);
		walker._sourcePipe      = _sourcePipe;
		walker._facingToHandler = _facingToHandler;
		walker._kind            = _kind;
		walker._origin          = _origin;
		return walker;
	}

	protected override bool TryGetCellAt((int x, int y) pos, out OpticalPipeCell cell)
	{
		var c = OpticalPipeLayerSystem.Pipes.CellAt(pos.x, pos.y);
		if (c is null) { cell = default; return false; }
		cell = c.Value;
		return true;
	}

	protected override void CheckPipe(OpticalPipeCell pipeTile, (int x, int y) pos) { }

	protected override bool IsValidPipe(OpticalPipeCell currentPipe, OpticalPipeCell otherPipe,
		(int x, int y) currentPos, IODirection side)
	{
		int bitHere = OpticalConn.Bit(side);
		int bitThere = OpticalConn.Bit(side.Opposite());
		return (currentPipe.Open & bitHere) != 0 && (otherPipe.Open & bitThere) != 0;
	}

	protected override void CheckNeighbour(
		OpticalPipeCell pipeNode, (int x, int y) pipePos, IODirection faceToNeighbour, object? neighbourTile)
	{
		if (pipePos.x == _sourcePipe.x && pipePos.y == _sourcePipe.y && faceToNeighbour == _facingToHandler)
			return;

		var root = (OpticalNetWalker)Root;
		if (root.RoutePath != null) return;

		var (dx, dy) = faceToNeighbour.Offset();
		int hx = pipePos.x + dx;
		int hy = pipePos.y + dy;
		if (_origin != null && MachineCellResolver.TryFindMachineAt(hx, hy, out var endpoint) && endpoint == _origin)
			return;
		bool matches = _kind == OpticalRouteKind.Computation
			? WorldCapability.Get<IOpticalComputationProvider>(hx, hy) != null
			: WorldCapability.Get<IDataAccessHatch>(hx, hy) != null;
		if (matches)
		{
			root.RoutePath = new OpticalRoutePath(pipePos, faceToNeighbour, WalkedBlocks);
			Stop();
		}
	}
}
