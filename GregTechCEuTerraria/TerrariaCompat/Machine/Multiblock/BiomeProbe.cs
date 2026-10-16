#nullable enable
using Microsoft.Xna.Framework;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

// vanilla SceneMetrics at an arbitrary center
public static class BiomeProbe
{
	public enum Biome
	{
		Forest, Desert, Snow, Jungle, Ocean, Mushroom,
		Crimson, Corruption, Hallow, Underworld,
		Underground, Cavern,
	}

	public enum Depth { Surface, Underground, Cavern, Underworld }

	public enum WallBiome { None, Granite, Marble, Hive, GemCave, LihzahrdTemple, Dungeon }

	private static readonly SceneMetrics _metrics = new();

	public static SceneMetrics ScanAt(int tileX, int tileY)
	{
		_metrics.ScanAndExportToMain(new SceneMetricsScanSettings
		{
			BiomeScanCenterPositionInWorld = new Vector2(tileX * 16 + 8, tileY * 16 + 8),
			ScanOreFinderData = false,
		});
		return _metrics;
	}

	public static Depth DepthAt(int tileY)
	{
		if (tileY > Main.UnderworldLayer) return Depth.Underworld;
		if (tileY > Main.rockLayer)       return Depth.Cavern;
		if (tileY > Main.worldSurface)    return Depth.Underground;
		return Depth.Surface;
	}

	public static WallBiome WallBiomeAt(int tileX, int tileY)
	{
		int wall = Framing.GetTileSafely(tileX, tileY).WallType;
		if (wall == 184 || wall == 180) return WallBiome.Granite;
		if (wall == 183 || wall == 178) return WallBiome.Marble;
		if (wall == 108 || wall == 86)  return WallBiome.Hive;
		if (wall >= 48 && wall <= 53)   return WallBiome.GemCave;
		if (wall == 87)                 return WallBiome.LihzahrdTemple;
		if (tileY > Main.worldSurface && Main.wallDungeon[wall]) return WallBiome.Dungeon;
		return WallBiome.None;
	}

	public static Biome GetForTile(int tileX, int tileY)
	{
		if (tileY > Main.UnderworldLayer) return Biome.Underworld;
		if (WorldGen.oceanDepths(tileX, tileY)) return Biome.Ocean;

		var m = ScanAt(tileX, tileY);
		if (m.EnoughTilesForGlowingMushroom) return Biome.Mushroom;
		if (m.EnoughTilesForJungle)          return Biome.Jungle;
		if (m.EnoughTilesForSnow)            return Biome.Snow;
		if (m.EnoughTilesForHallow)          return Biome.Hallow;
		if (m.EnoughTilesForCrimson)         return Biome.Crimson;
		if (m.EnoughTilesForCorruption)      return Biome.Corruption;
		if (m.EnoughTilesForDesert)          return Biome.Desert;
		return DepthAt(tileY) switch
		{
			Depth.Cavern      => Biome.Cavern,
			Depth.Underground => Biome.Underground,
			_                 => Biome.Forest,
		};
	}
}
