#nullable enable
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Api.Pipenet;
using System.Collections.Generic;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;

// BFS-flood connected components
public static class EnergyNetGraph
{
	public static List<NetworkComponent> Build(CableLayer layer)
	{
		var components = new List<NetworkComponent>();
		if (layer.Count == 0) return components;

		var visited = new HashSet<(int x, int y)>();
		foreach (var kv in layer.All)
		{
			if (visited.Contains(kv.Key)) continue;
			var comp = Flood(layer, kv.Key, visited);
			components.Add(comp);
		}
		return components;
	}

	private static NetworkComponent Flood(CableLayer layer, (int x, int y) start, HashSet<(int x, int y)> visited)
	{
		var cells = new Dictionary<(int x, int y), CableCell>();
		var queue = new Queue<(int x, int y)>();
		queue.Enqueue(start);
		visited.Add(start);

		VoltageTier minTier = VoltageTier.MAX;
		int maxAmperage = 0;
		int maxLossPerAmp = 0;
		bool seenAny = false;

		while (queue.Count > 0)
		{
			var pos = queue.Dequeue();
			var cell = layer.CellAt(pos.x, pos.y);
			if (cell is null) continue;
			cells[pos] = cell.Value;

			if (!seenAny || (int)cell.Value.Voltage < (int)minTier) minTier = cell.Value.Voltage;
			if (cell.Value.TotalAmperage > maxAmperage)              maxAmperage = cell.Value.TotalAmperage;
			if (cell.Value.LossPerAmp > maxLossPerAmp)               maxLossPerAmp = cell.Value.LossPerAmp;
			seenAny = true;

			Enqueue(layer, queue, visited, pos, pos.x, pos.y - 1);
			Enqueue(layer, queue, visited, pos, pos.x, pos.y + 1);
			Enqueue(layer, queue, visited, pos, pos.x - 1, pos.y);
			Enqueue(layer, queue, visited, pos, pos.x + 1, pos.y);
		}

		return new NetworkComponent(cells, minTier, maxAmperage, maxLossPerAmp);
	}

	private static void Enqueue(CableLayer layer, Queue<(int, int)> q, HashSet<(int, int)> visited,
		(int x, int y) from, int x, int y)
	{
		var (tx, ty) = PipePassthrough.EffectiveNeighbor(from.x, from.y, x - from.x, y - from.y);
		var key = (tx, ty);
		if (visited.Contains(key)) return;
		if (!layer.Connects(from.x, from.y, tx, ty)) return;
		visited.Add(key);
		q.Enqueue(key);
	}
}

public sealed record NetworkComponent(
	IReadOnlyDictionary<(int x, int y), CableCell> Cells,
	VoltageTier EffectiveTier,
	int MaxAmperage,
	int MaxLossPerAmp);
