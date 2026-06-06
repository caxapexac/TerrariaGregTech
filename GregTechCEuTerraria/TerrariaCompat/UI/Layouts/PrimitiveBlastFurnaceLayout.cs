#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Primitive;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

// 3 input slots -> progress arrow -> 3 output slots
public static class PrimitiveBlastFurnaceLayout
{
	public static MachineUILayout Build(PrimitiveBlastFurnaceMachine m) => new()
	{
		Width  = 184,
		Height = 132,
		Title  = m.DisplayName,

		Widgets =
		{
			// Input column
			new LabelWidgetSpec(X: 12, Y: 26, Text: "Input", Scale: 0.75f),
			new SlotWidgetSpec (X: 12, Y: 40, Group: SlotGroup.InventoryInput, SlotIndex: 0),
			new SlotWidgetSpec (X: 12, Y: 62, Group: SlotGroup.InventoryInput, SlotIndex: 1),
			new SlotWidgetSpec (X: 12, Y: 84, Group: SlotGroup.InventoryInput, SlotIndex: 2),

			// Progress arrow
			new ProgressArrowWidgetSpec(X: 56, Y: 62, Progress: () => (float)m.Recipe.GetProgressPercent()),

			// Output row
			new LabelWidgetSpec(X: 96, Y: 26, Text: "Output", Scale: 0.75f),
			new SlotWidgetSpec (X: 96,  Y: 40, Group: SlotGroup.InventoryOutput, SlotIndex: 0),
			new SlotWidgetSpec (X: 118, Y: 40, Group: SlotGroup.InventoryOutput, SlotIndex: 1),
			new SlotWidgetSpec (X: 140, Y: 40, Group: SlotGroup.InventoryOutput, SlotIndex: 2),

			// Live status under the slots
			new DynamicLabelWidgetSpec(X: 12, Y: 100,
				Getter: () => RecipeStatusText.StatusLineForMulti(m, m.Recipe), Scale: 0.7f),

			// Custom compat boost info
			new LabelWidgetSpec(X: 12, Y: 116, Text: "Hey I installed QoL mods for my PBF", Scale: 0.6f),
		},
	};
}
