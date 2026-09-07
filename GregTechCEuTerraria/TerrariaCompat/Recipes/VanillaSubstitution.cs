#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Items;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Recipes;

// Terraria substitutions
public static class VanillaItemMap
{
	private static readonly Dictionary<string, int> ExactItems = new()
	{
		{ "minecraft:stone",            ItemID.StoneBlock },
		{ "minecraft:cobblestone",      ItemID.StoneBlock },
		{ "minecraft:granite",          ItemID.Granite },
		{ "minecraft:andesite",         ItemID.SlushBlock },
		{ "minecraft:diorite",          ItemID.EbonstoneBlock },
		{ "minecraft:basalt",           ItemID.PearlstoneBlock },
		{ "minecraft:deepslate",        ItemID.HellstoneBrick },
		{ "minecraft:blackstone",       ItemID.AncientHellstoneBrick },
		{ "gtceu:marble",               ItemID.Marble },
		{ "gtceu:red_granite",          ItemID.CrimstoneBlock },
		{ "gtceu:rubber_sapling",       ItemID.Acorn },
		{ "gtceu:nightvision_goggles",  ItemID.NightVisionHelmet },
		{ "minecraft:cobblestone_slab", ItemID.StoneSlab },
		{ "minecraft:dirt",             ItemID.DirtBlock },
		{ "minecraft:gravel",           ItemID.SiltBlock },
		{ "minecraft:sand",             ItemID.SandBlock },
		{ "minecraft:sandstone",        ItemID.SandstoneBrick },
		{ "minecraft:obsidian",         ItemID.Obsidian },
		{ "minecraft:soul_sand",        ItemID.AshBlock },
		{ "minecraft:iron_bars",        ItemID.IronFence },
		{ "minecraft:tnt",              ItemID.Bomb },
		{ "gtceu:dynamite",             ItemID.Dynamite },
		{ "minecraft:snow_block",       ItemID.SnowBlock },
		{ "minecraft:snowball",         ItemID.Snowball },
		{ "minecraft:iron_ingot",       ItemID.IronBar },
		{ "minecraft:gold_ingot",       ItemID.GoldBar },
		{ "minecraft:copper_ingot",     ItemID.CopperBar },
		{ "gtceu:platinum_ingot",       ItemID.PlatinumBar },
		{ "gtceu:silver_ingot",         ItemID.SilverBar },
		{ "gtceu:tin_ingot",            ItemID.TinBar },
		{ "gtceu:lead_ingot",           ItemID.LeadBar },
		{ "minecraft:diamond",          ItemID.Diamond },
		{ "minecraft:emerald",          ItemID.Emerald },
		{ "minecraft:amethyst_shard", 	ItemID.Amethyst },
		{ "gtceu:amethyst_gem",		 	ItemID.Amethyst },
		{ "gtceu:ruby_gem", 			ItemID.Ruby },
		{ "gtceu:topaz_gem",			ItemID.Topaz },
		{ "gtceu:sapphire_gem",			ItemID.Sapphire },
		{ "gtceu:redstone_gem",			ItemID.Amber },
		{ "minecraft:torch",            ItemID.Torch },
		{ "minecraft:glass_pane",       ItemID.GlassWall },
		{ "minecraft:bone",             ItemID.Bone },
		{ "minecraft:anvil",            ItemID.IronAnvil },
		{ "minecraft:book",             ItemID.Book },
		{ "minecraft:bookshelf",        ItemID.Bookcase },
		{ "minecraft:lantern",          ItemID.ChainLantern },
		{ "minecraft:pumpkin",          ItemID.Pumpkin },
		{ "minecraft:pumpkin_seeds",    ItemID.PumpkinSeed },
		{ "minecraft:jack_o_lantern",   ItemID.JackOLantern },
		{ "minecraft:slime_ball",       ItemID.Gel },
		{ "minecraft:gunpowder",        ItemID.ExplosivePowder },
		{ "minecraft:string",           ItemID.Silk },
		{ "minecraft:feather",          ItemID.Feather },
		{ "minecraft:leather",          ItemID.Leather },
		{ "minecraft:bucket",           ItemID.EmptyBucket },
		{ "minecraft:cauldron",         ItemID.Cauldron },
		{ "minecraft:clock",            ItemID.GoldWatch },
		{ "minecraft:compass",          ItemID.Compass },
		{ "minecraft:glass_bottle",     ItemID.Bottle },
		{ "minecraft:bow",              ItemID.WoodenBow },
		{ "minecraft:chest",            ItemID.Chest },
		{ "minecraft:loom",             ItemID.Loom },
		{ "minecraft:crafting_table",   ItemID.WorkBench },
		{ "minecraft:white_wool",       ItemID.Cloud },
		{ "minecraft:dropper",          ItemID.DartTrap },
		{ "minecraft:item_frame",       ItemID.ItemFrame },
		{ "gtceu:nano_saber",           ItemID.RodofDiscord },
		{ "minecraft:painting",         ItemID.PlacePainting },
		{ "minecraft:redstone_torch",   ItemID.Wire },
		{ "minecraft:lever",            ItemID.Lever },
		{ "minecraft:heavy_weighted_pressure_plate", ItemID.GrayPressurePlate },
		{ "minecraft:light_weighted_pressure_plate", ItemID.YellowPressurePlate },
		{ "minecraft:iron_door",        ItemID.IronDoor },
		{ "minecraft:piston",           ItemID.Grate },
		{ "minecraft:flower_pot",       ItemID.ClayPot },
		{ "minecraft:furnace",          ItemID.Furnace },
		{ "minecraft:blast_furnace",    ItemID.Hellforge },
		{ "minecraft:chain",            ItemID.Chain },
		{ "gtceu:liquid_fuel_jetpack",     ItemID.CreativeWings },  // Fledgling Wings
		{ "gtceu:electric_jetpack",        ItemID.FairyWings },
		{ "gtceu:advanced_electric_jetpack", ItemID.Jetpack },
		{ "minecraft:apple",                  ItemID.Apple },
		{ "minecraft:cactus",                 ItemID.Cactus },
		{ "minecraft:sugar_cane",             ItemID.BambooBlock },
		{ "minecraft:red_mushroom",           ItemID.Mushroom },
		{ "minecraft:glow_berries",           ItemID.GlowingMushroom },
		{ "minecraft:enchanted_golden_apple", ItemID.CandyApple },
		{ "minecraft:beef",                   ItemID.Worm },
		{ "minecraft:fishing_rod",      ItemID.GoldenFishingRod },
		{ "minecraft:cod",              ItemID.AtlanticCod },
		{ "minecraft:pufferfish",       ItemID.BalloonPufferfish },
		{ "minecraft:salmon",           ItemID.Salmon },
		{ "minecraft:tropical_fish",    ItemID.NeonTetra },
		{ "minecraft:rail",             ItemID.MinecartTrack },
		{ "minecraft:powered_rail",     ItemID.BoosterTrack },
		{ "minecraft:detector_rail",    ItemID.PressureTrack },
		{ "minecraft:minecart",         ItemID.MinecartMech },
		{ "minecraft:red_dye",          ItemID.RedDye },
		{ "minecraft:orange_dye",       ItemID.OrangeDye },
		{ "minecraft:yellow_dye",       ItemID.YellowDye },
		{ "minecraft:lime_dye",         ItemID.LimeDye },
		{ "minecraft:green_dye",        ItemID.GreenDye },
		{ "minecraft:cyan_dye",         ItemID.CyanDye },
		{ "minecraft:light_blue_dye",   ItemID.SkyBlueDye },
		{ "minecraft:blue_dye",         ItemID.BlueDye },
		{ "minecraft:purple_dye",       ItemID.PurpleDye },
		{ "minecraft:magenta_dye",      ItemID.VioletDye },
		{ "minecraft:pink_dye",         ItemID.PinkDye },
		{ "minecraft:brown_dye",        ItemID.BrownDye },
		{ "minecraft:black_dye",        ItemID.BlackDye },
		{ "minecraft:white_dye",        ItemID.BrightSilverDye },
		{ "minecraft:light_gray_dye",   ItemID.SilverDye },
		{ "minecraft:gray_dye",         ItemID.BlackAndWhiteDye },
		{ "gtceu:chemical_red_dye",        ItemID.RedDye },
		{ "gtceu:chemical_orange_dye",     ItemID.OrangeDye },
		{ "gtceu:chemical_yellow_dye",     ItemID.YellowDye },
		{ "gtceu:chemical_lime_dye",       ItemID.LimeDye },
		{ "gtceu:chemical_green_dye",      ItemID.GreenDye },
		{ "gtceu:chemical_cyan_dye",       ItemID.CyanDye },
		{ "gtceu:chemical_light_blue_dye", ItemID.SkyBlueDye },
		{ "gtceu:chemical_blue_dye",       ItemID.BlueDye },
		{ "gtceu:chemical_purple_dye",     ItemID.PurpleDye },
		{ "gtceu:chemical_magenta_dye",    ItemID.VioletDye },
		{ "gtceu:chemical_pink_dye",       ItemID.PinkDye },
		{ "gtceu:chemical_brown_dye",      ItemID.BrownDye },
		{ "gtceu:chemical_black_dye",      ItemID.BlackDye },
		{ "gtceu:chemical_white_dye",      ItemID.BrightSilverDye },
		{ "gtceu:chemical_light_gray_dye", ItemID.SilverDye },
		{ "gtceu:chemical_gray_dye",       ItemID.BlackAndWhiteDye },
		{ "gtceu:red_dye_spray_can",        ItemID.RedPaint },
		{ "gtceu:orange_dye_spray_can",     ItemID.OrangePaint },
		{ "gtceu:yellow_dye_spray_can",     ItemID.YellowPaint },
		{ "gtceu:lime_dye_spray_can",       ItemID.LimePaint },
		{ "gtceu:green_dye_spray_can",      ItemID.GreenPaint },
		{ "gtceu:cyan_dye_spray_can",       ItemID.CyanPaint },
		{ "gtceu:light_blue_dye_spray_can", ItemID.SkyBluePaint },
		{ "gtceu:blue_dye_spray_can",       ItemID.BluePaint },
		{ "gtceu:purple_dye_spray_can",     ItemID.PurplePaint },
		{ "gtceu:magenta_dye_spray_can",    ItemID.VioletPaint },
		{ "gtceu:pink_dye_spray_can",       ItemID.PinkPaint },
		{ "gtceu:brown_dye_spray_can",      ItemID.BrownPaint },
		{ "gtceu:black_dye_spray_can",      ItemID.BlackPaint },
		{ "gtceu:white_dye_spray_can",      ItemID.WhitePaint },
		{ "gtceu:light_gray_dye_spray_can", ItemID.GrayPaint },
		{ "gtceu:gray_dye_spray_can",       ItemID.GrayPaint },
		{ "gtceu:fertilizer",           ItemID.Fertilizer },
		{ "minecraft:glass",            ItemID.Glass },
		{ "minecraft:clay",             ItemID.ClayBlock },
		{ "minecraft:ice",              ItemID.IceBlock },
		{ "minecraft:honeycomb_block",  ItemID.HoneyBlock },
		{ "minecraft:bone_block",       ItemID.BoneBlock },
		{ "minecraft:bricks",           ItemID.RedBrick },
	};

