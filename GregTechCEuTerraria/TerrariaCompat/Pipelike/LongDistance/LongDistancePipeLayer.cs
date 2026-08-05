#nullable enable
using GregTechCEuTerraria.Api.Pipenet;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.LongDistance;

public sealed class LongDistancePipeLayer : GridLayer<LongDistancePipeCell>
{
	protected override bool SupportsCrossover => true;

	public override bool Connects(int x1, int y1, int x2, int y2)
	{
		var a = CellAt(x1, y1);
		var b = CellAt(x2, y2);
		if (a is null || b is null) return false;
		return a.Value.Type == b.Value.Type;
	}
}
