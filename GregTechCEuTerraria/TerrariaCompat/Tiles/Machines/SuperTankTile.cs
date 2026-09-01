#nullable enable
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.UI;
using GregTechCEuTerraria.TerrariaCompat.UI.Layouts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public class SuperTankTile : TieredMachineTile
{
	public SuperTankTile() { }
	public SuperTankTile(VoltageTier tier, MachineDefinition def) : base(tier, def) { }

	protected override Color MapColor    => new(120, 160, 220);
	protected override int   MineDustType => Terraria.ID.DustID.Glass;

	internal const int FluidInsetArtPx = 4;

	public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
	{
		base.PostDraw(i, j, spriteBatch);

		if (!MachineCellResolver.TryFindAt<SuperTankTileEntity>(i, j, out var tank)) return;

		var (w, h) = tank.Size;
		int originX = tank.Position.X, originY = tank.Position.Y;
		if (i != originX + w - 1 || j != originY + h - 1) return;

		var stored = tank.GetTank(0);
		if (stored.IsEmpty || stored.Type is null) return;

		Vector2 zero = Main.drawToScreen ? Vector2.Zero
			: new Vector2(Main.offScreenRange, Main.offScreenRange);
		Vector2 pos = new Vector2(originX * 16 - (int)Main.screenPosition.X,
		                          originY * 16 - (int)Main.screenPosition.Y) + zero;

		var inner = new Rectangle(
			(int)pos.X + FluidInsetArtPx * w, (int)pos.Y + FluidInsetArtPx * h,
			(16 - 2 * FluidInsetArtPx) * w, (16 - 2 * FluidInsetArtPx) * h);
		FluidIconRenderer.Draw(spriteBatch, stored.Type, inner, light: Lighting.GetColor(originX, originY));
	}

	public override bool RightClick(int i, int j)
	{
		if (!MachineCellResolver.TryFindAt<SuperTankTileEntity>(i, j, out var tank)) return false;

		MachineUISystem.OpenFor(tank, SuperTankLayout.Build(tank));
		return true;
	}
}
