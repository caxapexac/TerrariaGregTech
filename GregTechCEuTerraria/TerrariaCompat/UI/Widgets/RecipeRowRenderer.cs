#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.TerrariaCompat.Tiles.CraftingStations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using RecipeContent = GregTechCEuTerraria.Api.Recipe.Content.Content;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

public static class RecipeRowRenderer
{
	public const int RowHeight = 40;
	public const int CellSize  = 36;
	public const int CellPad   = 2;
	private const int ArrowSize = 24;
	private const int LabelHeight = 10;
	private const int TagMembersListed = 12;

	private static readonly Item[] TempSlot = { new() };
	private static Mod? _mod;
	private static Mod? GetMod() => _mod ??= ModLoader.GetMod("GregTechCEuTerraria");
	private const int StationIconSize = 22;
	private const int CraftButtonWidth  = 52;
	private const int CraftButtonHeight = 22;

	private static IReadOnlyDictionary<int, int>?    _invSnapshot;
	private static IReadOnlyDictionary<string, int>? _fluidSnapshot;

	private static readonly Dictionary<string, int> EmptyFluids = new();

	public static IReadOnlyDictionary<int, int>? HaveCountsOverride;

	private static void EnsureInventorySnapshot()
	{
		if (HaveCountsOverride != null)
		{
			_invSnapshot   = HaveCountsOverride;
			_fluidSnapshot = EmptyFluids;
			return;
		}
		_invSnapshot   = GlobalRecipeBrowserState.InventoryCountsSnapshot();
		_fluidSnapshot = GlobalRecipeBrowserState.FluidCountsSnapshot();
	}

	private static readonly Color TintFull    = new(60, 230, 60);
	private static readonly Color TintPartial = new(240, 200, 40);

	public static HashSet<int>? CoveredStationTiles;

	private static Color? StationOverlay(int tile, string ownStation)
	{
		var covered = CoveredStationTiles;
		if (covered == null) return null;
		if (tile > 0) return covered.Contains(tile) ? null : new Color(220, 55, 45) * 0.5f;
		return VanillaCraftingBridge.IsHandStation(ownStation) ? null : new Color(220, 55, 45) * 0.5f;
	}

	public static int MeasureWidth(GTRecipe recipe)
	{
		var inputs  = AllInputs(recipe);
		var outputs = AllOutputs(recipe);
		int cells = inputs.Count + outputs.Count;
		return cells * (CellSize + CellPad) + ArrowSize + 12;
	}

