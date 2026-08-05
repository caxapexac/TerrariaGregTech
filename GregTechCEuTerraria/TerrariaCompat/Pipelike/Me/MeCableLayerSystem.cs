#nullable enable
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using GregTechCEuTerraria.AppliedEnergistics.Api.Util;
using GregTechCEuTerraria.TerrariaCompat.Items.MeCables;
using GregTechCEuTerraria.TerrariaCompat.Net;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class MeCableLayerSystem : ModSystem
{
	public static MeCableLayer Cables { get; } = new();

	public override void Load() => On_Main.DoDraw_WallsAndBlacks += DrawCablesAfterWalls;

	public override void Unload()
	{
		On_Main.DoDraw_WallsAndBlacks -= DrawCablesAfterWalls;
		Cables.Clear();
	}

	private static void DrawCablesAfterWalls(On_Main.orig_DoDraw_WallsAndBlacks orig, Main self)
	{
		orig(self);
		MeCableRenderer.DrawVisible();
	}

	public override void PostDrawTiles()
	{
		var held = Main.LocalPlayer?.HeldItem;
		bool meRelated = held?.ModItem is MeCableItem
			|| (held?.ModItem is Items.Tools.ToolItem tool && tool.IsWireCutter)
			|| Items.Tools.Multitool.MultitoolState.IsActiveLayer(Main.LocalPlayer, "me_cable");
		if (!meRelated) return;
		MeCableRenderer.DrawForegroundOverlay();
	}

	public override void ClearWorld() => Cables.Clear();

	public override void OnWorldLoad() => MeCablePackets.SendLayerRequest();

	public override void SaveWorldData(TagCompound tag)
	{
		lock (Api.SaveTickGate.Lock)
			WriteWorldData(tag);
	}

	private void WriteWorldData(TagCompound tag)
	{
		if (Cables.Count == 0) return;
		var xs = new List<int>(Cables.Count);
		var ys = new List<int>(Cables.Count);
		var colors = new List<byte>(Cables.Count);
		foreach (var kv in Cables.All)
		{
			xs.Add(kv.Key.x);
			ys.Add(kv.Key.y);
			colors.Add((byte)kv.Value.Color);
		}
		tag["me_cables.xs"] = xs;
		tag["me_cables.ys"] = ys;
		tag["me_cables.colors"] = colors;
	}

	public override void LoadWorldData(TagCompound tag)
	{
		Cables.Clear();
		if (!tag.ContainsKey("me_cables.xs")) return;
		var xs = tag.GetList<int>("me_cables.xs");
		var ys = tag.GetList<int>("me_cables.ys");
		var colors = tag.GetList<byte>("me_cables.colors");
		int n = System.Math.Min(xs.Count, ys.Count);
		for (int i = 0; i < n; i++)
		{
			byte c = colors.Count > i ? colors[i] : (byte)AEColor.TRANSPARENT;
			if (c > (byte)AEColor.TRANSPARENT) c = (byte)AEColor.TRANSPARENT;
			Cables.Set(xs[i], ys[i], new MeCableCell((AEColor)c));
		}
	}
}
