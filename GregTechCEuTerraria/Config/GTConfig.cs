#nullable enable
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace GregTechCEuTerraria.Config;

public sealed class GTConfig : ModConfig
{
	public override ConfigScope Mode => ConfigScope.ServerSide;

	[DefaultValue(true)]
	public bool EnableBossDrops { get; set; } = true;

	[DefaultValue(false)]
	public bool AllowShimmerDupes { get; set; } = false;

	// 1.0 = 20 MC ticks/sec
	[Range(0.1f, 10f)]
	[Increment(0.1f)]
	[DefaultValue(1.0f)]
	public float SimulationSpeed { get; set; } = 1.0f;

	// 6 = 10 Hz
	[Range(1, 60)]
	[Increment(1)]
	[DefaultValue(6)]
	public int NetworkSyncPeriod { get; set; } = 6;

	// cleanroom requirement overall
	[DefaultValue(true)]
	public bool EnableCleanroom { get; set; } = true;

	// cleanroom requirement for multis
	[DefaultValue(true)]
	public bool MultiblocksNeedCleanroom { get; set; } = true;

	// Debug gizmos for bosses
	[DefaultValue(false)]
	public bool DebugMobs { get; set; } = false;

	// Debug profiler logic
	[DefaultValue(false)]
	public bool EnableProfiler { get; set; } = false;

	[Range(0, 1000)]
	[Increment(10)]
	[DefaultValue(10)]
	public int LdItemPipeMinDistance { get; set; } = 10;

	[Range(0, 1000)]
	[Increment(10)]
	[DefaultValue(10)]
	public int LdFluidPipeMinDistance { get; set; } = 10;

	[DefaultValue(false)]
	public bool CraftingSimulatedExtraction { get; set; } = false;

	[Range(1, 100)]
	[Increment(1)]
	[DefaultValue(5)]
	public int CraftingCalculationTimePerTick { get; set; } = 5;

	[DefaultValue(true)]
	public bool FreeMePatterns { get; set; } = true;

	[DefaultValue(true)]
	public bool OrderedAssemblyLineItems { get; set; } = true;

	public override void OnChanged()
	{
		TerrariaCompat.Profiler.Profiler.Enabled = EnableProfiler;
	}

	public static GTConfig Instance => ModContent.GetInstance<GTConfig>();
}
