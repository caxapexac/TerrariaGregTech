#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;

public sealed class StationCraftWindow : UIModalWindow
{
	private Point16 _termPos;
	private int _itemType;
	private readonly List<(GTRecipe gt, Terraria.Recipe? rec, int index)> _recipes = new();
	private int _selected;
	private long _amount = 1;
	private long _minAmount = 1;

	private Dictionary<int, long> _netByType = new();
	private Dictionary<int, int> _haveInt = new();
	private HashSet<int> _stations = new();
	private int _netTick = -1;
	private GTRecipe? _pendingAdd;

	public void Bind(Point16 termPos, int itemType)
	{
		_termPos = termPos;
		_itemType = itemType;
		_recipes.Clear();
		if (GtRecipeIndex.ByOutput().TryGetValue(itemType, out var all))
			foreach (var g in all)
			{
				bool ok = GtRecipeIndex.TryResolveVanilla(g, out var rec, out int index);
				_recipes.Add((g, ok ? rec : null, ok ? index : -1));
			}
		RefreshNetwork();
		_selected = FirstCraftableOrZero();
		long yield = _recipes.Count > 0 && _recipes[_selected].rec is { } sr ? Math.Max(1, sr.createItem.stack) : 1;
		_amount = yield;
		RemoveAllChildren();
		BuildPanel();
	}

	public void Unbind()
	{
		UITextField.UnfocusAll();
		RemoveAllChildren();
		_recipes.Clear();
	}

	public override void Update(GameTime gameTime)
	{
		if (_pendingAdd is { } gt) { _pendingAdd = null; AddPattern(gt); return; }
		int now = (int)(UiFrame.Count / 6);
		if (now != _netTick) { _netTick = now; RefreshNetwork(); }
		base.Update(gameTime);
	}

	private void RefreshNetwork()
	{
		var next = new Dictionary<int, long>();
		var repo = MeTerminalClient.RepoFor(_termPos);
		if (repo != null)
			foreach (var e in repo.GetAllEntries())
				if (e.StoredAmount > 0 && e.What is AEItemKey ik)
					next[ik.GetItem()] = (next.TryGetValue(ik.GetItem(), out var c) ? c : 0) + e.StoredAmount;
		_netByType = next;
		var ints = new Dictionary<int, int>(next.Count);
		foreach (var kv in next) ints[kv.Key] = (int)Math.Min(int.MaxValue, kv.Value);
		_haveInt = ints;
		_stations = TileEntity.ByPosition.TryGetValue(_termPos, out var te) && te is IMeCraftingHost h
			? h.Crafting.StationTiles()
			: new HashSet<int>();
	}

	private static long RowYield(GTRecipe gt)
	{
		foreach (var (_, c) in gt.GetItemOutputs()) return Math.Max(1, c);
		return 1;
	}

	private bool Craftable(int i) =>
		_recipes[i].rec is { } r && RecipeNetworkCrafting.IsCraftable(r, _netByType, _stations);

	private IMePatternEncodingHost? EncodingHost() =>
		TileEntity.ByPosition.TryGetValue(_termPos, out var te) && te is IMePatternEncodingHost h ? h : null;

	private void AddPattern(GTRecipe gt)
	{
		if (EncodingHost() is not { } host) return;
		MePatternEncodingBar.FillFromRecipe(host, gt);
		MeStationCraftSystem.Close();
	}

	private int FirstCraftableOrZero()
	{
		for (int i = 0; i < _recipes.Count; i++)
			if (Craftable(i)) return i;
		return 0;
	}

	public void SelectRecipe(int idx)
	{
		if (idx < 0 || idx >= _recipes.Count) return;
		_selected = idx;
	}

	private void SetAmount(long v) => _amount = Math.Clamp(v, _minAmount, int.MaxValue);

	private void SetMax()
	{
		if (_recipes.Count == 0 || _recipes[_selected].rec is not { } recipe) return;
		long crafts = RecipeNetworkCrafting.MaxCrafts(recipe, _netByType, _stations);
		long yield = Math.Max(1, recipe.createItem.stack);
		if (crafts > 0) SetAmount(crafts * yield);
	}

