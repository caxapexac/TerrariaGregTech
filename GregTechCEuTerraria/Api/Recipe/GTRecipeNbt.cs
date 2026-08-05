#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Fluids.Attribute;
using GregTechCEuTerraria.Api.Recipe.Category;
using GregTechCEuTerraria.Api.Recipe.Chance.Logic;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Util.ValueProviders;
using GregTechCEuTerraria.Common.Recipe.Condition;
using Terraria.ModLoader.IO;
using IngredientBase = GregTechCEuTerraria.Api.Recipe.Ingredient.Ingredient;

namespace GregTechCEuTerraria.Api.Recipe;

// Full GTRecipe <-> TagCompound serialization
// upstream RecipeLogic carries `@SaveField GTRecipe lastRecipe` + `@SaveField GTRecipe lastOriginRecipe`
public static class GTRecipeNbt
{
	public static TagCompound Save(GTRecipe r)
	{
		var tag = new TagCompound
		{
			["recipeType"]  = r.RecipeType.RegistryName,
			["id"]          = r.Id,
			["duration"]    = r.Duration,
			["parallels"]   = r.Parallels,
			["subtick"]     = r.SubtickParallels,
			["batch"]       = r.BatchParallels,
			["ocLevel"]     = r.OcLevel,
			["groupColor"]  = r.GroupColor,
			["inputs"]      = SaveContentMap(r.Inputs),
			["outputs"]     = SaveContentMap(r.Outputs),
			["tickInputs"]  = SaveContentMap(r.TickInputs),
			["tickOutputs"] = SaveContentMap(r.TickOutputs),
			["inChance"]    = SaveChanceMap(r.InputChanceLogics),
			["outChance"]   = SaveChanceMap(r.OutputChanceLogics),
			["tinChance"]   = SaveChanceMap(r.TickInputChanceLogics),
			["toutChance"]  = SaveChanceMap(r.TickOutputChanceLogics),
			["conditions"]  = SaveConditions(r.Conditions),
		};
		if (r.CategoryId != null) tag["categoryId"] = r.CategoryId;
		if (r.Data is { } data && data.Count > 0) tag["data"] = data;
		return tag;
	}

	public static GTRecipe? Load(TagCompound tag)
	{
		var type = GTRecipeType.Get(tag.GetString("recipeType"));
		if (type is null) return null;

		var recipe = new GTRecipe(type, tag.GetString("id"),
			LoadContentMap(tag.Get<TagCompound>("inputs")),
			LoadContentMap(tag.Get<TagCompound>("outputs")),
			LoadContentMap(tag.Get<TagCompound>("tickInputs")),
			LoadContentMap(tag.Get<TagCompound>("tickOutputs")),
			LoadChanceMap(tag.Get<TagCompound>("inChance")),
			LoadChanceMap(tag.Get<TagCompound>("outChance")),
			LoadChanceMap(tag.Get<TagCompound>("tinChance")),
			LoadChanceMap(tag.Get<TagCompound>("toutChance")),
			LoadConditions(tag),
			Array.Empty<object>(),
			tag.ContainsKey("data") ? tag.Get<TagCompound>("data") : new TagCompound(),
			tag.GetInt("duration"),
			GTRecipeCategory.DEFAULT,
			tag.GetInt("groupColor"));

		recipe.Parallels        = tag.GetInt("parallels");
		recipe.SubtickParallels = tag.GetInt("subtick");
		recipe.BatchParallels   = tag.GetInt("batch");
		recipe.OcLevel          = tag.GetInt("ocLevel");
		if (tag.ContainsKey("categoryId")) recipe.CategoryId = tag.GetString("categoryId");
		return recipe;
	}

	private static string CapKey(object cap) =>
		ReferenceEquals(cap, ItemRecipeCapability.CAP)  ? "item"  :
		ReferenceEquals(cap, FluidRecipeCapability.CAP) ? "fluid" :
		ReferenceEquals(cap, EURecipeCapability.CAP)    ? "eu"    :
		ReferenceEquals(cap, CWURecipeCapability.CAP)   ? "cwu"   : "?";