	private static readonly Dictionary<string, (string Material, string Prefix)> MaterialSubs = new()
	{
		{ "minecraft:stick",            ("wood", "rod") },
		{ "minecraft:gold_nugget",      ("gold", "nugget") },
		{ "minecraft:iron_nugget",      ("iron", "nugget") },
		{ "minecraft:iron_block",       ("iron", "block") },
		{ "minecraft:gold_block",       ("gold", "block") },
		{ "minecraft:copper_block",     ("copper", "block") },
		{ "minecraft:diamond_block",    ("diamond", "block") },
		{ "minecraft:emerald_block",    ("emerald", "block") },
		{ "minecraft:lapis_block",      ("lapis", "block") },
		{ "minecraft:amethyst_block",   ("amethyst", "block") },
		{ "minecraft:coal_block",       ("coal", "block") },
		{ "minecraft:quartz_block",     ("nether_quartz", "block") },
		{ "minecraft:raw_iron_block",   ("iron", "raw_ore_block") },
		{ "minecraft:raw_gold_block",   ("gold", "raw_ore_block") },
		{ "minecraft:raw_copper_block", ("copper", "raw_ore_block") },
		{ "minecraft:glowstone_dust",   ("glowstone", "dust") },
		{ "minecraft:redstone",         ("redstone", "dust") },
		{ "minecraft:sugar",            ("sugar", "dust") },
		{ "minecraft:blaze_powder",     ("blaze", "dust") },
		{ "minecraft:bone_meal",        ("bone", "dust") },
		{ "minecraft:lapis_lazuli",     ("lapis", "gem") },
		{ "minecraft:quartz",           ("nether_quartz", "gem") },
		{ "minecraft:coal",             ("coal", "gem") },
		{ "minecraft:charcoal",         ("charcoal", "gem") },
		{ "minecraft:flint",            ("flint", "gem") },
		{ "minecraft:ender_pearl",      ("ender_pearl", "gem") },
		{ "minecraft:ender_eye",        ("ender_eye", "gem") },
		{ "minecraft:echo_shard",       ("echo_shard", "gem") },
		{ "minecraft:nether_star",      ("nether_star", "gem") },
		{ "minecraft:clay_ball",        ("clay", "gem") },
		{ "minecraft:netherite_ingot",  ("netherite", "ingot") },
		{ "minecraft:brick",            ("brick", "ingot") },
		{ "minecraft:honeycomb",        ("wax", "ingot") },
		{ "minecraft:blaze_rod",        ("blaze", "rod") },
		{ "minecraft:redstone_block",   ("redstone", "block") },
		{ "minecraft:glowstone",        ("glowstone", "block") },
		{ "minecraft:paper",            ("paper", "plate") },
	};