	private bool CanAffordSelected()
	{
		if (_recipes.Count == 0 || _recipes[_selected].rec is not { } recipe) return false;
		long yield = Math.Max(1, recipe.createItem.stack);
		long wantCrafts = (_amount + yield - 1) / yield;
		return RecipeNetworkCrafting.MaxCrafts(recipe, _netByType, _stations) >= wantCrafts;
	}

	private void Craft()
	{
		if (_recipes.Count == 0) return;
		var (_, rec, index) = _recipes[_selected];
		if (rec is null || index < 0) return;
		MeStationCraftPackets.Request(_termPos, index, (int)Math.Clamp(_amount, 1, int.MaxValue), _itemType);
	}

	private void BuildPanel()
	{
		const int W = 480, H = 410, sh = 28;
		var panel = new UITerrariaPanel
		{
			Width = StyleDimension.FromPixels(W),
			Height = StyleDimension.FromPixels(H),
			HAlign = 0.5f,
			VAlign = 0.4f,
		};

		panel.Append(new UIText("Request Crafting", 1.0f)
		{ Left = StyleDimension.FromPixels(14), Top = StyleDimension.FromPixels(12) });

		var probe = new Item();
		probe.SetDefaults(_itemType);
		panel.Append(new UIText(probe.Name, 0.8f)
		{ Left = StyleDimension.FromPixels(14), Top = StyleDimension.FromPixels(36) });

		panel.Append(new RecipeListView(this)
		{
			Left = StyleDimension.FromPixels(12),
			Top = StyleDimension.FromPixels(58),
			Width = StyleDimension.FromPixels(W - 24),
			Height = StyleDimension.FromPixels(248),
		});

		int y = 318;
		Step(panel, "-100", -100, 20, y, 42, sh);
		Step(panel, "-10", -10, 65, y, 42, sh);
		Step(panel, "-1", -1, 110, y, 34, sh);
		panel.Append(new UITextField(
			current: () => _amount.ToString(),
			onConfirm: txt => { if (long.TryParse(txt, out var v)) SetAmount(v); },
			maxLength: 10,
			filter: ch => ch >= '0' && ch <= '9',
			placeholder: "1")
		{
			Left = StyleDimension.FromPixels(147),
			Top = StyleDimension.FromPixels(y),
			Width = StyleDimension.FromPixels(90),
			Height = StyleDimension.FromPixels(sh),
		});
		Step(panel, "+1", +1, 240, y, 34, sh);
		Step(panel, "+10", +10, 277, y, 42, sh);
		Step(panel, "+100", +100, 322, y, 42, sh);
		panel.Append(new UITextButton(
			label: () => "Max",
			onLeft: SetMax,
			tooltip: "Set to the maximum craftable with the current network contents",
			width: 96, height: sh, textScale: 0.85f)
		{ Left = StyleDimension.FromPixels(370), Top = StyleDimension.FromPixels(y) });

		panel.Append(new UITextButton(
			label: () => "Craft",
			onLeft: Craft,
			tooltip: null,
			width: 160, height: 34, textScale: 0.95f)
		{
			Left = StyleDimension.FromPixels((W - 160) / 2),
			Top = StyleDimension.FromPixels(H - 44),
			IsDisabled = () => !CanAffordSelected(),
		});

		Append(panel);
	}

	private void Step(UIElement parent, string label, int delta, int x, int y, int w, int h)
	{
		parent.Append(new UITextButton(
			label: () => label,
			onLeft: () => SetAmount(_amount + delta),
			tooltip: null,
			width: w, height: h, textScale: 0.85f)
		{ Left = StyleDimension.FromPixels(x), Top = StyleDimension.FromPixels(y) });
	}

	private sealed class RecipeListView : UIElement
	{
		private readonly StationCraftWindow _w;
		private int _scroll;
		private readonly Scrollbar _bar = new();
		private static readonly int RowH = RecipeRowRenderer.RowHeight + 2;

		public RecipeListView(StationCraftWindow w) => _w = w;

		public override void ScrollWheel(UIScrollWheelEvent evt)
		{
			base.ScrollWheel(evt);
			if (!IsMouseHovering) return;
			Scrollbar.Wheel(evt, ref _scroll, RowH);
		}

