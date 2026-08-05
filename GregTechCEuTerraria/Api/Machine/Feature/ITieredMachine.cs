#nullable enable
using GregTechCEuTerraria.Common.Energy;

namespace GregTechCEuTerraria.Api.Machine.Feature;

public interface ITieredMachine
{
	int GetTier();

	long GetMaxVoltage() => VoltageTiers.Voltage((VoltageTier)GetTier());
}
