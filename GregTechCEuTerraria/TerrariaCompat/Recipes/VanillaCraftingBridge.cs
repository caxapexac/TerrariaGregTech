#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using RecipeContent = GregTechCEuTerraria.Api.Recipe.Content.Content;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

// At Mod.AddRecipes, walks RecipeRegistry.ForStation(...) for every station with
// a Terraria-tile equivalent and pushes fully-resolvable recipes into the
// vanilla Recipe API
public static class VanillaCraftingBridge
{
	// Station id = recipe.RecipeType.RegistryName
	private static readonly Dictionary<string, int> StationToTile = new()
	{
		{ "crafting_shaped",        TileID.WorkBenches },
		{ "crafting_shapeless",     TileID.WorkBenches },
		{ "crafting_shaped_strict", TileID.WorkBenches },
		{ "crafting_shaped_energy_transfer", TileID.WorkBenches },
		{ "crafting_shaped_fluid_container", TileID.WorkBenches },
		{ "smelting",               TileID.Furnaces },
		{ "blasting",               TileID.Furnaces },
		{ "smoking",                TileID.CookingPots },
		{ "campfire_cooking",       TileID.Campfire },
	};

	// DEVIATION: every GT crafting-TABLE recipe (shaped / shapeless / strict /
	// energy-transfer / fluid-container) is hand-craftable
	internal static readonly HashSet<string> HandStations = new()
	{
		"crafting_shaped", "crafting_shapeless", "crafting_shaped_strict",
		"crafting_shaped_energy_transfer", "crafting_shaped_fluid_container",
	};

	internal static bool IsHandStation(string station) => station.Length > 0 && HandStations.Contains(station);
	internal static bool TryGetStationTile(string station, out int tile) => StationToTile.TryGetValue(station, out tile);

	private static LocalizedText _fluidConditionText = null!;

	public static void Register(Mod mod)
	{
		BridgeRegistered.Clear();
		GTToVanilla.Clear();
		_fluidConditionText = Language.GetOrRegister(
			"Mods.GregTechCEuTerraria.RecipeConditions.FluidContainer",
			() => "Requires the fluid held in a cell or bucket");

		int totalConsidered = 0, totalRegistered = 0;
		var unresolvedItems  = new Dictionary<string, int>();
		var unresolvedTags   = new Dictionary<string, int>();
		// GTCEu declares 2x2-hand + 3x3-table variants of crafting_shaped_strict;
		// both collapse to one Terraria recipe so the duplicate is dropped.
		var seen = new HashSet<string>();
		_deduped = 0;

		for (int pass = 0; pass < 2; pass++)
		{
			bool handPass = pass == 0;
			foreach (var (station, tileId) in StationToTile)
			{
				bool isHand = HandStations.Contains(station);
				if (isHand != handPass) continue;
				foreach (var gt in RecipeRegistry.ForStation(station))
				{
					totalConsidered++;
					if (TryBuild(gt, tileId, isHand, unresolvedItems, unresolvedTags, seen))
						totalRegistered++;
				}
			}
		}

		mod.Logger.Info($"[recipes] total registered {totalRegistered} / {totalConsidered}" +
			$"  ({_deduped} duplicates dropped)");

		LogTopMisses(mod, "unresolved items", unresolvedItems);
		LogTopMisses(mod, "unresolved tags",  unresolvedTags);
	}

	private static int _deduped;

	private static bool TryBuild(GTRecipe gt, int tileId, bool isHand,
		Dictionary<string, int> missItems, Dictionary<string, int> missTags,
		HashSet<string> seen)
	{
		var itemInputs  = gt.GetInputContents(ItemRecipeCapability.CAP);
		var itemOutputs = gt.GetOutputContents(ItemRecipeCapability.CAP);
		var fluidInputs = gt.GetInputContents(FluidRecipeCapability.CAP);

		if (fluidInputs.Count > 0) return false;
		foreach (var c in itemInputs)
			if (((Ingredient)c.Payload) is IntCircuitIngredient) return false;
		if (itemInputs.Count == 0 || itemOutputs.Count == 0) return false;

		int maxChance = Api.Recipe.Chance.Logic.ChanceLogic.GetMaxChancedValue();

		RecipeContent? outContent = null;
		foreach (var o in itemOutputs)
			if (o.Chance >= maxChance) { outContent = o; break; }
		if (outContent is null) return false;
		if (!TryResolveItem((Ingredient)outContent.Payload, out int outType, out int outCount, out string outKey))
		{
			BumpMiss(missItems, outKey);
			return false;
		}

		var resolved = new List<(bool isGroup, int itemOrGroupId, int count)>(itemInputs.Count);
		var fluidReqs = new List<(FluidIngredient fluid, int units)>();
		bool hasCatalyst = false;
		foreach (var ci in itemInputs)
		{
			var ing = (Ingredient)ci.Payload;
			if (TryPeelFluidContainer(ing, out var fluid, out int units))
			{
				fluidReqs.Add((fluid, units));
				continue;
			}
			if (TryResolveItem(ing, out int it, out int ct, out _))
				resolved.Add((false, it, ct));
			else if (TryResolveGroup(ing, out int gid, out int gct))
			{
				resolved.Add((true, gid, gct));
				if (ToolRecipeGroups.IsCatalystGroup(gid)) hasCatalyst = true;
			}
			else
			{
				BumpMiss(IsTagIngredient(ing) ? missTags : missItems, RefKey(ing));
				return false;
			}
		}
		if (resolved.Count == 0 && fluidReqs.Count == 0) return false;

		if (fluidReqs.Count == 0)
		{
			var parts = resolved
				.Select(r => $"{(r.isGroup ? 'g' : 'i')}{r.itemOrGroupId}x{r.count}")
				.OrderBy(s => s, System.StringComparer.Ordinal);
			string sig = $"{outType}*{outCount}|{string.Join(",", parts)}";
			if (!seen.Add(sig)) { _deduped++; return false; }
		}

		var recipe = Terraria.Recipe.Create(outType, outCount);
		foreach (var (isGroup, itemOrGroupId, count) in resolved)
		{
			if (isGroup) recipe.AddRecipeGroup(itemOrGroupId, count);
			else         recipe.AddIngredient(itemOrGroupId, count);
		}

		if (!isHand)
			recipe.AddTile(tileId);

		if (hasCatalyst)
			recipe.AddConsumeIngredientCallback(NoConsumeCatalysts);

		foreach (var (fluid, units) in fluidReqs)
		{
			var f = fluid;
			int n = units;
			recipe.AddCondition(_fluidConditionText, () => PlayerHasFluidContainers(f, n));
			recipe.AddOnCraftCallback((_, _, _, _) => ConsumeFluidContainers(f, n));
		}

		recipe.Register();
		BridgeRegistered.Add(recipe);
		GTToVanilla[gt] = recipe;
		return true;
	}

