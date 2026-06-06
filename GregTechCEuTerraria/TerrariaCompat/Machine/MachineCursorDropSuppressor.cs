#nullable enable
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

// Fixing right clicking a GUI machine while holding a item on the cursor drops the item
public sealed class MachineCursorDropSuppressor : ModSystem
{
	public override void Load()
	{
		On_Player.dropItemCheck += SuppressMachineCursorDrop;
	}

	public override void Unload()
	{
		On_Player.dropItemCheck -= SuppressMachineCursorDrop;
	}

	private static void SuppressMachineCursorDrop(On_Player.orig_dropItemCheck orig, Player self)
	{
		if (self.whoAmI == Main.myPlayer
		    && Main.mouseItem is { type: > 0, stack: > 0 }
		    && Main.mouseRight && Main.mouseRightRelease
		    && !self.mouseInterface
		    && HoveringGuiMachine(self))
			self.mouseInterface = true;

		orig(self);
	}

	private static bool HoveringGuiMachine(Player self)
	{
		if (Main.SmartInteractShowingGenuine && Main.SmartInteractNPC == -1 && Main.SmartInteractProj == -1
		    && IsGuiMachineTile(Main.SmartInteractX, Main.SmartInteractY))
			return true;

		int tx = (int)((Main.mouseX + Main.screenPosition.X) / 16f);
		int ty = self.gravDir == -1f
			? (int)((Main.screenPosition.Y + Main.screenHeight - Main.mouseY) / 16f)
			: (int)((Main.mouseY + Main.screenPosition.Y) / 16f);
		return IsGuiMachineTile(tx, ty);
	}

	private static bool IsGuiMachineTile(int x, int y)
	{
		if (!WorldGen.InWorld(x, y, 1)) return false;
		var tile = Main.tile[x, y];
		if (!tile.HasTile) return false;
		return TileLoader.GetTile(tile.TileType) is MetaMachineTile machine
		       && machine.Definition.LayoutKey != "none";
	}
}
