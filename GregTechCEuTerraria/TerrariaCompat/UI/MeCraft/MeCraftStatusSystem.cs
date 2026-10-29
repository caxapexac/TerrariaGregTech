#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;

public sealed class MeCraftStatusSystem : ModalUISystem
{
	private const string LayerNameStr = "GregTechCEuTerraria: ME Craft Status";
	private CraftStatusWindow? _window;
	protected override string LayerName => LayerNameStr;

	public static CraftStatusSnapshot LastSnapshot { get; private set; } = CraftStatusSnapshot.Empty;
	public static int SelectedIndex { get; set; }
	private static long _lastPollTick = -1000;
	private static Point16 _lastPollPos;

	public override void Load()
	{
		base.Load();
		if (Ui != null) _window = new CraftStatusWindow();
	}

	public override void Unload()
	{
		base.Unload();
		_window = null;
		LastSnapshot = CraftStatusSnapshot.Empty;
	}

	public static bool IsOpen => ModContent.GetInstance<MeCraftStatusSystem>()?.IsOpenInternal ?? false;

	public static void Receive(CraftStatusSnapshot snapshot)
	{
		LastSnapshot = snapshot;
		if (snapshot.SelectedIndex >= 0) SelectedIndex = snapshot.SelectedIndex;
	}

	public static int CountFor(Point16 termPos)
	{
		if (UiFrame.Count - _lastPollTick > 30 || _lastPollPos != termPos)
		{
			_lastPollTick = UiFrame.Count;
			_lastPollPos = termPos;
			MeCraftPackets.RequestStatus(termPos, SelectedIndex);
		}
		return LastSnapshot.BusyCount;
	}

	public static void Open(Point16 termPos)
	{
		var sys = ModContent.GetInstance<MeCraftStatusSystem>();
		if (sys?.Ui is null || sys._window is null) return;
		sys._window.Bind(termPos);
		sys.Ui.SetState(sys._window);
		sys.PushModal(MachineUISystem.LayerNameStr);
		MeCraftPackets.RequestStatus(termPos, SelectedIndex);
		SoundEngine.PlaySound(SoundID.MenuOpen);
	}

	public static void Close() => ModContent.GetInstance<MeCraftStatusSystem>()?.CloseInternal();

	protected override void OnClose()
	{
		_window?.Unbind();
		SoundEngine.PlaySound(SoundID.MenuClose);
	}
}
