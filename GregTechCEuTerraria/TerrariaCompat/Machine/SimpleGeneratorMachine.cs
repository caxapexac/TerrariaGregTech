#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Content;
using GregTechCEuTerraria.Api.Recipe.Modifier;
using GregTechCEuTerraria.Common.Energy;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

// Dropped: hazardEmitter, tintColor / EditableMachineUI.
public class SimpleGeneratorMachine : WorkableTieredMachine
{
	public SimpleGeneratorMachine() { }
	public SimpleGeneratorMachine(VoltageTier tier) : base(tier) { }

	public override bool CanAccept  => false;
	public override bool CanExtract => true;

	protected override bool HasChargerSlot => false;

	public override bool RegressWhenWaiting() => false;

	public override bool CanVoidRecipeOutputs(object capability) => capability != Api.Capability.Recipe.EURecipeCapability.CAP;

	public static readonly RecipeModifier Modifier = new((machine, recipe) =>
	{
		if (machine is not SimpleGeneratorMachine generator)
			return RecipeModifier.NullWrongType();
		long EUt = recipe.OutputEUt.GetTotalEU();
		if (EUt <= 0) return ModifierFunction.NULL;

		int maxParallel = (int)(generator.OverclockVoltage / EUt);
		int parallels = ParallelLogic.GetParallelAmountFast(generator, recipe, maxParallel);

		return ModifierFunction.Builder()
			.InputModifier(ContentModifier.Multiplier_(parallels))
			.OutputModifier(ContentModifier.Multiplier_(parallels))
			.EutMultiplier(parallels)
			.Parallels(parallels)
			.Build();
	});

	public override RecipeModifier GetRecipeModifier() => Modifier;
}