	private static readonly (string Suffix, int Item)[] SuffixRules = new (string, int)[]
	{
		("_log",      ItemID.Wood),         // oak_log, stripped_birch_log, ...
		("_wood",     ItemID.Wood),         // oak_wood, stripped_birch_wood, ...
		("_stem",     ItemID.Wood),         // crimson_stem, warped_stem
		("_sapling",  ItemID.Acorn),        // oak_sapling, birch_sapling, ...
	};

	private static readonly (string Suffix, string Material, string Prefix)[] SuffixMaterialSubs =
		new (string, string, string)[]
	{
		("_planks", "wood", "plate"),
	};

	private static readonly Dictionary<string, (string Material, string Prefix)> TagMaterialSubs = new()
	{
		{ "minecraft:planks", ("wood", "plate") },
	};

	private static readonly Dictionary<string, int> TagItems = new()
	{
		{ "minecraft:saplings",                   ItemID.Acorn },
		{ "minecraft:stone_crafting_materials",   ItemID.StoneBlock },
		{ "forge:saplings",                       ItemID.Acorn },
		{ "forge:cobblestone",                    ItemID.StoneBlock },
		{ "forge:glass",                          ItemID.Glass },
		{ "forge:corals",                         ItemID.Coral },
		{ "forge:corals/alive",                   ItemID.Coral },
		{ "forge:corals/dead",                    ItemID.Coral },
		{ "forge:coral_fans/alive",               ItemID.Coral },
		{ "forge:coral_fans/dead",                ItemID.Coral },
		{ "forge:coral_plants/alive",             ItemID.Coral },
		{ "forge:coral_plants/dead",              ItemID.Coral },
		{ "forge:coral_blocks",                   ItemID.CoralstoneBlock },
		{ "forge:coral_blocks/alive",             ItemID.CoralstoneBlock },
		{ "forge:coral_blocks/dead",              ItemID.CoralstoneBlock },
		{ "minecraft:coals",                      ItemID.Coal },
		{ "forge:chests/wooden",                  ItemID.Chest },
		{ "forge:pistons",                        ItemID.Grate },
		{ "minecraft:wool",                       ItemID.Cloud },
		{ "forge:glass_panes",                    ItemID.GlassWall },
		{ "forge:sand",                           ItemID.SandBlock },
		{ "minecraft:sand",                       ItemID.SandBlock },
		{ "minecraft:smelts_to_glass",            ItemID.SandBlock },
	};

