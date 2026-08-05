#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Questbook;

public static class QuestbookProgress
{
	public static bool IsComplete(string questId)
		=> QuestbookWorldProgress.Completed.Contains(questId);

	public static bool IsTaskSatisfied(string questId, int taskIndex)
		=> QuestbookWorldProgress.SatisfiedTasks.Contains(QuestbookSystem.TaskKey(questId, taskIndex));

	public static void MarkManual(string questId)
	{
		if (QuestbookWorldProgress.Completed.Contains(questId))
			return;
		if (QuestbookSystem.IsAutoCheck(questId))
			return;

		if (Main.netMode == NetmodeID.MultiplayerClient)
			QuestbookPackets.SendCompleteRequest(questId);
		else
			QuestbookWorldProgress.Instance.CompleteQuest(questId, Main.myPlayer);
	}

	public static void MarkTask(string questId, int taskIndex)
	{
		if (QuestbookWorldProgress.Completed.Contains(questId))
			return;
		if (IsTaskSatisfied(questId, taskIndex))
			return;

		if (Main.netMode == NetmodeID.MultiplayerClient)
			QuestbookPackets.SendTaskCompleteRequest(questId, taskIndex);
		else
			QuestbookWorldProgress.Instance.CompleteTask(questId, taskIndex, Main.myPlayer);
	}
}