	private static object? CapFromKey(string key) => key switch
	{
		"item"  => ItemRecipeCapability.CAP,
		"fluid" => FluidRecipeCapability.CAP,
		"eu"    => EURecipeCapability.CAP,
		"cwu"   => CWURecipeCapability.CAP,
		_       => null,
	};

	private static TagCompound SaveContentMap(Dictionary<object, List<Content.Content>> map)
	{
		var tag = new TagCompound();
		foreach (var (cap, list) in map)
		{
			string key = CapKey(cap);
			if (key == "?" || list is null) continue;
			var entries = new List<TagCompound>(list.Count);
			foreach (var c in list) entries.Add(SaveContent(key, c));
			tag[key] = entries;
		}
		return tag;
	}

	private static Dictionary<object, List<Content.Content>> LoadContentMap(TagCompound tag)
	{
		var map = new Dictionary<object, List<Content.Content>>();
		if (tag is null) return map;
		foreach (var kv in tag)
		{
			var cap = CapFromKey(kv.Key);
			if (cap is null) continue;
			var entries = tag.GetList<TagCompound>(kv.Key);
			var list = new List<Content.Content>(entries.Count);
			foreach (var e in entries) list.Add(LoadContent(kv.Key, e));
			map[cap] = list;
		}
		return map;
	}

	private static TagCompound SaveContent(string capKey, Content.Content c)
	{
		var tag = new TagCompound
		{
			["chance"]    = c.Chance,
			["maxChance"] = c.MaxChance,
			["boost"]     = c.TierChanceBoost,
			["payload"]   = SavePayload(capKey, c.Payload),
		};
		return tag;
	}

	private static Content.Content LoadContent(string capKey, TagCompound tag)
	{
		int chance    = tag.GetInt("chance");
		int maxChance = tag.GetInt("maxChance");
		int stored = tag.GetInt("boost");
		int rawBoost = maxChance > 0
			? Math.Sign(stored) * (int)Math.Round(Math.Abs(stored) * 10000.0 / maxChance)
			: stored;
		return new Content.Content(LoadPayload(capKey, tag.Get<TagCompound>("payload")),
			chance, maxChance, rawBoost);
	}

	private static TagCompound SavePayload(string capKey, object payload) => capKey switch
	{
		"item"  => SaveIngredient((IngredientBase)payload),
		"fluid" => SaveFluidIngredient((FluidIngredient)payload),
		"eu"    => SaveEnergy((EnergyStack)payload),
		"cwu"   => SaveCwu((int)payload),
		_       => new TagCompound(),
	};

	private static object LoadPayload(string capKey, TagCompound tag) => capKey switch
	{
		"item"  => LoadIngredient(tag),
		"fluid" => LoadFluidIngredient(tag),
		"eu"    => LoadEnergy(tag),
		"cwu"   => LoadCwu(tag),
		_       => throw new InvalidOperationException($"Unknown content cap key '{capKey}'"),
	};

	private static TagCompound SaveCwu(int cwu) => new() { ["cwu"] = cwu };

	private static int LoadCwu(TagCompound tag) => tag.GetInt("cwu");

	private static TagCompound SaveEnergy(EnergyStack e) =>
		new() { ["voltage"] = e.Voltage, ["amperage"] = e.Amperage };

	private static EnergyStack LoadEnergy(TagCompound tag) =>
		new(tag.GetLong("voltage"), tag.GetLong("amperage"));

