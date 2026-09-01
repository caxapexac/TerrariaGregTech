#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.Api.Data.Chemical.Material.Properties;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Fluids.Attribute;
using GregTechCEuTerraria.Api.Transfer;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;

public class FluidPipeState : IFluidPipeHost
{
	public const int FREQUENCY = 5;

	public int X { get; }
	public int Y { get; }

	public byte LastReceivedFrom    = 0;
	public byte OldLastReceivedFrom = 0;

	private PipeTankList? _pipeTankList;
	private readonly Dictionary<CoverSide, PipeTankList> _tankLists = new();
	private CustomFluidTank[]? _fluidTanks;
	private long _timer = 0L;
	private readonly int _offset;

	private readonly FluidPipeProperties _nodeData;

	public FluidPipeState(int x, int y, FluidPipeProperties nodeData)
	{
		X = x;
		Y = y;
		_nodeData = nodeData;
		_offset = new Random(unchecked(x * 73856093 ^ y * 19349663)).Next(20);
		CreateTanksList();
	}

	public FluidPipeProperties NodeData => _nodeData;

	public long GetOffsetTimer() => _timer + _offset;

	public int CapacityPerTank => _nodeData.Throughput * 20;

	public bool IsBlocked(CoverSide side)
	{
		var (dx, dy) = CoverSides.Offset(side);
		var (px, py) = Api.Pipenet.PipePassthrough.EffectiveNeighbor(X, Y, dx, dy);
		if (PipeNeighborProbe.IsConnectedPipe(X, Y, px, py, PipeKind.Fluid))
			return false;
		var pcv = FluidPipeLayerSystem.GetSides(X, Y);
		return pcv is null || pcv.GetMode(side) == PipeSideMode.Off;
	}

	public void ReceivedFrom(CoverSide side)
	{
		LastReceivedFrom |= (byte)(1 << (int)side);
	}

	public PipeTankList GetTankList()
	{
		if (_pipeTankList is null || _fluidTanks is null) CreateTanksList();
		return _pipeTankList!;
	}

	public PipeTankList GetTankList(CoverSide facing)
	{
		if (_tankLists.Count == 0 || _fluidTanks is null) CreateTanksList();
		return _tankLists.TryGetValue(facing, out var l) ? l : _pipeTankList!;
	}

	public CustomFluidTank[] GetFluidTanks()
	{
		if (_pipeTankList is null || _fluidTanks is null) CreateTanksList();
		return _fluidTanks!;
	}

	public FluidStack[] GetContainedFluids()
	{
		var tanks = GetFluidTanks();
		var fluids = new FluidStack[tanks.Length];
		for (int i = 0; i < fluids.Length; i++) fluids[i] = tanks[i].Fluid;
		return fluids;
	}

	public bool HasAnyFluid()
	{
		var tanks = GetFluidTanks();
		foreach (var t in tanks) if (!t.Fluid.IsEmpty) return true;
		return false;
	}

	public Terraria.ModLoader.IO.TagCompound SaveTo()
	{
		var tag = new Terraria.ModLoader.IO.TagCompound();
		var tanks = GetFluidTanks();
		var list = new System.Collections.Generic.List<Terraria.ModLoader.IO.TagCompound>(tanks.Length);
		foreach (var t in tanks) list.Add(t.SerializeNBT());
		tag["tanks"] = list;
		tag["lrf"]   = LastReceivedFrom;
		tag["olrf"]  = OldLastReceivedFrom;
		return tag;
	}

	public void LoadFrom(Terraria.ModLoader.IO.TagCompound tag)
	{
		var tanks = GetFluidTanks();
		if (tag.ContainsKey("tanks"))
		{
			var list = tag.GetList<Terraria.ModLoader.IO.TagCompound>("tanks");
			int n = Math.Min(list.Count, tanks.Length);
			for (int i = 0; i < n; i++) tanks[i].DeserializeNBT(list[i]);
		}
		if (tag.ContainsKey("lrf"))  LastReceivedFrom    = tag.GetByte("lrf");
		if (tag.ContainsKey("olrf")) OldLastReceivedFrom = tag.GetByte("olrf");
	}

	private void CreateTanksList()
	{
		int channels = Math.Max(1, _nodeData.Channels);
		_fluidTanks = new CustomFluidTank[channels];
		for (int i = 0; i < channels; i++)
			_fluidTanks[i] = new CustomFluidTank(CapacityPerTank);
		_pipeTankList = new PipeTankList(this, default, _fluidTanks);
		_tankLists.Clear();
		foreach (var side in CoverSides.All)
			_tankLists[side] = new PipeTankList(this, side, _fluidTanks);
	}

	public void Update()
	{
		_timer++;
		if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) return;
		if (GetOffsetTimer() % global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(FREQUENCY) != 0) return;

