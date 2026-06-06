#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Machine;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

// warning when a machine per-tier item output limit caps recipe outputs below its physical output-slot count
internal static class OutputLimitWarning
{
	public static readonly Microsoft.Xna.Framework.Color Color = new Microsoft.Xna.Framework.Color(255, 70, 70);

	public static string? Text(MetaMachine m, int outputItemSlots)
	{
		if (outputItemSlots <= 0) return null;
		var limits = m.GetOutputLimits();
		if (limits != null
			&& limits.TryGetValue(ItemRecipeCapability.CAP, out var n)
			&& n >= 0 && n < outputItemSlots)
		{
			return $"Low tier machine - byproducts lost: only {n}/{outputItemSlots} slots kept";
		}
		return null;
	}
}