	private static TagCompound SaveIngredient(IngredientBase ing)
	{
		var tag = new TagCompound { ["type"] = ing.GetTypeName() };
		switch (ing)
		{
			case SizedIngredient s:
				tag["amount"] = s.Amount;
				tag["inner"]  = SaveIngredient(s.Inner);
				break;
			case IntProviderIngredient ip:
				tag["provider"] = SaveProvider(ip.CountProvider);
				tag["inner"]    = SaveIngredient(ip.Inner);
				break;
			case IntCircuitIngredient c:
				tag["config"] = c.Configuration;
				break;
			case FluidContainerIngredient fc:
				tag["fluid"] = SaveFluidIngredient(fc.Fluid);
				break;
			case NBTPredicateIngredient n:
				tag["upstreamId"] = n.UpstreamId;
				if (string.IsNullOrEmpty(n.UpstreamId)) tag["itemType"] = n.ItemType;
				if (n.OutputNbt != null) tag["outputNbt"] = n.OutputNbt;
				break;
			case TagIngredient t:
				tag["tagName"] = t.TagName;
				if (string.IsNullOrEmpty(t.TagName) || t.TagName[0] == '$')
					tag["types"] = new List<int>(t.ResolvedTypes);
				break;
			case ItemStackIngredient i:
				tag["upstreamId"] = i.UpstreamId;
				if (string.IsNullOrEmpty(i.UpstreamId)) tag["itemType"] = i.ItemType;
				break;
		}
		return tag;
	}

	private static IngredientBase LoadIngredient(TagCompound tag)
	{
		switch (tag.GetString("type"))
		{
			case "gtceu:sized":
				return new SizedIngredient(LoadIngredient(tag.Get<TagCompound>("inner")), tag.GetInt("amount"));
			case "gtceu:int_provider":
				return IntProviderIngredient.Of(
					LoadIngredient(tag.Get<TagCompound>("inner")), LoadProvider(tag.Get<TagCompound>("provider")));
			case "gtceu:circuit":
				return IntCircuitIngredient.Of(tag.GetInt("config"));
			case "gtceu:fluid_container":
				return new FluidContainerIngredient(LoadFluidIngredient(tag.Get<TagCompound>("fluid")));
			case "forge:nbt":
			{
				string upstreamId = tag.GetString("upstreamId");
				string? outputNbt = tag.ContainsKey("outputNbt") ? tag.GetString("outputNbt") : null;
				int itemType = 0;
				if (outputNbt != null && NBTPredicateIngredient.ResolveItemTypeFromNbt is { } nbtHook)
					itemType = nbtHook(upstreamId, outputNbt);
				if (itemType == 0)
					itemType = ResolveItemTypeStable(upstreamId, tag);
				return NBTPredicateIngredient.Of(itemType,
					NBTPredicateIngredient.ALWAYS_TRUE, upstreamId, outputNbt);
			}
			case "minecraft:tag":
			{
				string tagName = tag.GetString("tagName");
				IReadOnlyList<int>? resolved = !string.IsNullOrEmpty(tagName) && tagName[0] != '$'
					? IIngredientResolver.Default?.ResolveItemTag(tagName)
					: null;
				List<int> types = resolved is { Count: > 0 }
					? new List<int>(resolved)
					: (tag.ContainsKey("types") ? new List<int>(tag.GetList<int>("types")) : new List<int>());
				return new TagIngredient(tagName, types);
			}
			case "minecraft:item":
			default:
			{
				string upstreamId = tag.GetString("upstreamId");
				return new ItemStackIngredient(ResolveItemTypeStable(upstreamId, tag), upstreamId);
			}
		}
	}

	private static int ResolveItemTypeStable(string upstreamId, TagCompound tag)
	{
		if (string.IsNullOrEmpty(upstreamId) || IIngredientResolver.Default is null)
			return tag.GetInt("itemType");
		return IIngredientResolver.Default.ResolveItemType(upstreamId);
	}

