#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait.Multiblock;
using GregTechCEuTerraria.Api.Pattern;
using GregTechCEuTerraria.Api.Pattern.Error;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Tiles.Casings;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

public abstract class MultiblockControllerMachine : MetaMachine
{
	private MultiblockState? _multiblockState;
	private readonly List<IMultiPart> _parts = new();

	public Point16[] PartPositions { get; private set; } = Array.Empty<Point16>();

	public bool IsFormed { get; protected set; }

	private static readonly Dictionary<string, ushort> _fusedCasingCache = new();
	public virtual ushort FusedCasingTileType
	{
		get
		{
			var name = Definition?.FusedCasingTileName;
			if (string.IsNullOrEmpty(name)) return 0;
			if (_fusedCasingCache.TryGetValue(name, out var cached)) return cached;
			var mod = Terraria.ModLoader.ModLoader.GetMod("GregTechCEuTerraria");
			ushort id = mod.TryFind<Terraria.ModLoader.ModTile>(name, out var t) ? (ushort)t.Type : (ushort)0;
			_fusedCasingCache[name] = id;
			return id;
		}
	}

	public virtual string? FusedCasingTexture
	{
		get
		{
			var explicitPath = Definition?.FusedCasingTexturePath;
			if (!string.IsNullOrEmpty(explicitPath)) return explicitPath;
			ushort type = FusedCasingTileType;
			if (type == 0) return null;
			return TileLoader.GetTile(type) is Tiles.Casings.CasingTile casing
				? casing.BlockTexture : null;
		}
	}

	public bool IsFlipped { get; protected set; }

	private readonly object _patternLock = new();

	private readonly List<Point16> _activeCells = new();

	private ParallelHatchPartMachine? _parallelHatch;
	public ParallelHatchPartMachine? GetParallelHatch() => _parallelHatch;

	private int _offset = -1;

	private bool _structureNeighborDirty;
	private HashSet<(int x, int y)>? _footprintCells;
	private static readonly HashSet<MultiblockControllerMachine> _formedControllers = new();

	internal static void MarkStructureNeighborChanged(int x, int y)
	{
		foreach (var c in _formedControllers)
			if (c._footprintCells is { } cells && cells.Contains((x, y)))
				c._structureNeighborDirty = true;
	}

	internal static void ClearFootprintRegistry() => _formedControllers.Clear();

	private string?       _persistedUnformedReason;
	private int _persistedUnformedX = int.MinValue;
	private int _persistedUnformedY = int.MinValue;
	private int[]? _persistedSwapTypes;

	protected MultiblockControllerMachine() : base() { }

	public virtual void OnStructureFormed()
	{
		bool wasFormed = IsFormed;
		IsFormed = true;

		_parts.Clear();
		var set = GetMultiblockState().MatchContext
			.GetOrDefault("parts", (HashSet<IMultiPart>?)null) ?? new HashSet<IMultiPart>();
		foreach (var part in set)
		{
			if (ShouldAddPartToController(part))
				_parts.Add(part);
		}
		_parts.Sort(GetPartSorter());
		UpdatePartPositions();
		_parallelHatch = null;
		foreach (var part in _parts)
		{
			if (_parallelHatch == null && part is ParallelHatchPartMachine ph)
				_parallelHatch = ph;
			part.AddedToController(this);
		}
		UpdatePartPositions();
		foreach (var trait in Traits.AllTraits)
			if (trait is MultiblockMachineTrait mmt) mmt.OnStructureFormed();

		if (IsServer)
		{
			var cells = _footprintCells ??= new HashSet<(int x, int y)>();
			cells.Clear();
			foreach (var (cx, cy) in GetMultiblockState().GetCache())
			{
				cells.Add((cx, cy));
				cells.Add((cx + 1, cy));
				cells.Add((cx, cy + 1));
				cells.Add((cx + 1, cy + 1));
			}
			_formedControllers.Add(this);
			_structureNeighborDirty = false;
		}

		if (IsServer && !wasFormed)
			MultiblockFormedPacket.SendBroadcast(Position.X, Position.Y, true, IsFlipped);
	}

