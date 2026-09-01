#nullable enable
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.Api.Fluids;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;

public interface IFluidPipeHost
{
	bool IsBlocked(CoverSide side);
	int CapacityPerTank { get; }
	void ReceivedFrom(CoverSide side);
}
