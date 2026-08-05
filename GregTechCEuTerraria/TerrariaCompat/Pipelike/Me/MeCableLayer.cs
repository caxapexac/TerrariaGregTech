#nullable enable
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.AppliedEnergistics.Api.Util;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class MeCableLayer : GridLayer<MeCableCell>
{
	protected override bool SupportsCrossover => true;

	public override bool Connects(int x1, int y1, int x2, int y2)
	{
		var a = CellAt(x1, y1);
		var b = CellAt(x2, y2);
		if (a is null || b is null) return false;
		var ca = a.Value.Color;
		var cb = b.Value.Color;
		return ca == cb || ca == AEColor.TRANSPARENT || cb == AEColor.TRANSPARENT;
	}
}
