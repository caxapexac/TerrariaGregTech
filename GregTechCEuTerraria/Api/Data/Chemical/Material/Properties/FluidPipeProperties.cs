#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Fluids.Attribute;

namespace GregTechCEuTerraria.Api.Data.Chemical.Material.Properties;

public sealed class FluidPipeProperties : IPropertyFluidFilter
{
	public const int MAX_PIPE_CHANNELS = 9;

	public int  Throughput          { get; init; }
	public int  Channels            { get; init; }
	public int  MaxFluidTemperature { get; init; }
	public bool GasProof            { get; init; }
	public bool CryoProof           { get; init; }
	public bool PlasmaProof         { get; init; }

	private readonly Dictionary<FluidAttribute, bool> _containmentPredicate = new();

	public bool AcidProof
	{
		get => CanContain(FluidAttributes.ACID);
		init { if (value) SetCanContain(FluidAttributes.ACID, true); }
	}

	public bool CanContain(FluidState state) => state switch
	{
		FluidState.LIQUID => true,
		FluidState.GAS    => GasProof,
		FluidState.PLASMA => PlasmaProof,
		_                 => true,
	};

	public bool CanContain(FluidAttribute attribute) =>
		_containmentPredicate.TryGetValue(attribute, out bool v) && v;

	public void SetCanContain(FluidAttribute attribute, bool canContain) =>
		_containmentPredicate[attribute] = canContain;

	public IReadOnlyCollection<FluidAttribute> ContainedAttributes => _containmentPredicate.Keys;
}