	public static void Draw(SpriteBatch sb, Rectangle bounds, GTRecipe recipe, Color lightColor, bool craftButton = true, long amountScale = 1)
	{
		EnsureInventorySnapshot();
		if (amountScale < 1) amountScale = 1;
		int x = bounds.X + 4;
		int cy = bounds.Y + 2;
		bool craftable = FindAvailableVanillaCraft(recipe) != null;

		foreach (var c in AllInputs(recipe))
		{
			DrawIngredient(sb, new Rectangle(x, cy, CellSize, CellSize), c, lightColor, isOutput: false, amountScale);
			x += CellSize + CellPad;
		}

		DrawArrow(sb, new Rectangle(x + 2, cy + (CellSize - ArrowSize) / 2, ArrowSize, ArrowSize), lightColor, craftable);
		x += ArrowSize + 6;

		foreach (var c in AllOutputs(recipe))
		{
			DrawIngredient(sb, new Rectangle(x, cy, CellSize, CellSize), c, lightColor, isOutput: true, amountScale);
			x += CellSize + CellPad;
		}

		float labelX = x + 6;
		string ownStation = recipe.RecipeType.RegistryName ?? "";
		int nativeTile = recipe.Data.GetInt("nativeTile");
		{
			var stationKeys = CraftingStationRegistry.StationKeysFor(recipe);
			int firstStationItem = 0;
			if (stationKeys.Count > 0)
			{
				foreach (var key in stationKeys)
				{
					int it = StationIcon.ItemTypeFor(key, GetMod());
					if (it <= 0) continue;
					if (firstStationItem == 0) firstStationItem = it;
					int tile = CraftingStationRegistry.TryGetTile(key, out int kt) ? kt : -1;
					DrawItemIconFit(sb, new Rectangle((int)labelX, (int)cy + 1, StationIconSize, StationIconSize), it, StationOverlay(tile, ownStation));
					labelX += StationIconSize + 4;
				}
			}
			else
			{
				firstStationItem = StationIcon.ItemTypeFor(ownStation, GetMod());
				if (firstStationItem > 0)
				{
					DrawItemIconFit(sb, new Rectangle((int)labelX, (int)cy + 1, StationIconSize, StationIconSize), firstStationItem, StationOverlay(nativeTile > 0 ? nativeTile : -1, ownStation));
					labelX += StationIconSize + 4;
				}
				else
				{
					string stationLabel = StationIcon.TryGetDisplayName(ownStation, out var dn) ? dn : Humanize(ownStation);
					if (stationLabel.Length > 0)
						Terraria.Utils.DrawBorderString(sb, stationLabel,
							new Vector2(labelX, cy + 1), new Color(180, 220, 255), 0.7f);
				}
			}

			if (nativeTile > 0 && !VanillaCraftingBridge.IsHandStation(ownStation))
			{
				int nativeItemType = StationIcon.ItemTypeForTile(nativeTile);
				if (nativeItemType > 0 && nativeItemType != firstStationItem)
				{
					var alsoRect = new Rectangle((int)labelX, (int)cy + 1, StationIconSize, StationIconSize);
					DrawItemIconFit(sb, alsoRect, nativeItemType, StationOverlay(nativeTile, ownStation));
					labelX += StationIconSize + 4;
				}
				else if (nativeItemType == 0)
				{
					string tileName = StationIcon.TileDisplayName(nativeTile);
					if (tileName.Length > 0)
					{
						var font = Terraria.GameContent.FontAssets.MouseText.Value;
						Terraria.Utils.DrawBorderString(sb, tileName,
							new Vector2(labelX, cy + 4), new Color(180, 220, 255), 0.7f);
						labelX += font.MeasureString(tileName).X * 0.7f + 6;
					}
				}
			}

		}
		long eut = recipe.InputEUt.Voltage > 0 ? recipe.InputEUt.Voltage : recipe.OutputEUt.Voltage;
		bool totalCwu = Api.Recipe.RecipeDataUtil.GetBool(recipe.Data, "duration_is_total_cwu");
		if (recipe.Duration > 0 || eut > 0)
		{
			string durStr;
			if (totalCwu)
			{
				int minCwu = 0;
				foreach (var c in recipe.GetTickInputContents(Api.Capability.Recipe.CWURecipeCapability.CAP))
					if (c.Payload is int v) minCwu += v;
				durStr = $"{minCwu} CWU/t min ({recipe.Duration:N0} total)";
			}
			else
			{
				durStr = $"{recipe.Duration / 20.0:0.##}s";
			}
			string meta = eut > 0
				? $"{durStr}   {eut} EU/t ({VoltageTiers.ShortName(VoltageTiers.MinTierForVoltage(eut))})"
				: durStr;
			Terraria.Utils.DrawBorderString(sb, meta,
				new Vector2(labelX, cy + 14),
				lightColor, 0.7f);
		}

		string cond = ConditionSummary(recipe);
		if (cond.Length > 0)
		{
			if (cond.Length > 46) cond = cond.Substring(0, 45) + "...";
			Terraria.Utils.DrawBorderString(sb, cond,
				new Vector2(labelX, cy + 27),
				new Color(255, 210, 110) * (lightColor.A / 255f), 0.62f);
		}

		if (craftButton && craftable)
			DrawCraftButton(sb, CraftButtonRect(bounds));
	}

	private static void DrawCraftButton(SpriteBatch sb, Rectangle btn)
	{
		var px = TextureAssets.MagicPixel.Value;
		sb.Draw(px, btn, new Color(50, 130, 75));
		sb.Draw(px, new Rectangle(btn.X, btn.Y, btn.Width, 1), new Color(160, 220, 180));
		sb.Draw(px, new Rectangle(btn.X, btn.Y, 1, btn.Height), new Color(160, 220, 180));
		Terraria.Utils.DrawBorderString(sb, "Craft",
			new Vector2(btn.X + 9, btn.Y + 4), Color.White, 0.85f);
	}

	public static Rectangle CraftButtonRect(Rectangle bounds) =>
		new(bounds.Right - 6 - CraftButtonWidth,
			bounds.Y + (RowHeight - CraftButtonHeight) / 2,
			CraftButtonWidth, CraftButtonHeight);

	public const int SelectGutter = 44;
	public const int SelectButtonSize = 36;

	public static Rectangle SelectButtonRect(Rectangle rowBounds) =>
		new(rowBounds.X + 4, rowBounds.Y + (rowBounds.Height - SelectButtonSize) / 2, SelectButtonSize, SelectButtonSize);

