#nullable enable
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class MeStorageLayout
{
	public static MachineUILayout Build(MeStorageMachine store) => new()
	{
		Width = 140,
		Height = 96,
		Title = store.DisplayName,

		Widgets =
		{
			new LabelWidgetSpec(X: 12, Y: 28, Text: "Network Storage", Scale: 0.8f),

			new DynamicLabelWidgetSpec(X: 12, Y: 42, Getter: () =>
			{
				var net = MeNetworkSystem.NetOf(store);
				return net == null
					? "[c/FF8888:Not connected]"
					: $"{net.Cells.Count} cables, {net.MountedStorageCount} devices";
			}),

			new DynamicLabelWidgetSpec(X: 12, Y: 56, Getter: () =>
				$"{store.StoredTypeCount} types   {store.TotalStored:N0} units"),

			new MeStorageSlotWidgetSpec(X: 100, Y: 28),
		},
	};
}
