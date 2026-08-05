#nullable enable
using System.Collections.Generic;

namespace GregTechCEuTerraria.Api.Pipenet;

public abstract class GridLayer<TCell> where TCell : struct
{
	private readonly Dictionary<(int x, int y), TCell> _cells = new();

	public int Count => _cells.Count;
	public IReadOnlyDictionary<(int x, int y), TCell> All => _cells;

	public bool IsDirty { get; private set; }
	public void ClearDirty() => IsDirty = false;
	public void MarkDirty() => IsDirty = true;

	protected virtual bool SupportsCrossover => false;

	public bool CanPlaceAt(int x, int y) => !SupportsCrossover || !PipePassthrough.IsCrossover(x, y);

	public bool Has(int x, int y) => _cells.ContainsKey((x, y));

	public TCell? CellAt(int x, int y) =>
		_cells.TryGetValue((x, y), out var c) ? c : null;

	public void Set(int x, int y, TCell cell)
	{
		if (_cells.TryGetValue((x, y), out var existing) &&
			EqualityComparer<TCell>.Default.Equals(existing, cell)) return;
		lock (SaveTickGate.Lock)
			_cells[(x, y)] = cell;
		IsDirty = true;
	}

	public bool Remove(int x, int y)
	{
		bool removed;
		lock (SaveTickGate.Lock)
			removed = _cells.Remove((x, y));
		if (removed) IsDirty = true;
		return removed;
	}

	public void Clear()
	{
		if (_cells.Count == 0) return;
		lock (SaveTickGate.Lock)
			_cells.Clear();
		IsDirty = true;
	}

	public abstract bool Connects(int x1, int y1, int x2, int y2);

	public int ConnectionMask(int x, int y)
	{
		int mask = 0;
		if (!SupportsCrossover)
		{
			if (Connects(x, y, x, y - 1)) mask |= 1;
			if (Connects(x, y, x, y + 1)) mask |= 2;
			if (Connects(x, y, x - 1, y)) mask |= 4;
			if (Connects(x, y, x + 1, y)) mask |= 8;
			return mask;
		}
		var n = PipePassthrough.EffectiveNeighbor(x, y, 0, -1); if (Connects(x, y, n.x, n.y)) mask |= 1;
		var s = PipePassthrough.EffectiveNeighbor(x, y, 0,  1); if (Connects(x, y, s.x, s.y)) mask |= 2;
		var w = PipePassthrough.EffectiveNeighbor(x, y, -1, 0); if (Connects(x, y, w.x, w.y)) mask |= 4;
		var e = PipePassthrough.EffectiveNeighbor(x, y,  1, 0); if (Connects(x, y, e.x, e.y)) mask |= 8;
		return mask;
	}
}