	public static void DrawSelectButton(SpriteBatch sb, Rectangle rect, bool hot)
	{
		var px = TextureAssets.MagicPixel.Value;
		float t = (float)(0.5 + 0.22 * System.Math.Sin(Main.GameUpdateCount * 0.035));
		var bg = Color.Lerp(new Color(36, 130, 60), new Color(96, 232, 122), t);
		if (hot) bg = Color.Lerp(bg, Color.White, 0.25f);
		sb.Draw(px, rect, bg);
		var border = hot ? new Color(200, 255, 215) : new Color(28, 90, 45);
		sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), border);
		sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), border);
		sb.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), border);
		sb.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), border);
		var font = FontAssets.MouseText.Value;
		const float scale = 1.9f;
		var size = font.MeasureString("+") * scale;
		Terraria.Utils.DrawBorderString(sb, "+",
			new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f - 2),
			Color.White, scale);
	}

	public static Terraria.Recipe? FindAvailableVanillaCraft(GTRecipe recipe)
	{
		var rec = AppliedEnergistics.Crafting.CraftingRecipeResolver.FindForGtRecipe(recipe, out _);
		if (rec is null || rec.createItem.IsAir) return null;
		int n = Main.numAvailableRecipes;
		for (int i = 0; i < n; i++)
		{
			int idx = Main.availableRecipe[i];
			if (idx < 0 || idx >= Main.recipe.Length) continue;
			if (ReferenceEquals(Main.recipe[idx], rec)) return rec;
		}
		return null;
	}

	private static string ConditionSummary(GTRecipe recipe)
	{
		var sb = new System.Text.StringBuilder();

		string heat = HeatRequirement(recipe);
		if (heat.Length > 0) sb.Append(heat);

		foreach (var c in recipe.Conditions)
		{
			if (sb.Length > 0) sb.Append("   ");
			sb.Append(ConditionText(c));
		}
		return sb.ToString();
	}

	private static string HeatRequirement(GTRecipe recipe)
	{
		if (recipe.Data is null || !recipe.Data.ContainsKey("ebf_temp")) return "";
		int kelvin = Common.Recipe.GTRecipeModifiers.ReadDataInt(recipe.Data, "ebf_temp");
		if (kelvin <= 0) return "";

		foreach (var coil in Api.Block.CoilType.All)
			if (coil.Temperature >= kelvin)
				return $"Heat: {kelvin:N0} K ({Humanize(coil.Name)})";
		return $"Heat: {kelvin:N0} K";
	}

	private static string ConditionText(RecipeCondition c)
	{
		string s = c.GetTooltips();
		if (string.IsNullOrEmpty(s)) s = c.GetTypeName();
		return c.IsReverse ? "Not: " + s : s;
	}

	private static int ResolveItemType(RecipeContent content) => ItemTypeOf((Ingredient)content.Payload);

	public static int ItemTypeOf(Ingredient ing) => Inner(ing) switch
	{
		ItemStackIngredient isi    => isi.ItemType,
		NBTPredicateIngredient nbt => nbt.ItemType,
		TagIngredient tag          => tag.GetItems().Count > 0 ? tag.GetItems()[0].type : 0,
		IntCircuitIngredient       => CircuitItemType(),
		_                          => 0,
	};

	private static int _circuitItemType = -1;

	private static int CircuitItemType()
	{
		if (_circuitItemType == -1)
			_circuitItemType = GetMod() is { } mod && mod.TryFind<ModItem>("programmed_circuit", out var mi) ? mi.Type : 0;
		return _circuitItemType;
	}

	private static int ScaledCount(Ingredient ing, long amountScale) =>
		(int)System.Math.Min(int.MaxValue, (long)CountOf(ing) * amountScale);

	private static void DrawIngredient(SpriteBatch sb, Rectangle dest, RecipeContent content, Color lightColor, bool isOutput, long amountScale = 1)
	{
		if (!isOutput) DrawAvailabilityBackdrop(sb, dest, content, lightColor, amountScale);

		var ing = (Ingredient)content.Payload;
		if (IsFluid(ing))
		{
			DrawFluidCell(sb, dest, ing, lightColor);
			DrawChanceOverlay(sb, dest, content, lightColor, isOutput);
			return;
		}

		if (Inner(ing) is TagIngredient tag && tag.GetItems().Count > 0)
		{
			DrawItemSprite(sb, dest, CycleMember(tag), ScaledCount(ing, amountScale), lightColor);
			DrawTagGlyph(sb, dest, lightColor);
			DrawChanceOverlay(sb, dest, content, lightColor, isOutput);
			return;
		}

		int itemType = ResolveItemType(content);
		if (itemType <= 0)
		{
			DrawUnresolved(sb, dest, ing, lightColor);
			return;
		}

		DrawItemSprite(sb, dest, itemType, ScaledCount(ing, amountScale), lightColor, Inner(ing));
		DrawChanceOverlay(sb, dest, content, lightColor, isOutput);
	}

	private static void ApplyIngredientState(Item item, Ingredient? ing)
	{
		if (ing is NBTPredicateIngredient nbt)
			Items.ResearchDataGlobalItem.StampFromSnbt(item, nbt.OutputNbt);
		if (ing is IntCircuitIngredient circ && item.ModItem is Items.IntCircuitItem ic)
			ic.Configuration = circ.Configuration;
	}

	public static Item BuildDisplayItem(Api.Recipe.Content.Content content, int itemType)
	{
		var item = new Item();
		item.SetDefaults(itemType);
		if (content?.Payload is Ingredient ing)
			ApplyIngredientState(item, Inner(ing));
		return item;
	}

	private static void DrawAvailabilityBackdrop(SpriteBatch sb, Rectangle dest, RecipeContent content, Color lightColor, long amountScale = 1)
	{
		if (_invSnapshot is null || _fluidSnapshot is null) return;
		var state = IsFluid((Ingredient)content.Payload)
			? GlobalRecipeBrowserState.GetFluidAvailability(content, _fluidSnapshot, amountScale)
			: GlobalRecipeBrowserState.GetItemAvailability(content, _invSnapshot, amountScale);
		if (state == GlobalRecipeBrowserState.AvailabilityState.None) return;
		var tint = state == GlobalRecipeBrowserState.AvailabilityState.Full ? TintFull : TintPartial;
		sb.Draw(TextureAssets.MagicPixel.Value, dest, tint * (0.85f * lightColor.A / 255f));
	}

	private static void DrawItemSprite(SpriteBatch sb, Rectangle dest, int itemType, int stack, Color lightColor, Ingredient? researchSrc = null)
	{
		float prev = Main.inventoryScale;
		Main.inventoryScale = dest.Width / 52f;
		try
		{
			TempSlot[0] = new Item();
			TempSlot[0].SetDefaults(itemType);
			TempSlot[0].stack = stack;
			ApplyIngredientState(TempSlot[0], researchSrc);
			ItemSlot.Draw(sb, TempSlot, ItemSlot.Context.CraftingMaterial, 0,
				new Vector2(dest.X, dest.Y), lightColor);
		}
		finally
		{
			Main.inventoryScale = prev;
		}
	}

	private static int CycleMember(TagIngredient tag)
	{
		var items = tag.GetItems();
		return items[TagCycle.Index(items.Count)].type;
	}

	private static void DrawTagGlyph(SpriteBatch sb, Rectangle dest, Color lightColor)
	{
		Terraria.Utils.DrawBorderString(sb, "*",
			new Vector2(dest.X + 2, dest.Y - 3),
			new Color(130, 230, 130) * (lightColor.A / 255f), 1.1f);
	}

	private static void DrawChanceOverlay(SpriteBatch sb, Rectangle dest, RecipeContent content, Color lightColor, bool isOutput)
	{
		int max = content.MaxChance;
		if (max <= 0 || content.Chance >= max) return;
		var font = FontAssets.MouseText.Value;
		float scale = 0.6f;

		string label;
		Color tint;
		if (!isOutput && content.Chance == 0)
		{
			label = "Tool";
			tint  = new Color(255, 200, 90) * (lightColor.A / 255f);
		}
		else
		{
			label = $"{FormatChancePercent(content.Chance, max)}%";
			tint  = isOutput
				? lightColor * 0.95f
				: new Color(255, 240, 160) * (lightColor.A / 255f);
		}

		var size = font.MeasureString(label) * scale;
		Terraria.Utils.DrawBorderString(sb, label,
			new Vector2(dest.Right - size.X - 2, dest.Y + 2),
			tint, scale);
	}

	private static string FormatChancePercent(int chance, int max)
	{
		float pct = chance * 100f / max;
		return pct != MathF.Floor(pct)
			? pct.ToString("0.##", CultureInfo.InvariantCulture)
			: ((int)pct).ToString(CultureInfo.InvariantCulture);
	}

	private static void DrawFluidCell(SpriteBatch sb, Rectangle dest, Ingredient ing, Color lightColor)
	{
		ResolveFluidCell(ing, out var fluid, out int amount, out string? fallback);
		var inset = new Rectangle(dest.X + 2, dest.Y + 2, dest.Width - 4, dest.Height - 4);
		BrowserFluidSlot.Draw(sb, inset, fluid, amount, fallback, lightColor);
	}

	private static void ResolveFluidCell(Ingredient ing,
		out FluidType? fluid, out int amount, out string? fallbackLabel)
	{
		fluid = null; amount = 0; fallbackLabel = null;
		if (FluidPart(ing) is { } fi)
		{
			amount = fi.Amount;
			fluid = fi.ExactType ?? (fi.GetFluids().Count > 0 ? fi.GetFluids()[0] : null);
			if (fluid is null) fallbackLabel = fi.TagName ?? fi.Attribute?.Id ?? "?";
		}
		if (Inner(ing) is IntProviderFluidIngredient ipfi) amount = ipfi.RollSampledCount();
	}

	private static void DrawUnresolved(SpriteBatch sb, Rectangle dest, Ingredient ing, Color lightColor)
	{
		var px = TextureAssets.MagicPixel.Value;
		sb.Draw(px, dest, new Color(60, 30, 30) * (lightColor.A / 255f));
		TankFrame.DrawBorder(sb, dest, TankFrame.BorderColor * (lightColor.A / 255f));

		string raw = ShortLabel(ing);
		if (raw.Length > 8) raw = raw.Substring(0, 8);
		Terraria.Utils.DrawBorderString(sb, raw,
			new Vector2(dest.X + 2, dest.Y + 8),
			lightColor * 0.7f, 0.55f);
	}

	private static void DrawArrow(SpriteBatch sb, Rectangle dest, Color lightColor, bool craftable)
	{
		if (TextureAssets.CraftToggle[craftable ? 3 : 2]?.Value is not { } tex) return;
		var center = new Vector2(dest.X + dest.Width / 2f, dest.Y + dest.Height / 2f);
		sb.Draw(tex, center, null, lightColor, 0f, tex.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
	}

	public static void EmitTooltipFor(GTRecipe recipe, Rectangle bounds, Point mouse)
	{
		int x = bounds.X + 4;
		int cy = bounds.Y + 2;
		foreach (var c in AllInputs(recipe))
		{
			if (new Rectangle(x, cy, CellSize, CellSize).Contains(mouse)) { EmitIngTooltip(c, isOutput: false); return; }
			x += CellSize + CellPad;
		}
		x += ArrowSize + 6 + 2;
		foreach (var c in AllOutputs(recipe))
		{
			if (new Rectangle(x, cy, CellSize, CellSize).Contains(mouse)) { EmitIngTooltip(c, isOutput: true); return; }
			x += CellSize + CellPad;
		}

		string stationId = recipe.RecipeType.RegistryName ?? "";
		float labelX = x + 6;
		var stationKeys = CraftingStationRegistry.StationKeysFor(recipe);
		int firstStationItem = 0;
		if (stationKeys.Count > 0)
		{
			foreach (var key in stationKeys)
			{
				int it = StationIcon.ItemTypeFor(key, GetMod());
				if (it <= 0) continue;
				if (firstStationItem == 0) firstStationItem = it;
				if (new Rectangle((int)labelX, cy + 1, StationIconSize, StationIconSize).Contains(mouse))
				{
					Main.instance.MouseText(Lang.GetItemNameValue(it));
					return;
				}
				labelX += StationIconSize + 4;
			}
		}
		else
		{
			firstStationItem = StationIcon.ItemTypeFor(stationId, GetMod());
			if (firstStationItem > 0)
			{
				var iconRect = new Rectangle((int)labelX, cy + 1, StationIconSize, StationIconSize);
				if (iconRect.Contains(mouse))
				{
					string label = VanillaCraftingBridge.IsHandStation(stationId)
						? "By Hand"
						: StationIcon.TryGetDisplayName(stationId, out var dn) ? dn : Humanize(stationId);
					Main.instance.MouseText(label);
					return;
				}
				labelX += StationIconSize + 4;
			}
		}

		int nativeTile = recipe.Data.GetInt("nativeTile");
		if (nativeTile > 0 && !VanillaCraftingBridge.IsHandStation(stationId))
		{
			int nativeItemType = StationIcon.ItemTypeForTile(nativeTile);
			if (nativeItemType > 0 && nativeItemType != firstStationItem)
			{
				var alsoRect = new Rectangle((int)labelX, cy + 1, StationIconSize, StationIconSize);
				if (alsoRect.Contains(mouse))
				{
					Main.instance.MouseText(Lang.GetItemNameValue(nativeItemType));
					return;
				}
				labelX += StationIconSize + 4;
			}
		}

	}

	private static int TrailingStartX(GTRecipe recipe, Rectangle bounds)
	{
		int x = bounds.X + 4;
		foreach (var _ in AllInputs(recipe))
			x += CellSize + CellPad;
		x += ArrowSize + 6 + 2;
		foreach (var _ in AllOutputs(recipe))
			x += CellSize + CellPad;
		return x;
	}

	public static string? StationChipAt(GTRecipe recipe, Rectangle bounds, Point mouse)
	{
		string stationId = recipe.RecipeType.RegistryName ?? "";
		int cy = bounds.Y + 2;
		float labelX = TrailingStartX(recipe, bounds) + 6;

		var stationKeys = CraftingStationRegistry.StationKeysFor(recipe);
		int firstStationItem = 0;
		if (stationKeys.Count > 0)
		{
			foreach (var key in stationKeys)
			{
				int it = StationIcon.ItemTypeFor(key, GetMod());
				if (it <= 0) continue;
				if (firstStationItem == 0) firstStationItem = it;
				if (new Rectangle((int)labelX, cy + 1, StationIconSize, StationIconSize).Contains(mouse))
					return key;
				labelX += StationIconSize + 4;
			}
		}
		else
		{
			if (stationId.Length == 0) return null;
			firstStationItem = StationIcon.ItemTypeFor(stationId, GetMod());
			if (firstStationItem > 0)
			{
				if (new Rectangle((int)labelX, cy + 1, StationIconSize, StationIconSize).Contains(mouse))
					return stationId;
				labelX += StationIconSize + 4;
			}
		}

		int nativeTile = recipe.Data.GetInt("nativeTile");
		if (nativeTile > 0 && !VanillaCraftingBridge.IsHandStation(stationId))
		{
			int nativeItemType = StationIcon.ItemTypeForTile(nativeTile);
			if (nativeItemType > 0 && nativeItemType != firstStationItem
			    && new Rectangle((int)labelX, cy + 1, StationIconSize, StationIconSize).Contains(mouse))
				return stationId;
		}
		return null;
	}


	private static void EmitIngTooltip(RecipeContent content, bool isOutput)
	{
		var ing = (Ingredient)content.Payload;
		string chanceLine = "";
		int max = content.MaxChance;
		if (max > 0 && content.Chance < max)
		{
			chanceLine = content.Chance == 0
				? "\nTool - not consumed"
				: $"\nChance: {FormatChancePercent(content.Chance, max)}%";
		}

		if (IsFluid(ing))
		{
			ResolveFluidCell(ing, out var fluid, out int amount, out string? fallback);
			string containerNote = Inner(ing) is FluidContainerIngredient ? "\nin a filled container (bucket / cell)" : "";
			BrowserFluidSlot.EmitTooltip(fluid, amount, fallback, containerNote + chanceLine);
			return;
		}

		if (Inner(ing) is TagIngredient tag && tag.GetItems().Count > 0)
		{
			var members = tag.GetItems();
			string header = FriendlyGroupName(tag.TagName) ?? ("#" + StripNs(tag.TagName));
			string chanceInline = chanceLine.Length > 0 ? " (" + chanceLine.TrimStart('\n') + ")" : "";
			BrowserTooltipGlobal.Begin();
			BrowserTooltipGlobal.AfterName(header, BrowserTooltipGlobal.Header);
			BrowserTooltipGlobal.AfterName($"Accepts any of {members.Count} items{chanceInline}", BrowserTooltipGlobal.Detail);
			BrowserTooltipGlobal.Append("Accepts any of:", BrowserTooltipGlobal.Header);
			int shown = members.Count < TagMembersListed ? members.Count : TagMembersListed;
			for (int i = 0; i < shown; i++)
				BrowserTooltipGlobal.Append("  " + members[i].Name, BrowserTooltipGlobal.Member);
			if (members.Count > TagMembersListed)
				BrowserTooltipGlobal.Append("  +" + (members.Count - TagMembersListed) + " more", BrowserTooltipGlobal.Member);
			Main.HoverItem = new Item();
			Main.HoverItem.SetDefaults(CycleMember(tag));
			Main.HoverItem.stack = CountOf(ing);
			Main.instance.MouseText("");
			return;
		}

		int itemType = ResolveItemType(content);
		if (itemType > 0)
		{
			Main.HoverItem = new Item();
			Main.HoverItem.SetDefaults(itemType);
			Main.HoverItem.stack = CountOf(ing);
			ApplyIngredientState(Main.HoverItem, Inner(ing));
			if (chanceLine.Length > 0)
			{
				BrowserTooltipGlobal.Begin();
				BrowserTooltipGlobal.AfterName(chanceLine.TrimStart('\n'), BrowserTooltipGlobal.Chance);
			}
			Main.instance.MouseText("");
			return;
		}
		Main.instance.MouseText($"{ShortLabel(ing)}\n(unresolved){chanceLine}");
	}

	public static RecipeContent? IngredientAt(GTRecipe recipe, Rectangle bounds, Point mouse)
	{
		int x = bounds.X + 4;
		int cy = bounds.Y + 2;
		foreach (var c in AllInputs(recipe))
		{
			if (new Rectangle(x, cy, CellSize, CellSize).Contains(mouse)) return c;
			x += CellSize + CellPad;
		}
		x += ArrowSize + 6 + 2;
		foreach (var c in AllOutputs(recipe))
		{
			if (new Rectangle(x, cy, CellSize, CellSize).Contains(mouse)) return c;
			x += CellSize + CellPad;
		}
		return null;
	}

	public static HashSet<int> InputItemTypesInRecipe(GTRecipe recipe)
	{
		var set = new HashSet<int>();
		foreach (var c in recipe.GetInputContents(ItemRecipeCapability.CAP))
			AddItemTypes(c, set);
		return set;
	}

	public static HashSet<int> OutputItemTypesInRecipe(GTRecipe recipe)
	{
		var set = new HashSet<int>();
		foreach (var c in recipe.GetOutputContents(ItemRecipeCapability.CAP))
			AddItemTypes(c, set);
		return set;
	}

	private static void AddItemTypes(RecipeContent c, HashSet<int> sink)
	{
		if (Inner((Ingredient)c.Payload) is TagIngredient tag)
		{
			foreach (var item in tag.GetItems())
				if (item.type > Terraria.ID.ItemID.None) sink.Add(item.type);
			return;
		}
		if (Inner((Ingredient)c.Payload) is FluidContainerIngredient fc)
		{
			foreach (var item in fc.GetItems())
				if (item.type > Terraria.ID.ItemID.None) sink.Add(item.type);
			return;
		}
		int t = ResolveItemType(c);
		if (t > 0) sink.Add(t);
	}

	public static HashSet<string> InputFluidIdsInRecipe(GTRecipe recipe)
	{
		var set = new HashSet<string>();
		foreach (var c in recipe.GetInputContents(FluidRecipeCapability.CAP)) AddFluidIds(c, set);
		return set;
	}

	public static HashSet<string> OutputFluidIdsInRecipe(GTRecipe recipe)
	{
		var set = new HashSet<string>();
		foreach (var c in recipe.GetOutputContents(FluidRecipeCapability.CAP)) AddFluidIds(c, set);
		return set;
	}

	private static void AddFluidIds(RecipeContent c, HashSet<string> sink)
	{
		if (Inner((Ingredient)c.Payload) is not FluidIngredient fi) return;
		if (fi.ExactType is not null) { sink.Add(fi.ExactType.Id); return; }
		foreach (var f in fi.GetFluids()) sink.Add(f.Id);
		if (fi.TagName is not null)
		{
			int colon = fi.TagName.IndexOf(':');
			sink.Add(colon >= 0 ? fi.TagName.Substring(colon + 1) : fi.TagName);
		}
	}

	private static List<RecipeContent> AllInputs(GTRecipe recipe)
	{
		var list = new List<RecipeContent>();
		list.AddRange(recipe.GetInputContents(ItemRecipeCapability.CAP));
		list.AddRange(recipe.GetInputContents(FluidRecipeCapability.CAP));
		return list;
	}

	private static List<RecipeContent> AllOutputs(GTRecipe recipe)
	{
		var list = new List<RecipeContent>();
		list.AddRange(recipe.GetOutputContents(ItemRecipeCapability.CAP));
		list.AddRange(recipe.GetOutputContents(FluidRecipeCapability.CAP));
		return list;
	}

	private static Ingredient Inner(Ingredient ing) => ing switch
	{
		SizedIngredient sized      => Inner(sized.Inner),
		IntProviderIngredient ipi  => Inner(ipi.Inner),
		_                          => ing,
	};

	private static int CountOf(Ingredient ing) => ing switch
	{
		SizedIngredient sized      => sized.Amount,
		IntProviderIngredient ipi  => ipi.RollSampledCount(),
		_                          => 1,
	};

	private static bool IsFluid(Ingredient ing) =>
		Inner(ing) is FluidIngredient or IntProviderFluidIngredient or FluidContainerIngredient;

	private static FluidIngredient? FluidPart(Ingredient ing) => Inner(ing) switch
	{
		FluidIngredient fi          => fi,
		FluidContainerIngredient fc => fc.Fluid,
		_                           => null,
	};

	private static string ShortLabel(Ingredient ing) => Inner(ing) switch
	{
		ItemStackIngredient isi      => string.IsNullOrEmpty(isi.UpstreamId) ? $"i{isi.ItemType}" : StripNs(isi.UpstreamId),
		TagIngredient tag            => FriendlyGroupName(tag.TagName) ?? ("#" + StripNs(tag.TagName)),
		NBTPredicateIngredient nbt   => StripNs(nbt.UpstreamId),
		FluidIngredient fi           => fi.ExactType?.Id ?? fi.TagName ?? fi.Attribute?.Id ?? "?",
		FluidContainerIngredient fc  => fc.Fluid.ExactType?.Id ?? fc.Fluid.TagName ?? "?",
		_                            => ing.GetTypeName(),
	};

	private static string StripNs(string id)
	{
		int colon = id.IndexOf(':');
		return colon >= 0 ? id.Substring(colon + 1) : id;
	}

	public static string? FriendlyGroupName(string? tagName)
	{
		if (tagName is null) return null;
		if (IngredientResolverImpl.TryGetRecipeGroup(tagName, out var g))
			return g.GetText();
		if (VanillaItemMap.TryGetFungibleGroupName(tagName, out var name))
			return name;
		return null;
	}

	public static void ResolveCell(RecipeContent content,
		out int itemType, out int itemAmount,
		out FluidType? fluid, out int fluidAmountMb,
		out string? tagLabel, out HashSet<int>? tagMembers)
	{
		itemType = 0; itemAmount = 1; fluid = null; fluidAmountMb = 0;
		tagLabel = null; tagMembers = null;

		var raw = (Ingredient)content.Payload;
		itemAmount = raw switch
		{
			SizedIngredient s         => s.Amount,
			IntProviderIngredient ipi => ipi.RollSampledCount(),
			_                         => 1,
		};
		if (itemAmount <= 0) itemAmount = 1;
		var inner = Inner(raw);
		if (inner is TagIngredient tag && tag.GetItems().Count > 0)
		{
			var members = new HashSet<int>();
			foreach (var m in tag.GetItems())
				if (m.type > Terraria.ID.ItemID.None) members.Add(m.type);
			if (members.Count >= 2)
			{
				tagLabel = FriendlyGroupName(tag.TagName) ?? StripNs(tag.TagName);
				tagMembers = members;
				return;
			}
			itemType = tag.GetItems()[0].type;
			return;
		}

		switch (inner)
		{
			case FluidIngredient fi:
				fluid = fi.ExactType ?? (fi.GetFluids().Count > 0 ? fi.GetFluids()[0] : null);
				fluidAmountMb = fi.Amount;
				return;
			case ItemStackIngredient isi when isi.ItemType > 0:    itemType = isi.ItemType; return;
			case NBTPredicateIngredient nbt when nbt.ItemType > 0: itemType = nbt.ItemType; return;
			case FluidContainerIngredient fc:
				fluid = fc.Fluid.ExactType ?? (fc.Fluid.GetFluids().Count > 0 ? fc.Fluid.GetFluids()[0] : null);
				fluidAmountMb = fc.Fluid.Amount;
				return;
		}

		if (Inner((Ingredient)content.Payload) is IntProviderFluidIngredient ipfi)
			fluidAmountMb = ipfi.RollSampledCount();
	}

	private static void RecordHover(RecipeContent content)
	{
		ResolveCell(content, out int itemType, out _, out var fluid, out _,
			out string? tagLabel, out var tagMembers);
		if (tagLabel is not null && tagMembers is not null) BrowserHover.SetTag(tagLabel, tagMembers);
		else if (itemType > 0) BrowserHover.SetItem(itemType);
		else if (fluid is not null) BrowserHover.SetFluid(fluid.Id, fluid.DisplayName);
	}

	public static bool HandleQueryClick(GTRecipe recipe, Rectangle bounds, Point mouse,
		bool dragging, bool stationClickAllowed, Action<string>? onStationFilter)
	{
		EmitTooltipFor(recipe, bounds, mouse);

		string? chipStation = onStationFilter == null ? null : StationChipAt(recipe, bounds, mouse);
		if (chipStation is not null)
		{
			if (stationClickAllowed && MouseClick.LeftPressed) onStationFilter!(chipStation);
			return true;
		}

		var ing = IngredientAt(recipe, bounds, mouse);
		if (ing is null) return false;
		RecordHover(ing);
		if (dragging) return true;

		ResolveCell(ing, out int itemType, out int itemAmt, out var fluid, out int fluidAmt,
			out string? tagLabel, out var tagMembers);
		if (MouseClick.LeftPressed)
		{
			if (itemType > 0) ItemDrag.ArmItem(itemType);
			else if (fluid is not null) ItemDrag.ArmFluid(fluid.Id, fluid.DisplayName);
			else if (tagMembers is { Count: > 0 })
				foreach (var m in tagMembers) { ItemDrag.ArmItem(m); break; }
		}
		var click = ItemDrag.Active ? default : BrowserSlotInteraction.PollReleased();
		if (tagLabel is not null && tagMembers is not null)
			BrowserSlotInteraction.HandleTag(click, tagLabel, tagMembers, recipeAmount: itemAmt);
		else if (fluid is not null)
			BrowserSlotInteraction.HandleFluid(click, fluid, fluidAmt > 0 ? fluidAmt : (int?)null, inFavoritesPane: false);
		else if (itemType > 0)
			BrowserSlotInteraction.HandleItem(click, BuildDisplayItem(ing, itemType), inFavoritesPane: false, recipeAmount: itemAmt);
		return true;
	}

	private static readonly Item[] _stationSlotItem = { new() };
	private const float VanillaNativeSlotPixels = 52f;
	private static void DrawItemIconFit(SpriteBatch sb, Rectangle dest, int itemType, Color? overlay)
	{
		if (itemType <= 0 || itemType >= TextureAssets.Item.Length) return;
		_stationSlotItem[0].SetDefaults(itemType);
		float oldScale = Main.inventoryScale;
		Main.inventoryScale = dest.Width / VanillaNativeSlotPixels;
		try
		{
			ItemSlot.Draw(sb, _stationSlotItem, ItemSlot.Context.CraftingMaterial, 0,
				new Vector2(dest.X, dest.Y));
		}
		finally
		{
			Main.inventoryScale = oldScale;
		}
		if (overlay is { } o)
			sb.Draw(TextureAssets.MagicPixel.Value, dest, o);
	}

	// "chemical_reactor" -> "Chemical Reactor".
	private static string Humanize(string snake)
	{
		if (string.IsNullOrEmpty(snake)) return snake;
		var sb = new System.Text.StringBuilder(snake.Length);
		bool capNext = true;
		foreach (char c in snake)
		{
			if (c == '_') { sb.Append(' '); capNext = true; continue; }
			sb.Append(capNext ? char.ToUpperInvariant(c) : c);
			capNext = false;
		}
		return sb.ToString();
	}
}
