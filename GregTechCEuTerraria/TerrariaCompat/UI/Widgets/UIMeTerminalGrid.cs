#nullable enable
using System;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Client.Gui.Me.Common;
using GregTechCEuTerraria.AppliedEnergistics.Client.Gui.Me.Search;
using GregTechCEuTerraria.AppliedEnergistics.Client.Gui.Widgets;
using GregTechCEuTerraria.AppliedEnergistics.Helpers;
using GregTechCEuTerraria.AppliedEnergistics.Menu.Me.Common;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Net.Actions;
using GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;
using Terraria.UI.Chat;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

public sealed class UIMeTerminalGrid : UIElement, IScrollSource, ISortSource
{
	private const int CellSize = 44;
	private const int CellPad  = 2;
	private const int Margin   = 6;
	private const int SearchH  = 22;
	private const int ScrollbarWidth = Scrollbar.Width;
	private const float VanillaNativeSlotPixels = 52f;

	private static readonly Item[] _drawSlot = { new() };

	private readonly MeTerminalMachine _term;
	private readonly UISearchBar _search;
	private readonly Repo _repo;

	private SortOrder _sortBy = TerminalSortPersist.SortBy;
	private SortDir _sortDir = TerminalSortPersist.SortDir;
	private ViewItems _viewMode = TerminalSortPersist.ViewMode;

	private int _scroll;
	private readonly Scrollbar _bar = new();

	private static readonly System.Collections.Generic.Dictionary<AEItemKey, bool> _emptyStationCraft = new();
	private System.Collections.Generic.Dictionary<AEItemKey, bool> _stationCraftables = _emptyStationCraft;
	private System.Collections.Generic.List<GridInventoryEntry> _stationExtra = new();
	private int _stationCraftTick = -1;
	private TerminalCraftableView.Mode _stationViewMode = TerminalCraftableView.Mode.DontShow;
	private string _stationSearch = "";
	private System.Collections.Generic.Dictionary<int, long> _netByType = new();
	private System.Collections.Generic.HashSet<int> _stations = new();
	private System.Collections.Generic.HashSet<AEItemKey> _gtCraftables = new();

	public UIMeTerminalGrid(MeTerminalMachine term)
	{
		_term = term;
		_repo = new Repo(this, this);
		MeTerminalClient.Bind(term.Position, _repo);
		term.RequestResync();

		_search = new UISearchBar("Search...   RMB clear   @mod $tag *id", t =>
		{
			_repo.SetSearchString(t ?? "");
			if (TerminalSearchPersist.KeepOnClose) TerminalSearchPersist.Saved = t ?? "";
			if (_repo.IsPaused()) _repo.SetPaused(false);
			else _repo.UpdateView();
		})
		{
			Left   = StyleDimension.FromPixels(Margin),
			Top    = StyleDimension.FromPixels(Margin),
			Width  = new StyleDimension(-(Margin * 2), 1f),
			Height = StyleDimension.FromPixels(SearchH),
		};
		Append(_search);

		if (TerminalSearchPersist.KeepOnClose && TerminalSearchPersist.Saved.Length > 0)
			_search.SetText(TerminalSearchPersist.Saved);
	}

	public void ToggleSearchPersist()
	{
		TerminalSearchPersist.KeepOnClose = !TerminalSearchPersist.KeepOnClose;
		TerminalSearchPersist.Saved = TerminalSearchPersist.KeepOnClose ? _search.Text : "";
	}

	public SortOrder GetSortBy() => _sortBy;
	public SortDir GetSortDir() => _sortDir;
	public ViewItems GetSortDisplay() => _viewMode;
	public TypeFilter GetTypeFilter() => TypeFilter.ALL;

	public int GetCurrentScroll() => 0;

	public override void ScrollWheel(UIScrollWheelEvent evt)
	{
		base.ScrollWheel(evt);
		if (!IsMouseHovering) return;
		Scrollbar.Wheel(evt, ref _scroll);
	}

