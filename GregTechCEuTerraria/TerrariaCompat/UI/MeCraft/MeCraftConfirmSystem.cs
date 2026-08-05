#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;

public sealed class MeCraftConfirmSystem : ModalUISystem
{
	private const string LayerNameStr = "GregTechCEuTerraria: ME Craft Confirm";
	private CraftConfirmWindow? _window;
	protected override string LayerName => LayerNameStr;
	protected override bool CloseOnClickOutside => true;

	public override void Load()
	{
		base.Load();
		if (Ui != null) _window = new CraftConfirmWindow();
	}

	public override void Unload()
	{
		base.Unload();
		_window = null;
	}

	public static bool IsOpen => ModContent.GetInstance<MeCraftConfirmSystem>()?.IsOpenInternal ?? false;

	public static void OpenFor(Point16 termPos, AEKey what, long amount, CraftingPlanSummary summary,
		List<CpuInfo> cpus, List<(AEKey what, string reason)> invalid)
	{
		var sys = ModContent.GetInstance<MeCraftConfirmSystem>();
		if (sys?.Ui is null || sys._window is null) return;
		MeCraftSystem.Close();
		sys._window.Bind(termPos, what, amount, summary, cpus, invalid);
		sys.Ui.SetState(sys._window);
		sys.PushModal(MachineUISystem.LayerNameStr);
		SoundEngine.PlaySound(SoundID.MenuOpen);
	}

	public static void OpenForStation(Point16 termPos, AEKey what, CraftingPlanSummary summary)
	{
		var sys = ModContent.GetInstance<MeCraftConfirmSystem>();
		if (sys?.Ui is null || sys._window is null) return;
		MeStationCraftSystem.Close();
		sys._window.BindStation(termPos, what, summary);
		sys.Ui.SetState(sys._window);
		sys.PushModal(MachineUISystem.LayerNameStr);
		SoundEngine.PlaySound(SoundID.MenuOpen);
	}

	public static void Close() => ModContent.GetInstance<MeCraftConfirmSystem>()?.CloseInternal();

	protected override void OnClose()
	{
		_window?.Unbind();
		MeCraftPackets.Abandon();
		SoundEngine.PlaySound(SoundID.MenuClose);
	}
}
