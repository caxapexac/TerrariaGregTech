#nullable enable
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Worldgen;

public static class VeinWorldDimensions
{
	private const double SurfaceLowFraction = 0.15;

	public static WorldDimensions Current() => new(
		SurfaceLow:      (int)(Main.maxTilesY * SurfaceLowFraction),
		SurfaceHigh:     (int)Main.worldSurface - 25,
		RockLayer:       (int)Main.rockLayer,
		UnderworldLayer: Main.UnderworldLayer,
		MaxY:            Main.maxTilesY);
}