	protected override void DrawSelf(SpriteBatch sb)
	{
		var outer = GetDimensions().ToRectangle();
		var px = TextureAssets.MagicPixel.Value;
		sb.Draw(px, outer, new Color(20, 22, 50) * 0.45f);

		if (IsMouseHovering)
		{
			Main.LocalPlayer.mouseInterface = true;
			PlayerInput.LockVanillaMouseScroll("GregTechCEuTerraria/MeTerminal");
		}

		var mouse = ModalEscape.PollCursor();

		var content = new Rectangle(
			outer.X + Margin,
			outer.Y + Margin + SearchH + 2,
			outer.Width - Margin * 2 - ScrollbarWidth,
			outer.Height - (Margin * 2 + SearchH + 2));

		_repo.SetPaused(_repo.Size() > 0 && content.Contains(mouse));

		RefreshStationCraftables();

		var rawView = _repo.GetView();
		var pinned = _repo.GetPinnedEntries();
		System.Collections.Generic.IReadOnlyList<GridInventoryEntry> view = rawView;
		if (pinned.Count > 0)
		{
			var combined = new System.Collections.Generic.List<GridInventoryEntry>(pinned.Count + rawView.Count);
			combined.AddRange(pinned);
			combined.AddRange(rawView);
			view = combined;
		}

		if (_stationExtra.Count > 0 && _viewMode != ViewItems.STORED)
		{
			var present = new System.Collections.Generic.HashSet<AEItemKey>();
			foreach (var e in view)
				if (e.What is AEItemKey ik) present.Add(ik);
			var combined = new System.Collections.Generic.List<GridInventoryEntry>(view.Count + _stationExtra.Count);
			combined.AddRange(view);
			foreach (var e in _stationExtra)
				if (e.What is AEItemKey ik && !present.Contains(ik)) combined.Add(e);
			if (combined.Count != view.Count) view = combined;
		}

		bool cursorHeld = !Main.mouseItem.IsAir;

		var cpuSet = IsCraftingTerminal ? _term.Network?.GetCraftables() : null;

		bool leftEdge  = MouseClick.LeftPressed;
		bool rightEdge = MouseClick.RightPressed;

		if (view.Count == 0)
		{
			string emptyMsg = _term.Network is null ? "[Not connected]"
				: _repo.GetAllEntries().Count > 0 ? "No matching items"
				: "Network empty. Put ME cable near\nchests/tanks/Magic Storage Access tiles and\nsetup Storage Bus on a cable to connect\nthem to the network";
			Terraria.Utils.DrawBorderString(sb, emptyMsg,
				new Vector2(content.X + 8, content.Y + 8), Color.LightGray, 0.85f);
			if (cursorHeld && content.Contains(mouse))
			{
				if (leftEdge)  Send(-1, InventoryAction.PICKUP_OR_SET_DOWN);
				if (rightEdge) Send(-1, InventoryAction.SPLIT_OR_PLACE_SINGLE);
			}
			return;
		}

		int step = CellSize + CellPad;
		int cols = Math.Max(1, (content.Width + CellPad) / step);
		int rows = (view.Count + cols - 1) / cols;
		int totalH = rows * step;
		int viewH = content.Height;
		int maxScroll = Math.Max(0, totalH - viewH);
		if (_scroll > maxScroll) _scroll = maxScroll;

		Rectangle trackRect = Rectangle.Empty, thumbRect = Rectangle.Empty;
		if (totalH > viewH)
		{
			int barX = outer.Right - Margin - ScrollbarWidth;
			trackRect = new Rectangle(barX, content.Y, ScrollbarWidth, content.Height);
			thumbRect = _bar.Update(trackRect, maxScroll, (float)viewH / totalH, ref _scroll, mouse);
		}
		bool draggingThisFrame = _bar.Dragging;

		bool inside = content.Contains(mouse);
		int firstRow = (_scroll + step - 1) / step;
		int lastRow  = Math.Min(rows - 1, (_scroll + viewH) / step - 1);

		GridInventoryEntry? hovered = null;
		AEItemKey? hoveredStationKey = null;

		float oldScale = Main.inventoryScale;
		Main.inventoryScale = CellSize / VanillaNativeSlotPixels;
		try
		{
			var font = FontAssets.ItemStack.Value;
			for (int r = firstRow; r <= lastRow; r++)
			{
				int yTop = content.Y - _scroll + r * step;
				for (int c = 0; c < cols; c++)
				{
					int idx = r * cols + c;
					if (idx >= view.Count) break;
					var entry = view[idx];
					int xLeft = content.X + c * step;
					var rect = new Rectangle(xLeft, yTop, CellSize, CellSize);
					if (rect.Bottom < content.Y || rect.Y > content.Bottom) continue;

					bool isHover = !draggingThisFrame && inside && rect.Contains(mouse);
					if (isHover) hovered = entry;

					if (entry.What is AEItemKey itemKey)
					{
						bool cpu = entry.Craftable && (cpuSet == null || cpuSet.Contains(itemKey));
						bool stationCraft = !cpu && _stationCraftables.ContainsKey(itemKey);
						bool completable = stationCraft && _stationCraftables[itemKey];
						bool gtCraft = !cpu && !stationCraft && _gtCraftables.Contains(itemKey);
						bool zeroStock = entry.StoredAmount == 0;
						bool grey = zeroStock && ((stationCraft && !completable) || gtCraft);

						var stackView = itemKey.GetReadOnlyStack().Clone();
						stackView.stack = 1;
						_drawSlot[0] = stackView;
						if (isHover)
						{
							ItemSlot.OverrideHover(_drawSlot, ItemSlot.Context.CraftingMaterial, 0);
							ItemSlot.MouseHover(_drawSlot, ItemSlot.Context.CraftingMaterial, 0);
							if (stationCraft || gtCraft) hoveredStationKey = itemKey;
							BrowserHover.SetItem(itemKey.GetItem());
						}
						ItemSlot.Draw(sb, _drawSlot, ItemSlot.Context.CraftingMaterial, 0, new Vector2(rect.X, rect.Y));

						if (grey)
							sb.Draw(px, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4),
								new Color(48, 50, 58) * 0.6f);

						if (cpu)
						{
							ChatManager.DrawColorCodedStringWithShadow(sb, font, "*",
								new Vector2(rect.X + 3, rect.Y - 1), new Color(120, 255, 120),
								0f, Vector2.Zero, new Vector2(0.9f));
							if (zeroStock)
							{
								var csize = ChatManager.GetStringSize(font, "Craft", new Vector2(0.7f));
								ChatManager.DrawColorCodedStringWithShadow(sb, font, "Craft",
									new Vector2(rect.X + (rect.Width - csize.X) / 2f, rect.Bottom - 15),
									new Color(255, 220, 60), 0f, Vector2.Zero, new Vector2(0.7f));
							}
						}
						else if (stationCraft)
						{
							ChatManager.DrawColorCodedStringWithShadow(sb, font, "*",
								new Vector2(rect.X + 3, rect.Y - 1),
								completable ? new Color(255, 220, 60) : new Color(150, 150, 160),
								0f, Vector2.Zero, new Vector2(0.9f));
						}
					}
					else if (entry.What is AEFluidKey fluidKey)
					{
						int mb = (int)Math.Min(entry.StoredAmount, int.MaxValue);
						BrowserFluidSlot.Draw(sb, rect, fluidKey.GetFluid(), amountMb: 0);
						if (isHover)
						{
							BrowserFluidSlot.EmitTooltip(fluidKey.GetFluid(), mb);
							var f = fluidKey.GetFluid();
							BrowserHover.SetFluid(f.Id, f.DisplayName);
						}
					}

					if (entry.StoredAmount > 0)
					{
						string text = UINumberFormat.Amount(entry.What, entry.StoredAmount);
						const float overlayScale = 0.7f;
						var size = ChatManager.GetStringSize(font, text, new Vector2(overlayScale));
						var pos = new Vector2(rect.Right - size.X - 3, rect.Bottom - 15);
						ChatManager.DrawColorCodedStringWithShadow(sb, font, text, pos,
							Color.White, 0f, Vector2.Zero, new Vector2(overlayScale));
					}
				}
			}
		}
		finally
		{
			Main.inventoryScale = oldScale;
		}

