#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GregTechCEuTerraria.TerrariaCompat.Items.Fluids;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Questbook;

// Loads Data/Questbook/questlog.json
public sealed class QuestbookSystem : ModSystem
{
	public const string LocRoot = "Mods.GregTechCEuTerraria.Questbook";

	internal static QuestLogData Data { get; private set; } = new();
	internal static Dictionary<string, QuestData> QuestsById { get; private set; } = [];
	internal static Dictionary<string, ResolvedQuest> Resolved { get; private set; } = [];

	public override void PostSetupContent()
	{
		try
		{
			LoadQuestbook();
		}
		catch (Exception e)
		{
			Mod.Logger.Error("Questbook load failed", e);
		}
	}

	private void LoadQuestbook()
	{
		byte[] bytes = Mod.GetFileBytes("Data/Questbook/questlog.json");
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		Data = JsonSerializer.Deserialize<QuestLogData>(bytes, options) ?? new QuestLogData();
		QuestsById = Data.Quests.ToDictionary(q => q.Id);

		Resolved = [];

		foreach (QuestData quest in Data.Quests)
			Resolved[quest.Id] = ResolveQuest(quest);

		int total = Resolved.Count;
		int auto = Resolved.Values.Count(r => r.AutoCheck);
		Mod.Logger.Info($"Questbook: {Data.Chapters.Count} chapters, {total} quests "
			+ $"({auto} auto-check, {total - auto} manual)");
	}

	private static ResolvedQuest ResolveQuest(QuestData quest)
	{
		IngredientResolverImpl resolver = IngredientResolverImpl.Instance;
		var resolved = new ResolvedQuest();
		bool anyItemResolved = false;

		foreach (TaskData task in quest.Tasks)
		{
			if (task.Type == "item")
			{
				var types = new List<int>();
				foreach (string id in task.Items)
				{
					int t = resolver.ResolveItemType(id);
					if (t > 0)
						types.Add(t);
				}
				if (!string.IsNullOrEmpty(task.Tag))
					foreach (int t in resolver.ResolveItemTag(task.Tag))
						if (t > 0)
							types.Add(t);

				int[] accept = types.Distinct().ToArray();
				resolved.Tasks.Add(new ResolvedTask
				{
					IsItem = true,
					IsFluid = AllFluidBuckets(accept),
					AcceptTypes = accept,
					Count = Math.Max(1, task.Count),
					Label = task.Label,
				});
				if (accept.Length > 0)
					anyItemResolved = true;
			}
			else
			{
				resolved.Tasks.Add(new ResolvedTask { IsItem = false });
			}
		}

		resolved.AutoCheck = anyItemResolved;

		resolved.IconType = resolver.ResolveItemType(quest.Icon);
		if (resolved.IconType <= 0)
			foreach (ResolvedTask t in resolved.Tasks)
				if (t.IsItem && t.AcceptTypes.Length > 0)
				{
					resolved.IconType = t.AcceptTypes[0];
					break;
				}

		return resolved;
	}

	private static bool AllFluidBuckets(int[] types)
	{
		if (types.Length == 0)
			return false;
		foreach (int t in types)
			if (ModContent.GetModItem(t) is not FluidBucketItem)
				return false;
		return true;
	}

	public static bool IsFluidTask(string questId, int taskIndex)
		=> Resolved.TryGetValue(questId, out ResolvedQuest? r)
			&& taskIndex >= 0 && taskIndex < r.Tasks.Count && r.Tasks[taskIndex].IsFluid;

	public static bool GivesReward(string questId)
	{
		if (!Resolved.TryGetValue(questId, out ResolvedQuest? r))
			return false;
		bool hasCollectible = false;
		foreach (ResolvedTask t in r.Tasks)
		{
			if (!t.IsItem)
				return false;
			if (!t.IsFluid && t.AcceptTypes.Length > 0)
				hasCollectible = true;
		}
		return hasCollectible;
	}

	internal static void ReResolve(QuestData quest)
		=> Resolved[quest.Id] = ResolveQuest(quest);

	internal static void RebuildIndex()
		=> QuestsById = Data.Quests.ToDictionary(q => q.Id);

	public static bool IsAutoCheck(string questId)
		=> Resolved.TryGetValue(questId, out ResolvedQuest? r) && r.AutoCheck;

	public static string TaskKey(string questId, int taskIndex) => questId + "#" + taskIndex;

	internal static bool IsItemTaskSatisfied(ResolvedTask task, Player player)
	{
		int have = 0;
		foreach (int type in task.AcceptTypes)
			have += CountInInventory(player, type);
		return have >= task.Count;
	}

	public static int CountInInventory(Player player, int itemType)
	{
		int total = 0;
		foreach (Item item in player.inventory)
			if (item is { IsAir: false } && item.type == itemType)
				total += item.stack;

		return total;
	}
}

internal sealed class ResolvedQuest
{
	public bool AutoCheck;
	public int IconType;
	public List<ResolvedTask> Tasks = [];
}

internal sealed class ResolvedTask
{
	public bool IsItem;
	public bool IsFluid;
	public int[] AcceptTypes = [];
	public int Count = 1;
	public string Label = "";
}
