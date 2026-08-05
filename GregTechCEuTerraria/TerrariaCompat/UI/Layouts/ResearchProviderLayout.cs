#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Machine.Multiblock;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric.Research;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class ResearchProviderLayout
{
	private const int Padding = 12;
	private const int TitleH  = 14;
	private const int BodyW   = 190;
	private const int BodyH   = 120;

	public static MachineUILayout Build(WorkableElectricMultiblockMachine machine)
	{
		var layout = new MachineUILayout
		{
			Width  = Padding + BodyW + Padding,
			Height = Padding + TitleH + BodyH + Padding,
			Title  = machine.DisplayName,
		};

		layout.Widgets.Add(new MultiLineDynamicLabelWidgetSpec(
			X: Padding, Y: Padding + TitleH,
			Getter: () => BuildDisplayLines(machine),
			Width: BodyW, Height: BodyH));

		return layout;
	}

	internal static List<string> BuildDisplayLines(WorkableElectricMultiblockMachine machine)
	{
		var lines = new List<string>();
		var recipeLogic = machine.Recipe;

		if (!machine.IsFormed)
			RecipeStatusText.AppendUnformedStatus(machine, recipeLogic, lines);

		machine.Definition?.AdditionalDisplay?.Invoke(machine, lines);

		bool providing = machine is HPCAMachine hpca
			? hpca.DisplayCachedCWUt > 0
			: machine.DisplayActive && recipeLogic.IsWorkingEnabled();

		MultiblockDisplayText.Create(lines, machine.IsFormed,
				showIncompleteStructureWarning: false)
			.SetWorkingStatus(true, providing)
			.SetWorkingStatusKeys(
				"gtceu.multiblock.idling",
				"gtceu.multiblock.idling",
				"gtceu.multiblock.data_bank.providing")
			.AddWorkingStatusLine();

		return lines;
	}
}