		if (hoveredStationKey != null)
			PushStationRecipePreview(hoveredStationKey);

		bool middleEdge = MouseClick.MiddlePressed;
		if (!draggingThisFrame && inside)
		{
			bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
			bool alt = Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt);
			bool ctrl = Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl);
			if (hovered != null && alt && (leftEdge || rightEdge))
			{
				if (hovered.What is AEItemKey fav) FavoritesPlayer.Local.BringItemToFront(fav.GetItem());
				else if (hovered.What is AEFluidKey favF)
				{
					var f = favF.GetFluid();
					FavoritesPlayer.Local.BringFluidToFront(f.Id, f.DisplayName);
				}
			}
			else if (hovered != null)
			{
				bool cpu = hovered.Craftable && (cpuSet == null || cpuSet.Contains(hovered.What!));
				bool station = !cpu && hovered.What is AEItemKey stKey
					&& (_stationCraftables.ContainsKey(stKey) || _gtCraftables.Contains(stKey));

				if ((cpu || station) && hovered.StoredAmount > 0)
					MeCraft.CraftWarningTooltipGlobal.PushInfo("MMB - request crafting");

				if (ctrl && leftEdge && Main.GameModeInfo.IsJourneyMode && hovered.StoredAmount > 0 && hovered.What is AEItemKey)
					Send(hovered.Serial, InventoryAction.CREATIVE_DUPLICATE);
				else if (station && (middleEdge || (hovered.StoredAmount == 0 && leftEdge && !cursorHeld)))
					MeCraft.MeStationCraftSystem.OpenFor(_term.Position, ((AEItemKey)hovered.What!).GetItem());
				else if (cpu && (middleEdge || (hovered.StoredAmount == 0 && leftEdge && !cursorHeld)))
					MeCraft.MeCraftSystem.OpenFor(_term.Position, hovered.What!, 1);
				else if (leftEdge && (hovered.StoredAmount > 0 || cursorHeld))
					Send(hovered.Serial, shift ? InventoryAction.SHIFT_CLICK : InventoryAction.PICKUP_OR_SET_DOWN);
				else if (rightEdge && (hovered.StoredAmount > 0 || cursorHeld))
					Send(hovered.Serial, InventoryAction.SPLIT_OR_PLACE_SINGLE);
			}
			else if (cursorHeld)
			{
				if (leftEdge)  Send(-1, InventoryAction.PICKUP_OR_SET_DOWN);
				if (rightEdge) Send(-1, InventoryAction.SPLIT_OR_PLACE_SINGLE);
			}
		}

		if (totalH > viewH)
			_bar.Draw(sb, trackRect, thumbRect, mouse);
	}

	public SortOrder SortBy => _sortBy;
	public SortDir Dir => _sortDir;
	public ViewItems ViewMode => _viewMode;

	public void CycleSort()
	{
		_sortBy = _sortBy switch
		{
			SortOrder.NAME => SortOrder.AMOUNT,
			SortOrder.AMOUNT => SortOrder.MOD,
			_ => SortOrder.NAME,
		};
		TerminalSortPersist.SortBy = _sortBy;
		_repo.UpdateView();
	}

	public void ToggleDir()
	{
		_sortDir = _sortDir == SortDir.ASCENDING ? SortDir.DESCENDING : SortDir.ASCENDING;
		TerminalSortPersist.SortDir = _sortDir;
		_repo.UpdateView();
	}

	public void CycleView()
	{
		_viewMode = _viewMode switch
		{
			ViewItems.ALL => ViewItems.STORED,
			ViewItems.STORED => ViewItems.CRAFTABLE,
			_ => ViewItems.ALL,
		};
		TerminalSortPersist.ViewMode = _viewMode;
		_repo.UpdateView();
	}

	private bool IsCraftingTerminal =>
		_term is IMeCraftingHost h && h.IsCraftingActive;

	private int _previewType = -1;
	private long _previewTick = -1;
	private MeCraft.StationRecipePreview? _previewCache;

	private void PushStationRecipePreview(AEItemKey key)
	{
		int type = key.GetItem();
		if (_previewType != type || UiFrame.Count - _previewTick >= 6)
		{
			_previewCache = BuildStationPreview(type);
			_previewType = type;
			_previewTick = UiFrame.Count;
		}
		if (_previewCache != null)
			MeCraft.CraftWarningTooltipGlobal.PushStationRecipe(_previewCache);
	}

	private MeCraft.StationRecipePreview? BuildStationPreview(int itemType)
	{
		if (!GtRecipeIndex.ByOutput().TryGetValue(itemType, out var all) || all.Count == 0) return null;

		var mode = TerminalCraftableView.Current;
		var ordered = new System.Collections.Generic.List<Api.Recipe.GTRecipe>();
		foreach (var r in all)
		{
			bool isVanilla = GtRecipeIndex.TryResolveVanilla(r, out var vr, out _);
			bool include = mode switch
			{
				TerminalCraftableView.Mode.ShowAllGregtech => true,
				TerminalCraftableView.Mode.ShowAll => isVanilla && Terminal.StationCapabilities.IsSupportable(vr),
				_ => isVanilla && RecipeNetworkCrafting.StationsSatisfy(vr, _stations),
			};
			if (include) ordered.Add(r);
		}
		if (ordered.Count == 0) return null;

		int max = MeCraft.CraftWarningTooltipGlobal.MaxRecipes;
		bool hasMore = ordered.Count > max;
		var shown = hasMore ? ordered.GetRange(0, max) : ordered;
		return new MeCraft.StationRecipePreview
		{
			GtRecipes = shown.ToArray(),
			Covered = new System.Collections.Generic.HashSet<int>(_stations),
			HasMore = hasMore,
		};
	}

	private void RefreshStationCraftables()
	{
		if (TerminalCraftableView.Current == TerminalCraftableView.Mode.DontShow
			|| _term is not IMeCraftingHost chost || !chost.IsCraftingActive)
		{
			_stationCraftables = _emptyStationCraft;
			_stationExtra = _emptyExtra;
			if (_gtCraftables.Count > 0) _gtCraftables = new System.Collections.Generic.HashSet<AEItemKey>();
			return;
		}

		if (TerminalCraftableView.Current != _stationViewMode)
		{
			_stationViewMode = TerminalCraftableView.Current;
			_stationCraftTick = -1;
		}
		if (_repo.GetSearchString() != _stationSearch)
		{
			_stationSearch = _repo.GetSearchString();
			_stationCraftTick = -1;
		}

		int now = (int)(UiFrame.Count / 20);
		if (_stationCraftTick == now && !ReferenceEquals(_stationCraftables, _emptyStationCraft)) return;
		_stationCraftTick = now;

		_stations = chost.Crafting.StationTiles();
		_netByType = new System.Collections.Generic.Dictionary<int, long>();
		foreach (var e in _repo.GetAllEntries())
			if (e.StoredAmount > 0 && e.What is AEItemKey ik)
				_netByType[ik.GetItem()] = (_netByType.TryGetValue(ik.GetItem(), out var c) ? c : 0) + e.StoredAmount;

		var mode = TerminalCraftableView.Current;
		bool showAll = mode == TerminalCraftableView.Mode.ShowAll || mode == TerminalCraftableView.Mode.ShowAllGregtech;
		bool showGt = mode == TerminalCraftableView.Mode.ShowAllGregtech;

		var dict = new System.Collections.Generic.Dictionary<AEItemKey, bool>();
		int count = Terraria.Recipe.numRecipes;
		for (int i = 0; i < count; i++)
		{
			var r = Main.recipe[i];
			if (r.createItem.IsAir) continue;
			bool include = mode switch
			{
				TerminalCraftableView.Mode.ShowAllGregtech => true,
				TerminalCraftableView.Mode.ShowAll => Terminal.StationCapabilities.IsSupportable(r),
				_ => RecipeNetworkCrafting.StationsSatisfy(r, _stations),
			};
			if (!include) continue;
			var key = KeyForType(r.createItem);
			if (key == null) continue;
			bool completable = RecipeNetworkCrafting.IsCraftable(r, _netByType, _stations);
			if (dict.TryGetValue(key, out var prev)) { if (!prev && completable) dict[key] = true; }
			else dict[key] = completable;
		}
		_stationCraftables = dict;

		var search = new RepoSearch();
		search.SetSearchString(_stationSearch);
		long serial = -1;
		var extra = new System.Collections.Generic.List<GridInventoryEntry>();
		foreach (var kv in dict)
		{
			if (!showAll && !kv.Value) continue;
			var e = new GridInventoryEntry(serial--, kv.Key, 0, 0, true);
			if (search.Matches(e)) extra.Add(e);
		}

		var gt = new System.Collections.Generic.HashSet<AEItemKey>();
		if (showGt)
			foreach (var (type, key) in GtRecipeIndex.OutputKeys())
			{
				if (dict.ContainsKey(key) || _netByType.ContainsKey(type)) continue;
				gt.Add(key);
				var e = new GridInventoryEntry(serial--, key, 0, 0, false);
				if (search.Matches(e)) extra.Add(e);
			}
		_gtCraftables = gt;

		var keyed = new System.Collections.Generic.List<(string name, GridInventoryEntry e)>(extra.Count);
		foreach (var e in extra)
			keyed.Add((((AEItemKey)e.What!).GetReadOnlyStack().Name, e));
		keyed.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
		extra.Clear();
		foreach (var (_, e) in keyed) extra.Add(e);
		_stationExtra = extra;
	}

	private static readonly System.Collections.Generic.List<GridInventoryEntry> _emptyExtra = new();

	private static readonly System.Collections.Generic.Dictionary<int, AEItemKey?> _keyByType = new();
	private static AEItemKey? KeyForType(Item createItem)
	{
		if (_keyByType.TryGetValue(createItem.type, out var k)) return k;
		k = AEItemKey.Of(createItem);
		_keyByType[createItem.type] = k;
		return k;
	}

	private void Send(long serial, InventoryAction action)
	{
		MachineActions.Send(MeTerminalAction.OfGrid(serial, action, Main.mouseItem), _term);
		SoundEngine.PlaySound(SoundID.Grab);
	}

}
