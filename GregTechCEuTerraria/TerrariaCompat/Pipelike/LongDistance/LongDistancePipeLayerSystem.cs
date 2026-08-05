#nullable enable
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.LongDistance;

public sealed class LongDistancePipeLayerSystem : ModSystem
{
	public static LongDistancePipeLayer Pipes { get; } = new();

	public override void Load()
	{
		On_Main.DoDraw_WallsAndBlacks += DrawAfterWalls;
	}

	public override void Unload()
	{
		On_Main.DoDraw_WallsAndBlacks -= DrawAfterWalls;
		Pipes.Clear();
	}

	private static void DrawAfterWalls(On_Main.orig_DoDraw_WallsAndBlacks orig, Main self)
	{
		orig(self);
		LongDistancePipeRenderer.DrawPipes();
	}

	public override void ClearWorld() => Pipes.Clear();

	public override void OnWorldLoad()
	{
		Net.PipePackets.SendLayerRequest(PipeKind.LongDistance);
	}

	public override void PostDrawTiles()
	{
		var held = Main.LocalPlayer?.HeldItem;
		bool ldLayer = held?.ModItem is Items.Pipes.LongDistancePipeItem
		            || (held?.ModItem is Items.Tools.ToolItem tool && tool.IsWrench)
		            || Items.Tools.Multitool.MultitoolState.IsActiveLayer(Main.LocalPlayer, "long_distance");
		if (ldLayer)
			LongDistancePipeRenderer.DrawForegroundOverlay();
	}

	public override void SaveWorldData(TagCompound tag)
	{
		lock (Api.SaveTickGate.Lock)
			WriteWorldData(tag);
	}

	private void WriteWorldData(TagCompound tag)
	{
		if (Pipes.Count == 0) return;
		var xs = new List<int>(Pipes.Count);
		var ys = new List<int>(Pipes.Count);
		var ty = new List<int>(Pipes.Count);
		foreach (var kv in Pipes.All)
		{
			xs.Add(kv.Key.x);
			ys.Add(kv.Key.y);
			ty.Add((int)kv.Value.Type);
		}
		tag["ld_pipes.xs"] = xs;
		tag["ld_pipes.ys"] = ys;
		tag["ld_pipes.type"] = ty;
	}

	public override void LoadWorldData(TagCompound tag)
	{
		Pipes.Clear();
		if (!tag.ContainsKey("ld_pipes.xs")) return;
		var xs = tag.GetList<int>("ld_pipes.xs");
		var ys = tag.GetList<int>("ld_pipes.ys");
		var ty = tag.ContainsKey("ld_pipes.type") ? tag.GetList<int>("ld_pipes.type") : null;
		int n = System.Math.Min(xs.Count, ys.Count);
		for (int i = 0; i < n; i++)
		{
			var type = (ty != null && i < ty.Count) ? (LongDistancePipeType)ty[i] : LongDistancePipeType.Item;
			Pipes.Set(xs[i], ys[i], new LongDistancePipeCell(type));
		}
	}
}
