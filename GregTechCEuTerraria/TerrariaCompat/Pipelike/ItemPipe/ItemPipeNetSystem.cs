#nullable enable
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.Api.Data.Chemical.Material.Properties;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;

public sealed class ItemPipeNetSystem : ModSystem
{
	private static LevelItemPipeNet? _level;

	public static readonly System.Collections.Generic.Dictionary<(int x, int y), int>
		ClientTransferStats = new();

	public static LevelItemPipeNet Level
	{
		get
		{
			if (_level is null) _level = new LevelItemPipeNet();
			return _level;
		}
	}

	public override void ClearWorld()
	{
		_level = new LevelItemPipeNet();
		ClientTransferStats.Clear();
	}

	public override void PostUpdateEverything() => MaybeRebuild();

	public override void PostUpdateWorld()
	{
		lock (Api.SaveTickGate.Lock)
			TickItemPipeNet();
	}

	private void TickItemPipeNet()
	{
		using (Profiler.Profiler.Time("tick", "item_pipe_net"))
		{
		MaybeRebuild();

		foreach (var pcv in ItemPipeLayerSystem.AllSides.Values)
			pcv.SystemTick();

		Profiler.Profiler.Gauge("counts", "item_pipe_sides", ItemPipeLayerSystem.AllSides.Count);
		Profiler.Profiler.Gauge("counts", "item_pipe_nets",  Level.AllPipeNets.Count);
		}

		int syncPeriod = global::GregTechCEuTerraria.Config.GTConfig.Instance?.NetworkSyncPeriod ?? 6;
		if (Main.netMode == NetmodeID.Server && Main.GameUpdateCount % syncPeriod == 0)
			PipeStatsPacket.Broadcast();
	}

	public static void MaybeRebuild()
	{
		if (!ItemPipeLayerSystem.Pipes.IsDirty) return;

		_level = new LevelItemPipeNet();
		foreach (var kv in ItemPipeLayerSystem.Pipes.All)
		{
			ItemPipeLayerSystem.EnsureSides(kv.Key.x, kv.Key.y);
			OnPipeAdded(kv.Key.x, kv.Key.y, kv.Value);
		}
		ItemPipeLayerSystem.Pipes.ClearDirty();
	}

	public static void OnPipeAdded(int x, int y, ItemPipeCell cell)
	{
		if (Level.GetNetFromPos((x, y)) is not null) return;

		var nodeData = new ItemPipeProperties(cell.Priority, cell.TransferRate);
		int mark = MaterialMark(cell.MaterialId);
		Level.AddNode((x, y), nodeData, mark, Node<ItemPipeProperties>.ALL_OPENED, isActive: false);
	}

	public static void OnPipeRemoved(int x, int y) => Level.RemoveNode((x, y));

	private static int MaterialMark(string id)
	{
		uint h = 2166136261u;
		for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
		return unchecked((int)(h | 0x80000000u));
	}
}

public sealed class LevelItemPipeNet : LevelPipeNet<ItemPipeProperties, ItemPipeNet>
{
	protected internal override ItemPipeNet CreateNetInstance() => new(this);
}