	public override void OnKill()
	{
		if (IsServer && IsFormed) OnStructureInvalid();
		if (IsServer) _formedControllers.Remove(this);
		base.OnKill();
	}

	public virtual void OnStructureInvalid()
	{
		bool wasFormed = IsFormed;
		IsFormed = false;
		_parallelHatch = null;

		if (IsServer)
		{
			_formedControllers.Remove(this);
			_footprintCells?.Clear();
			_structureNeighborDirty = false;
		}

		foreach (var part in _parts)
			part.RemovedFromController(this);
		_parts.Clear();
		UpdatePartPositions();
		_activeCells.Clear();
		foreach (var trait in Traits.AllTraits)
			if (trait is MultiblockMachineTrait mmt) mmt.OnStructureInvalid();

		if (IsServer && wasFormed)
			MultiblockFormedPacket.SendBroadcast(Position.X, Position.Y, false, IsFlipped);
	}

	public void ApplyClientFormedSync(bool isFormed, bool isFlipped)
	{
		IsFormed = isFormed;
		IsFlipped = isFlipped;
		if (isFormed) GetMultiblockState().SetError(null);
	}

	public virtual void OnPartUnload()
	{
		_parts.RemoveAll(part => part.Self() is null);
		GetMultiblockState().SetError(MultiblockState.UNLOAD_ERROR);
		UpdatePartPositions();
	}

	public MultiblockState GetMultiblockState()
	{
		if (_multiblockState is null)
			_multiblockState = new MultiblockState(Position.X, Position.Y);
		return _multiblockState;
	}

	public virtual string? GetUnformedReason()
	{
		var liveErr = GetMultiblockState().Error;
		if (liveErr is not null && liveErr != MultiblockState.UNINIT_ERROR)
			return MultiblockErrorText.Describe(liveErr);
		return _persistedUnformedReason;
	}

	public virtual IReadOnlyList<int>? GetSwapCandidateTypes()
	{
		if (GetMultiblockState().Error is SinglePredicateError spe
			&& (spe.Type == 1 || spe.Type == 3))
		{
			var live = CandidateTypes(spe);
			if (live is { Length: > 0 }) return live;
		}
		return _persistedSwapTypes;
	}

	private static int[]? CandidateTypes(SinglePredicateError spe)
	{
		var cand = spe.Predicate.GetCandidates();
		if (cand.Count == 0) return null;
		var types = new int[cand.Count];
		for (int i = 0; i < cand.Count; i++) types[i] = cand[i].type;
		return types;
	}

	public virtual (int X, int Y)? GetUnformedErrorCell()
	{
		var liveErr = GetMultiblockState().Error;
		if (liveErr is not null && liveErr.GetType() == typeof(PatternError))
			return (liveErr.GetX(), liveErr.GetY());
		if (_persistedUnformedX != int.MinValue)
			return (_persistedUnformedX, _persistedUnformedY);
		return null;
	}

	protected void SetUnformedReason(string reason)
	{
		_persistedUnformedReason = reason;
	}

	public virtual Comparison<IMultiPart> GetPartSorter() =>
		(a, b) =>
		{
			var ap = a.Self().Position;
			var bp = b.Self().Position;
			long ak = ((long)(uint)(ushort)ap.Y << 32) | (uint)(ushort)ap.X;
			long bk = ((long)(uint)(ushort)bp.Y << 32) | (uint)(ushort)bp.X;
			return ak.CompareTo(bk);
		};

