#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Cover;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.Api.Pipenet;

public abstract class LevelPipeNet<TData, TNet> : ILevelPipeNet<TData>
	where TData : notnull
	where TNet  : PipeNet<TData>
{
	protected List<TNet> PipeNets = new();
	protected readonly Dictionary<(int cx, int cy), List<TNet>> PipeNetsByChunk = new();

	private bool _dirty;
	public bool IsDirty => _dirty;
	public void SetDirty() => _dirty = true;
	public void ClearDirty() => _dirty = false;

	protected virtual void Init()
	{
	}

	public void AddNode((int x, int y) pos, TData nodeData, int mark, int openConnections, bool isActive)
	{
		TNet? myPipeNet = null;
		var node = new Node<TData>(nodeData, openConnections, mark, isActive);
		foreach (var facing in CoverSides.All)
		{
			var offsetPos = Offset(pos, facing);
			var pipeNet = GetNetFromPos(offsetPos);
			var secondNode = pipeNet?.GetNodeAt(offsetPos);
			if (pipeNet != null && pipeNet.CanAttachNode(nodeData) &&
				pipeNet.CanNodesConnect(secondNode!, CoverSides.Opposite(facing), node, null))
			{
				if (myPipeNet == null)
				{
					myPipeNet = pipeNet;
					AsBaseNet(myPipeNet)!.AddNode(pos, node);
				}
				else if (!ReferenceEquals(myPipeNet, pipeNet))
				{
					myPipeNet.UniteNetworks(pipeNet);
				}
			}
		}
		if (myPipeNet == null)
		{
			myPipeNet = CreateNetInstance();
			AsBaseNet(myPipeNet)!.AddNode(pos, node);
			AddPipeNet(myPipeNet);
			SetDirty();
		}
	}

	public void AddPipeNetToChunk((int cx, int cy) chunkPos, PipeNet<TData> pipeNet)
	{
		if (!PipeNetsByChunk.TryGetValue(chunkPos, out var list))
			PipeNetsByChunk[chunkPos] = list = new List<TNet>();
		list.Add((TNet)pipeNet);
	}

	public void RemovePipeNetFromChunk((int cx, int cy) chunkPos, PipeNet<TData> pipeNet)
	{
		if (PipeNetsByChunk.TryGetValue(chunkPos, out var list))
		{
			list.Remove((TNet)pipeNet);
			if (list.Count == 0) PipeNetsByChunk.Remove(chunkPos);
		}
	}

	public void RemoveNode((int x, int y) pos)
	{
		var pipeNet = GetNetFromPos(pos);
		AsBaseNet(pipeNet)?.RemoveNode(pos);
	}

	public void UpdateBlockedConnections((int x, int y) pos, CoverSide side, bool isBlocked)
	{
		var pipeNet = GetNetFromPos(pos);
		if (pipeNet == null) return;
		AsBaseNet(pipeNet)!.UpdateBlockedConnections(pos, side, isBlocked);
		AsBaseNet(pipeNet)!.OnPipeConnectionsUpdate();
	}

	public void UpdateData((int x, int y) pos, TData data)
	{
		var pipeNet = GetNetFromPos(pos);
		AsBaseNet(pipeNet)?.UpdateNodeData(pos, data);
	}

	public void UpdateMark((int x, int y) pos, int newMark)
	{
		var pipeNet = GetNetFromPos(pos);
		AsBaseNet(pipeNet)?.UpdateMark(pos, newMark);
	}

	public TNet? GetNetFromPos((int x, int y) pos)
	{
		if (!PipeNetsByChunk.TryGetValue(ToChunkPos(pos), out var list)) return null;
		foreach (var pn in list)
			if (pn.ContainsNode(pos)) return pn;
		return null;
	}

	PipeNet<TData>? ILevelPipeNet<TData>.GetNetFromPos((int x, int y) pos) => GetNetFromPos(pos);

	public IReadOnlyList<TNet> AllPipeNets => PipeNets;

	public void AddPipeNet(PipeNet<TData> pipeNet) => AddPipeNetSilently((TNet)pipeNet);

	internal void AddPipeNetSilently(TNet pipeNet)
	{
		PipeNets.Add(pipeNet);
		foreach (var chunkPos in pipeNet.ContainedChunks)
			AddPipeNetToChunk(chunkPos, pipeNet);
		pipeNet.IsValidInternal = true;
	}

	public void RemovePipeNet(PipeNet<TData> pipeNet)
	{
		PipeNets.Remove((TNet)pipeNet);
		foreach (var chunkPos in pipeNet.ContainedChunks)
			RemovePipeNetFromChunk(chunkPos, pipeNet);
		pipeNet.IsValidInternal = false;
		SetDirty();
	}

	PipeNet<TData> ILevelPipeNet<TData>.CreateNetInstance() => CreateNetInstance();

	protected internal abstract TNet CreateNetInstance();

	public TagCompound Save()
	{
		var compound = new TagCompound();
		var allPipeNets = new List<TagCompound>(PipeNets.Count);
		foreach (var pipeNet in PipeNets)
			allPipeNets.Add(pipeNet.SerializeNBT());
		compound["PipeNets"] = allPipeNets;
		_dirty = false;
		return compound;
	}

	public void Load(TagCompound tag)
	{
		PipeNets.Clear();
		PipeNetsByChunk.Clear();
		if (!tag.ContainsKey("PipeNets")) return;
		var allPipeNets = tag.GetList<TagCompound>("PipeNets");
		foreach (var pNetTag in allPipeNets)
		{
			var pipeNet = CreateNetInstance();
			pipeNet.DeserializeNBT(pNetTag);
			AddPipeNetSilently(pipeNet);
		}
		Init();
	}

	private static PipeNet<TData>? AsBaseNet(TNet? n) => n;

	private static (int x, int y) Offset((int x, int y) pos, CoverSide side)
	{
		var (dx, dy) = CoverSides.Offset(side);
		return PipePassthrough.EffectiveNeighbor(pos.x, pos.y, dx, dy);
	}

	private static (int cx, int cy) ToChunkPos((int x, int y) pos) => (pos.x >> 4, pos.y >> 4);
}
