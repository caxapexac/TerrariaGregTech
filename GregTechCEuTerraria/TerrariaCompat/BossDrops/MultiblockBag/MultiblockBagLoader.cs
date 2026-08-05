#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.BossDrops.MultiblockBag;

public static class MultiblockBagLoader
{
	public const string NamePrefix = "multiblock_bag_";

	private static readonly Dictionary<string, int> _byMultiId = new();
	public static IReadOnlyDictionary<string, int> ByMultiId => _byMultiId;

	public static bool TryGet(string multiId, out int itemType) =>
		_byMultiId.TryGetValue(multiId, out itemType);

	public static IEnumerable<KeyValuePair<string, int>> All => _byMultiId;

	public static void Register(Mod mod)
	{
		_byMultiId.Clear();
		int registered = 0;
		var untiered = new List<string>();
		foreach (var def in MachineRegistry.All)
		{
			if (def.PatternFactory is null) continue;
			var bag = new MultiblockBagItem(def.Id, def.Label);
			mod.AddContent(bag);
			_byMultiId[def.Id] = bag.Type;
			registered++;
			if (!MultiblockBagTierMap.HasTier(def.Id)) untiered.Add(def.Id);
		}
		mod.Logger.Info($"MultiblockBagLoader: registered {registered} multiblock bags.");
		if (untiered.Count > 0)
			mod.Logger.Warn($"MultiblockBagLoader: {untiered.Count} multi(s) missing from MultiblockBagTierMap - " +
				$"their bags will drop at the default tier {MultiblockBagTierMap.DefaultTier}: {string.Join(", ", untiered)}");
	}

	public static void Unload() => _byMultiId.Clear();
}
