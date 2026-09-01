#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Machine.Multiblock;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

public static class FusionAdditionalDisplay
{
	public static void FusionInfo(MetaMachine controller, List<string> lines)
	{
		if (controller is not FusionReactorMachine f || !f.IsFormed) return;
		var cap = f.CapacitorContainer;
		lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.fusion_reactor.energy",
			$"{cap.EnergyStored:N0}", $"{cap.EnergyCapacity:N0}"));
		lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.fusion_reactor.heat",
			$"{f.Heat:N0}"));
	}
}
