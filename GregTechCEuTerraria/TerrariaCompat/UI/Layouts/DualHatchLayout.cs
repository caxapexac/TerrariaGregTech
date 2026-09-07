#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class DualHatchLayout
{
	private const int SlotSize = 22;
	private const int Padding  = 12;
	private const int TitleH   = 14;

	public static MachineUILayout Build(DualHatchPartMachine hatch)
	{
		int size = hatch.Inventory?.SlotCount ?? 0;
		int n    = (int)System.Math.Sqrt(size);

		int itemStripW = n * SlotSize;
		int stripH     = n * SlotSize;
		int tankColX   = Padding + itemStripW;

		var group = hatch.Io == IO.IN
			? TerrariaCompat.Machine.SlotGroup.InventoryInput
			: TerrariaCompat.Machine.SlotGroup.InventoryOutput;

		bool showCircuit = hatch.Io == IO.IN
			&& hatch is IHasCircuitSlot icc && icc.IsCircuitSlotEnabled()
			&& hatch.CircuitInventory != null && hatch.CircuitInventory.SlotCount > 0;
		int circuitColW = showCircuit ? (Padding + SlotSize) : 0;

		var layout = new MachineUILayout
		{
			Width  = Padding + itemStripW + SlotSize + circuitColW + Padding,
			Height = Padding + TitleH + stripH + Padding,
			Title  = hatch.DisplayName,
		};

		int baseY = Padding + TitleH;

		for (int idx = 0; idx < size; idx++)
		{
			int c = idx % n, r = idx / n;
			layout.Widgets.Add(new SlotWidgetSpec(
				X: Padding + c * SlotSize,
				Y: baseY + r * SlotSize,
				Group: group,
				SlotIndex: idx));
		}

		int tankCount = hatch.Tank?.Storages.Length ?? n;
		for (int t = 0; t < tankCount; t++)
		{
			layout.Widgets.Add(new FluidSlotWidgetSpec(
				X: tankColX,
				Y: baseY + t * SlotSize,
				Width: SlotSize, Height: SlotSize,
				Direction: hatch.Io,
				TankIndex: t));
		}

		if (showCircuit)
		{
			int circuitX = Padding + itemStripW + SlotSize + Padding;
			int circuitY = baseY + (stripH - SlotSize) / 2;
			layout.Widgets.Add(new CircuitButtonWidgetSpec(X: circuitX, Y: circuitY));
		}

		return layout;
	}
}
