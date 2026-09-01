#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using Terraria;
using Terraria.Localization;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Pipes;

public static class PipeHeldItemBehavior
{
	public static void Tick(
		Player player,
		string heldKindLabel,
		IGridLayerHandle layer,
		Func<int, int, string?> hoverBody,
		ref int removeCooldown,
		int useTime)
	{
		if (Main.myPlayer != player.whoAmI) return;
		Hover(player, heldKindLabel, hoverBody);
		HandleRmbCut(player, layer, ref removeCooldown, useTime);
	}

	public static void Tick(
		Player player,
		PipeKind kind,
		string heldKindLabel,
		IGridLayerHandle layer,
		ref int removeCooldown,
		int useTime)
		=> Tick(player, heldKindLabel, layer, (x, y) => PipeBody(kind, x, y), ref removeCooldown, useTime);

	private static void Hover(Player player, string heldKindLabel, Func<int, int, string?> hoverBody)
	{
		if (player.mouseInterface || Main.gameMenu || UI.UILayers.IsCursorOverAnyModal()) return;
		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1) return;

		string controlsLine = $"[c/A0E0FF:LMB]: Place {heldKindLabel}   [c/FFA0A0:RMB]: Remove {heldKindLabel}";
		string? body = hoverBody(x, y);
		UI.WorldHoverTooltip.Set(body is null ? controlsLine : controlsLine + "\n" + body);
	}

	private static string? PipeBody(PipeKind kind, int x, int y)
	{
		if (kind == PipeKind.Laser)
		{
			if (!Pipelike.Laser.LaserPipeLayerSystem.Pipes.Has(x, y)) return null;
			bool active = Pipelike.Laser.LaserPipeLayerSystem.IsActive(x, y);
			return "Laser Pipe\n" + (active ? "[c/55FF55:Active]" : "[c/AAAAAA:Idle]");
		}
		if (kind == PipeKind.Fluid)
		{
			var c = FluidPipeLayerSystem.Pipes.CellAt(x, y);
			if (c is null) return null;
			var f = c.Value;
			string sizeWord = PipeSizes.Word(f.Size);
			string line1 = f.IsSimple
				? "Simple Fluid Pipe"
				: $"{HumanizeMaterial(f.MaterialId)} {Capitalize(sizeWord)} Fluid Pipe";
			return $"{line1}\n{f.Throughput:N0} mB/t   {f.Channels} channel{(f.Channels == 1 ? "" : "s")}";
		}

		var ci = ItemPipeLayerSystem.Pipes.CellAt(x, y);
		if (ci is null) return null;
		var i = ci.Value;
		string isizeWord = PipeSizes.Word(i.Size);
		string kindWord = i.Restrictive ? "Restrictive Item Pipe" : "Item Pipe";
		string iline1 = i.IsSimple
			? "Simple Item Pipe"
			: $"{HumanizeMaterial(i.MaterialId)} {Capitalize(isizeWord)} {kindWord}";
		float rate = i.TransferRate;
		return $"{iline1}\n[c/55FFFF:Transfer Rate:] {(int)((rate * 64f) + 0.5f)} items/s";
	}

	private static void HandleRmbCut(Player player, IGridLayerHandle layer, ref int removeCooldown, int useTime)
	{
		if (player.mouseInterface || Main.gameMenu || UI.UILayers.IsCursorOverAnyModal()) { removeCooldown = 0; return; }
		if (!Main.mouseRight) { removeCooldown = 0; return; }
		if (removeCooldown > 0) { removeCooldown--; return; }

		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (layer.CutAt(x, y, player))
			removeCooldown = useTime;
	}

	internal static string HumanizeMaterial(string materialId)
	{
		string key = $"Mods.GregTechCEuTerraria.Materials.{materialId}";
		string text = Language.GetTextValue(key);
		return text == key ? TitleCase(materialId) : text;
	}

	private static string TitleCase(string snake)
	{
		var sb = new System.Text.StringBuilder(snake.Length);
		bool capNext = true;
		foreach (char c in snake)
		{
			if (c == '_') { sb.Append(' '); capNext = true; continue; }
			sb.Append(capNext ? char.ToUpperInvariant(c) : c);
			capNext = false;
		}
		return sb.ToString();
	}

	private static string Capitalize(string s) =>
		string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
