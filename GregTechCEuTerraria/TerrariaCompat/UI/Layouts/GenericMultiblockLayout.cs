#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Machine.Multiblock;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class GenericMultiblockLayout
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
		int baseY = Padding + TitleH;

		layout.Widgets.Add(new MultiLineDynamicLabelWidgetSpec(
			X: Padding, Y: baseY,
			Getter: () => BuildDisplayLines(machine),
			Width: BodyW, Height: BodyH));

		return layout;
	}

	internal static List<string> BuildDisplayLines(WorkableElectricMultiblockMachine machine)
	{
		var lines = new List<string>();
		var recipeLogic = machine.Recipe;

		int numParallels, subtickParallels, batchParallels, totalRuns;
		bool exact = false;
		var lastRecipe = recipeLogic.GetLastRecipe();
		if (recipeLogic.IsActive() && lastRecipe != null)
		{
			numParallels      = lastRecipe.Parallels;
			subtickParallels  = lastRecipe.SubtickParallels;
			batchParallels    = lastRecipe.BatchParallels;
			totalRuns         = lastRecipe.GetTotalRuns();
			exact = true;
		}
		else
		{
			numParallels      = machine.GetParallelHatch()?.CurrentParallel ?? 0;
			subtickParallels  = 0;
			batchParallels    = 0;
			totalRuns         = 0;
		}

		if (!machine.IsFormed)
			RecipeStatusText.AppendUnformedStatus(machine, recipeLogic, lines);

		var activeRecipe = recipeLogic.GetLastRecipe();
		if (activeRecipe == null && machine.LastRecipeId is { } rid)
			activeRecipe = machine.GetRecipeType()?.GetRecipeById(rid);
		bool hideDuration = activeRecipe != null &&
			global::GregTechCEuTerraria.Api.Recipe.RecipeDataUtil.GetBool(activeRecipe.Data, "hide_duration");

		var b = MultiblockDisplayText.Create(lines, machine.IsFormed,
				showIncompleteStructureWarning: false)
			.SetWorkingStatus(recipeLogic.IsWorkingEnabled(), machine.DisplayActive)
			.AddEnergyUsageLine(machine.GetDisplayEnergyContainer())
			.AddEnergyTierLine(machine.MultiTier)
			.AddMachineModeLine(machine.GetRecipeType(), machine.GetRecipeTypes().Length > 1)
			.AddTotalRunsLine(totalRuns)
			.AddParallelsLine(numParallels, exact)
			.AddSubtickParallelsLine(subtickParallels)
			.AddBatchModeLine(machine.IsBatchEnabled(), batchParallels)
			.AddWorkingStatusLine(recipeLogic);

		if (hideDuration)
			b.AddProgressLineOnlyPercent(recipeLogic.GetProgressPercent());
		else
			b.AddProgressLine(recipeLogic);

		b.AddRecipeFailReasonLine(recipeLogic)
			.AddOutputLines(lastRecipe);

		if (machine.IsFormed && !machine.DisplayActive)
			MultiblockInputDisplay.Append(machine, lines);

		machine.Definition?.AdditionalDisplay?.Invoke(machine, lines);

		foreach (var part in machine.GetParts())
			part.AddMultiText(lines);

		return lines;
	}
}