		protected override void DrawSelf(SpriteBatch sb)
		{
			var b = GetDimensions().ToRectangle();
			var px = TextureAssets.MagicPixel.Value;
			sb.Draw(px, b, new Color(20, 22, 50) * 0.5f);
			if (IsMouseHovering)
			{
				Main.LocalPlayer.mouseInterface = true;
				PlayerInput.LockVanillaMouseScroll("GregTechCEuTerraria/StationCraftList");
			}

			var recipes = _w._recipes;
			int sbW = Scrollbar.Width;
			int viewRows = Math.Max(1, b.Height / RowH);
			int maxScroll = Math.Max(0, recipes.Count - viewRows);
			var mouse = ModalEscape.PollCursor();
			bool showBar = recipes.Count > viewRows;
			Rectangle track = Rectangle.Empty, thumb = Rectangle.Empty;
			if (showBar)
			{
				track = new Rectangle(b.Right - sbW, b.Y, sbW, b.Height);
				thumb = _bar.Update(track, maxScroll, (float)viewRows / recipes.Count, ref _scroll, mouse);
			}
			if (_scroll > maxScroll) _scroll = maxScroll;

			int rowW = b.Width - (showBar ? sbW : 0);
			bool click = MouseClick.LeftPressed && !_bar.Dragging;
			int gutter = _w.EncodingHost() != null ? RecipeRowRenderer.SelectGutter : 0;

			RecipeRowRenderer.CoveredStationTiles = _w._stations;
			RecipeRowRenderer.HaveCountsOverride = _w._haveInt;
			try
			{
				for (int r = 0; r < viewRows; r++)
				{
					int idx = r + _scroll;
					if (idx >= recipes.Count) break;
					var full = new Rectangle(b.X, b.Y + r * RowH, rowW, RecipeRowRenderer.RowHeight);
					var content = gutter > 0 ? new Rectangle(full.X + gutter, full.Y, full.Width - gutter, full.Height) : full;
					bool sel = idx == _w._selected;
					bool craftable = _w.Craftable(idx);
					bool hovRow = full.Contains(mouse);

					Color bg = sel ? new Color(60, 110, 70) * 0.85f
						: hovRow ? new Color(40, 44, 80) * 0.7f
						: new Color(30, 34, 64) * 0.5f;
					sb.Draw(px, full, bg);

					long yield = RowYield(recipes[idx].gt);
					long crafts = (_w._amount + yield - 1) / yield;
					RecipeRowRenderer.Draw(sb, content, recipes[idx].gt, Color.White, craftButton: false, amountScale: crafts);

					if (!craftable) sb.Draw(px, content, new Color(28, 30, 38) * 0.5f);
					if (sel) DrawBorder(sb, px, full, new Color(120, 220, 140));

					if (gutter > 0)
					{
						var plus = RecipeRowRenderer.SelectButtonRect(full);
						bool overPlus = plus.Contains(mouse);
						RecipeRowRenderer.DrawSelectButton(sb, plus, overPlus);
						if (overPlus)
						{
							Main.LocalPlayer.mouseInterface = true;
							Main.instance.MouseText("Add this recipe as a pattern");
							if (click) { _w._pendingAdd = recipes[idx].gt; break; }
							continue;
						}
					}

					if (content.Contains(mouse))
					{
						bool overCell = RecipeRowRenderer.HandleQueryClick(recipes[idx].gt, content, mouse,
							_bar.Dragging, !_bar.Dragging, GlobalRecipeBrowserSystem.OpenStation);
						if (click && !overCell) _w.SelectRecipe(idx);
					}
				}
			}
			finally
			{
				RecipeRowRenderer.CoveredStationTiles = null;
				RecipeRowRenderer.HaveCountsOverride = null;
			}

			if (showBar) _bar.Draw(sb, track, thumb, mouse);
		}

		private static void DrawBorder(SpriteBatch sb, Texture2D px, Rectangle r, Color c)
		{
			sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 1), c);
			sb.Draw(px, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
			sb.Draw(px, new Rectangle(r.X, r.Y, 1, r.Height), c);
			sb.Draw(px, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
		}
	}
}