	public static readonly HashSet<Terraria.Recipe> BridgeRegistered = new();

	public static readonly Dictionary<GTRecipe, Terraria.Recipe> GTToVanilla = new();

	private static bool TryPeelFluidContainer(Ingredient ing, out FluidIngredient fluid, out int units)
	{
		fluid = null!;
		units = 1;
		switch (ing)
		{
			case FluidContainerIngredient fci:
				fluid = fci.Fluid;
				return true;
			case SizedIngredient sized when sized.Inner is FluidContainerIngredient fciS:
				fluid = fciS.Fluid;
				units = sized.Amount;
				return true;
			case IntProviderIngredient ipi when ipi.Inner is FluidContainerIngredient fciI:
				fluid = fciI.Fluid;
				units = ipi.RollSampledCount();
				return true;
		}
		return false;
	}

	private static bool PlayerHasFluidContainers(FluidIngredient fluid, int units)
	{
		int found = 0;
		foreach (var it in Main.LocalPlayer.inventory)
		{
			if (it is null || it.IsAir || !MatchesContainer(it, fluid)) continue;
			found += it.stack;
			if (found >= units) return true;
		}
		return found >= units;
	}

	// Mirrors upstream ShapedFluidContainerRecipe.getRemainingItems +
	// FluidContainerIngredient.getExtractedStack
	private static void ConsumeFluidContainers(FluidIngredient fluid, int units)
	{
		var inv = Main.LocalPlayer.inventory;
		int remaining = units;
		int returnedBuckets = 0;
		for (int i = 0; i < inv.Length && remaining > 0; i++)
		{
			var it = inv[i];
			if (it is null || it.IsAir || !MatchesContainer(it, fluid)) continue;
			// Drainable container
			if (it.ModItem is Api.Capability.IFluidHandlerItem handler
			    && handler.Drain(fluid.Amount, simulate: false).Amount >= fluid.Amount)
			{
				remaining--;
				continue;
			}

			// Non-drainable container
			it.stack -= 1;
			if (it.stack <= 0) it.TurnToAir();
			returnedBuckets++;
			remaining--;
		}

		if (returnedBuckets > 0)
		{
			var src = new EntitySource_Misc("gtceu:fluid_container_recipe");
			global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(
				Main.LocalPlayer, src, ItemID.EmptyBucket, returnedBuckets);
		}
	}

	private static bool MatchesContainer(Item it, FluidIngredient fluid)
	{
		if (it.ModItem is Api.Capability.IFluidHandlerItem handler)
		{
			var fs = handler.GetTank(0);
			return !fs.IsEmpty && fluid.TestStack(fs);
		}
		return BucketMatches(it.type, fluid);
	}

	private static bool BucketMatches(int itemType, FluidIngredient fluid)
	{
		string? id = itemType switch
		{
			ItemID.WaterBucket => "water",
			ItemID.LavaBucket  => "lava",
			ItemID.HoneyBucket => "honey",
			_                  => null,
		};
		return id is not null
		    && FluidRegistry.TryGet(id, out var type)
		    && fluid.TestFluid(type)
		    && fluid.Amount <= 1000;
	}

