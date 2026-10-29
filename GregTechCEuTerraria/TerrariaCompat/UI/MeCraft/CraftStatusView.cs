#nullable enable
using System;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;

public sealed class CraftStatusView : UIElement
{
	private readonly Point16 _termPos;

	public CraftStatusView(Point16 termPos, float width, float height)
	{
		_termPos = termPos;

		const int margin = 6, gap = 6, cancelH = 28, contentTop = 4;
		int contentH = (int)height - contentTop - cancelH - 6;
		int cpuW = (int)Math.Min(170f, width * 0.4f);
		int tableW = (int)width - margin * 2 - cpuW - gap;

		Append(new CraftStatusWindow.StatusTable(termPos)
		{
			Left = StyleDimension.FromPixels(margin),
			Top = StyleDimension.FromPixels(contentTop),
			Width = StyleDimension.FromPixels(tableW),
			Height = StyleDimension.FromPixels(contentH),
		});

		Append(new CraftStatusWindow.CpuList(termPos)
		{
			Left = StyleDimension.FromPixels(margin + tableW + gap),
			Top = StyleDimension.FromPixels(contentTop),
			Width = StyleDimension.FromPixels(cpuW),
			Height = StyleDimension.FromPixels(contentH),
		});

		Append(new UITextButton(
			label: () => "Cancel Job",
			onLeft: CancelSelected,
			tooltip: "Cancel the selected CPU's crafting job",
			width: 160, height: cancelH, textScale: 0.85f)
		{
			Left = StyleDimension.FromPixels(margin),
			Top = StyleDimension.FromPixels(contentTop + contentH + 6),
		});
	}

	private static void CancelSelected()
	{
		var s = MeCraftStatusSystem.LastSnapshot;
		int i = MeCraftStatusSystem.SelectedIndex;
		if (i >= 0 && i < s.Cpus.Count) MeCraftPackets.Cancel(s.Cpus[i].Pos);
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		if (UiFrame.Count % 30 == 0)
			MeCraftPackets.RequestStatus(_termPos, MeCraftStatusSystem.SelectedIndex);
	}
}
