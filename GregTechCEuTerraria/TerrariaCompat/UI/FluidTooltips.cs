#nullable enable
using GregTechCEuTerraria.Api.Fluids;
using Microsoft.Xna.Framework;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public static class FluidTooltips
{
	public static readonly Color TemperatureColor = new(255, 85, 85);

	public static string Temperature(FluidType fluid) => $"Temperature: {fluid.Temperature:N0} K";
}