	private static TagCompound SaveFluidIngredient(FluidIngredient f)
	{
		var tag = new TagCompound { ["type"] = f.GetTypeName(), ["amount"] = f.Amount };
		if (f is IntProviderFluidIngredient ip)
		{
			tag["provider"] = SaveProvider(ip.CountProvider);
			tag["inner"]    = SaveFluidIngredient(ip.Inner);
			return tag;
		}
		if (f.ExactType != null)
		{
			tag["kind"]  = "exact";
			tag["fluid"] = f.ExactType.Id;
		}
		else if (f.Attribute != null)
		{
			tag["kind"] = "attr";
			tag["attr"] = f.Attribute.Id;
			tag["fluids"] = FluidIds(f.GetFluids());
		}
		else
		{
			tag["kind"] = "tag";
			tag["tagName"] = f.TagName ?? "";
			tag["fluids"]  = FluidIds(f.GetFluids());
		}
		return tag;
	}

	private static FluidIngredient LoadFluidIngredient(TagCompound tag)
	{
		if (tag.GetString("type") == "gtceu:int_provider_fluid")
			return IntProviderFluidIngredient.Of(
				LoadFluidIngredient(tag.Get<TagCompound>("inner")), LoadProvider(tag.Get<TagCompound>("provider")));

		int amount = tag.GetInt("amount");
		switch (tag.GetString("kind"))
		{
			case "exact":
			{
				var ft = FluidRegistry.Get(tag.GetString("fluid"));
				return ft != null
					? new FluidIngredient(ft, amount)
					: new FluidIngredient("", new List<FluidType>(), amount);
			}
			case "attr":
			{
				// ACID is the only ported FluidAttribute (FluidAttributes.ACID).
				return new FluidIngredient(FluidAttributes.ACID, LoadFluids(tag), amount);
			}
			case "tag":
			default:
				return new FluidIngredient(tag.GetString("tagName"), LoadFluids(tag), amount);
		}
	}

	private static List<string> FluidIds(IReadOnlyList<FluidType> fluids)
	{
		var ids = new List<string>(fluids.Count);
		foreach (var f in fluids) ids.Add(f.Id);
		return ids;
	}

	private static List<FluidType> LoadFluids(TagCompound tag)
	{
		var result = new List<FluidType>();
		foreach (var id in tag.GetList<string>("fluids"))
		{
			var ft = FluidRegistry.Get(id);
			if (ft != null) result.Add(ft);
		}
		return result;
	}

	private static TagCompound SaveProvider(IntProvider p)
	{
		var tag = new TagCompound { ["type"] = p.GetTypeName() };
		switch (p)
		{
			case ConstantInt c:        tag["value"] = c.Value; break;
			case UniformInt u:         tag["min"] = u.MinInclusive; tag["max"] = u.MaxInclusive; break;
			case BiasedToBottomInt b:  tag["min"] = b.MinInclusive; tag["max"] = b.MaxInclusive; break;
			case WeightedListInt w:
				var entries = new List<TagCompound>(w.Entries.Count);
				foreach (var (prov, weight) in w.Entries)
					entries.Add(new TagCompound { ["provider"] = SaveProvider(prov), ["weight"] = weight });
				tag["entries"] = entries;
				break;
		}
		return tag;
	}

	private static IntProvider LoadProvider(TagCompound tag)
	{
		switch (tag.GetString("type"))
		{
			case "minecraft:uniform":          return new UniformInt(tag.GetInt("min"), tag.GetInt("max"));
			case "minecraft:biased_to_bottom": return new BiasedToBottomInt(tag.GetInt("min"), tag.GetInt("max"));
			case "minecraft:weighted_list":
				var entries = new List<(IntProvider, int)>();
				foreach (var e in tag.GetList<TagCompound>("entries"))
					entries.Add((LoadProvider(e.Get<TagCompound>("provider")), e.GetInt("weight")));
				return new WeightedListInt(entries);
			case "minecraft:constant":
			default:                           return new ConstantInt(tag.GetInt("value"));
		}
	}

	private static TagCompound SaveChanceMap(Dictionary<object, ChanceLogic> map)
	{
		var tag = new TagCompound();
		foreach (var (cap, logic) in map)
		{
			string key = CapKey(cap);
			if (key != "?") tag[key] = logic.Name;
		}
		return tag;
	}

