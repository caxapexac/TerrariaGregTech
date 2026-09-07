#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public sealed class MachinePaintWatcher : ModSystem
{
	public override void Load()
	{
		On_WorldGen.paintTile += UpdatePartHandlerColor;
	}

	public override void Unload()
	{
		On_WorldGen.paintTile -= UpdatePartHandlerColor;
	}

	private static bool UpdatePartHandlerColor(On_WorldGen.orig_paintTile orig,
		int x, int y, byte color, bool broadCast)
	{
		bool painted = orig(x, y, color, broadCast);
		if (!painted || !MetaMachine.IsServer) return painted;

		try
		{
			if (MachineCellResolver.TryFindMachineAt(x, y, out var machine)
			    && machine is MultiblockPartMachine part)
				part.OnPaintingColorChanged(part.PaintingColor);
		}
		catch (System.Exception e)
		{
			ModContent.GetInstance<GregTechCEuTerraria>().Logger.Error(
				$"[MachinePaintWatcher] handler color update failed at {x},{y}", e);
		}
		return painted;
	}
}
