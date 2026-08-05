#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;

public static class CableRenderer
{
	private const string WireSideTex = "GregTechCEuTerraria/Content/Textures/block/material_sets/dull/wire_side";

	private static readonly Dictionary<byte, int> _thicknessBySize = new()
	{
		{ 1,  3 },
		{ 2,  4 },
		{ 4,  5 },
		{ 8,  6 },
		{ 16, 8 },
	};
	private const int DefaultThickness = 5;

	private static int ThicknessFor(byte wireSize) =>
		_thicknessBySize.TryGetValue(wireSize, out var t) ? t : DefaultThickness;

	public static void DrawVisible() => DrawAll(Main.spriteBatch, foreground: false);

	public static void DrawForegroundOverlay()
	{
		var sb = Main.spriteBatch;
		sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
			DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
		try
		{
			if (CableLayerSystem.Cables.Count > 0) DrawAll(sb, foreground: true);
			DrawFaceHints(sb);
		}
		finally { sb.End(); }
	}

	private static readonly Color OutputHint = Color.Orange;
	private static readonly Color InputHint  = new Color(60, 140, 255);

	private static void DrawFaceHints(SpriteBatch sb)
	{
		var px = Terraria.GameContent.TextureAssets.MagicPixel?.Value;
		if (px is null) return;

		int firstX = (int)(Main.screenPosition.X / 16) - 2;
		int lastX  = (int)((Main.screenPosition.X + Main.screenWidth) / 16) + 2;
		int firstY = (int)(Main.screenPosition.Y / 16) - 2;
		int lastY  = (int)((Main.screenPosition.Y + Main.screenHeight) / 16) + 2;

		foreach (var te in Terraria.DataStructures.TileEntity.ByID.Values)
		{
			if (te is not TerrariaCompat.Machine.MetaMachine machine) continue;
			if (machine is not Api.Capability.IEnergyContainer container) continue;

			foreach (var (cx, cy) in machine.Cells())
			{
				if (cx < firstX || cx > lastX || cy < firstY || cy > lastY) continue;

				var face = container.EnergyFaceForCell(cx, cy);
				if (face == Api.Capability.IODirection.None) continue;

				Color c;
				if (container.OutputsEnergy(face))     c = OutputHint;
				else if (container.InputsEnergy(face)) c = InputHint;
				else continue;

				int sx = cx * 16 - (int)Main.screenPosition.X;
				int sy = cy * 16 - (int)Main.screenPosition.Y;
				sb.Draw(px, new Rectangle(sx, sy, 16, 16), c * 0.38f);
				Color edge = c * 0.85f;
				sb.Draw(px, new Rectangle(sx,      sy,      16, 2), edge);
				sb.Draw(px, new Rectangle(sx,      sy + 14, 16, 2), edge);
				sb.Draw(px, new Rectangle(sx,      sy,      2, 16), edge);
				sb.Draw(px, new Rectangle(sx + 14, sy,      2, 16), edge);
			}
		}
	}

	private static void DrawAll(SpriteBatch sb, bool foreground)
	{
		var cables = CableLayerSystem.Cables;
		if (cables.Count == 0) return;

		int firstX = (int)(Main.screenPosition.X / 16) - 1;
		int lastX  = (int)((Main.screenPosition.X + Main.screenWidth) / 16) + 1;
		int firstY = (int)(Main.screenPosition.Y / 16) - 1;
		int lastY  = (int)((Main.screenPosition.Y + Main.screenHeight) / 16) + 1;

		var tex = PipeBodyArt.Tex(WireSideTex);
		if (tex is null) return;

		foreach (var kv in cables.All)
		{
			int x = kv.Key.x;
			int y = kv.Key.y;
			if (x < firstX || x > lastX || y < firstY || y > lastY) continue;

			int mask = cables.ConnectionMask(x, y);

			Vector2 pos = new Vector2(
				x * 16 - (int)Main.screenPosition.X,
				y * 16 - (int)Main.screenPosition.Y);

			Color tint = MaterialColor(kv.Value.MaterialId);

			if (foreground)
			{
				var net = TerrariaCompat.Pipelike.Cable.EnergyNetSystem.NetAt(x, y);
				if (net is not null)
				{
					float lossPct = net.GetCableLossPercent(x, y);
					if (lossPct >= 0.5f)
					{
						float t = System.Math.Min(1f, (lossPct - 0.5f) * 2f);
						tint = Color.Lerp(tint, Color.Red, 0.4f + 0.5f * t);
					}
				}
			}

			if (!foreground)
			{
				Color light = Lighting.GetColor(x, y);
				tint = new Color(
					(byte)(tint.R * light.R / 255),
					(byte)(tint.G * light.G / 255),
					(byte)(tint.B * light.B / 255));
			}

			int n = ThicknessFor(kv.Value.WireSize);
			if (kv.Value.Insulated)
			{
				PipeBodyArt.DrawCell(sb, tex, pos, mask, n + 2, JacketColor(tint));
				PipeBodyArt.DrawCell(sb, tex, pos, mask, n, tint);
			}
			else
			{
				PipeBodyArt.DrawCell(sb, tex, pos, mask, n, tint);
			}

			float activity = EnergyNetSystem.WireActivityAt(x, y);
			if (activity > 0.003f)
			{
				float lvl   = (float)System.Math.Sqrt(activity);
				float phase = Main.GameUpdateCount * 0.08f - (x + y) * 0.6f;
				float pulse = 0.6f + 0.4f * (0.5f + 0.5f * (float)System.Math.Sin(phase));
				float a = (0.5f + 0.5f * lvl) * pulse;
				if (a > 1f) a = 1f;
				var elec = Color.White * a;
				int coreN = System.Math.Max(1, n - 2);
				PipeBodyArt.DrawCell(sb, tex, pos, mask, coreN, elec);
			}
		}
	}

	public static Color JacketColor(Color c) =>
		new Color((byte)(c.R * 0.35f), (byte)(c.G * 0.35f), (byte)(c.B * 0.35f), c.A);

	private static Color MaterialColor(string materialId)
	{
		if (!MaterialRegistry.All.TryGetValue(materialId, out var mat)) return Color.White;
		uint c = mat.Color ?? 0xFFFFFFu;
		return new Color((byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF));
	}
}