	private static readonly Dictionary<string, int[]> MultiTagItems = new()
	{
		{ "minecraft:fishes", new int[] {
			ItemID.Bass, ItemID.Trout, ItemID.Salmon, ItemID.AtlanticCod,
			ItemID.Tuna, ItemID.RedSnapper, ItemID.NeonTetra,
			ItemID.BalloonPufferfish,
		}},
		{ "forge:marble",       new int[] { ItemID.Marble } },
		{ "forge:granite_red",  new int[] { ItemID.CrimstoneBlock } },
	};

	private static readonly Dictionary<string, int> Groups = new()
	{
		{ "minecraft:logs",             RecipeGroupID.Wood },
		{ "minecraft:logs_that_burn",   RecipeGroupID.Wood },
		{ "minecraft:oak_logs",         RecipeGroupID.Wood },
		{ "forge:oak_logs",             RecipeGroupID.Wood },
		{ "forge:wood",                 RecipeGroupID.Wood },
		{ "forge:ingots/iron",          RecipeGroupID.IronBar },
		{ "minecraft:sand",             RecipeGroupID.Sand },
		{ "forge:sand",                 RecipeGroupID.Sand },
	};

	public static HashSet<int> SubstitutedVanillaIngotBars()
	{
		var set = new HashSet<int>();
		foreach (var kv in ExactItems)
			if (kv.Value > 0 && kv.Key.EndsWith("_ingot", System.StringComparison.Ordinal))
				set.Add(kv.Value);
		return set;
	}

