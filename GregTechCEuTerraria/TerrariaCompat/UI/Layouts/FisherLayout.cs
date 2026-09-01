#nullable enable
using System;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Net.Actions;
using GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class FisherLayout
{
	public static MachineUILayout Build(FisherMachine fisher)
	{
		int t = (int)fisher.Tier;
		int rowSize = t + 1;
		int inventorySize = rowSize * rowSize;

		const int SlotSize = 22;
		const int SlotGap  = 2;
		const int Padding  = 12;
		const int EnergyW  = 18;
		const int LabelRow = 14;

		int cacheW = rowSize * SlotSize + (rowSize - 1) * SlotGap;
		int cacheH = cacheW;
		int leftW  = EnergyW;

		int baitW = SlotSize;
		int baitH = SlotSize + 4 + 3 * SlotSize + 2 * SlotGap;

		int contentH = Math.Max(cacheH, baitH);
		int width  = Padding + leftW + 8 + baitW + 8 + cacheW + Padding;
		int height = Padding + LabelRow + contentH + Padding;

		int contentTop = Padding + LabelRow;

		var layout = new MachineUILayout
		{
			Width  = width,
			Height = height,
			Title  = fisher.DisplayName,
		};

		int leftX = Padding;
		layout.Widgets.Add(new EnergyBarWidgetSpec(
			X: leftX, Y: contentTop, Width: EnergyW, Height: cacheH));

		int baitX = leftX + leftW + 8;
		int baitY = contentTop + (cacheH - baitH) / 2;
		layout.Widgets.Add(new SlotWidgetSpec(
			X: baitX, Y: baitY,
			Group: SlotGroup.InventoryInput,
			SlotIndex: 0,
			EmptyHint: "Accepts: Silk, Bait"));

		int filterY = baitY + SlotSize + 4;
		void AddFilter(FisherFilter filter, string tooltip)
		{
			layout.Widgets.Add(new ToggleButtonWidgetSpec(
				X: baitX, Y: filterY,
				IconAssetPath: "GregTechCEuTerraria/Content/Textures/gui/widget/button_blacklist",
				Getter: () => fisher.IsFiltered(filter),
				Setter: v => MachineActions.Send(new FisherFilterAction(filter, v), fisher),
				Tooltip: tooltip)
			{ VerticalSplit = true });
			filterY += SlotSize + SlotGap;
		}

		const string BaitNote = "\n+1 bait per catch, discarded catches still cost bait";

		AddFilter(FisherFilter.Junk,
			"Filter junk out\nDiscards shoes, cans and seaweed" + BaitNote);
		AddFilter(FisherFilter.Fish,
			"Filter fish out\nDiscards every fish catch" + BaitNote);
		AddFilter(FisherFilter.Crate,
			"Filter treasures out\nDiscards fishing crates" + BaitNote);

		int cacheX = baitX + baitW + 8;
		for (int r = 0; r < rowSize; r++)
		{
			for (int c = 0; c < rowSize; c++)
			{
				int idx = r * rowSize + c;
				if (idx >= inventorySize) break;
				layout.Widgets.Add(new SlotWidgetSpec(
					X: cacheX + c * (SlotSize + SlotGap),
					Y: contentTop + r * (SlotSize + SlotGap),
					Group: SlotGroup.InventoryOutput,
					SlotIndex: idx));
			}
		}

		return layout;
	}
}
