#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike;

public enum SideNeighbourKind : byte { None, Pipe, Inventory }

public static class PipeNeighborProbe
{
	public static SideNeighbourKind[] Probe(int x, int y, PipeKind layer)
	{
		var result = new SideNeighbourKind[CoverSides.Count];
		foreach (var side in CoverSides.All)
			result[(int)side] = ProbeAt(x, y, side, layer);
		return result;
	}

	public static SideNeighbourKind ProbeAt(int x, int y, CoverSide side, PipeKind layer) => layer switch
	{
		PipeKind.Fluid => ResolveFluid(x, y, side).kind,
		PipeKind.Item  => ResolveItem (x, y, side).kind,
		_              => UnsupportedLayer(layer, nameof(ProbeAt), SideNeighbourKind.None),
	};

	public static (SideNeighbourKind kind, IFluidHandler? handler) ResolveFluid(
		int x, int y, CoverSide side)
	{
		var dir = ToIODirection(side);
		var (dx, dy) = dir.Offset();
		int nx = x + dx, ny = y + dy;

		var (px, py) = Api.Pipenet.PipePassthrough.EffectiveNeighbor(x, y, dx, dy);
		if (IsConnectedPipe(x, y, px, py, PipeKind.Fluid))
			return (SideNeighbourKind.Pipe, null);

		var face = dir.Opposite();
		if (MachineCellResolver.TryFindMachineAt(nx, ny, out var machine))
		{
			var h = machine.GetFluidHandlerCap(face);
			return h != null ? (SideNeighbourKind.Inventory, h) : (SideNeighbourKind.None, null);
		}
		// No vanilla fluid-container tiles
		return (SideNeighbourKind.None, null);
	}

	public static (SideNeighbourKind kind, IItemHandler? handler) ResolveItem(
		int x, int y, CoverSide side)
	{
		var dir = ToIODirection(side);
		var (dx, dy) = dir.Offset();
		int nx = x + dx, ny = y + dy;

		var (px, py) = Api.Pipenet.PipePassthrough.EffectiveNeighbor(x, y, dx, dy);
		if (IsConnectedPipe(x, y, px, py, PipeKind.Item))
			return (SideNeighbourKind.Pipe, null);

		var face = dir.Opposite();
		if (MachineCellResolver.TryFindMachineAt(nx, ny, out var machine))
		{
			var h = machine.GetItemHandlerCap(face);
			return h != null ? (SideNeighbourKind.Inventory, h) : (SideNeighbourKind.None, null);
		}
		var leaf = Capabilities.WorldCapability.LeafInventoryAt(nx, ny);
		return leaf != null ? (SideNeighbourKind.Inventory, leaf) : (SideNeighbourKind.None, null);
	}

	public static bool IsConnectedPipe(int x1, int y1, int x2, int y2, PipeKind layer) => layer switch
	{
		PipeKind.Fluid => FluidPipeLayerSystem.Pipes.Connects(x1, y1, x2, y2),
		PipeKind.Item  => ItemPipeLayerSystem .Pipes.Connects(x1, y1, x2, y2),
		_              => UnsupportedLayer(layer, nameof(IsConnectedPipe), false),
	};

	private static T UnsupportedLayer<T>(PipeKind layer, string caller, T fallback)
	{
		Terraria.ModLoader.ModContent.GetInstance<GregTechCEuTerraria>()?.Logger
			.Warn($"PipeNeighborProbe.{caller}: unsupported layer {layer} - only Item and Fluid are probeable.");
		return fallback;
	}

	public static bool[] ProbeItem(int x, int y)  => ProbeBool(x, y, PipeKind.Item);
	public static bool[] ProbeFluid(int x, int y) => ProbeBool(x, y, PipeKind.Fluid);

	private static bool[] ProbeBool(int x, int y, PipeKind layer)
	{
		var live = new bool[CoverSides.Count];
		foreach (var side in CoverSides.All)
			live[(int)side] = ProbeAt(x, y, side, layer) == SideNeighbourKind.Inventory;
		return live;
	}

	public static bool HasAnyLive(int x, int y, PipeKind layer)
	{
		foreach (var side in CoverSides.All)
			if (ProbeAt(x, y, side, layer) == SideNeighbourKind.Inventory) return true;
		return false;
	}

	private static IODirection ToIODirection(CoverSide side)
		=> Capabilities.WorldCapability.ToIODirection(side);
}
