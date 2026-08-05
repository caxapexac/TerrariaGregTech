#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Machine.Multiblock;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class CleanroomLayout
{
	private const int Padding = 12;
	private const int TitleH  = 14;
	private const int BodyW   = 190;
	private const int BodyH   = 120;

	public static MachineUILayout Build(CleanroomMachine machine)
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

	private static List<string> BuildDisplayLines(CleanroomMachine machine)
	{
		var lines = new List<string>();
		var recipeLogic = machine.Recipe;

		if (!machine.IsFormed)
			RecipeStatusText.AppendUnformedStatus(machine, recipeLogic, lines);

		if (machine.IsFormed)
		{
			long maxVoltage = machine.GetMaxVoltage();
			if (maxVoltage > 0)
			{
				int voltageTier = VoltageTiers.FloorTierByVoltage(maxVoltage);
				string voltageName = VoltageTiers.ShortName((VoltageTier)voltageTier);
				lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.max_energy_per_tick",
					maxVoltage.ToString("N0"), voltageName));
			}

			var cleanroomType = machine.CleanroomTypeResolved;
			if (cleanroomType != null)
				lines.Add(MultiblockDisplayText.Tr(cleanroomType.TranslationKey));

			if (!machine.WorkingEnabled)
			{
				lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.work_paused"));
			}
			else if (recipeLogic.IsActive())
			{
				lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.running"));
				int currentProgress = (int)(recipeLogic.GetProgressPercent() * 100);
				double maxInSec     = recipeLogic.GetMaxProgress() / 20.0;
				double currentInSec = recipeLogic.GetProgress()    / 20.0;
				lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.progress",
					currentInSec.ToString("0.00"), maxInSec.ToString("0.00"),
					currentProgress));
			}
			else
			{
				lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.idling"));
			}

			if (recipeLogic.IsWaiting())
				lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.waiting"));

			lines.Add(MultiblockDisplayText.Tr(machine.CleanroomActive
				? "gtceu.multiblock.cleanroom.clean_state"
				: "gtceu.multiblock.cleanroom.dirty_state"));
			lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.cleanroom.clean_amount",
				machine.CleanAmount));

			lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.dimensions.0"));
			lines.Add(MultiblockDisplayText.Tr("gtceu.multiblock.dimensions.1.2d",
				machine.FormedTileWidth / 2, machine.FormedTileHeight / 2));
		}

		return lines;
	}
}
