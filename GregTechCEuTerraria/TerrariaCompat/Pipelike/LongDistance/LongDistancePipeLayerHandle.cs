#nullable enable
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.LongDistance;

public sealed class LongDistancePipeLayerHandle : IGridLayerHandle
{
	public static readonly LongDistancePipeLayerHandle Item  =
		new(LongDistancePipeType.Item,  "long_distance_item_pipeline");
	public static readonly LongDistancePipeLayerHandle Fluid =
		new(LongDistancePipeType.Fluid, "long_distance_fluid_pipeline");

	private readonly LongDistancePipeType _type;
	private readonly string _itemName;

	private LongDistancePipeLayerHandle(LongDistancePipeType type, string itemName)
	{
		_type = type;
		_itemName = itemName;
	}

	public bool Has(int x, int y) => LongDistancePipeLayerSystem.Pipes.Has(x, y);

	public bool TryPlace(int x, int y, Player placer)
	{
		if (!LongDistancePipeLayerSystem.Pipes.CanPlaceAt(x, y)) return false;
		if (LongDistancePipeLayerSystem.Pipes.Has(x, y)) return false;
		LongDistancePipeLayerSystem.Pipes.Set(x, y, new LongDistancePipeCell(_type));
		LongDistancePipeNetSystem.OnPipeAdded(x, y);
		PipePackets.SendPlacedLongDistance(x, y, _type);
		return true;
	}

	public bool CutAt(int x, int y, Player remover)
	{
		var cell = LongDistancePipeLayerSystem.Pipes.CellAt(x, y);
		if (cell is null) return false;
		LongDistancePipeLayerSystem.Pipes.Remove(x, y);
		LongDistancePipeNetSystem.OnPipeRemoved(x, y);
		PipePackets.SendRemove(x, y, PipeKind.LongDistance);
		string itemName = cell.Value.Type == LongDistancePipeType.Fluid
			? "long_distance_fluid_pipeline" : "long_distance_item_pipeline";
		if (ModContent.GetInstance<GregTechCEuTerraria>().TryFind<ModItem>(itemName, out var mi))
			global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(remover, remover.GetSource_Misc("PipeRemove"), mi.Type, 1);
		return true;
	}
}
