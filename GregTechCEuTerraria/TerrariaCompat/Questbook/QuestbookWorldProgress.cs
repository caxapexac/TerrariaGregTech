#nullable enable
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Achievements;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.Questbook;

public sealed class QuestbookWorldProgress : ModSystem
{
	internal const int RewardGold = 5;
	private const int PollInterval = 30;

	public static HashSet<string> Completed { get; private set; } = [];

	public static HashSet<string> SatisfiedTasks { get; private set; } = [];

	internal static QuestbookWorldProgress Instance => ModContent.GetInstance<QuestbookWorldProgress>();

	private int _pollTimer;

	public override void ClearWorld()
	{
		Completed = [];
		SatisfiedTasks = [];
	}

	public override void SaveWorldData(TagCompound tag)
	{
		lock (Api.SaveTickGate.Lock)
		{
			if (Completed.Count > 0) tag["completed"] = Completed.ToList();
			if (SatisfiedTasks.Count > 0) tag["tasks"] = SatisfiedTasks.ToList();
		}
	}

	public override void LoadWorldData(TagCompound tag)
	{
		Completed = tag.TryGet("completed", out List<string> c) ? [.. c] : [];
		SatisfiedTasks = tag.TryGet("tasks", out List<string> t) ? [.. t] : [];
	}

	public override void OnWorldLoad() => QuestbookPackets.SendStateRequest();

	public override void PostUpdateWorld()
	{
		if (QuestbookSystem.Resolved.Count == 0)
			return;
		if (++_pollTimer < PollInterval)
			return;
		_pollTimer = 0;

		foreach (string id in QuestbookSystem.Resolved.Keys)
		{
			if (Completed.Contains(id) || !QuestbookSystem.IsAutoCheck(id))
				continue;

			if (TryAdvance(id, out int contributor))
				CompleteQuest(id, contributor);
		}
	}

	private bool TryAdvance(string questId, out int contributor)
	{
		contributor = -1;
		ResolvedQuest r = QuestbookSystem.Resolved[questId];
		bool all = true;
		bool changed = false;

		for (int i = 0; i < r.Tasks.Count; i++)
		{
			ResolvedTask task = r.Tasks[i];
			if (!task.IsItem || task.AcceptTypes.Length == 0)
				continue;

			string key = QuestbookSystem.TaskKey(questId, i);
			if (SatisfiedTasks.Contains(key))
				continue;

			int who = FindSatisfyingPlayer(task);
			if (who >= 0)
			{
				lock (Api.SaveTickGate.Lock)
					SatisfiedTasks.Add(key);
				contributor = who;
				changed = true;
			}
			else
			{
				all = false;
			}
		}

		if (changed && !all)
			QuestbookPackets.BroadcastSync();

		return all;
	}

	private static int FindSatisfyingPlayer(ResolvedTask task)
	{
		for (int p = 0; p < Main.maxPlayers; p++)
		{
			Player pl = Main.player[p];
			if (pl is not { active: true })
				continue;
			if (QuestbookSystem.IsItemTaskSatisfied(task, pl))
				return p;
		}
		return -1;
	}

	internal void CompleteTask(string questId, int taskIndex, int contributor)
	{
		if (Completed.Contains(questId))
			return;
		if (!QuestbookSystem.IsFluidTask(questId, taskIndex))
			return;

		string key = QuestbookSystem.TaskKey(questId, taskIndex);
		bool added;
		lock (Api.SaveTickGate.Lock)
			added = SatisfiedTasks.Add(key);

		if (TryAdvance(questId, out int autoContributor))
			CompleteQuest(questId, contributor >= 0 ? contributor : autoContributor);
		else if (added)
			QuestbookPackets.BroadcastSync();
	}

	internal void CompleteQuest(string questId, int contributor)
	{
		if (!QuestbookSystem.QuestsById.ContainsKey(questId))
			return;
		bool newlyCompleted;
		lock (Api.SaveTickGate.Lock)
			newlyCompleted = Completed.Add(questId);
		if (!newlyCompleted)
			return;

		Player? p = contributor >= 0 && contributor < Main.maxPlayers ? Main.player[contributor] : null;
		string who = p is { active: true } ? p.name : "Someone";
		string title = QuestbookSystem.QuestsById.TryGetValue(questId, out QuestData? q) ? q.Title : questId;

		bool reward = QuestbookSystem.GivesReward(questId);
		if (reward && p is { active: true })
			PlayerGive.Give(p, p.GetSource_GiftOrReward(), ItemID.GoldCoin, RewardGold);

		Announce(reward
			? $"{who} finished \"{title}\" and received {RewardGold} gold coins!"
			: $"{who} finished \"{title}\"!");

		QuestbookPackets.BroadcastSync();

		if (Main.netMode == NetmodeID.SinglePlayer)
			FireBanner(questId);
	}

	private static void Announce(string message)
	{
		var color = new Color(255, 220, 90);
		if (Main.netMode == NetmodeID.Server)
			ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), color);
		else if (!Main.dedServ)
			Main.NewText(message, color.R, color.G, color.B);
	}

	internal static void ApplySync(HashSet<string> completed, HashSet<string> tasks, bool initial)
	{
		if (!initial)
			foreach (string id in completed)
				if (!Completed.Contains(id))
					FireBanner(id);

		Completed = completed;
		SatisfiedTasks = tasks;
	}

	private static void FireBanner(string questId)
	{
		if (Main.dedServ)
			return;

		string key = "Achievements.GTQuest_" + questId + "_Name";
		string title = QuestbookSystem.QuestsById.TryGetValue(questId, out QuestData? q)
			? q.Title : questId;
		Language.GetOrRegister(key, () => title);

		var achievement = new Achievement("GTQuest_" + questId);
		InGameNotificationsTracker.AddCompleted(achievement);
		SoundEngine.PlaySound(SoundID.AchievementComplete);
	}
}
