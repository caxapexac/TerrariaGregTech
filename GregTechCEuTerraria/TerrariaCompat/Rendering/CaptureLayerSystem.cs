#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Laser;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.LongDistance;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Optical;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Capture;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Rendering;

public sealed class CaptureLayerSystem : ModSystem
{
	private static bool _inCapture;

	public override void Load()
	{
		if (Main.dedServ) return;
		On_Main.DrawCapture += TrackCapture;
		On_Main.DrawWalls += DrawLayersAfterWalls;
	}

	public override void Unload()
	{
		if (Main.dedServ) return;
		On_Main.DrawCapture -= TrackCapture;
		On_Main.DrawWalls -= DrawLayersAfterWalls;
		_inCapture = false;
	}

	private static void TrackCapture(On_Main.orig_DrawCapture orig, Main self,
		Rectangle area, CaptureSettings settings)
	{
		_inCapture = true;
		try { orig(self, area, settings); }
		finally { _inCapture = false; }
	}

	private static void DrawLayersAfterWalls(On_Main.orig_DrawWalls orig, Main self)
	{
		orig(self);
		if (!_inCapture) return;

		CableRenderer.DrawVisible();
		MeCableRenderer.DrawVisible();
		PipeRenderer.DrawItemPipes();
		PipeRenderer.DrawFluidPipes();
		LaserPipeRenderer.DrawLaserPipes();
		OpticalPipeRenderer.DrawOpticalPipes();
		LongDistancePipeRenderer.DrawPipes();
		CleanroomWallRenderer.Draw(Main.spriteBatch);
	}
}
