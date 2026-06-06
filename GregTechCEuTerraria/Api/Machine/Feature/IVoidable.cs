#nullable enable
using System;
using GregTechCEuTerraria.Api.Capability.Recipe;

namespace GregTechCEuTerraria.Api.Machine.Feature;

// Port of com.gregtechceu.gtceu.api.machine.feature.IVoidable.
//
// adaptations:
//  `IMachineFeature` dropped, consumers cast `this` to `MetaMachine` directly.
//  `attachConfigurators` UI helper dropped - the voiding-mode UI selector lands separately when the multi GUI does.
//  `getOutputLimits()` (per-capability output cap on the MachineDefinition) maps to `MetaMachine.GetOutputLimits()`
public interface IVoidable
{
	bool CanVoidRecipeOutputs(object capability) =>
		GetVoidingMode().CanVoid(capability) ||
		((this as TerrariaCompat.Machine.MetaMachine)?.GetOutputLimits() is { } limits
			&& limits.TryGetValue(capability, out var n) && n == 0);

	void SetVoidingMode(MultiblockVoidingMode mode) { }

	MultiblockVoidingMode GetVoidingMode() => MultiblockVoidingMode.VoidNone;
}

public enum MultiblockVoidingMode
{
	VoidNone        = 0,
	VoidItems       = 1,
	VoidFluids      = 2,
	VoidItemsFluids = 3,
}

public static class MultiblockVoidingModeExtensions
{
	public static bool CanVoid(this MultiblockVoidingMode mode, object capability) => mode switch
	{
		MultiblockVoidingMode.VoidNone        => false,
		MultiblockVoidingMode.VoidItems       => capability == ItemRecipeCapability.CAP,
		MultiblockVoidingMode.VoidFluids      => capability == FluidRecipeCapability.CAP,
		MultiblockVoidingMode.VoidItemsFluids => capability == ItemRecipeCapability.CAP
		                                       || capability == FluidRecipeCapability.CAP,
		_ => false,
	};

	public static string LocaleName(this MultiblockVoidingMode mode) => mode switch
	{
		MultiblockVoidingMode.VoidNone        => "gtceu.gui.no_voiding",
		MultiblockVoidingMode.VoidItems       => "gtceu.gui.item_voiding",
		MultiblockVoidingMode.VoidFluids      => "gtceu.gui.fluid_voiding",
		MultiblockVoidingMode.VoidItemsFluids => "gtceu.gui.all_voiding",
		_ => "gtceu.gui.no_voiding",
	};
}
