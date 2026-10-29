#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Items.Fluids;
using GregTechCEuTerraria.TerrariaCompat.Loot;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using UISearchBar = GregTechCEuTerraria.TerrariaCompat.UI.Widgets.UISearchBar;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public sealed class GlobalRecipeBrowserState : FreeModalWindow
{
	public enum BrowseFilter { None, Output, Input }
	public enum BrowseMode { Recipes, Items, Loot, Equippable }

	[System.Flags]
	private enum EquipCat
	{
		None     = 0,
		Helmet   = 1 << 0,
		Shirt    = 1 << 1,
		Pants    = 1 << 2,
		Trinket  = 1 << 3,
		Dye      = 1 << 4,
		Mount    = 1 << 5,
		Hook     = 1 << 6,
		Minecart = 1 << 7,
		Pet      = 1 << 8,
		LightPet = 1 << 9,
	}

	private static readonly (EquipCat Cat, string Name)[] _equipCats =
	{
		(EquipCat.Helmet,   "Helmets"),
		(EquipCat.Shirt,    "Shirts"),
		(EquipCat.Pants,    "Pants"),
		(EquipCat.Trinket,  "Trinkets"),
		(EquipCat.Dye,      "Dyes"),
		(EquipCat.Mount,    "Mounts"),
		(EquipCat.Hook,     "Hooks"),
		(EquipCat.Minecart, "Minecarts"),
		(EquipCat.Pet,      "Pet"),
		(EquipCat.LightPet, "Light Pet"),
	};

	private UITerrariaPanel? _panel;
	private UITerrariaPanel? _settingsPanel;
	private UIRecipeList? _list;
	private UIItemGrid? _grid;
	private UIItemGrid? _equipGrid;
	private UILootList? _loot;
	private UISearchBar? _search;
	private UIFavoritesPanel? _favorites;
	private UIFavoritesPanel? _historyPanel;
	private UICheckButton? _haveOnlyToggle;
	private UICheckButton? _hideObviousToggle;
	private UICheckButton? _searchIngredientsToggle;
	private BrowseMode _mode = BrowseMode.Recipes;
	private bool _modeSwapPending;

	private void SetMode(BrowseMode m)
	{
		if (m == _mode) return;
		_mode = m;
		_modeSwapPending = true;
	}

	private List<int>? _allItems;
	private List<int> _filteredItems = new();
	private List<string> _filteredFluids = new();
	private static List<FluidType>? _allFluids;
	private List<int>? _allEquippable;
	private List<int> _filteredEquippable = new();
	private static EquipCat _shownEquip = EquipCat.None;
	private static readonly Dictionary<int, EquipCat> _equipCatCache = new();
	private UIList? _equipList;
	private UIScrollbar? _equipScroll;
	private List<LootRegistry.LootEntry> _filteredLoot = new();
	private UIText? _chipHint;
	private UITextButton? _chipButton;
	private UIElement? _chipShown;
	private UIElement? _chipPending;
	private string _chipLabel = "";
	private bool _haveOnly;
	private bool _hideObvious;
	private static bool _lastHideObvious = false;
	private bool _searchIngredients;
	private static bool _lastSearchIngredients = false;
	private static readonly HashSet<string> _shownMods = new();
	private static List<string>? _modOrder;
	private static bool _modOrderReady;
	private static readonly Dictionary<GTRecipe, string> _recipeModCache = new();
	private const string VanillaMod = "Terraria";
	private const string GregTechMod = "GregTechCEuTerraria";
	private UIList? _modList;
	private UIScrollbar? _modScroll;
	private int _modBtnW;
	private int _setBtnW;
	private int _listTop;
	private int _listH;
	private int _haveOnlyTick;
	private List<GTRecipe> _all = new();
	private List<GTRecipe> _filtered = new();
	private static string _lastQuery = "";
	private static BrowseMode _lastMode = BrowseMode.Recipes;
	public string CurrentQuery => _search?.Text ?? "";
	public void SaveQueryForReopen()
	{
		_lastQuery = CurrentQuery;
		_lastMode = _mode;
		_lastHideObvious = _hideObvious;
		_lastSearchIngredients = _searchIngredients;
	}

	private BrowseFilter _filter = BrowseFilter.None;
	private int _filterItem;
	private string? _filterFluid;
	private string? _filterFluidLabel;
	private string? _filterTagLabel;
	private HashSet<int>? _filterTagItems;

	private readonly struct FilterState
	{
		public readonly BrowseFilter Filter;
		public readonly int Item;
		public readonly string? Fluid;
		public readonly string? FluidLabel;
		public readonly string? TagLabel;
		public readonly HashSet<int>? TagItems;

		public FilterState(BrowseFilter filter, int item, string? fluid,
			string? fluidLabel, string? tagLabel, HashSet<int>? tagItems)
		{
			Filter = filter; Item = item; Fluid = fluid;
			FluidLabel = fluidLabel; TagLabel = tagLabel; TagItems = tagItems;
		}
	}

	private readonly List<FilterState> _history = new();
	private UITextButton? _backButton;
	private bool _backShown;
	private int _chipFullW;

	private const int HeaderPad = 8;
	private const int SearchH   = 26;
	private const int ChipH     = 18;
	private const int BackW     = 52;
	private const int HintH     = 32;
	private const int ResizeKnobSz = 32;
	private const int MoveKnobW    = 20;
	private const int MoveGap      = 6;

	public bool Docked;
	private bool _modFilterOpen;
	private const int DockedSetHeightClosed = 46;
	private const int DockedSetHeightOpen = 230;
	private int DockedSetHeight => _modFilterOpen ? DockedSetHeightOpen : DockedSetHeightClosed;
	private Rectangle _dockMainBuilt, _dockSetBuilt, _dockFavBuilt;

	private float _baseMainLeft, _baseSetLeft, _baseFavLeft;
	private const string CheatHintLine = "Ctrl+LMB: cheat 1 to inventory   Ctrl+RMB: a full stack (Journey Mode)";

	private static string HintFor(BrowseMode mode) =>
		$"Tip: hover an item and press {UILayers.HotkeyLabel(RecipeBrowserKeybinds.HowToObtain)} (how to obtain) or {UILayers.HotkeyLabel(RecipeBrowserKeybinds.UsedAsIngredient)} (used as ingredient)\n" + CheatHintLine;

	protected override void RebuildWindow()
	{
		UIItemGrid.WarmVanillaItemTextures();
		string preservedQuery = _search?.Text ?? "";
		int preservedScroll = _list?.ScrollOffsetPx ?? 0;
		RemoveAllChildren();

		// Three-column layout: [settings] [main] [favorites]
		const float SetWidth = 156f;
		const float SetGap   = 6f;
		float FavWidth = UIFavoritesPanel.PanelWidth;
		const float FavGap   = 6f;

		var root = RootSize();
		float uiW = root.X, uiH = root.Y;

		ResolveSize(uiW, uiH);
		float w = CurW, h = CurH;

		float settingsH = h;
		Rectangle dockMain = default, dockFav = default, dockSet = default;
		if (Docked)
		{
			(dockMain, dockSet, dockFav) = ComputeDockRects();
			_dockMainBuilt = dockMain; _dockSetBuilt = dockSet; _dockFavBuilt = dockFav;
			w = dockMain.Width; h = dockMain.Height;
			settingsH = dockSet.Height;
		}

		float mainLeft = (SetWidth + SetGap - FavGap - FavWidth) / 2f;
		float setLeft  = -(SetGap + w + FavGap + FavWidth) / 2f;
		float favLeft  =  (SetWidth + SetGap + w + FavGap) / 2f;

		const float DragMargin = 8f;
		float gLeft  = uiW / 2f + mainLeft - w / 2f;
		float gRight = gLeft + w;
		float gTop   = uiH / 2f - h / 2f;
		float gBot   = gTop + h;
		OffMinX = DragMargin - gLeft;   OffMaxX = (uiW - DragMargin) - gRight;
		OffMinY = DragMargin - gTop;    OffMaxY = (uiH - DragMargin) - gBot;
		_baseMainLeft = mainLeft; _baseSetLeft = setLeft; _baseFavLeft = favLeft;
		OffsetX = ClampOffset(OffsetX, OffMinX, OffMaxX);
		OffsetY = ClampOffset(OffsetY, OffMinY, OffMaxY);

		_panel = new UITerrariaPanel
		{
			HAlign = Docked ? 0f : 0.5f,
			VAlign = Docked ? 0f : 0.5f,
			Left   = StyleDimension.FromPixels(Docked ? dockMain.X : mainLeft + OffsetX),
			Top    = StyleDimension.FromPixels(Docked ? dockMain.Y : OffsetY),
			Width  = StyleDimension.FromPixels(w),
			Height = StyleDimension.FromPixels(h),
		};
		Append(_panel);

		const int FavHistGap = 8;
		bool showFav  = Config.GTClientConfig.Instance.ShowFavoritesPanel;
		bool showHist = Docked && Config.GTClientConfig.Instance.ShowHistoryPanel;
		bool split    = showFav && showHist;
		float favH    = Docked ? (split ? (dockFav.Height - FavHistGap) / 2f : dockFav.Height) : h;

		_favorites = null;
		_historyPanel = null;

		if (showFav)
		{
			_favorites = new UIFavoritesPanel
			{
				HAlign = Docked ? 0f : 0.5f,
				VAlign = Docked ? 0f : 0.5f,
				Left   = StyleDimension.FromPixels(Docked ? dockFav.X : favLeft + OffsetX),
				Top    = StyleDimension.FromPixels(Docked ? dockFav.Y : OffsetY),
				Width  = StyleDimension.FromPixels(Docked ? dockFav.Width : UIFavoritesPanel.PanelWidth),
				DrawBackground = !Docked,
				HideWhenMagicStorageOpen = Docked,
			};
			_favorites.SetHeight(favH);
			Append(_favorites);
		}

		if (showHist)
		{
			float histTop = showFav ? dockFav.Y + favH + FavHistGap : dockFav.Y;
			float histH   = showFav ? dockFav.Height - favH - FavHistGap : dockFav.Height;
			_historyPanel = new UIFavoritesPanel
			{
				HAlign = 0f,
				VAlign = 0f,
				Left   = StyleDimension.FromPixels(dockFav.X),
				Top    = StyleDimension.FromPixels(histTop),
				Width  = StyleDimension.FromPixels(dockFav.Width),
				DrawBackground = false,
				Title = "History",
				EmptyHint = "lookups appear here",
				IsFavoritesPane = false,
				EntriesSource = () => FavoritesPlayer.Local.History,
				HideWhenMagicStorageOpen = true,
			};
			_historyPanel.SetHeight(histH);
			Append(_historyPanel);
		}

		_search = new UISearchBar(
			placeholder: "Search...  |  RMB to clear",
			onChanged: Refilter)
		{
			Left  = StyleDimension.FromPixels(Docked ? HeaderPad : HeaderPad + MoveKnobW + MoveGap),
			Top   = StyleDimension.FromPixels(HeaderPad),
			Width = StyleDimension.FromPixels(w * 0.5f),
			Height = StyleDimension.FromPixels(SearchH),
		};
		_panel.Append(_search);

		if (!Docked)
		{
			var moveKnob = NewMoveKnob("Drag to move the browser");
			moveKnob.Left   = StyleDimension.FromPixels(HeaderPad);
			moveKnob.Top    = StyleDimension.FromPixels(HeaderPad);
			moveKnob.Width  = StyleDimension.FromPixels(MoveKnobW);
			moveKnob.Height = StyleDimension.FromPixels(SearchH);
			_panel.Append(moveKnob);

			LayoutHeaderButtons(_panel, w, HeaderPad, SearchH);
		}

		_settingsPanel = new UITerrariaPanel
		{
			HAlign = Docked ? 0f : 0.5f,
			VAlign = Docked ? 0f : 0.5f,
			Left   = StyleDimension.FromPixels(Docked ? dockSet.X : setLeft + OffsetX),
			Top    = StyleDimension.FromPixels(Docked ? dockSet.Y : OffsetY),
			Width  = StyleDimension.FromPixels(Docked ? dockSet.Width : SetWidth),
			Height = StyleDimension.FromPixels(settingsH),
		};
		Append(_settingsPanel);

		const int SetPad  = 8;
		int setBtnW       = (int)SetWidth - SetPad * 2;
		int setRowH       = SearchH + 6;
		int setRow0       = 52;

		if (!Docked)
		{
			var setTitle = new UIText("Settings", 0.8f, large: false)
			{
				Left = StyleDimension.FromPixels(SetPad),
				Top  = StyleDimension.FromPixels(8),
			};
			_settingsPanel.Append(setTitle);
		}

		var count = new UIDynamicLabel(() => _mode switch
		{
			BrowseMode.Items      => $"{_filteredItems.Count + _filteredFluids.Count:N0} / {(_allItems?.Count ?? 0) + (_allFluids?.Count ?? 0):N0}",
			BrowseMode.Loot       => $"{_filteredLoot.Count:N0} / {LootRegistry.All.Count:N0}",
			BrowseMode.Equippable => $"{_filteredEquippable.Count:N0} / {(_allEquippable?.Count ?? 0):N0}",
			_                     => $"{_filtered.Count:N0} / {_all.Count:N0}",
		}, 0.85f);
		if (Docked)
		{
			int searchRight = HeaderPad + (int)(w * 0.5f) + 8;
			count.Left   = StyleDimension.FromPixels(searchRight);
			count.Top    = StyleDimension.FromPixels(HeaderPad + 4);
			count.Width  = StyleDimension.FromPixels((int)w - searchRight - HeaderPad);
			count.Height = StyleDimension.FromPixels(20);
			_panel.Append(count);
		}
		else
		{
			count.Left   = StyleDimension.FromPixels(SetPad);
			count.Top    = StyleDimension.FromPixels(28);
			count.Width  = StyleDimension.FromPixels(setBtnW);
			count.Height = StyleDimension.FromPixels(20);
			_settingsPanel.Append(count);
		}

		// Mode selector
		var modeRow = new (BrowseMode Mode, int Item, string Tip)[]
		{
			(BrowseMode.Recipes,    ItemID.WorkBench,        "Recipes"),
			(BrowseMode.Items,      ItemID.StoneBlock,       "Items"),
			(BrowseMode.Loot,       ItemID.CopperShortsword, "Loot"),
			(BrowseMode.Equippable, ItemID.IronHelmet,       "Equippable"),
		};
		const int ModeIcon = 30;
		const int ModeGap  = 6;
		int modeRowY = Docked ? SetPad : setRow0;
		for (int i = 0; i < modeRow.Length; i++)
		{
			var (m, item, tip) = modeRow[i];
			var modeBtn = new UIIconButton(item, () => SetMode(m), () => _mode == m, tip, ModeIcon)
			{
				Left = StyleDimension.FromPixels(SetPad + i * (ModeIcon + ModeGap)),
				Top  = StyleDimension.FromPixels(modeRowY),
			};
			_settingsPanel.Append(modeBtn);
		}

		int filterBtnX = Docked ? SetPad + modeRow.Length * (ModeIcon + ModeGap) : SetPad;
		int filterBtnY = Docked ? modeRowY : setRow0 + setRowH;
		int filterBtnW = Docked ? ModeIcon : setBtnW;
		int filterBtnH = Docked ? ModeIcon : ChipH;
		var filterBtn = new UITextButton(
			() => "...",
			() => { _modFilterOpen = !_modFilterOpen; RelayoutNow(Docked); },
			null,
			"Show or hide recipe filters",
			filterBtnW, filterBtnH)
		{
			Left = StyleDimension.FromPixels(filterBtnX),
			Top  = StyleDimension.FromPixels(filterBtnY),
		};
		_settingsPanel.Append(filterBtn);

		if (Docked)
		{
			var dockCloseBtn = new UITextButton(
				() => "X",
				() => GlobalRecipeBrowserSystem.ToggleDocked(),
				null,
				"Close docked browser",
				ModeIcon, ModeIcon)
			{
				Left = StyleDimension.FromPixels(dockSet.Width - SetPad - ModeIcon),
				Top  = StyleDimension.FromPixels(modeRowY),
			};
			_settingsPanel.Append(dockCloseBtn);
		}

		int spoilerY = Docked ? modeRowY + ModeIcon + 6 : filterBtnY + ChipH + 4;
		int toggleW  = Docked ? dockSet.Width - SetPad * 2 : setBtnW;

		_hideObviousToggle = new UICheckButton(
			label:     () => "Hide obvious",
			isChecked: () => _hideObvious,
			onClick:   ToggleHideObvious,
			tooltip:   "Hide useless recipes (machines recycling, extracting, etc.)",
			height:    SearchH)
		{
			Left  = StyleDimension.FromPixels(SetPad),
			Top   = StyleDimension.FromPixels(spoilerY),
			Width = StyleDimension.FromPixels(toggleW),
		};

		_haveOnlyToggle = new UICheckButton(
			label:     () => "Have ingredients",
			isChecked: () => _haveOnly,
			onClick:   ToggleHaveOnly,
			tooltip:   "Show only recipes you can craft from current inventory",
			height:    SearchH)
		{
			Left  = StyleDimension.FromPixels(SetPad),
			Top   = StyleDimension.FromPixels(spoilerY + setRowH),
			Width = StyleDimension.FromPixels(toggleW),
		};

		_searchIngredientsToggle = new UICheckButton(
			label:     () => "Search ingredients",
			isChecked: () => _searchIngredients,
			onClick:   ToggleSearchIngredients,
			tooltip:   "Also match recipes by their ingredients, not just their results",
			height:    SearchH)
		{
			Left  = StyleDimension.FromPixels(SetPad),
			Top   = StyleDimension.FromPixels(spoilerY + setRowH * 2),
			Width = StyleDimension.FromPixels(toggleW),
		};

		if (_modFilterOpen)
		{
			_settingsPanel.Append(_hideObviousToggle);
			_settingsPanel.Append(_haveOnlyToggle);
			_settingsPanel.Append(_searchIngredientsToggle);
		}

		const int ScrollW = 20;
		int modListTop    = spoilerY + setRowH * 3;
		int modListH      = System.Math.Max(setRowH * 2, (int)settingsH - modListTop - SetPad);
		_modBtnW          = setBtnW - ScrollW - 2;
		_setBtnW          = setBtnW;
		_listTop          = modListTop;
		_listH            = modListH;
		_modList = new UIList
		{
			Left        = StyleDimension.FromPixels(SetPad),
			Top         = StyleDimension.FromPixels(modListTop),
			Width       = StyleDimension.FromPixels(_modBtnW),
			Height      = StyleDimension.FromPixels(modListH),
			ListPadding = 4f,
			ManualSortMethod = _ => { },
		};
		_settingsPanel.Append(_modList);
		_modScroll = new UIScrollbar
		{
			Left   = StyleDimension.FromPixels(SetPad + _modBtnW + 2),
			Top    = StyleDimension.FromPixels(modListTop),
			Width  = StyleDimension.FromPixels(ScrollW),
			Height = StyleDimension.FromPixels(modListH),
		};
		_modList.SetScrollbar(_modScroll);
		RebuildModToggles();

		_equipList = new UIList
		{
			Left        = StyleDimension.FromPixels(SetPad),
			Top         = StyleDimension.FromPixels(modListTop),
			Width       = StyleDimension.FromPixels(_modBtnW),
			Height      = StyleDimension.FromPixels(modListH),
			ListPadding = 4f,
			ManualSortMethod = _ => { },
		};
		_equipScroll = new UIScrollbar
		{
			Left   = StyleDimension.FromPixels(SetPad + _modBtnW + 2),
			Top    = StyleDimension.FromPixels(modListTop),
			Width  = StyleDimension.FromPixels(ScrollW),
			Height = StyleDimension.FromPixels(modListH),
		};
		_equipList.SetScrollbar(_equipScroll);
		BuildEquipToggles();
		ApplySettingsForMode();

		_chipHint = new UIText(HintFor(_mode), 0.78f, large: false)
		{
			Left   = StyleDimension.FromPixels(HeaderPad),
			Top    = StyleDimension.FromPixels(HeaderPad + SearchH + 5),
			Width  = StyleDimension.FromPixels(w - HeaderPad * 2),
			Height = StyleDimension.FromPixels(HintH),
			TextColor = new Color(150, 160, 190),
		};
		_chipFullW = (int)(w - HeaderPad * 2);
		_chipButton = new UITextButton(
			label:    () => _chipLabel,
			onLeft:   () => { if (_filter != BrowseFilter.None) ClearFilter(); },
			tooltip:  "Click to clear the active filter",
			width:    _chipFullW,
			height:   ChipH)
		{
			Left = StyleDimension.FromPixels(HeaderPad),
			Top  = StyleDimension.FromPixels(HeaderPad + SearchH + 5),
		};
		_chipShown = null;
		_backShown = false;
		_backButton = new UITextButton(
			label:    () => "< Back",
			onLeft:   GoBack,
			width:    BackW,
			height:   ChipH)
		{
			Left = StyleDimension.FromPixels(HeaderPad),
			Top  = StyleDimension.FromPixels(HeaderPad + SearchH + 5),
		};
		_backButton.IsVisible = () => _backShown;
		_panel.Append(_backButton);

		void ToggleHideObvious()
		{
			_hideObvious = !_hideObvious;
			Refilter(_search?.Text ?? "");
		}

		void ToggleSearchIngredients()
		{
			_searchIngredients = !_searchIngredients;
			Refilter(_search?.Text ?? "");
		}

		void ToggleHaveOnly()
		{
			_haveOnly = !_haveOnly;
			Refilter(_search?.Text ?? "");
		}

		int listTop = HeaderPad + SearchH + 5 + HintH + 5;
		int scrollInset = ResizeKnobSz;
		_list = new UIRecipeList(() => _filtered, emptyHint: "No recipes match this search")
		{
			Left = StyleDimension.FromPixels(4),
			Top  = StyleDimension.FromPixels(listTop),
			Width  = StyleDimension.FromPixels(w - 8),
			Height = StyleDimension.FromPixels(h - listTop - 4),
			OnStationFilter = s => _search?.SetText("@" + s),
			ScrollBottomInset = scrollInset,
		};
		_list.ScrollOffsetPx = preservedScroll;
		_grid = new UIItemGrid(() => _filteredItems, fluidSource: () => _filteredFluids)
		{
			Left = StyleDimension.FromPixels(4),
			Top  = StyleDimension.FromPixels(listTop),
			Width  = StyleDimension.FromPixels(w - 8),
			Height = StyleDimension.FromPixels(h - listTop - 4),
			ScrollBottomInset = scrollInset,
		};
		_equipGrid = new UIItemGrid(() => _filteredEquippable, emptyHint: "No equippable items match this search")
		{
			Left = StyleDimension.FromPixels(4),
			Top  = StyleDimension.FromPixels(listTop),
			Width  = StyleDimension.FromPixels(w - 8),
			Height = StyleDimension.FromPixels(h - listTop - 4),
			ScrollBottomInset = scrollInset,
		};
		_loot = new UILootList(() => _filteredLoot, emptyHint: "No loot matches this search")
		{
			Left = StyleDimension.FromPixels(4),
			Top  = StyleDimension.FromPixels(listTop),
			Width  = StyleDimension.FromPixels(w - 8),
			Height = StyleDimension.FromPixels(h - listTop - 4),
			ScrollBottomInset = scrollInset,
		};
		ApplyMode();
		UpdateChip();

		if (!Docked)
			LayoutResizeKnob(_panel, w, h, ResizeKnobSz, "Drag to resize the browser");

		if (preservedQuery.Length > 0) _search?.SetText(preservedQuery);
	}

	public void SetDocked(bool docked)
	{
		if (Docked == docked) return;
		Docked = docked;
		MarkGeometryDirty();
	}

	public void RelayoutNow(bool docked)
	{
		Docked = docked;
		ForceRebuild();
	}

	protected override float HeaderDragHeight => Docked ? 0f : base.HeaderDragHeight;

	private (Rectangle main, Rectangle set, Rectangle fav) ComputeDockRects()
	{
		var windows = new List<Rectangle>(UILayers.OpenModalBounds(GlobalRecipeBrowserSystem.LayerNameStr));
		var strip = DockRegions.Recipes(windows);
		var set = DockRegions.SettingsBand(strip, DockedSetHeight);
		var main = DockRegions.RecipesBody(strip, DockedSetHeight);
		var fav = DockRegions.Favorites(windows);
		return (main, set, fav);
	}

	private void ApplyMode()
	{
		if (_panel is null || _list is null || _grid is null || _equipGrid is null || _loot is null) return;

		if (_list.Parent == _panel)      _panel.RemoveChild(_list);
		if (_grid.Parent == _panel)      _panel.RemoveChild(_grid);
		if (_equipGrid.Parent == _panel) _panel.RemoveChild(_equipGrid);
		if (_loot.Parent == _panel)      _panel.RemoveChild(_loot);

		switch (_mode)
		{
			case BrowseMode.Recipes:    _panel.Append(_list); break;
			case BrowseMode.Items:      _panel.Append(_grid); break;
			case BrowseMode.Loot:       _panel.Append(_loot); break;
			case BrowseMode.Equippable: _panel.Append(_equipGrid); break;
		}

		ApplySettingsForMode();
		UpdateChip();
		Refilter(_search?.Text ?? "");

		RaiseResizeKnobToTop(_panel);
	}

	private void ApplySettingsForMode()
	{
		if (_settingsPanel is null || _modList is null || _equipList is null) return;
		bool equip = _mode == BrowseMode.Equippable;

		void Detach(UIElement? e) { if (e is not null && e.Parent == _settingsPanel) _settingsPanel.RemoveChild(e); }

		if (!_modFilterOpen)
		{
			Detach(_modList); Detach(_modScroll);
			Detach(_equipList); Detach(_equipScroll);
			return;
		}

		PositionSettingsLists();

		if (_modList.Parent != _settingsPanel) _settingsPanel.Append(_modList);
		UpdateModScrollVisibility();

		if (equip)
		{
			if (_equipList.Parent != _settingsPanel) _settingsPanel.Append(_equipList);
			UpdateEquipScrollVisibility();
		}
		else
		{
			Detach(_equipList);
			Detach(_equipScroll);
		}
	}

	private void PositionSettingsLists()
	{
		if (_modList is null || _modScroll is null || _equipList is null || _equipScroll is null) return;

		void Geom(UIElement list, UIElement scroll, int top, int height)
		{
			list.Top    = StyleDimension.FromPixels(top);
			list.Height = StyleDimension.FromPixels(height);
			scroll.Top    = StyleDimension.FromPixels(top);
			scroll.Height = StyleDimension.FromPixels(height);
		}

		if (_mode == BrowseMode.Equippable)
		{
			const int Gap = 6;
			int equipH = (int)((_listH - Gap) * 0.55f);
			int modH   = _listH - Gap - equipH;
			Geom(_equipList, _equipScroll, _listTop, equipH);
			Geom(_modList,   _modScroll,   _listTop + equipH + Gap, modH);
		}
		else
		{
			Geom(_modList, _modScroll, _listTop, _listH);
		}
	}

	public void RebuildFromScratch()
	{
		_filter = BrowseFilter.None;
		_filterItem = 0;
		_filterFluid = null;
		_filterFluidLabel = null;
		_filterTagLabel = null;
		_filterTagItems = null;
		_search?.SetText(_lastQuery);
		_hideObvious = _lastHideObvious;
		_searchIngredients = _lastSearchIngredients;
		SetMode(_lastMode);
		if (_modeSwapPending) { _modeSwapPending = false; ApplyMode(); }
		if (!_modOrderReady) { _modOrderReady = true; _modOrder = null; }
		RebuildModToggles();
		RecomputeAll();
	}

	public void ApplyStationFilter(string station)
	{
		if (string.IsNullOrEmpty(station)) return;
		ClearFilter();
		SetMode(BrowseMode.Recipes);
		_search?.SetText("@" + station);
		RecomputeAll();
	}

	public void ApplyItemFilter(int itemType, BrowseFilter filter)
	{
		if (itemType <= 0 || filter == BrowseFilter.None) { ClearFilter(); return; }
		PushHistoryBefore(filter, itemType, null, null);
		SetMode(BrowseMode.Recipes);
		_filter = filter;
		_filterItem = itemType;
		_filterFluid = null;
		_filterFluidLabel = null;
		_filterTagLabel = null;
		_filterTagItems = null;
		_search?.SetText("");
		RecomputeAll();
	}

	public void ApplyFluidFilter(string fluidId, string label, BrowseFilter filter)
	{
		if (string.IsNullOrEmpty(fluidId) || filter == BrowseFilter.None) { ClearFilter(); return; }
		PushHistoryBefore(filter, 0, fluidId, null);
		SetMode(BrowseMode.Recipes);
		_filter = filter;
		_filterItem = 0;
		_filterFluid = fluidId;
		_filterFluidLabel = label;
		_filterTagLabel = null;
		_filterTagItems = null;
		_search?.SetText("");
		RecomputeAll();
	}

	public void ApplyTagFilter(string tagLabel, HashSet<int> items, BrowseFilter filter)
	{
		if (items.Count == 0 || filter == BrowseFilter.None) { ClearFilter(); return; }
		PushHistoryBefore(filter, 0, null, tagLabel);
		SetMode(BrowseMode.Recipes);
		_filter = filter;
		_filterItem = 0;
		_filterFluid = null;
		_filterFluidLabel = null;
		_filterTagLabel = tagLabel;
		_filterTagItems = items;
		_search?.SetText("");
		RecomputeAll();
	}

	private void ClearFilter()
	{
		_history.Clear();
		_filter = BrowseFilter.None;
		_filterItem = 0;
		_filterFluid = null;
		_filterFluidLabel = null;
		_filterTagLabel = null;
		_filterTagItems = null;
		RecomputeAll();
	}

	private void PushHistoryBefore(BrowseFilter filter, int item, string? fluid, string? tagLabel)
	{
		if (_filter == BrowseFilter.None) return;
		if (_filter == filter && _filterItem == item && _filterFluid == fluid && _filterTagLabel == tagLabel) return;
		_history.Add(new FilterState(_filter, _filterItem, _filterFluid,
			_filterFluidLabel, _filterTagLabel, _filterTagItems));
		if (_history.Count > 64) _history.RemoveAt(0);
	}

	public void GoBack()
	{
		if (_history.Count == 0) return;
		var s = _history[_history.Count - 1];
		_history.RemoveAt(_history.Count - 1);
		SetMode(BrowseMode.Recipes);
		_filter = s.Filter;
		_filterItem = s.Item;
		_filterFluid = s.Fluid;
		_filterFluidLabel = s.FluidLabel;
		_filterTagLabel = s.TagLabel;
		_filterTagItems = s.TagItems;
		_search?.SetText("");
		RecomputeAll();
	}

	private void RecomputeAll()
	{
		_all.Clear();
		foreach (var kv in RecipeRegistry.ByStation)
			foreach (var r in kv.Value)
			{
				if (_filter == BrowseFilter.None)
				{
					_all.Add(r);
					continue;
				}
				bool match;
				if (_filterFluid is not null)
				{
					var set = _filter == BrowseFilter.Output
						? Widgets.RecipeRowRenderer.OutputFluidIdsInRecipe(r)
						: Widgets.RecipeRowRenderer.InputFluidIdsInRecipe(r);
					match = set.Contains(_filterFluid);
				}
				else if (_filterTagItems is not null)
				{
					var set = _filter == BrowseFilter.Output
						? Widgets.RecipeRowRenderer.OutputItemTypesInRecipe(r)
						: Widgets.RecipeRowRenderer.InputItemTypesInRecipe(r);
					match = set.Overlaps(_filterTagItems);
				}
				else
				{
					var set = _filter == BrowseFilter.Output
						? Widgets.RecipeRowRenderer.OutputItemTypesInRecipe(r)
						: Widgets.RecipeRowRenderer.InputItemTypesInRecipe(r);
					match = set.Contains(_filterItem);
				}
				if (match) _all.Add(r);
			}
		UpdateChip();
		Refilter(_search?.Text ?? "");
	}

	private void Refilter(string text)
	{
		SyncLocaleCaches();
		if (_mode == BrowseMode.Items)      { RefilterItems(text);      return; }
		if (_mode == BrowseMode.Loot)       { RefilterLoot(text);       return; }
		if (_mode == BrowseMode.Equippable) { RefilterEquippable(text); return; }

		var tokens = RecipeSearch.Tokenize(text);
		bool needText = tokens.Length > 0;
		bool needModFilter = _shownMods.Count > 0;
		bool needFilter = needText || _haveOnly || _hideObvious || needModFilter;

		Dictionary<int, int>? inv  = _haveOnly ? BuildInventoryCounts() : null;
		Dictionary<string, int>? fluids = _haveOnly ? BuildFluidCounts() : null;

		_filtered.Clear();
		if (needFilter)
		{
			foreach (var r in _all)
			{
				if (_hideObvious && IsObviousRecipe(r)) continue;
				if (needModFilter && !_shownMods.Contains(RecipeModName(r))) continue;
				if (needText && !RecipeSearch.Matches(r, tokens, outputsOnly: !_searchIngredients)) continue;
				if (inv is not null && !RecipeCraftableNow(r, inv, fluids!)) continue;
				_filtered.Add(r);
			}
		}
		else
		{
			_filtered.AddRange(_all);
		}

		if (_filtered.Count > 1)
		{
			var sortInv    = inv    ?? BuildInventoryCounts();
			var sortFluids = fluids ?? BuildFluidCounts();
			bool useCraftableKey = !_haveOnly;
			SortByRank(_filtered, tokens, useCraftableKey, sortInv, sortFluids);
		}
	}

	private const int OutputsMissBit = 1 << 16;
	private const int ObviousBit     = 1 << 20;
	private const int MissingMask    = 0xFFFF;

	private static long[]     _sortKeys  = System.Array.Empty<long>();
	private static GTRecipe[] _sortItems = System.Array.Empty<GTRecipe>();

	private static void SortByRank(List<GTRecipe> list, string[] tokens,
		bool useCraftableKey,
		Dictionary<int, int> inv,
		Dictionary<string, int> fluids)
	{
		bool needText = tokens.Length > 0;
		int n = list.Count;
		if (n <= 1) return;

		if (_sortKeys.Length < n)
		{
			_sortKeys  = new long[n];
			_sortItems = new GTRecipe[n];
		}
		var keys  = _sortKeys;
		var items = _sortItems;
		for (int i = 0; i < n; i++)
		{
			var r = list[i];
			items[i] = r;
			int rank = 0;
			if (IsObviousRecipe(r)) rank |= ObviousBit;
			if (needText && !RecipeSearch.MatchesOutputs(r, tokens)) rank |= OutputsMissBit;
			if (useCraftableKey)
			{
				int missing = CountMissingInputs(r, inv, fluids);
				if (missing > MissingMask) missing = MissingMask;
				rank |= missing;
			}
			keys[i] = ((long)rank << 32) | (uint)i;
		}
		System.Array.Sort(keys, items, 0, n);
		list.Clear();
		for (int i = 0; i < n; i++) list.Add(items[i]);
	}

	private static int CountMissingInputs(GTRecipe recipe,
		IReadOnlyDictionary<int, int> inv,
		IReadOnlyDictionary<string, int> fluids)
	{
		int missing = 0;
		if (recipe.Inputs.TryGetValue(ItemRecipeCapability.CAP, out var items))
		{
			foreach (var content in items)
			{
				int needed = CountFor(content);
				if (needed <= 0) continue;
				if (GetItemAvailability(content, inv) != AvailabilityState.Full) missing++;
			}
		}
		if (recipe.Inputs.TryGetValue(FluidRecipeCapability.CAP, out var liquids))
		{
			foreach (var content in liquids)
			{
				if (GetFluidAvailability(content, fluids) != AvailabilityState.Full) missing++;
			}
		}
		return missing;
	}


	private static bool IsObviousRecipe(GTRecipe r)
	{
		var cat = r.CategoryId;
		if (cat is not null && cat.EndsWith("_recycling", System.StringComparison.Ordinal))
			return true;

		string id = r.Id;
		if (id.Length == 0) return false;

		if (id.StartsWith("crafting_shaped/compat_convert_legacy_", System.StringComparison.Ordinal))
			return true;

		// wiremill bundled-wire variants - ingot -> double/quadruple/octal/hex.
		if (id.StartsWith("wiremill/mill_", System.StringComparison.Ordinal))
		{
			if (id.EndsWith("_wire_2", System.StringComparison.Ordinal) ||
			    id.EndsWith("_wire_4", System.StringComparison.Ordinal) ||
			    id.EndsWith("_wire_8", System.StringComparison.Ordinal) ||
			    id.EndsWith("_wire_16", System.StringComparison.Ordinal))
				return true;
		}

		// packer wire-tier conversions (single <-> double <-> quadruple <-> octal <-> hex).
		if (id.StartsWith("packer/pack_", System.StringComparison.Ordinal) &&
		    id.Contains("_wires_", System.StringComparison.Ordinal))
			return true;

		// packer cable-strip - cable -> wire + rubber_plate (pure unpack).
		if (id.StartsWith("packer/strip_", System.StringComparison.Ordinal) &&
		    id.Contains("_cable_gt_", System.StringComparison.Ordinal))
			return true;

		// shapeless wire-tier conversions (workbench equivalent of the packer recipes).
		if (id.Contains("_wire_wire_gt_", System.StringComparison.Ordinal))
			return true;

		// Dust-size conversions: tiny <-> small <-> regular (packer + shapeless).
		// Also fluid-pipe size packing (4 small -> quadruple, 9 -> nonuple).
		if (id.StartsWith("packer/package_", System.StringComparison.Ordinal) ||
		    id.StartsWith("packer/unpackage_", System.StringComparison.Ordinal))
		{
			if (id.EndsWith("_small_dust", System.StringComparison.Ordinal) ||
			    id.EndsWith("_tiny_dust", System.StringComparison.Ordinal))
				return true;
			if (id.EndsWith("_quadruple_pipe", System.StringComparison.Ordinal) ||
			    id.EndsWith("_nonuple_pipe", System.StringComparison.Ordinal))
				return true;
		}
		if (id.StartsWith("shaped/small_dust_", System.StringComparison.Ordinal) ||
		    id.StartsWith("shaped/tiny_dust_", System.StringComparison.Ordinal))
			return true;

		return false;
	}

	private static List<string> ModOrder()
	{
		if (_modOrder is not null) return _modOrder;
		var set = new HashSet<string>();
		foreach (var kv in ContentSamples.ItemsByType)
			if (kv.Key > 0 && kv.Value is not null)
				set.Add(ItemModName(kv.Key));
		set.Add(GregTechMod);
		var list = new List<string>(set);
		list.Sort((a, b) =>
		{
			int ra = ModRank(a), rb = ModRank(b);
			if (ra != rb) return ra.CompareTo(rb);
			return string.Compare(ModDisplayName(a), ModDisplayName(b),
				System.StringComparison.OrdinalIgnoreCase);
		});
		if (list.Count > 0) _modOrder = list;
		return list;
	}

	private static int ModRank(string mod) => mod == VanillaMod ? 0 : mod == GregTechMod ? 1 : 2;

	private void RebuildModToggles()
	{
		if (_modList is null) return;
		_modList.Clear();

		var allBtn = new UICheckButton(
			label:     () => "Show everything",
			isChecked: () => _shownMods.Count == 0,
			onClick:   () => { _shownMods.Clear(); Refilter(_search?.Text ?? ""); },
			tooltip:   "",
			height:    SearchH);
		allBtn.Width = StyleDimension.FromPercent(1f);
		_modList.Add(allBtn);

		foreach (var modName in ModOrder())
		{
			string captured = modName;
			var modBtn = new UICheckButton(
				label:     () => "Show " + ModDisplayName(captured),
				isChecked: () => _shownMods.Contains(captured),
				onClick:   () => ToggleShowMod(captured),
				tooltip:   "",
				height:    SearchH);
			modBtn.Width = StyleDimension.FromPercent(1f);
			_modList.Add(modBtn);
		}
		UpdateModScrollVisibility();
	}

	private void UpdateModScrollVisibility()
	{
		if (_modList is null || _modScroll is null || _settingsPanel is null) return;
		int count = _modList.Count;
		const float ListPadding = 4f;
		float contentH = count * SearchH + (count > 1 ? count * ListPadding : 0f);
		bool need = contentH > _modList.Height.Pixels + 0.5f;
		_modList.Width = StyleDimension.FromPixels(need ? _modBtnW : _setBtnW);
		_modList.Recalculate();
		bool attached = _modScroll.Parent == _settingsPanel;
		if (need && !attached) _settingsPanel.Append(_modScroll);
		else if (!need && attached) _settingsPanel.RemoveChild(_modScroll);
	}

	private void ToggleShowMod(string mod)
	{
		if (!_shownMods.Remove(mod)) _shownMods.Add(mod);
		Refilter(_search?.Text ?? "");
	}

	private static string ModDisplayName(string internalName) => internalName switch
	{
		VanillaMod  => "Vanilla",
		GregTechMod => "GregTech",
		"ModLoader" => "tModLoader",
		_           => ModLoader.TryGetMod(internalName, out var m) ? m.DisplayName : internalName,
	};

	private static string RecipeModName(GTRecipe r)
	{
		if (_recipeModCache.TryGetValue(r, out var cached)) return cached;
		string mod = ClassifyRecipeMod(r);
		_recipeModCache[r] = mod;
		return mod;
	}

	private static string ClassifyRecipeMod(GTRecipe r)
	{
		if (VanillaCraftingBridge.GTToVanilla.TryGetValue(r, out var native))
			return native.Mod?.Name ?? VanillaMod;
		return GregTechMod;
	}

	private static string ItemModName(int type)
	{
		if (type <= 0 || type < ItemID.Count) return VanillaMod;
		if (ContentSamples.ItemsByType.TryGetValue(type, out var item) && item.ModItem is not null)
			return item.ModItem.Mod.Name;
		return VanillaMod;
	}

	private void RefilterLoot(string text)
	{
		var all = LootRegistry.All;
		string[] tokens = RecipeSearch.Tokenize(text);
		bool needText = tokens.Length > 0;

		bool scopeByItem = _filter == BrowseFilter.Output && _filterItem > 0;
		bool scopeByTag  = _filter == BrowseFilter.Output && _filterTagItems is not null;
		bool scopeEmpty  = _filter == BrowseFilter.Input || _filterFluid is not null;
		bool needMod     = _shownMods.Count > 0;

		if (scopeEmpty) { _filteredLoot = new List<LootRegistry.LootEntry>(); return; }

		if (!needText && !scopeByItem && !scopeByTag && !needMod)
		{
			_filteredLoot = new List<LootRegistry.LootEntry>(all);
			return;
		}

		_filteredLoot = new List<LootRegistry.LootEntry>(all.Count);
		foreach (var e in all)
		{
			if (needMod && !_shownMods.Contains(ItemModName(e.TargetItem))) continue;
			if (scopeByItem && e.TargetItem != _filterItem) continue;
			if (scopeByTag  && !_filterTagItems!.Contains(e.TargetItem)) continue;
			if (needText && !LootRegistry.Matches(e, tokens)) continue;
			_filteredLoot.Add(e);
		}

		if (_filteredLoot.Count <= 1) return;
		var ranks = new int[_filteredLoot.Count];
		for (int i = 0; i < _filteredLoot.Count; i++)
			ranks[i] = LootRegistry.MatchesTarget(_filteredLoot[i], tokens) ? 0 : 1;
		for (int i = 1; i < _filteredLoot.Count; i++)
		{
			var e = _filteredLoot[i];
			int rank = ranks[i];
			int j = i - 1;
			while (j >= 0 && ranks[j] > rank)
			{
				_filteredLoot[j + 1] = _filteredLoot[j];
				ranks[j + 1] = ranks[j];
				j--;
			}
			_filteredLoot[j + 1] = e;
			ranks[j + 1] = rank;
		}
	}

	private void RefilterItems(string text)
	{
		var allItems = EnsureItemUniverse();
		string[] tokens = RecipeSearch.Tokenize(text ?? string.Empty);
		bool needText = tokens.Length > 0;
		bool needMod  = _shownMods.Count > 0;
		_filteredItems.Clear();
		_filteredFluids.Clear();
		if (!needText && !needMod)
			_filteredItems.AddRange(allItems);
		else
			foreach (int type in allItems)
			{
				if (needMod && !_shownMods.Contains(ItemModName(type))) continue;
				if (needText)
				{
					string name = ItemNameLower(type) + " " + RecipeSearch.ItemTagText(type);
					if (!MatchesAllTokens(name, tokens)) continue;
				}
				_filteredItems.Add(type);
			}

		if (!needMod || _shownMods.Contains(GregTechMod))
			foreach (var f in EnsureFluidUniverse())
			{
				if (needText && !MatchesAllTokens(f.DisplayName.ToLowerInvariant(), tokens)) continue;
				_filteredFluids.Add(f.Id);
			}
	}

	private static List<FluidType> EnsureFluidUniverse()
	{
		if (_allFluids != null) return _allFluids;
		_allFluids = new List<FluidType>(FluidRegistry.All);
		_allFluids.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
		return _allFluids;
	}

	private static bool MatchesAllTokens(string name, string[] tokens)
	{
		if (name.Length == 0) return false;
		foreach (string t in tokens)
			if (!name.Contains(t)) return false;
		return true;
	}

	private static readonly Dictionary<int, string> _itemNameLowerCache = new();

	private static string? _cacheCulture;
	internal static void SyncLocaleCaches()
	{
		string culture = LanguageManager.Instance.ActiveCulture?.Name ?? "";
		if (culture == _cacheCulture) return;
		_cacheCulture = culture;
		_itemNameLowerCache.Clear();
		RecipeSearch.ClearCache();
	}

	private List<int> EnsureItemUniverse()
	{
		if (_allItems is not null) return _allItems;
		_allItems = new List<int>(ContentSamples.ItemsByType.Count);
		foreach (var kv in ContentSamples.ItemsByType)
			if (kv.Key > 0 && kv.Value != null && !Terraria.ID.ItemID.Sets.Deprecated[kv.Key]) _allItems.Add(kv.Key);
		_allItems.Sort();
		return _allItems;
	}

	private List<int> EnsureEquippableUniverse()
	{
		if (_allEquippable is not null) return _allEquippable;
		_allEquippable = new List<int>();
		foreach (var kv in ContentSamples.ItemsByType)
			if (kv.Key > 0 && kv.Value is not null && EquipCatOf(kv.Key) != EquipCat.None)
				_allEquippable.Add(kv.Key);
		_allEquippable.Sort();
		return _allEquippable;
	}

	public void Warm()
	{
		SyncLocaleCaches();
		EnsureItemUniverse();
		EnsureEquippableUniverse();
		WarmItemNames();
	}

	private static void WarmItemNames()
	{
		foreach (var kv in ContentSamples.ItemsByType)
			if (kv.Key > 0 && kv.Value is not null)
				ItemNameLower(kv.Key);
	}

	private static string ItemNameLower(int type)
	{
		if (_itemNameLowerCache.TryGetValue(type, out var s)) return s;
		string name = string.Empty;
		if (ContentSamples.ItemsByType.TryGetValue(type, out var it) && it is not null)
		{
			name = it.Name ?? string.Empty;
			if (it.ModItem is not null)
				name = name + " " + it.ModItem.Name;
		}
		s = name.ToLowerInvariant();
		_itemNameLowerCache[type] = s;
		return s;
	}


	private static EquipCat ClassifyEquip(Item it)
	{
		var c = EquipCat.None;
		if (it.headSlot >= 0) c |= EquipCat.Helmet;
		if (it.bodySlot >= 0) c |= EquipCat.Shirt;
		if (it.legSlot  >= 0) c |= EquipCat.Pants;
		if (it.dye > 0)       c |= EquipCat.Dye;
		if (it.accessory)     c |= EquipCat.Trinket;
		if (it.mountType != -1)
			c |= (it.mountType < MountID.Sets.Cart.Length && MountID.Sets.Cart[it.mountType])
				? EquipCat.Minecart : EquipCat.Mount;
		if (it.shoot > ProjectileID.None && it.shoot < Main.projHook.Length && Main.projHook[it.shoot])
			c |= EquipCat.Hook;
		if (it.buffType > 0)
		{
			if (it.buffType < Main.vanityPet.Length && Main.vanityPet[it.buffType]) c |= EquipCat.Pet;
			if (it.buffType < Main.lightPet.Length  && Main.lightPet[it.buffType])  c |= EquipCat.LightPet;
		}
		return c;
	}

	private static EquipCat EquipCatOf(int type)
	{
		if (_equipCatCache.TryGetValue(type, out var c)) return c;
		c = ContentSamples.ItemsByType.TryGetValue(type, out var it) && it is not null
			? ClassifyEquip(it) : EquipCat.None;
		_equipCatCache[type] = c;
		return c;
	}

	private void RefilterEquippable(string text)
	{
		var allEquip = EnsureEquippableUniverse();
		string[] tokens = RecipeSearch.Tokenize(text ?? string.Empty);
		bool needText = tokens.Length > 0;
		bool needMod  = _shownMods.Count > 0;
		var shown     = _shownEquip;
		_filteredEquippable.Clear();
		foreach (int type in allEquip)
		{
			var cat = EquipCatOf(type);
			if (shown != EquipCat.None && (cat & shown) == EquipCat.None) continue;
			if (needMod && !_shownMods.Contains(ItemModName(type))) continue;
			if (needText)
			{
				string name = ItemNameLower(type) + " " + RecipeSearch.ItemTagText(type);
				if (!MatchesAllTokens(name, tokens)) continue;
			}
			_filteredEquippable.Add(type);
		}
	}

	private void BuildEquipToggles()
	{
		if (_equipList is null) return;
		_equipList.Clear();

		var allBtn = new UICheckButton(
			label:     () => "Show all types",
			isChecked: () => _shownEquip == EquipCat.None,
			onClick:   () => { _shownEquip = EquipCat.None; Refilter(_search?.Text ?? ""); },
			tooltip:   "",
			height:    SearchH);
		allBtn.Width = StyleDimension.FromPercent(1f);
		_equipList.Add(allBtn);

		foreach (var (cat, name) in _equipCats)
		{
			var captured = cat;
			string captName = name;
			var btn = new UICheckButton(
				label:     () => "Show " + captName,
				isChecked: () => (_shownEquip & captured) != 0,
				onClick:   () => ToggleShowEquip(captured),
				tooltip:   "",
				height:    SearchH);
			btn.Width = StyleDimension.FromPercent(1f);
			_equipList.Add(btn);
		}
	}

	private void ToggleShowEquip(EquipCat cat)
	{
		_shownEquip ^= cat;
		Refilter(_search?.Text ?? "");
	}

	private void UpdateEquipScrollVisibility()
	{
		if (_equipList is null || _equipScroll is null || _settingsPanel is null) return;
		if (_mode != BrowseMode.Equippable)
		{
			if (_equipScroll.Parent == _settingsPanel) _settingsPanel.RemoveChild(_equipScroll);
			return;
		}
		int count = _equipList.Count;
		const float ListPadding = 4f;
		float contentH = count * SearchH + (count > 1 ? count * ListPadding : 0f);
		bool need = contentH > _equipList.Height.Pixels + 0.5f;
		_equipList.Width = StyleDimension.FromPixels(need ? _modBtnW : _setBtnW);
		_equipList.Recalculate();
		bool attached = _equipScroll.Parent == _settingsPanel;
		if (need && !attached) _settingsPanel.Append(_equipScroll);
		else if (!need && attached) _settingsPanel.RemoveChild(_equipScroll);
	}

	private static uint   _snapTick    = uint.MaxValue;
	private static Dictionary<int, int>?    _snapInv;
	private static Dictionary<string, int>? _snapFluids;
	private static void EnsureSnapshot()
	{
		if (_snapTick == UiFrame.Count && _snapInv is not null && _snapFluids is not null) return;
		_snapInv    = BuildInventoryCountsImpl();
		_snapFluids = BuildFluidCountsImpl();
		_snapTick   = UiFrame.Count;
	}
	internal static Dictionary<int, int> InventoryCountsSnapshot()
	{ EnsureSnapshot(); return _snapInv!; }
	internal static Dictionary<string, int> FluidCountsSnapshot()
	{ EnsureSnapshot(); return _snapFluids!; }

	internal static Dictionary<int, int> BuildInventoryCounts() => InventoryCountsSnapshot();
	private  static Dictionary<int, int> BuildInventoryCountsImpl()
	{
		var counts = new Dictionary<int, int>();
		var player = Main.LocalPlayer;
		void Add(Item? it)
		{
			if (it is null || it.IsAir) return;
			counts[it.type] = (counts.TryGetValue(it.type, out int n) ? n : 0) + it.stack;
		}
		void AddArray(Item[]? arr)
		{
			if (arr is null) return;
			foreach (var it in arr) Add(it);
		}

		AddArray(player.inventory);
		Add(Main.mouseItem);
		Add(player.trashItem);

		var seen = new HashSet<Item[]>(ReferenceEqualityComparer.Instance);
		WalkCraftingChests(player, seen, AddArray);

		AddNetworkItems(counts);

		return counts;
	}

	private static void AddNetworkItems(Dictionary<int, int> counts)
	{
		var repo = MeTerminalClient.ActiveRepo;
		if (repo is null) return;
		foreach (var e in repo.GetAllEntries())
		{
			if (e.StoredAmount <= 0 || e.What is not AEItemKey ik) continue;
			int t = ik.GetItem();
			long total = (counts.TryGetValue(t, out int n) ? n : 0) + e.StoredAmount;
			counts[t] = (int)System.Math.Min(total, int.MaxValue);
		}
	}

	private static void WalkCraftingChests(Player player, HashSet<Item[]> seen, System.Action<Item[]?> addArray)
	{
		void AddChest(Chest? c)
		{
			if (c is null || c.item is null) return;
			if (!seen.Add(c.item)) return;
			addArray(c.item);
		}

		if (player.chest != -1)
		{
			Chest? open = player.chest switch
			{
				-2 => player.bank,
				-3 => player.bank2,
				-4 => player.bank3,
				-5 => player.bank4,
				_  => (player.chest >= 0 && player.chest < Main.chest.Length) ? Main.chest[player.chest] : null,
			};
			AddChest(open);
		}

		// Void Bag - skipped when its own interface is the open chest (dedup).
		if (player.useVoidBag() && player.chest != -5) AddChest(player.bank4);

		const float Range = 600f;
		var center = player.Center;
		for (int i = 0; i < Main.chest.Length; i++)
		{
			var c = Main.chest[i];
			if (c is null) continue;
			var pos = new Microsoft.Xna.Framework.Vector2(c.x * 16 + 16, c.y * 16 + 16);
			if (Microsoft.Xna.Framework.Vector2.Distance(pos, center) <= Range) AddChest(c);
		}
	}

	private static bool RecipeCraftableNow(GTRecipe recipe, Dictionary<int, int> inv,
		Dictionary<string, int> fluidsHeld)
	{
		bool hasAnyInput = false;
		if (recipe.Inputs.TryGetValue(ItemRecipeCapability.CAP, out var items))
		{
			foreach (var content in items)
			{
				int needed = CountFor(content);
				if (needed <= 0) continue;
				if (Inner((Ingredient)content.Payload) is IntCircuitIngredient) continue;
				hasAnyInput = true;
				if (!HasAny(content, needed, inv)) return false;
			}
		}
		if (recipe.Inputs.TryGetValue(FluidRecipeCapability.CAP, out var liquids))
		{
			foreach (var content in liquids)
			{
				hasAnyInput = true;
				if (!HasFluid(content, fluidsHeld)) return false;
			}
		}
		return hasAnyInput;
	}

	internal static Dictionary<string, int> BuildFluidCounts() => FluidCountsSnapshot();
	private  static Dictionary<string, int> BuildFluidCountsImpl()
	{
		var fluids = new Dictionary<string, int>();
		var player = Main.LocalPlayer;
		void AddStack(FluidStack stack, int itemStack)
		{
			if (stack.IsEmpty || stack.Type is null) return;
			fluids[stack.Type.Id] =
				(fluids.TryGetValue(stack.Type.Id, out int a) ? a : 0)
				+ stack.Amount * itemStack;
		}
		void Add(Item? it)
		{
			if (it is null || it.IsAir) return;
			var vanilla = VanillaFluidBridge.StackFor(it.type);
			if (!vanilla.IsEmpty) { AddStack(vanilla, it.stack); return; }
			if (it.ModItem is FluidBucketItem bucket && bucket.Fluid is { } gf)
			{
				AddStack(new FluidStack(gf, VanillaFluidBridge.BucketAmount), it.stack);
				return;
			}
			if (it.ModItem is not IFluidHandlerItem handler) return;
			for (int tank = 0; tank < handler.TankCount; tank++)
				AddStack(handler.GetTank(tank), it.stack);
		}
		void AddArray(Item[]? arr)
		{
			if (arr is null) return;
			foreach (var it in arr) Add(it);
		}

		AddArray(player.inventory);
		Add(Main.mouseItem);
		Add(player.trashItem);

		WalkCraftingChests(player, new HashSet<Item[]>(ReferenceEqualityComparer.Instance), AddArray);

		AddNetworkFluids(fluids);

		return fluids;
	}

	private static void AddNetworkFluids(Dictionary<string, int> fluids)
	{
		var repo = MeTerminalClient.ActiveRepo;
		if (repo is null) return;
		foreach (var e in repo.GetAllEntries())
		{
			if (e.StoredAmount <= 0 || e.What is not AEFluidKey fk) continue;
			string id = fk.GetFluid().Id;
			long total = (fluids.TryGetValue(id, out int a) ? a : 0) + e.StoredAmount;
			fluids[id] = (int)System.Math.Min(total, int.MaxValue);
		}
	}

	private static bool HasFluid(Api.Recipe.Content.Content content, Dictionary<string, int> fluids)
	{
		var ing = (Ingredient)content.Payload;
		FluidIngredient? fi = Inner(ing) switch
		{
			FluidIngredient direct        => direct,
			FluidContainerIngredient fc   => fc.Fluid,
			_                             => null,
		};
		if (fi is null) return true;
		int needed = fi.Amount;
		if (needed <= 0) return true;

		if (fi.ExactType is { } exact)
			return fluids.TryGetValue(exact.Id, out int a) && a >= needed;

		foreach (var f in fi.GetFluids())
			if (fluids.TryGetValue(f.Id, out int a) && a >= needed) return true;
		return false;
	}

	private static int CountFor(Api.Recipe.Content.Content content)
	{
		var ing = (Ingredient)content.Payload;
		return ing switch
		{
			SizedIngredient sized       => sized.Amount,
			IntProviderIngredient ipi   => ipi.RollSampledCount(),
			_                           => 1,
		};
	}

	private static bool HasAny(Api.Recipe.Content.Content content, int needed,
		Dictionary<int, int> inv)
	{
		var ing = Inner((Ingredient)content.Payload);
		switch (ing)
		{
			case ItemStackIngredient isi:
				if (isi.ItemType <= 0) return false;
				return inv.TryGetValue(isi.ItemType, out int a) && a >= needed;
			case NBTPredicateIngredient nbt:
				if (nbt.ItemType <= 0) return false;
				return inv.TryGetValue(nbt.ItemType, out int b) && b >= needed;
			case TagIngredient tag:
			{
				var members = tag.GetItems();
				if (members.Count == 0) return false;
				int have = 0;
				foreach (var it in members)
				{
					if (inv.TryGetValue(it.type, out int n)) have += n;
					if (have >= needed) return true;
				}
				return false;
			}
			case IntCircuitIngredient:
				return true;
			default:
				return false;
		}
	}

	private static Ingredient Inner(Ingredient ing) => ing switch
	{
		SizedIngredient sized      => Inner(sized.Inner),
		IntProviderIngredient ipi  => Inner(ipi.Inner),
		_                          => ing,
	};

	internal enum AvailabilityState { None, Partial, Full }

	internal static AvailabilityState GetItemAvailability(
		Api.Recipe.Content.Content content,
		IReadOnlyDictionary<int, int> inv,
		long needMultiplier = 1)
	{
		int baseNeed = CountFor(content);
		if (baseNeed <= 0) return AvailabilityState.None;
		long needed = (long)baseNeed * System.Math.Max(1, needMultiplier);
		var ing = Inner((Ingredient)content.Payload);
		int have = 0;
		switch (ing)
		{
			case ItemStackIngredient isi:
				if (isi.ItemType <= 0) return AvailabilityState.None;
				inv.TryGetValue(isi.ItemType, out have);
				break;
			case NBTPredicateIngredient nbt:
				if (nbt.ItemType <= 0) return AvailabilityState.None;
				inv.TryGetValue(nbt.ItemType, out have);
				break;
			case TagIngredient tag:
			{
				var members = tag.GetItems();
				if (members.Count == 0) return AvailabilityState.None;
				foreach (var it in members)
					if (inv.TryGetValue(it.type, out int n)) have += n;
				break;
			}
			case IntCircuitIngredient:
				return AvailabilityState.None;
			default:
				return AvailabilityState.None;
		}
		if (have >= needed) return AvailabilityState.Full;
		if (have > 0)       return AvailabilityState.Partial;
		return AvailabilityState.None;
	}

	internal static AvailabilityState GetFluidAvailability(
		Api.Recipe.Content.Content content,
		IReadOnlyDictionary<string, int> fluidsHeld,
		long needMultiplier = 1)
	{
		var ing = (Ingredient)content.Payload;
		FluidIngredient? fi = Inner(ing) switch
		{
			FluidIngredient direct      => direct,
			FluidContainerIngredient fc => fc.Fluid,
			_                           => null,
		};
		if (fi is null) return AvailabilityState.None;
		long needed = (long)fi.Amount * System.Math.Max(1, needMultiplier);
		if (needed <= 0) return AvailabilityState.None;

		int have = 0;
		if (fi.ExactType is { } exact)
		{
			fluidsHeld.TryGetValue(exact.Id, out have);
		}
		else
		{
			foreach (var f in fi.GetFluids())
				if (fluidsHeld.TryGetValue(f.Id, out int a) && a > have) have = a;
		}
		if (have >= needed) return AvailabilityState.Full;
		if (have > 0)       return AvailabilityState.Partial;
		return AvailabilityState.None;
	}

	private void UpdateChip()
	{
		if (_panel is null || _chipHint is null || _chipButton is null) return;

		_chipHint.SetText(HintFor(_mode));

		bool filterable = _mode == BrowseMode.Recipes || _mode == BrowseMode.Loot;

		UIElement desired;
		if (!filterable || _filter == BrowseFilter.None)
		{
			desired = _chipHint;
		}
		else
		{
			string verb = _filter == BrowseFilter.Output ? "How to obtain" : "Used as ingredient";
			string name;
			if (_filterFluid is not null)
			{
				name = _filterFluidLabel ?? _filterFluid;
			}
			else if (_filterTagLabel is not null && _filterTagItems is not null)
			{
				name = $"#{_filterTagLabel}  ({_filterTagItems.Count} items)";
			}
			else
			{
				var probe = new Item();
				probe.SetDefaults(_filterItem);
				name = string.IsNullOrEmpty(probe.Name) ? $"item #{_filterItem}" : probe.Name;
			}
			_chipLabel = $"{verb}:  {name}      - click to clear";
			desired = _chipButton;
		}

		_chipPending = desired;
	}

	private void ApplyPendingChipSwap()
	{
		if (_panel is null || _chipPending is null) return;
		if (!ReferenceEquals(_chipPending, _chipShown))
		{
			if (_chipShown is not null) _panel.RemoveChild(_chipShown);
			_panel.Append(_chipPending);
			_chipShown = _chipPending;
		}
		UpdateBackLayout();
	}

	private void UpdateBackLayout()
	{
		if (_chipButton is null) return;
		bool show = ReferenceEquals(_chipShown, _chipButton) && _history.Count > 0;
		if (show == _backShown) return;
		_backShown = show;
		if (show)
		{
			_chipButton.Left  = StyleDimension.FromPixels(HeaderPad + BackW + 4);
			_chipButton.Width = StyleDimension.FromPixels(_chipFullW - BackW - 4);
		}
		else
		{
			_chipButton.Left  = StyleDimension.FromPixels(HeaderPad);
			_chipButton.Width = StyleDimension.FromPixels(_chipFullW);
		}
		_panel?.Recalculate();
	}

	protected override void ApplyOffsetLive()
	{
		if (_panel is null) return;
		_panel.Left = StyleDimension.FromPixels(_baseMainLeft + OffsetX);
		_panel.Top  = StyleDimension.FromPixels(OffsetY);
		if (_settingsPanel is not null)
		{
			_settingsPanel.Left = StyleDimension.FromPixels(_baseSetLeft + OffsetX);
			_settingsPanel.Top  = StyleDimension.FromPixels(OffsetY);
		}
		if (_favorites is not null)
		{
			_favorites.Left = StyleDimension.FromPixels(_baseFavLeft + OffsetX);
			_favorites.Top  = StyleDimension.FromPixels(OffsetY);
		}
		Recalculate();
	}

	private AppliedEnergistics.IMePatternEncodingHost? _selTerm;
	private void RefreshSelectionMode()
	{
		var term = MachineUISystem.CurrentEntity as AppliedEnergistics.IMePatternEncodingHost;
		if (term != null && !term.IsEncodingActive) term = null;
		bool needArm = term != null && (_list?.OnSelectRecipe == null || !ReferenceEquals(term, _selTerm));
		if (ReferenceEquals(term, _selTerm) && !needArm) return;

		_selTerm = term;
		if (_list == null) return;

		if (term != null)
		{
			SetMode(BrowseMode.Recipes);
			if (_modeSwapPending) { _modeSwapPending = false; ApplyMode(); }
			_list.OnSelectRecipe = r =>
			{
				Widgets.MePatternEncodingBar.FillFromRecipe(term, r);
				if (!Docked) GlobalRecipeBrowserSystem.Close();
			};
			_list.IgnoreHeldClick();
		}
		else
		{
			_list.OnSelectRecipe = null;
		}
	}

	protected override void OnModalUpdate(GameTime gameTime)
	{
		if (_modeSwapPending)
		{
			_modeSwapPending = false;
			ApplyMode();
		}
		RefreshSelectionMode();
		ApplyPendingChipSwap();
		if (_haveOnly && ++_haveOnlyTick >= 30)
		{
			_haveOnlyTick = 0;
			Refilter(_search?.Text ?? "");
		}
		if (Docked)
		{
			var (m, s, f) = ComputeDockRects();
			if (m != _dockMainBuilt || s != _dockSetBuilt || f != _dockFavBuilt)
				MarkGeometryDirty();
		}
	}
}
