#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric.Research;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class ResearchStationLayout
{
	private const int Padding = 12;
	private const int TitleH  = 14;
	private const int BodyW   = 190;
	private const int BodyH   = 120;

	public static MachineUILayout Build(ResearchStationMachine machine)
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

	internal static List<string> BuildDisplayLines(ResearchStationMachine machine)
	{
		var lines = new List<string>();
		var recipeLogic = machine.Recipe;

		if (!machine.IsFormed)
			RecipeStatusText.AppendUnformedStatus(machine, recipeLogic, lines);

		Api.Machine.Multiblock.MultiblockDisplayText.Create(lines, machine.IsFormed,
				showIncompleteStructureWarning: false)
			.SetWorkingStatus(recipeLogic.IsWorkingEnabled(), machine.DisplayActive)
			.SetWorkingStatusKeys(
				"gtceu.multiblock.idling",
				"gtceu.multiblock.work_paused",
				"gtceu.multiblock.research_station.researching")
			.AddEnergyUsageLine(machine.GetDisplayEnergyContainer())
			.AddEnergyTierLine(machine.MultiTier)
			.AddWorkingStatusLine()
			.AddProgressLineOnlyPercent(recipeLogic.GetProgressPercent());

		int capacity = machine.DisplayCapacityCwu;
		int req      = machine.DisplayRequiredCwu;
		if (req > 0)
		{
			string color = capacity < req ? "FF5555" : "55FFFF";
			lines.Add($"Computation: [c/{color}:{capacity} / {req} CWU/t]");
			if (capacity < req)
				lines.Add($"[c/FF5555:Insufficient computation - need {req} CWU/t, HPCA chain provides {capacity}]");
		}
		else if (capacity > 0)
		{
			lines.Add($"Computation: [c/55FFFF:{capacity} CWU/t available]");
		}

		return lines;
	}
}
