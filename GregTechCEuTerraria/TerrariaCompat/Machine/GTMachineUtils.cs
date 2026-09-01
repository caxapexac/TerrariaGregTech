#nullable enable
using System;
using GregTechCEuTerraria.Common.Energy;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public static class GTMachineUtils
{
	public const int BUCKET_VOLUME = 1000;

	public static readonly Func<VoltageTier, int> DefaultTankSizeFunction = tier =>
		(tier <= VoltageTier.LV ? 8 :
			tier == VoltageTier.MV ? 12 : tier == VoltageTier.HV ? 16 : tier == VoltageTier.EV ? 32 : 64) *
		BUCKET_VOLUME;

	public static readonly Func<VoltageTier, int> HvCappedTankSizeFunction = tier =>
		(tier <= VoltageTier.LV ? 8 :
			tier == VoltageTier.MV ? 12 : 16) * BUCKET_VOLUME;

	public static readonly Func<VoltageTier, int> LargeTankSizeFunction = tier =>
		(tier <= VoltageTier.LV ? 32 :
			tier == VoltageTier.MV ? 48 : 64) * BUCKET_VOLUME;

	public static readonly Func<VoltageTier, int> SteamGeneratorTankSizeFunction = tier =>
		Math.Min(16 * (1 << ((int)tier - 1)), 64) * BUCKET_VOLUME;

	public static readonly Func<VoltageTier, int> GenericGeneratorTankSizeFunction = tier =>
		Math.Min(4 * (1 << ((int)tier - 1)), 16) * BUCKET_VOLUME;
}
