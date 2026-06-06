#nullable enable
using System.Collections.Generic;
using System.Text;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

// Maps a recipe station id to the ItemType whose icon represents it, so a
// recipe row draws a block icon instead of a `@station_id` text chip
public static class StationIcon
{
	private static readonly Dictionary<string, int> _cache = new();
	private static readonly string[] _tiers = { "lv", "mv", "hv", "ev", "iv", "luv", "zpm", "uv", "uhv", "ulv" };

	public static int ItemTypeFor(string stationId, Mod? mod)
	{
		if (string.IsNullOrEmpty(stationId)) return 0;
		if (_cache.TryGetValue(stationId, out int cached)) return cached;

		if (VanillaCraftingBridge.IsHandStation(stationId))
		{
			_cache[stationId] = ItemID.HandOfCreation;
			return ItemID.HandOfCreation;
		}

		if (VanillaCraftingBridge.TryGetStationTile(stationId, out int bridgeTile))
		{
			int bridgeItem = FindItemForTile(bridgeTile);
			if (bridgeItem > 0) { _cache[stationId] = bridgeItem; return bridgeItem; }
		}

		int found = 0;

		if (mod is not null)
		{
			// 1. Tiered machine
			foreach (var t in _tiers)
				if (mod.TryFind<ModItem>($"{t}_{stationId}", out var item)) { found = item.Type; break; }

			// 2. Bare station name (drum, crate, coke_oven, ...)
			if (found == 0 && mod.TryFind<ModItem>(stationId, out var bare))
				found = bare.Type;
		}

		// 3. Vanilla tile reverse-lookup
		if (found == 0)
		{
			string pascal = SnakeToPascal(stationId);
			if (TileID.Search.TryGetId(pascal, out int tileType))
				found = FindItemForTile(tileType);
		}

		_cache[stationId] = found;
		return found;
	}

	private static readonly Dictionary<int, int> _tileItemCache = new();
	public static int ItemTypeForTile(int tileType)
	{
		if (tileType <= 0) return 0;
		if (_tileItemCache.TryGetValue(tileType, out int cached)) return cached;
		int found = FindItemForTile(tileType);
		_tileItemCache[tileType] = found;
		return found;
	}

	private static int FindItemForTile(int tileType)
	{
		foreach (var (id, item) in ContentSamples.ItemsByType)
			if (item.createTile == tileType)
				return id;
		return 0;
	}

	private static string SnakeToPascal(string snake)
	{
		var sb = new StringBuilder(snake.Length);
		bool capNext = true;
		foreach (char c in snake)
		{
			if (c == '_') { capNext = true; continue; }
			sb.Append(capNext ? char.ToUpperInvariant(c) : c);
			capNext = false;
		}
		return sb.ToString();
	}

	public static void ClearCache() => _cache.Clear();
}