	public bool TryGetPreviewCell(int tileX, int tileY, out char ch,
		out TraceabilityPredicate predicate)
	{
		ch = ' ';
		predicate = null!;
		if (IsFormed) return false;
		var pattern = GetPattern();
		if (pattern is null) return false;
		var preview = pattern.GetPreviewPattern();

		int originX = Position.X - preview.ControllerCol * 2;
		int originY = Position.Y - preview.ControllerRow * 2;
		int col = (tileX - originX) / 2;
		int row = (tileY - originY) / 2;
		if (row < 0 || row >= preview.Height) return false;
		if (col < 0 || col >= preview.Width)  return false;
		if (tileX < originX || tileY < originY) return false;

		ch = preview.Shape[row][col];
		if (!preview.Predicates.TryGetValue(ch, out predicate!)) return false;
		return true;
	}

	public override void AppendTooltip(System.Collections.Generic.List<string> lines)
	{
		base.AppendTooltip(lines);
		if (!IsFormed)
			AppendUnformedStructureBlock(lines);
	}

	protected void AppendUnformedStructureBlock(System.Collections.Generic.List<string> lines) =>
		lines.Add(RecipeStatusText.StatusLineForMulti(this, null));

	public IReadOnlyList<IMultiPart> GetParts()
	{
		if (_parts.Count != PartPositions.Length)
		{
			_parts.Clear();
			foreach (var pos in PartPositions)
			{
				if (MetaMachine.GetMachineAt(pos.X, pos.Y) is IMultiPart part)
					_parts.Add(part);
			}
		}
		return _parts;
	}

	public virtual bool IsBatchEnabled() => false;
	public virtual void SetBatchEnabled(bool batch) { }

	public void SetFlipped(bool flipped) => IsFlipped = flipped;

	protected void UpdatePartPositions()
	{
		PartPositions = _parts.Count == 0
			? Array.Empty<Point16>()
			: _parts.Select(part => part.Self().Position).ToArray();
	}

	public virtual bool ShouldAddPartToController(IMultiPart part) => true;

	public virtual bool AllowFlip() => Definition?.AllowFlip ?? false;

	public virtual bool AllowCircuitSlots() => true;

	private IBlockPattern? _cachedPattern;
	public virtual IBlockPattern? GetPattern() => _cachedPattern ??= Definition?.PatternFactory?.Invoke();

	public virtual bool CheckPattern()
	{
		var pattern = GetPattern();
		return pattern != null && pattern.CheckPatternAt(GetMultiblockState(), savePredicate: false);
	}

	public bool CheckPatternWithLock()
	{
		lock (_patternLock)
			return CheckPattern();
	}

	public bool CheckPatternWithTryLock()
	{
		if (Monitor.TryEnter(_patternLock))
		{
			try { return CheckPattern(); }
			finally { Monitor.Exit(_patternLock); }
		}
		return false;
	}

	public void AsyncCheckPattern(long periodID)
	{
		if (_offset < 0)
			_offset = (int)(((Position.X * 13L + Position.Y * 7L) & 0x3FF) % 4);

		bool unformed = GetMultiblockState().HasError() || !IsFormed;
		if (unformed)
		{
			if ((_offset + periodID) % 4 != 0) return;
		}
		else
		{
			if (!_structureNeighborDirty) return;
		}
		_structureNeighborDirty = false;

		bool ok = CheckPatternWithTryLock();
		if (ok)
		{
			_persistedUnformedReason  = null;
			_persistedUnformedX = int.MinValue;
			_persistedUnformedY = int.MinValue;
			_persistedSwapTypes = null;
		}
		else
		{
			var liveErr = GetMultiblockState().Error;
			if (liveErr is not null && liveErr != MultiblockState.UNINIT_ERROR)
			{
				_persistedUnformedReason  = MultiblockErrorText.Describe(liveErr);
				if (liveErr.GetType() == typeof(PatternError))
				{
					_persistedUnformedX = liveErr.GetX();
					_persistedUnformedY = liveErr.GetY();
				}
				else
				{
					_persistedUnformedX = int.MinValue;
					_persistedUnformedY = int.MinValue;
				}
				_persistedSwapTypes = liveErr is SinglePredicateError spe
					&& (spe.Type == 1 || spe.Type == 3)
					? CandidateTypes(spe)
					: null;
			}
		}

		if (ok)
		{
			SetFlipped(GetMultiblockState().NeededFlip);
			OnStructureFormed();
		}
		else if (IsFormed)
		{
			OnStructureInvalid();
		}
	}

