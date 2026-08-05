#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Machine;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Optical;

public sealed class OpticalPipeLayer : GridLayer<OpticalPipeCell>
{
	protected override bool SupportsCrossover => true;

	public override bool Connects(int x1, int y1, int x2, int y2)
	{
		var a = CellAt(x1, y1);
		var b = CellAt(x2, y2);
		if (a is null || b is null) return false;
		var side = SideBetween(x1, y1, x2, y2);
		if (side == IODirection.None) return false;
		int bitA = OpticalConn.Bit(side);
		int bitB = OpticalConn.Bit(side.Opposite());
		return (a.Value.Open & bitA) != 0 && (b.Value.Open & bitB) != 0;
	}

	private static IODirection SideBetween(int x1, int y1, int x2, int y2)
	{
		foreach (var (side, dx, dy) in OpticalConn.Sides)
		{
			var (nx, ny) = PipePassthrough.EffectiveNeighbor(x1, y1, dx, dy);
			if (nx == x2 && ny == y2) return side;
		}
		return IODirection.None;
	}
}
