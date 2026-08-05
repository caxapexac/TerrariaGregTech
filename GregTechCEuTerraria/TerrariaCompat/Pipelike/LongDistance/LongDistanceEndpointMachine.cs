#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Capabilities.Handlers;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.LongDistance;

public abstract class LongDistanceEndpointMachine : MetaMachine, ILDEndpoint
{
	protected LongDistanceEndpointMachine() { }
	protected LongDistanceEndpointMachine(VoltageTier tier) : base(tier) { }

	public abstract LongDistancePipeType PipeType { get; }

	private IO _ioType = IO.NONE;
	public IO IoType { get => _ioType; set => _ioType = value; }

	public (int x, int y) EndpointPos => (Position.X, Position.Y);

	private ILDEndpoint? _link;
	private bool _linkResolved;
	private bool _removed;
	public bool IsRemoved => _removed;

	public IODirection PipeSide
	{
		get
		{
			IODirection found = IODirection.None;
			foreach (var (side, x, y) in WorldCapability.Perimeter(this))
			{
				var cell = LongDistancePipeLayerSystem.Pipes.CellAt(x, y);
				if (cell is null || cell.Value.Type != PipeType) continue;
				if (found == IODirection.None) found = side;
				else if (found != side) return IODirection.None; // >1 distinct side
			}
			return found;
		}
	}

	public LongDistancePipeNet? AttachedNet
	{
		get
		{
			var side = PipeSide;
			if (side == IODirection.None) return null;
			foreach (var (s, x, y) in WorldCapability.Perimeter(this))
			{
				if (s != side) continue;
				if (!LongDistancePipeLayerSystem.Pipes.Has(x, y)) continue;
				var net = LongDistancePipeNetSystem.Level.GetNetFromPos((x, y));
				if (net is not null) return net;
			}
			return null;
		}
	}

	public ILDEndpoint? GetLink()
	{
		if (!_linkResolved)
		{
			_link = LongDistanceEndpointRegistry.ResolveLink(this);
			_linkResolved = true;
		}
		return _link;
	}

	public void InvalidateLink()
	{
		_link = null;
		_linkResolved = false;
	}

	private static System.Collections.Generic.IEnumerable<(int x, int y, IODirection arrival)>
		LinkDestCells(ILDEndpoint link)
	{
		if (link is not MetaMachine linkMachine) yield break;
		foreach (var (side, x, y) in WorldCapability.Perimeter(linkMachine))
		{
			if (side == IODirection.None) continue;
			yield return (x, y, side.Opposite());
		}
	}

	public override IItemHandler? GetItemHandlerCap(IODirection side, bool useCoverCapability = true)
	{
		if (PipeType != LongDistancePipeType.Item || IsClient || IoType != IO.IN) return null;
		if (side == IODirection.None) return null;
		var link = GetLink();
		if (link is null) return null;
		foreach (var (x, y, arrival) in LinkDestCells(link))
		{
			var handler = WorldCapability.ItemHandlerAt(x, y, arrival);
			if (handler is not null) return new InsertOnlyItemHandler(handler);
		}
		return null;
	}

	public override IFluidHandler? GetFluidHandlerCap(IODirection side, bool useCoverCapability = true)
	{
		if (PipeType != LongDistancePipeType.Fluid || IsClient || IoType != IO.IN) return null;
		if (side == IODirection.None) return null;
		var link = GetLink();
		if (link is null) return null;
		foreach (var (x, y, arrival) in LinkDestCells(link))
		{
			var handler = WorldCapability.FluidHandlerAt(x, y, arrival);
			if (handler is not null) return new FillOnlyFluidHandler(handler);
		}
		return null;
	}

	protected override void OnMachineLoaded() => LongDistanceEndpointRegistry.Register(this);

	public override void OnKill()
	{
		_removed = true;
		LongDistanceEndpointRegistry.Unregister(this);
		base.OnKill();
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		string role = IoType switch
		{
			IO.IN  => "[c/AAFFAA:Input - feeds the network]",
			IO.OUT => "[c/AAFFAA:Output - receives from the network]",
			_      => "[c/FFAA44:Unset - screwdriver to set Input/Output]",
		};
		lines.Add(role);
		if (PipeSide == IODirection.None)
			lines.Add("[c/FF6666:Not touching a single long-distance pipe]");
		else if (IsServer && IoType is IO.IN or IO.OUT && GetLink() is null)
			lines.Add("[c/FFAA44:No partner endpoint (need one far Input + one far Output)]");
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["ldIo"] = (byte)_ioType;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("ldIo")) _ioType = (IO)tag.GetByte("ldIo");
	}
}

public sealed class LDItemEndpointMachine : LongDistanceEndpointMachine
{
	public LDItemEndpointMachine() { }
	public LDItemEndpointMachine(VoltageTier tier) : base(tier) { }
	protected override string Label => "Long Distance Item Pipeline Endpoint";
	public override LongDistancePipeType PipeType => LongDistancePipeType.Item;
}

public sealed class LDFluidEndpointMachine : LongDistanceEndpointMachine
{
	public LDFluidEndpointMachine() { }
	public LDFluidEndpointMachine(VoltageTier tier) : base(tier) { }
	protected override string Label => "Long Distance Fluid Pipeline Endpoint";
	public override LongDistancePipeType PipeType => LongDistancePipeType.Fluid;
}
