#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Common.Energy;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public static class MachineTooltipBuilders
{
	private static readonly Dictionary<string, Action<List<string>, MachineDefinition>> _byId = new()
	{
		["data_access_hatch"] = static (lines, def) =>
		{
			int slots = def.PartIo != null
				? SlotsForTier(def)
				: 1;
			if (lines.Count > 1) lines[1] = string.Format(lines[1], slots);
		},

		["data_bank"] = static (lines, _) =>
		{
			int eutNormal  = VoltageTiers.VA((int)VoltageTier.EV);
			int eutChained = VoltageTiers.VA((int)VoltageTier.LuV);
			if (lines.Count > 3) lines[3] = string.Format(lines[3], eutNormal);
			if (lines.Count > 4) lines[4] = string.Format(lines[4], eutChained);
		},

		["network_switch"] = static (lines, _) =>
		{
			int eut = VoltageTiers.VA((int)VoltageTier.IV);
			if (lines.Count > 3) lines[3] = string.Format(lines[3], eut);
		},

		["power_substation"] = static (lines, _) =>
		{
			const int maxLayers = 18;
			const int kEutPerStorage = 100;
			if (lines.Count > 2) lines[2] = string.Format(lines[2], maxLayers);
			if (lines.Count > 4) lines[4] = string.Format(lines[4], kEutPerStorage);
		},

		["luv_fusion_reactor"] = FusionCapacity,
		["zpm_fusion_reactor"] = FusionCapacity,
		["uv_fusion_reactor"]  = FusionCapacity,

		// Upstream IMiner.getWorkingArea(tier * 8) = tier * 16 - 1.
		["miner"] = static (lines, def) =>
		{
			int tier = (def.Tiers.Length > 0 ? (int)def.Tiers[0] : (int)VoltageTier.LV);
			int area = tier * 16 - 1;
			if (lines.Count > 0) lines[0] = string.Format(lines[0], area, area);
		},
	};

	private static void FusionCapacity(List<string> lines, MachineDefinition def)
	{
		int tier = def.Tiers.Length > 0 ? (int)def.Tiers[0] : (int)VoltageTier.LuV;
		long megaEu = Multiblock.Electric.FusionReactorMachine
			.CalculateEnergyStorageFactor(tier, 16) / 1_000_000L;
		if (lines.Count > 0) lines[0] = string.Format(lines[0], megaEu);
	}

	private static int SlotsForTier(MachineDefinition def)
	{
		int tier = def.Tiers.Length > 0 ? (int)def.Tiers[0] : (int)VoltageTier.HV;
		return tier switch
		{
			(int)VoltageTier.LuV => 16,
			(int)VoltageTier.EV  => 9,
			(int)VoltageTier.HV  => 4,
			_                           => 1,
		};
	}

	public static Action<List<string>, MachineDefinition>? Get(string? id) =>
		id != null && _byId.TryGetValue(id, out var b) ? b : null;
}