	private static bool TryResolveItem(Ingredient ing, out int itemType, out int count, out string key)
	{
		itemType = 0; count = 1; key = RefKey(ing);
		switch (ing)
		{
			case SizedIngredient sized:
				if (!TryResolveItem(sized.Inner, out itemType, out _, out key)) return false;
				count = sized.Amount;
				return true;
			case IntProviderIngredient ipi:
				if (!TryResolveItem(ipi.Inner, out itemType, out _, out key)) return false;
				count = ipi.RollSampledCount();
				return true;
			case ItemStackIngredient isi when isi.ItemType > 0:
				itemType = isi.ItemType;
				return true;
			case NBTPredicateIngredient nbt when nbt.ItemType > 0:
				itemType = nbt.ItemType;
				return true;
			case TagIngredient tag when tag.ResolvedTypes.Count == 1:
				itemType = tag.ResolvedTypes[0];
				return true;
		}
		return false;
	}

	private static bool TryResolveGroup(Ingredient ing, out int groupId, out int count)
	{
		groupId = 0; count = 1;
		switch (ing)
		{
			case SizedIngredient sized:
				if (!TryResolveGroup(sized.Inner, out groupId, out _)) return false;
				count = sized.Amount;
				return true;
			case TagIngredient tag:
				return TryResolveTagToGroup(tag, out groupId);
		}
		return false;
	}

	// Tag -> RecipeGroup. Order: (1) alias / pre-registered (vanilla group ids
	// or tool catalysts via VanillaSubstitution.Groups - required so e.g.
	// forge:wood recipes accept Terraria's Boreal/Mahogany alongside our
	// items). (2) Lazy auto-register from tag.ResolvedTypes when >=2 items.
	// Single-item tags route through TryResolveItem instead.
	private static bool TryResolveTagToGroup(TagIngredient tag, out int groupId)
	{
		if (VanillaItemMap.TryGetGroup(tag.TagName, out groupId)) return true;
		if (tag.ResolvedTypes.Count < 2) return false;

		groupId = RecipeGroup.RegisterGroup(
			$"GregTechCEuTerraria:Auto/{tag.TagName}",
			new RecipeGroup(() => BuildLabel(tag.TagName), tag.ResolvedTypes.ToArray()));
		VanillaItemMap.RegisterGroup(tag.TagName, groupId);
		return true;
	}

	private static string BuildLabel(string tag)
	{
		int colon = tag.IndexOf(':');
		string path = colon >= 0 ? tag[(colon + 1)..] : tag;
		var parts = path.Split('/');
		if (parts.Length == 1) return "Any " + Prettify(Singularise(parts[0]));

		string category  = Singularise(parts[0]);
		string qualifier = parts[^1];
		bool isAcronym = qualifier.Length <= 4
		                 && !qualifier.Contains('_')
		                 && qualifier.All(c => c is >= 'a' and <= 'z');
		string qualText = isAcronym
			? qualifier.ToUpperInvariant()
			: Prettify(qualifier);
		return $"Any {qualText} {Prettify(category)}";
	}

	private static string Singularise(string s)
	{
		if (s.EndsWith("ies") && s.Length > 3) return s[..^3] + "y";
		if (s.EndsWith("s")   && s.Length > 1) return s[..^1];
		return s;
	}

	private static string Prettify(string snake)
	{
		var sb = new System.Text.StringBuilder(snake.Length);
		bool cap = true;
		foreach (char c in snake)
		{
			if (c == '_') { sb.Append(' '); cap = true; continue; }
			sb.Append(cap ? char.ToUpper(c) : c);
			cap = false;
		}
		return sb.ToString();
	}

	private static void NoConsumeCatalysts(Terraria.Recipe recipe, int type, ref int amount, bool isDecrafting)
	{
		if (Items.Tools.ToolItemLoader.CatalystItemTypes.Contains(type))
			amount = 0;
	}

	private static bool IsTagIngredient(Ingredient ing) => ing switch
	{
		TagIngredient _              => true,
		SizedIngredient s            => IsTagIngredient(s.Inner),
		IntProviderIngredient ipi    => IsTagIngredient(ipi.Inner),
		_                            => false,
	};

	private static string RefKey(Ingredient ing) => ing switch
	{
		ItemStackIngredient isi      => string.IsNullOrEmpty(isi.UpstreamId) ? $"item:{isi.ItemType}" : isi.UpstreamId,
		TagIngredient tag            => "#" + tag.TagName,
		SizedIngredient sized        => RefKey(sized.Inner),
		IntProviderIngredient ipi    => RefKey(ipi.Inner),
		NBTPredicateIngredient nbt   => nbt.UpstreamId,
		_                            => ing.GetTypeName(),
	};

	private static void BumpMiss(Dictionary<string, int> m, string key) =>
		m[key] = m.GetValueOrDefault(key) + 1;

	private static void LogTopMisses(Mod mod, string label, Dictionary<string, int> misses)
	{
		if (misses.Count == 0) return;
		mod.Logger.Info($"[recipes] top 15 {label}:");
		int shown = 0;
		foreach (var kv in misses.OrderByDescending(p => p.Value))
		{
			mod.Logger.Info($"[recipes]   {kv.Value,5}x {kv.Key}");
			if (++shown >= 15) break;
		}
		mod.Logger.Info($"[recipes]   ... {misses.Count - shown} more distinct refs");
	}
}
