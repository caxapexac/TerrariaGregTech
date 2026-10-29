#nullable enable
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public sealed class UiFrame : ModSystem
{
	public static uint Count { get; private set; }

	public override void Load()
	{
		if (Main.dedServ) return;
		On_Main.UpdateUIStates += Advance;
	}

	private static void Advance(On_Main.orig_UpdateUIStates orig, GameTime gameTime)
	{
		Count++;
		orig(gameTime);
	}
}