	protected override void OnTick()
	{
		base.OnTick();
		AsyncCheckPattern((long)Terraria.Main.GameUpdateCount);
	}

	public bool ShouldGlow => IsFormed && IsActive;
	public IReadOnlyList<Point16> ActiveCells => _activeCells;

	internal void RefreshActiveCells()
	{
		_activeCells.Clear();
		if (!ShouldGlow) return;
		foreach (var (cx, cy) in GetMultiblockState().GetCache())
			_activeCells.Add(new Point16(cx, cy));
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["mb_formed"]  = IsFormed;
		tag["mb_flipped"] = IsFlipped;
		if (_persistedUnformedReason is not null)
			tag["mb_unformed_reason"] = _persistedUnformedReason;
		if (_persistedUnformedX != int.MinValue)
		{
			tag["mb_unformed_x"] = _persistedUnformedX;
			tag["mb_unformed_y"] = _persistedUnformedY;
		}
		if (_persistedSwapTypes is { Length: > 0 })
			tag["mb_swap_types"] = _persistedSwapTypes.ToList();
		if (PartPositions.Length > 0)
		{
			var px = new int[PartPositions.Length];
			var py = new int[PartPositions.Length];
			for (int i = 0; i < PartPositions.Length; i++)
			{
				px[i] = PartPositions[i].X;
				py[i] = PartPositions[i].Y;
			}
			tag["mb_part_x"] = px;
			tag["mb_part_y"] = py;
		}
		if (IsServer) RefreshActiveCells();
		if (_activeCells.Count > 0)
		{
			var ax = new int[_activeCells.Count];
			var ay = new int[_activeCells.Count];
			for (int i = 0; i < _activeCells.Count; i++)
			{
				ax[i] = _activeCells[i].X;
				ay[i] = _activeCells[i].Y;
			}
			tag["mb_active_x"] = ax;
			tag["mb_active_y"] = ay;
		}
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("mb_formed"))  IsFormed  = tag.GetBool("mb_formed");
		if (tag.ContainsKey("mb_flipped")) IsFlipped = tag.GetBool("mb_flipped");
		_persistedUnformedReason  = tag.ContainsKey("mb_unformed_reason")  ? tag.GetString("mb_unformed_reason") : null;
		_persistedUnformedX = tag.ContainsKey("mb_unformed_x") ? tag.GetInt("mb_unformed_x") : int.MinValue;
		_persistedUnformedY = tag.ContainsKey("mb_unformed_y") ? tag.GetInt("mb_unformed_y") : int.MinValue;
		_persistedSwapTypes = tag.ContainsKey("mb_swap_types") ? tag.GetList<int>("mb_swap_types").ToArray() : null;
		if (tag.ContainsKey("mb_part_x") && tag.ContainsKey("mb_part_y"))
		{
			var px = tag.GetIntArray("mb_part_x");
			var py = tag.GetIntArray("mb_part_y");
			int n = System.Math.Min(px.Length, py.Length);
			var pos = new Point16[n];
			for (int i = 0; i < n; i++) pos[i] = new Point16(px[i], py[i]);
			PartPositions = pos;
			_parts.Clear();
		}
		_activeCells.Clear();
		if (tag.ContainsKey("mb_active_x") && tag.ContainsKey("mb_active_y"))
		{
			var ax = tag.GetIntArray("mb_active_x");
			var ay = tag.GetIntArray("mb_active_y");
			int an = System.Math.Min(ax.Length, ay.Length);
			for (int i = 0; i < an; i++) _activeCells.Add(new Point16(ax[i], ay[i]));
		}
	}
}
