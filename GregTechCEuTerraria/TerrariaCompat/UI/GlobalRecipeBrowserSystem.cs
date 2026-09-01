#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.TerrariaCompat.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public sealed class GlobalRecipeBrowserSystem : ModalUISystem
{
	private static GlobalRecipeBrowserSystem? _instance;
	private GlobalRecipeBrowserState? _state;

	public const string LayerNameStr = "GregTechCEuTerraria: GlobalRecipeBrowser";
	protected override string LayerName => LayerNameStr;
	public override bool PinSupported => true;

	public override void Load()
	{
		_instance = this;
		base.Load();
		if (!Main.dedServ)
		{
			_state = new GlobalRecipeBrowserState();
			_state.Host = this;
			_state.Activate();
		}
	}

	public override void Unload()
	{
		_instance = null;
		_state = null;
		base.Unload();
	}

	private bool _dockedWanted;

	public override void OnWorldLoad()
	{
		if (!Main.dedServ)
		{
			GlobalRecipeBrowserState.SyncLocaleCaches();
			RecipeSearch.WarmCache();
			_state?.Warm();
			Loot.LootRegistry.Warm();
			_dockedWanted = Config.GTClientConfig.Instance.OpenDockedBrowserOnLaunch;
		}
	}

	public override void UpdateUI(GameTime gameTime)
	{
		if (_dockedWanted && !Main.dedServ && Main.playerInventory && !IsOpenInternal)
		{
			_dockedWanted = false;
			EnterDocked();
		}
		base.UpdateUI(gameTime);
	}

	protected override void OnClose()
	{
		_state?.SaveQueryForReopen();
		Widgets.UISearchBar.UnfocusAll();
	}

	protected override void AddExtraLayers(List<GameInterfaceLayer> layers)
		=> UILayers.InsertButton(layers,
			"GregTechCEuTerraria: TooManyItems Button",
			() => { DrawInventoryButton(); DrawDockedButton(); return true; });

	public static void Open()
	{
		var s = _instance;
		if (s?.Ui is null || s._state is null) return;
		s.Pinned = false;
		Main.playerInventory = true;
		if (s.Ui.CurrentState != s._state)
		{
			s._state.RebuildFromScratch();
			s.Ui.SetState(s._state);
		}
		s.PushModal();
		s._state.RelayoutNow(false);
	}

	public static bool DockedActive
		=> _instance is { } s && s.Pinned && (s._state?.Docked ?? false);

	public static void ToggleDocked()
	{
		if (DockedActive) ExitDocked();
		else EnterDocked();
	}

	public static void ToggleSidePanels()
	{
		var cfg = Config.GTClientConfig.Instance;
		bool show = !(cfg.ShowFavoritesPanel || cfg.ShowHistoryPanel);
		cfg.ShowFavoritesPanel = show;
		cfg.ShowHistoryPanel = show;
		cfg.Persist();

		var s = _instance;
		if (s?._state is { } state && s.IsOpenInternal)
			state.RelayoutNow(state.Docked);
	}

	private static void EnterDocked()
	{
		var s = _instance;
		if (s?.Ui is null || s._state is null) return;
		Main.playerInventory = true;
		if (s.Ui.CurrentState != s._state)
		{
			s._state.RebuildFromScratch();
			s.Ui.SetState(s._state);
		}
		s.Pinned = true;
		s.PushModal();
		s._state.RelayoutNow(true);
	}

	private static void ExitDocked()
	{
		var s = _instance;
		if (s is null) return;
		s.Pinned = false;
		s._state?.SetDocked(false);
		s.CloseInternal();
	}

	public static void OpenFiltered(int itemType, GlobalRecipeBrowserState.BrowseFilter filter)
	{
		FavoritesPlayer.Local.RecordItem(itemType);
		if (Show() is { } s) s._state!.ApplyItemFilter(itemType, filter);
	}

	public static void OpenFilteredFluid(string fluidId, string label,
		GlobalRecipeBrowserState.BrowseFilter filter)
	{
		FavoritesPlayer.Local.RecordFluid(fluidId, label);
		if (Show() is { } s) s._state!.ApplyFluidFilter(fluidId, label, filter);
	}

	public static void OpenFilteredTag(string tagLabel, HashSet<int> items,
		GlobalRecipeBrowserState.BrowseFilter filter)
	{
		FavoritesPlayer.Local.RecordTag(tagLabel, items);
		if (Show() is { } s) s._state!.ApplyTagFilter(tagLabel, items, filter);
	}

	public static void OpenStation(string station)
	{
		if (Show() is { } s) s._state!.ApplyStationFilter(station);
	}

	private static GlobalRecipeBrowserSystem? Show()
	{
		var s = _instance;
		if (s?.Ui is null || s._state is null) return null;
		if (s.Ui.CurrentState != s._state) s.Ui.SetState(s._state);
		s.PushModal();
		Main.playerInventory = true;
		return s;
	}

	public static void Close() => _instance?.CloseInternal();

	public static void Toggle()
	{
		if (IsOpen)
			Close();
		else
			Open();
	}

	public static bool IsOpen => _instance?.IsOpenInternal ?? false;


	private static void DrawInventoryButton()
		=> UILayers.DrawStackedButton(
			slot: 0,
			background: new Color(38, 42, 70),
			drawIcon: (sb, r) =>
			{
				var plate = TooManyItemsArt.PlateTexture;
				if (plate != null) sb.Draw(plate, r, Color.White);
			},
			tooltip: "Open Too Many Items",
			onClick: Open,
			keybind: ModalToggleKeybinds.OpenRecipeBrowser);

	private static void DrawDockedButton()
		=> UILayers.DrawStackedButton(
			slot: 1,
			background: DockedActive ? new Color(52, 92, 60) : new Color(38, 42, 70),
			drawIcon: (sb, r) =>
			{
				var plate = TooManyItemsArt.PlateTexture;
				if (plate != null)
					sb.Draw(plate, r, DockedActive ? Color.White : new Color(150, 170, 210));
			},
			tooltip: DockedActive ? "Close Too Many Items docked" : "Open Too Many Items docked",
			onClick: ToggleDocked,
			keybind: ModalToggleKeybinds.ToggleDockedBrowser);
}