	public static bool TryGet(string item, out int itemType)
	{
		if (ExactItems.TryGetValue(item, out itemType)) return true;
		if (MaterialSubs.TryGetValue(item, out var mp))
		{
			var t = MaterialItemRegistry.Get(mp.Material, mp.Prefix);
			if (t.HasValue) { itemType = t.Value; return true; }
		}
		if (item.StartsWith("minecraft:", System.StringComparison.Ordinal))
		{
			foreach (var (suffix, mat, prefix) in SuffixMaterialSubs)
			{
				if (!item.EndsWith(suffix)) continue;
				var t = MaterialItemRegistry.Get(mat, prefix);
				if (t.HasValue) { itemType = t.Value; return true; }
			}
			foreach (var (suffix, t) in SuffixRules)
			{
				if (item.EndsWith(suffix)) { itemType = t; return true; }
			}
		}
		itemType = 0;
		return false;
	}

	public static bool TryGetTagItem(string tag, out int itemType)
	{
		if (TagItems.TryGetValue(tag, out itemType)) return true;
		if (TagMaterialSubs.TryGetValue(tag, out var mp))
		{
			var t = MaterialItemRegistry.Get(mp.Material, mp.Prefix);
			if (t.HasValue) { itemType = t.Value; return true; }
		}
		itemType = 0;
		return false;
	}

	public static bool TryGetTagItems(string tag, out int[] itemTypes)
	{
		if (MultiTagItems.TryGetValue(tag, out var arr)) { itemTypes = arr; return true; }
		itemTypes = System.Array.Empty<int>();
		return false;
	}

	private static readonly Dictionary<string, int> _runtimeGroups = new();

	public static void RegisterGroup(string tag, int groupId) => _runtimeGroups[tag] = groupId;

	public static bool TryGetGroup(string tag, out int id) =>
		Groups.TryGetValue(tag, out id) || _runtimeGroups.TryGetValue(tag, out id);

	private static readonly HashSet<int> GtFungibleGroups = new() { RecipeGroupID.Wood };

	private static readonly Dictionary<int, RecipeGroupItemView> _groupViews = new();

	public static IReadOnlyList<int> GroupItemView(int groupId)
	{
		if (!_groupViews.TryGetValue(groupId, out var v))
			_groupViews[groupId] = v = new RecipeGroupItemView(groupId);
		return v;
	}

	public static bool TryGetFungibleGroupView(string tag, out IReadOnlyList<int> view)
	{
		if (TryGetGroup(tag, out int gid) && GtFungibleGroups.Contains(gid))
		{
			view = GroupItemView(gid);
			return true;
		}
		view = System.Array.Empty<int>();
		return false;
	}

	public static bool TryGetFungibleGroupName(string tag, out string name)
	{
		if (TryGetGroup(tag, out int gid) && GtFungibleGroups.Contains(gid)
		    && Terraria.RecipeGroup.recipeGroups.TryGetValue(gid, out var g))
		{
			name = g.GetText();
			return true;
		}
		name = "";
		return false;
	}
}

internal sealed class RecipeGroupItemView : IReadOnlyList<int>
{
	private readonly int _groupId;
	private int[] _cache = System.Array.Empty<int>();

	public RecipeGroupItemView(int groupId) => _groupId = groupId;

	private int[] Resolve()
	{
		if (Terraria.RecipeGroup.recipeGroups.TryGetValue(_groupId, out var g))
		{
			if (_cache.Length != g.ValidItems.Count)
				_cache = System.Linq.Enumerable.ToArray(g.ValidItems);
		}
		return _cache;
	}

	public int this[int index] => Resolve()[index];
	public int Count => Resolve().Length;
	public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)Resolve()).GetEnumerator();
	System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Resolve().GetEnumerator();
}