	private static Dictionary<object, ChanceLogic> LoadChanceMap(TagCompound tag)
	{
		var map = new Dictionary<object, ChanceLogic>();
		if (tag is null) return map;
		foreach (var kv in tag)
		{
			var cap = CapFromKey(kv.Key);
			if (cap is null) continue;
			map[cap] = ChanceLogicByName(tag.GetString(kv.Key));
		}
		return map;
	}

	private static ChanceLogic ChanceLogicByName(string name)
	{
		foreach (var c in ChanceLogic.All)
			if (c.Name == name) return c;
		return ChanceLogic.OR;
	}

	private static List<TagCompound> SaveConditions(List<RecipeCondition> conditions)
	{
		var list = new List<TagCompound>(conditions.Count);
		foreach (var c in conditions)
		{
			var tag = new TagCompound { ["type"] = c.GetTypeName(), ["reverse"] = c.IsReverse };
			switch (c)
			{
				case RainingCondition r:              tag["level"] = r.Level; break;
				case ThunderCondition t:              tag["level"] = t.Level; break;
				case DaytimeCondition d:              tag["daytime"] = d.Daytime; break;
				case DimensionCondition dim:          tag["id"] = dim.DimensionId; break;
				case BiomeCondition b:                tag["id"] = b.BiomeId; break;
				case BiomeTagCondition bt:            tag["id"] = bt.BiomeTag; break;
				case PositionYCondition py:           tag["min"] = py.MinY; tag["max"] = py.MaxY; break;
				case CleanroomCondition cr:           tag["id"] = cr.CleanroomType; break;
				case EUToStartCondition eu:           tag["eu"] = eu.EUToStart; break;
				case ResearchCondition res:           tag["id"] = res.ResearchId; break;
				case AdjacentBlockCondition ab:       tag["tile"] = (int)ab.RequiredTileType; tag["min"] = ab.MinCount; break;
				case AdjacentFluidCondition af:       tag["liquid"] = (int)af.RequiredLiquidType; tag["min"] = af.MinCount; break;
				case EnvironmentalHazardCondition eh: tag["hazard"] = eh.HazardType; break;
			}
			list.Add(tag);
		}
		return list;
	}

	private static List<RecipeCondition> LoadConditions(TagCompound tag)
	{
		var result = new List<RecipeCondition>();
		if (!tag.ContainsKey("conditions")) return result;
		foreach (var t in tag.GetList<TagCompound>("conditions"))
		{
			RecipeCondition? c = t.GetString("type") switch
			{
				"gtceu:rain"                 => new RainingCondition(t.GetFloat("level")),
				"gtceu:thunder"              => new ThunderCondition(t.GetFloat("level")),
				"gtceu:daytime"              => new DaytimeCondition(t.GetInt("daytime")),
				"gtceu:dimension"            => new DimensionCondition(t.GetString("id")),
				"gtceu:biome"                => new BiomeCondition(t.GetString("id")),
				"gtceu:biome_tag"            => new BiomeTagCondition(t.GetString("id")),
				"gtceu:pos_y"                => new PositionYCondition(t.GetInt("min"), t.GetInt("max")),
				"gtceu:cleanroom"            => new CleanroomCondition(t.GetString("id")),
				"gtceu:eu_to_start"          => new EUToStartCondition(t.GetLong("eu")),
				"gtceu:research"             => new ResearchCondition(t.GetString("id")),
				"gtceu:adjacent_block"       => new AdjacentBlockCondition((ushort)t.GetInt("tile"), t.GetInt("min")),
				"gtceu:adjacent_fluid"       => new AdjacentFluidCondition((short)t.GetInt("liquid"), t.GetInt("min")),
				"gtceu:vent"                 => new VentCondition(),
				"gtceu:environmental_hazard" => new EnvironmentalHazardCondition(t.GetString("hazard")),
				_                            => null,
			};
			if (c != null) result.Add(c.SetReverse(t.GetBool("reverse")));
		}
		return result;
	}
}
