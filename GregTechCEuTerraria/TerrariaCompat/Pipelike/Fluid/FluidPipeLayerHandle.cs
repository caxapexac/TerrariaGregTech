#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Items.Pipes;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;

public sealed class FluidPipeLayerHandle : IGridLayerHandle
{
	public static readonly FluidPipeLayerHandle Instance = new();
	private FluidPipeLayerHandle() { }

	public bool Has(int x, int y) => FluidPipeLayerSystem.Pipes.Has(x, y);

	public bool TryPlace(FluidPipeCell cell, int x, int y, Player placer, bool refundOverwrite = true)
	{
		if (!FluidPipeLayerSystem.Pipes.CanPlaceAt(x, y)) return false;
		var existing = FluidPipeLayerSystem.Pipes.CellAt(x, y);
		if (existing.HasValue && existing.Value.Equals(cell)) return false;
		if (existing.HasValue && refundOverwrite) RefundAt(placer, existing.Value);
		FluidPipeLayerSystem.Pipes.Set(x, y, cell);
		FluidPipeLayerSystem.EnsureSides(x, y);
		FluidPipeNetSystem.OnPipeAdded(x, y, cell);
		FluidPipeLayerSystem.EnsureState(x, y);
		PipePackets.SendPlacedFluid(x, y, cell);
		NotifyAdjacentCoversNeighborChanged(x, y);
		return true;
	}

	public bool CutAt(int x, int y, Player remover) => CutAt(x, y, remover, true);

	public bool CutAt(int x, int y, Player remover, bool refund)
	{
		var existing = FluidPipeLayerSystem.Pipes.CellAt(x, y);
		if (existing is null) return false;
		FluidPipeLayerSystem.Pipes.Remove(x, y);
		FluidPipeLayerSystem.DropSides(x, y);
		FluidPipeNetSystem.OnPipeRemoved(x, y);
		PipePackets.SendRemove(x, y, PipeKind.Fluid);
		if (refund) RefundAt(remover, existing.Value);
		NotifyAdjacentCoversNeighborChanged(x, y);
		return true;
	}

	internal static void NotifyAdjacentCoversNeighborChanged(int x, int y)
	{
		PipeRenderer.InvalidateGeom(x, y);
		foreach (var (_, dx, dy) in IODirectionExtensions.Cardinal4)
		{
			int nx = x + dx, ny = y + dy;
			var pcv = FluidPipeLayerSystem.GetSides(nx, ny);
			if (pcv is null) continue;
			((Api.Cover.ICoverable)pcv).OnCoversNeighborChanged();
		}
	}

	private static void RefundAt(Player player, FluidPipeCell cell)
	{
		int type;
		if (cell.IsSimple)
		{
			string simpleId = cell.Size == PipeSize.Normal
				? "simple_fluid_pipe"
				: "simple_fluid_pipe_" + PipeSizes.Word(cell.Size);
			if (!ModContent.GetInstance<GregTechCEuTerraria>().TryFind<ModItem>(simpleId, out var mi))
				return;
			type = mi.Type;
		}
		else
		{
			string id = cell.MaterialId + "_" + PipeSizes.Word(cell.Size) + "_fluid_pipe";
			int? t = PipeItemRegistry.Get(id);
			if (t is null) return;
			type = t.Value;
		}
		global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(player, player.GetSource_Misc("PipeRemove"), type, 1);
	}
}
