#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Machine;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Laser;

public readonly record struct LaserPipeCell
{
	public byte Open { get; init; }
}

public static class LaserConn
{
	public static int Bit(IODirection side) => side switch
	{
		IODirection.Up    => 1 << 0,
		IODirection.Down  => 1 << 1,
		IODirection.Left  => 1 << 2,
		IODirection.Right => 1 << 3,
		_                 => 0,
	};

	public static readonly (IODirection side, int dx, int dy)[] Sides =
	{
		(IODirection.Up,   0, -1),
		(IODirection.Down, 0,  1),
		(IODirection.Left, -1, 0),
		(IODirection.Right, 1, 0),
	};

	public static void ConnectOnPlace(LaserPipeLayer pipes, int x, int y)
	{
		byte open = 0;
		foreach (var (side, dx, dy) in Sides)
		{
			var (nx, ny) = PipePassthrough.EffectiveNeighbor(x, y, dx, dy);
			var nc = pipes.CellAt(nx, ny);
			if (nc is null) continue;                              // pipe-to-pipe only
			int axis = Bit(side) | Bit(side.Opposite());
			if ((open & ~axis) != 0) continue;                     // this pipe would bend -> reject
			if ((nc.Value.Open & ~axis) != 0) continue;            // neighbour would bend -> reject
			open |= (byte)Bit(side);
			pipes.Set(nx, ny, nc.Value with { Open = (byte)(nc.Value.Open | Bit(side.Opposite())) });
		}
		pipes.Set(x, y, new LaserPipeCell { Open = open });
	}

	public static void ClearOnRemove(LaserPipeLayer pipes, int x, int y)
	{
		foreach (var (side, dx, dy) in Sides)
		{
			var (nx, ny) = PipePassthrough.EffectiveNeighbor(x, y, dx, dy);
			var nc = pipes.CellAt(nx, ny);
			if (nc is null) continue;
			byte cleared = (byte)(nc.Value.Open & ~Bit(side.Opposite()));
			if (cleared != nc.Value.Open)
				pipes.Set(nx, ny, nc.Value with { Open = cleared });
		}
	}

	private static readonly (IODirection side, int dx, int dy)[] Axes =
	{
		(IODirection.Right, 1, 0),
		(IODirection.Down,  0, 1),
	};

	public static void LinkAcross(LaserPipeLayer pipes, int x, int y)
	{
		foreach (var (side, dx, dy) in Axes)
		{
			var (nx, ny) = PipePassthrough.EffectiveNeighbor(x, y,  dx,  dy);
			var (fx, fy) = PipePassthrough.EffectiveNeighbor(x, y, -dx, -dy);
			var near = pipes.CellAt(nx, ny);
			var far  = pipes.CellAt(fx, fy);
			if (near is null || far is null) continue;
			int axis = Bit(side) | Bit(side.Opposite());
			if ((near.Value.Open & ~axis) != 0) continue;
			if ((far.Value.Open  & ~axis) != 0) continue;
			pipes.Set(nx, ny, near.Value with { Open = (byte)(near.Value.Open | Bit(side.Opposite())) });
			pipes.Set(fx, fy, far.Value  with { Open = (byte)(far.Value.Open  | Bit(side)) });
		}
	}

	public static void UnlinkAcross(LaserPipeLayer pipes, int x, int y)
	{
		foreach (var (side, dx, dy) in Axes)
		{
			var (nx, ny) = PipePassthrough.EffectiveNeighbor(x, y,  dx,  dy);
			var (fx, fy) = PipePassthrough.EffectiveNeighbor(x, y, -dx, -dy);
			if (pipes.CellAt(nx, ny) is { } near)
			{
				byte nc = (byte)(near.Open & ~Bit(side.Opposite()));
				if (nc != near.Open) pipes.Set(nx, ny, near with { Open = nc });
			}
			if (pipes.CellAt(fx, fy) is { } far)
			{
				byte fc = (byte)(far.Open & ~Bit(side));
				if (fc != far.Open) pipes.Set(fx, fy, far with { Open = fc });
			}
		}
	}
}
