#nullable enable
namespace GregTechCEuTerraria.Api;

internal static class SaveTickGate
{
	internal static readonly object Lock = new();
}