		LastReceivedFrom &= 15;
		if (LastReceivedFrom == 15)
		{
			LastReceivedFrom = 0;
		}

		bool shouldDistribute = (OldLastReceivedFrom == LastReceivedFrom);
		int tanks = _nodeData.Channels;
		int j = new Random(unchecked((int)(_timer ^ X ^ Y))).Next(Math.Max(1, tanks));
		for (int i = 0; i < tanks; i++)
		{
			int index = (i + j) % tanks;
			CustomFluidTank tank = _fluidTanks![index];
			FluidStack fluid = tank.Fluid;
			if (fluid.IsEmpty || fluid.Type is null)
				continue;
			if (fluid.Amount <= 0)
			{
				tank.SetFluid(FluidStack.Empty);
				continue;
			}

			if (shouldDistribute)
			{
				DistributeFluid(index, tank, fluid);
				LastReceivedFrom = 0;
			}
		}
		OldLastReceivedFrom = LastReceivedFrom;
	}

	private sealed class FluidTransaction
	{
		public readonly IFluidHandler Target;
		public readonly IFluidHandler PipeTank;
		public int Amount;
		public FluidTransaction(IFluidHandler target, IFluidHandler pipeTank, int amount)
		{
			Target = target; PipeTank = pipeTank; Amount = amount;
		}
	}

	private void DistributeFluid(int channel, CustomFluidTank tank, FluidStack fluid)
	{
		var transactions = new List<FluidTransaction>();
		int amount = fluid.Amount;

		FluidStack maxFluid = fluid;
		double availableCapacity = 0;

		int sideCount = CoverSides.Count;
		int sideStart = new Random(unchecked((int)(_timer * 31 ^ X * 17 ^ Y * 13))).Next(sideCount);

		for (int i = 0; i < sideCount; i++)
		{
			byte side = (byte)((i + sideStart) % sideCount);
			CoverSide facing = (CoverSide)side;

			if (!IsConnected(facing) || (LastReceivedFrom & (1 << side)) != 0)
				continue;

			var neighborHandler = GetNeighborFluidHandler(facing);
			if (neighborHandler is null) continue;

			IFluidHandler pipeTank = tank;
			var cover = FluidPipeLayerSystem.GetSides(X, Y)?.GetCoverAtSide(facing);

			if (cover is not null)
			{
				pipeTank = cover.GetFluidHandlerCap(pipeTank)!;
				if (pipeTank is null || CheckForPumpCover(cover)) continue;
			}

			FluidStack drainable = pipeTank.Drain(maxFluid, simulate: true);
			if (drainable.IsEmpty || drainable.Amount <= 0)
			{
				continue;
			}

			int filled = Math.Min(neighborHandler.Fill(maxFluid, simulate: true), drainable.Amount);

			if (filled > 0)
			{
				transactions.Add(new FluidTransaction(neighborHandler, pipeTank, filled));
				availableCapacity += filled;
			}
			maxFluid = maxFluid.WithAmount(amount);
		}

		if (availableCapacity <= 0) return;

		double maxAmount = Math.Min(CapacityPerTank / 2.0, fluid.Amount);

		foreach (var transaction in transactions)
		{
			if (availableCapacity > maxAmount)
			{
				transaction.Amount = (int)Math.Floor(transaction.Amount * maxAmount / availableCapacity);
			}
			if (transaction.Amount == 0)
			{
				if (tank.FluidAmount <= 0) break;
				transaction.Amount = 1;
			}
			else if (transaction.Amount < 0)
			{
				continue;
			}

			FluidStack toInsert = fluid.WithAmount(transaction.Amount);
			if (toInsert.IsEmpty || toInsert.Type is null) continue;

			int inserted = transaction.Target.Fill(toInsert, simulate: false);
			if (inserted > 0)
			{
				transaction.PipeTank.Drain(inserted, simulate: false);
			}
		}
	}

	private bool CheckForPumpCover(CoverBehavior? cover) => false;

	private bool IsConnected(CoverSide facing)
	{
		var kind = PipeNeighborProbe.ProbeAt(X, Y, facing, PipeKind.Fluid);
		if (kind == SideNeighbourKind.Pipe) return true;
		if (kind != SideNeighbourKind.Inventory) return false;
		return !IsBlocked(facing);
	}

	protected virtual IFluidHandler? GetNeighborFluidHandler(CoverSide facing)
	{
		var (kind, handler) = PipeNeighborProbe.ResolveFluid(X, Y, facing);
		if (kind == SideNeighbourKind.Pipe)
		{
			var (dx, dy) = CoverSides.Offset(facing);
			var (px, py) = Api.Pipenet.PipePassthrough.EffectiveNeighbor(X, Y, dx, dy);
			var neighborState = FluidPipeLayerSystem.EnsureState(px, py);
			return neighborState.GetTankList(CoverSides.Opposite(facing));
		}
		return handler;
	}


}
