#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Fluids.Attribute;

namespace GregTechCEuTerraria.Api.Capability;

public interface IPropertyFluidFilter
{
	const int CryogenicFluidThreshold = 120;

	bool Test(FluidStack stack)
	{
		if (stack.IsEmpty || stack.Type is null) return true;
		FluidType fluid = stack.Type;

		if (fluid.Temperature < CryogenicFluidThreshold && !CryoProof) return false;

		FluidState state = fluid.State;
		if (!CanContain(state)) return false;
		foreach (FluidAttribute attribute in fluid.Attributes)
			if (!CanContain(attribute))
				return false;

		if (state == FluidState.PLASMA) return true;

		return fluid.Temperature <= MaxFluidTemperature;
	}

	bool CanContain(FluidState state);
	bool CanContain(FluidAttribute attribute);
	void SetCanContain(FluidAttribute attribute, bool canContain);

	IReadOnlyCollection<FluidAttribute> ContainedAttributes { get; }

	int MaxFluidTemperature { get; }
	bool GasProof { get; }
	bool CryoProof { get; }
	bool PlasmaProof { get; }
}
