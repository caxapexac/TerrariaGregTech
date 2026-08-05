#nullable enable
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Laser;

public sealed class LaserPipeLayerHandle : IGridLayerHandle
{
	public static readonly LaserPipeLayerHandle Instance = new();
	private LaserPipeLayerHandle() { }

	public bool Has(int x, int y) => LaserPipeLayerSystem.Pipes.Has(x, y);

	public bool TryPlace(int x, int y, Player placer)
	{
		if (!LaserPipeLayerSystem.Pipes.CanPlaceAt(x, y)) return false;
		if (LaserPipeLayerSystem.Pipes.Has(x, y)) return false;
		LaserConn.ConnectOnPlace(LaserPipeLayerSystem.Pipes, x, y);
		LaserPipeNetSystem.OnPipeAdded(x, y);
		PipePackets.SendPlacedLaser(x, y);
		return true;
	}

	public bool CutAt(int x, int y, Player remover)
	{
		if (!LaserPipeLayerSystem.Pipes.Has(x, y)) return false;
		LaserPipeLayerSystem.Pipes.Remove(x, y);
		LaserConn.ClearOnRemove(LaserPipeLayerSystem.Pipes, x, y);
		LaserPipeNetSystem.OnPipeRemoved(x, y);
		PipePackets.SendRemove(x, y, PipeKind.Laser);
		if (ModContent.GetInstance<GregTechCEuTerraria>().TryFind<ModItem>("normal_laser_pipe", out var mi))
			global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(remover, remover.GetSource_Misc("PipeRemove"), mi.Type, 1);
		return true;
	}
}
