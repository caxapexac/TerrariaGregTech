#nullable enable
namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

internal static class TagCycle
{
	public const int Period = 48;

	public static int Index(int count) =>
		count <= 0 ? 0 : (int)(UiFrame.Count / Period % (uint)count);
}
