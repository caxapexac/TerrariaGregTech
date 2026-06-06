#!/usr/bin/env python3
"""
Snapshot upstream's per-recipe runData JSON output into a single bundled
file for our recipe pipeline.

Workflow:
  1. In `GregTech-Modern-1.20.1/`, run `./gradlew runData`. This produces
     ~33k JSON files under `runtime/data/gtceu/recipes/<type>/<id>.json`
     (and various sub-paths) using upstream's GTRecipeSerializer schema.
  2. Run this script to walk that tree, derive `<type>/<id>` from each
     file's relative path, inject it as the recipe's `id` field, and emit
     one flat JSON array.
  3. Loader (`Common/Recipes/RecipeJsonLoader.cs`) reads the bundle, calls
     `GTRecipeSerializer.Read` on each entry.

Why bundled:
  - 33k small files is a checkout/loadtime headache; a single sequential-
    read JSON is faster.
  - One file ships easier in the mod distribution.
  - Diff-friendly (one big file shows recipe drift across upstream versions).

Default invocation:
  python tools/scripts/snapshot-recipes.py

Custom paths:
  python tools/scripts/snapshot-recipes.py \\
      --input  GregTech-Modern-1.20.1/runtime/data/gtceu/recipes \\
      --output GregTechCEuTerraria/Data/Recipes/all.json \\
      --clean
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from collections import Counter
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_INPUT  = REPO_ROOT / "GregTech-Modern-1.20.1" / "src" / "generated" / "resources" / "data" / "gtceu" / "recipes"
DEFAULT_OUTPUT = REPO_ROOT / "GregTechCEuTerraria" / "Data" / "Recipes" / "all.json"


def walk_recipes(input_dir: Path):
    """Yield (recipe_id, json_obj) for every .json file under input_dir.

    recipe_id is the path relative to input_dir, minus the .json extension,
    with backslashes normalized to forward slashes (Windows compatibility).
    Examples:
      runtime/data/gtceu/recipes/macerator/iron_dust.json  -> id="macerator/iron_dust"
      .../assembler/circuits/lv_circuit.json               -> id="assembler/circuits/lv_circuit"
    """
    if not input_dir.is_dir():
        raise SystemExit(f"Input directory not found: {input_dir}\n"
                         "Did you run `./gradlew runData` in the upstream tree?")

    for json_path in sorted(input_dir.rglob("*.json")):
        rel = json_path.relative_to(input_dir).with_suffix("")
        recipe_id = str(rel).replace(os.sep, "/")
        try:
            with json_path.open(encoding="utf-8") as f:
                obj = json.load(f)
        except json.JSONDecodeError as e:
            print(f"  WARN: skipping malformed JSON {json_path}: {e}", file=sys.stderr)
            continue
        yield recipe_id, obj


NON_OAK_WOOD = (
    "spruce", "birch", "jungle", "acacia", "dark_oak",
    "mangrove", "cherry", "bamboo", "crimson", "warped",
)


def is_non_oak_wood_recipe(recipe_id: str) -> bool:
    rid = recipe_id.lower()
    return any(sp in rid for sp in NON_OAK_WOOD)


def clean_legacy(legacy_dir: Path) -> int:
    """Delete legacy per-station JSON files (our old normalized format)."""
    if not legacy_dir.is_dir():
        return 0
    removed = 0
    for f in legacy_dir.glob("*.json"):
        if f.name == "all.json":
            continue
        f.unlink()
        removed += 1
    return removed


# Materials / items deliberately not ported.
# Any recipe mentioning one - in its id OR anywhere in its body - is dropped
REMOVED_TOKENS = (
    "sculk", "end_rod", "grass_block", "mycelium", "terracotta", "mud",
    "bubble_coral", "brain_coral", "fire_coral", "horn_coral", "tube_coral",
    "composter", "hopper", "chest_minecart", "furnace_minecart", "tnt_minecart",
    "cartography_table", "industrial_tnt", "chest_boat", "wheat", "fire_charge",
    "saddle", "lectern", "chiseled_bookshelf", "enchanted_book", "writable_book",
    "cake", "wooden_slabs", "iron_door", "iron_trapdoor", "oak_trapdoor", "wooden_trapdoors",
    "trapped_chest", "soul_lantern", "sea_lantern", "soul_torch", "magma_cream",
    "cut_copper", "oxidized_copper", "exposed_copper", "weathered_copper", "waxed",
    "name_tag", "moss", "comparator", "tinted_glass", "tripwire_hook",
    "chipped_anvil", "damaged_anvil", "beacon", "enchanting_table", "banner",
    "orange_wool", "magenta_wool", "light_blue_wool", "yellow_wool", "lime_wool",
    "pink_wool", "gray_wool", "light_gray_wool", "cyan_wool", "purple_wool",
    "blue_wool", "brown_wool", "green_wool", "red_wool", "black_wool",
    "polished_andesite", "polished_blackstone", "polished_deepslate",
    "polished_diorite", "polished_granite",
    "deepslate_tile", "end_stone", "red_sand", "netherrack",
    "conduit", "bowl", "crossbow", "daylight_detector", "dead_bush",
    "dried_kelp_block", "fletching_table", "azalea", "jukebox", "ladder",
    "note_block", "scaffolding", "smithing_table",
    "oak_slab", "oak_stairs", "oak_boat", "oak_door", "oak_fence", "oak_sign",
    "smooth_", "smoker", "bread", "dough",
    "treated_wood_boat", "treated_wood_door", "treated_wood_fence",
    "treated_wood_sign", "treated_wood_slab", "treated_wood_stairs",
    "treated_wood_trapdoor",
    "candle", "white_bed", "white_carpet",
    "end_crystal", "ghast_tear", "polished_basalt", "stained_glass",
    "activator_rail", "nether_brick",
    "hazmat", "flint_and_steel", "horse_armor", "sticky_piston", "iron_door",
    "slime_block", "ender_chest", "rubber_slab", "glass_vial",
    "rubber_boat", "rubber_door", "rubber_gloves", "rubber_fence", "rubber_trapdoor",
    "rubber_sign",
    "_indicator",
    "_pressure_plate", "_button",
    "chainmail", "face_mask",
    "bronze_helmet", "bronze_chestplate", "bronze_leggings", "bronze_boots",
    "steel_helmet", "steel_chestplate", "steel_leggings", "steel_boots",
    "titanium_helmet", "titanium_chestplate", "titanium_leggings", "titanium_boots",
    "rubber_stairs", "coarse_dirt", "lodestone", "stone_bricks", "cut_sandstone",
    "chiseled_sandstone", "dispenser", "observer", "respawn_anchor", "stonecutter",
    "dripstone_block", "pointed_dripstone",
    "azure_bluet", "blue_orchid", "cornflower", "dandelion", "lilac",
    "lily_of_the_valley", "oxeye_daisy", "peony", "poppy", "rose_bush",
    "wither_rose", "_tulip", "sunflower", "torchflower", "pink_petals",
    "pitcher_plant", "sea_pickle", "rubber_leaves",
    "beetroot", "carrot", "melon_seeds",
    "brown_mushroom", "kelp", "pitcher_pod", "potato", "sweet_berries",
    "ink_sac", "music_disc", "deepslate_bricks",
    "energy_converter",
    # Monitors (central_monitor / advanced_monitor / monitor) and their
    # casing + computer_monitor_cover - not ported. Bare `monitor` token
    # catches every variant (monitor / advanced_monitor / central_monitor /
    # monitor_casing / computer_monitor_cover).
    "monitor",
    "charcoal_pile_igniter", # not ported
    "chicken", "rabbit", "porkchop", "mutton",
    "melon", "packed_ice", "blue_ice",
    # Every GregTech coloured lamp - not ported
    "borderless_lamp", "redstone_lamp",
    "white_lamp", "orange_lamp", "magenta_lamp", "light_blue_lamp",
    "yellow_lamp", "lime_lamp", "pink_lamp", "gray_lamp", "light_gray_lamp",
    "cyan_lamp", "purple_lamp", "blue_lamp", "brown_lamp", "green_lamp",
    "red_lamp", "black_lamp",
    "andesite_wall", "andesite_stairs", "andesite_slab",
    "diorite_wall", "diorite_stairs", "diorite_slab",
    "granite_wall", "granite_stairs", "granite_slab",
    "blackstone_wall", "blackstone_slab",
    "sandstone_wall", "sandstone_slab",
    "cobblestone_wall", "stone_stairs",
    "brick_wall", "brick_stairs", "brick_slab",
    "quartz_stairs",
    "calcite", "cobbled_deepslate", "chiseled_deepslate",
    "purpur", "prismarine",
    "quartz_bricks", "quartz_pillar", "chiseled_quartz_block",
    "nether_wart", "spider_eye", "cocoa_beans", "shulker", "chorus",
    "beehive", "hanging_sign",
    "grindstone",
    "duct_pipe", # not ported
)

REMOVED_EXACT_IDS = (
    "minecraft:snow",
    "minecraft:golden_apple",
    "minecraft:barrel",
    "minecraft:map",
    "minecraft:lead",
    "minecraft:target",
    "minecraft:allium",
    "minecraft:wooden_axe", "minecraft:wooden_pickaxe", "minecraft:wooden_sword",
    "minecraft:wooden_shovel", "minecraft:wooden_hoe",
    "minecraft:stone_axe", "minecraft:stone_pickaxe", "minecraft:stone_sword",
    "minecraft:stone_shovel", "minecraft:stone_hoe",
    "gtceu:nanomuscle_boots", "gtceu:quarktech_boots",
)


KEEP_RECIPE_IDS = frozenset((
    "assembler/cover_fluid_detector",
    "assembler/cover_item_detector",
    "assembler/cover_shutter",
))

REMOVED_RECIPE_IDS = frozenset((
    "arc_furnace/arc_fluid_detector_cover",
    "macerator/macerate_fluid_detector_cover",
    "arc_furnace/arc_item_detector_cover",
    "macerator/macerate_item_detector_cover",
    "arc_furnace/arc_shutter_module_cover",
    "macerator/macerate_shutter_module_cover",
))


def mentions_removed(recipe_id, obj):
    """True if a recipe involves any REMOVED_TOKENS (substring) or
    REMOVED_EXACT_IDS (whole-string) material/item."""
    if recipe_id in KEEP_RECIPE_IDS:
        return False
    rid = recipe_id.lower()
    if any(tok in rid for tok in REMOVED_TOKENS):
        return True

    def walk(x):
        if isinstance(x, dict):
            return any(walk(v) for v in x.values())
        if isinstance(x, list):
            return any(walk(v) for v in x)
        if not isinstance(x, str):
            return False
        s = x.lower()
        return (any(tok in s for tok in REMOVED_TOKENS)
                or s in REMOVED_EXACT_IDS)

    return walk(obj)


ORE_HOSTS = (
    "granite", "red_granite", "diorite", "andesite", "deepslate", "tuff",
    "marble", "basalt", "blackstone", "netherrack", "endstone",
    "sand", "red_sand", "gravel",
)


def _is_alt_host_ore_id(item_id):
    """True for an id like gtceu:granite_iron_ore - a host-prefixed ore."""
    if not (item_id.startswith("gtceu:") and item_id.endswith("_ore")):
        return False
    bare = item_id[len("gtceu:"):]
    for h in ORE_HOSTS:
        if bare.startswith(h + "_") and bare[len(h) + 1:].endswith("_ore"):
            return True
    return False


def is_alt_host_ore_recipe(obj):
    """True if a recipe references any alt-stone-host ore item."""
    def walk(x):
        if isinstance(x, dict):
            return any(walk(v) for v in x.values())
        if isinstance(x, list):
            return any(walk(v) for v in x)
        return isinstance(x, str) and _is_alt_host_ore_id(x)

    return walk(obj)


# Applied Energistics 2 (ME network) parts are not ported until the AE2
# integration lands
def is_ae2_recipe(obj):
    """True if a recipe references any AE2 ME part (gtceu:me_* item id)."""
    def walk(x):
        if isinstance(x, dict):
            return any(walk(v) for v in x.values())
        if isinstance(x, list):
            return any(walk(v) for v in x)
        return isinstance(x, str) and x.startswith("gtceu:me_")

    return walk(obj)


# Concrete blocks are explicitly not ported except crafting ingredients
KEEP_CONCRETE = ("concrete_dust", "dusts/concrete", "concrete_bucket")


def is_concrete_block_recipe(recipe_id, obj):
    """True if a recipe references a concrete BLOCK (MC coloured concrete /
    concrete_powder, or a GregTech light/dark concrete building block).
    Recipes touching only the concrete dust material / fluid are kept."""
    def is_block_token(s):
        return ("concrete" in s.lower()
                and not any(k in s.lower() for k in KEEP_CONCRETE))

    if is_block_token(recipe_id):
        return True

    def walk(x):
        if isinstance(x, dict):
            return any(walk(v) for v in x.values())
        if isinstance(x, list):
            return any(walk(v) for v in x)
        return isinstance(x, str) and is_block_token(x)

    return walk(obj)


def is_stripped_wood_recipe(recipe_id, obj):
    if "stripped" in recipe_id.lower():
        return True

    def walk(x):
        if isinstance(x, dict):
            return any(walk(v) for v in x.values())
        if isinstance(x, list):
            return any(walk(v) for v in x)
        return isinstance(x, str) and "stripped" in x.lower()

    return walk(obj)



REMOVED_RECIPE_TYPES = (
    "minecraft:smithing_transform",
    "gtceu:crafting_facade_cover",
    "gtceu:crafting_tool_head_replace",
)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--input",  type=Path, default=DEFAULT_INPUT,
                        help=f"upstream runData recipe directory (default: {DEFAULT_INPUT.relative_to(REPO_ROOT)})")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT,
                        help=f"bundled output file (default: {DEFAULT_OUTPUT.relative_to(REPO_ROOT)})")
    parser.add_argument("--clean", action="store_true",
                        help="delete legacy per-station JSON files in the output directory after writing the bundle")
    parser.add_argument("--pretty", action="store_true",
                        help="emit pretty-printed JSON (larger file, easier to diff)")
    args = parser.parse_args()

    print(f"Scanning {args.input.relative_to(REPO_ROOT)}...")
    recipes = []
    type_counts: Counter[str] = Counter()
    dropped_wood = 0
    dropped_removed = 0
    dropped_ore_host = 0
    dropped_concrete = 0
    dropped_ae2 = 0
    dropped_stripped = 0
    dropped_recipe_type = 0
    dropped_recipe_id = 0
    for recipe_id, obj in walk_recipes(args.input):
        entry = {"id": recipe_id}
        entry.update(obj)
        if recipe_id in REMOVED_RECIPE_IDS:
            dropped_recipe_id += 1
            continue
        if obj.get("type") in REMOVED_RECIPE_TYPES:
            dropped_recipe_type += 1
            continue
        if is_non_oak_wood_recipe(recipe_id):
            dropped_wood += 1
            continue
        if mentions_removed(recipe_id, obj):
            dropped_removed += 1
            continue
        if is_alt_host_ore_recipe(obj):
            dropped_ore_host += 1
            continue
        if is_concrete_block_recipe(recipe_id, obj):
            dropped_concrete += 1
            continue
        if is_ae2_recipe(obj):
            dropped_ae2 += 1
            continue
        if is_stripped_wood_recipe(recipe_id, obj):
            dropped_stripped += 1
            continue
        # Rock Crusher: strip the `adjacent_fluid` (lava + water) recipe condition
        if entry.get("type") == "gtceu:rock_breaker":
            entry.pop("recipeConditions", None)
        recipes.append(entry)
        recipe_type = obj.get("type", "unknown")
        # Strip "gtceu:" / "minecraft:" prefix for the histogram.
        type_counts[recipe_type.split(":", 1)[-1]] += 1

    if not recipes:
        raise SystemExit("No recipes found - check --input path.")

    if dropped_wood:
        print(f"  dropped {dropped_wood:,} non-oak wood-species recipes (kept oak only)")
    if dropped_removed:
        print(f"  dropped {dropped_removed:,} recipes for unported content ({', '.join(REMOVED_TOKENS)})")
    if dropped_ore_host:
        print(f"  dropped {dropped_ore_host:,} alt-stone-host ore recipes (kept plain stone host)")
    if dropped_concrete:
        print(f"  dropped {dropped_concrete:,} concrete-block recipes (kept concrete dust material)")
    if dropped_ae2:
        print(f"  dropped {dropped_ae2:,} AE2 ME-part recipes (gtceu:me_* - unported until AE2 lands)")
    if dropped_stripped:
        print(f"  dropped {dropped_stripped:,} log-stripping recipes (stripped wood is a no-op in Terraria)")
    if dropped_recipe_type:
        print(f"  dropped {dropped_recipe_type:,} recipes of unported types ({', '.join(REMOVED_RECIPE_TYPES)})")
    if dropped_recipe_id:
        print(f"  dropped {dropped_recipe_id:,} recipes by exact id ({', '.join(sorted(REMOVED_RECIPE_IDS))})")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    print(f"Writing {len(recipes):,} recipes to {args.output.relative_to(REPO_ROOT)}...")
    with args.output.open("w", encoding="utf-8") as f:
        if args.pretty:
            json.dump(recipes, f, ensure_ascii=False, indent=2)
        else:
            json.dump(recipes, f, ensure_ascii=False, separators=(",", ":"))

    size_mb = args.output.stat().st_size / (1024 * 1024)
    print(f"  wrote {size_mb:.1f} MiB")

    if args.clean:
        removed = clean_legacy(args.output.parent)
        if removed > 0:
            print(f"Cleaned {removed} legacy per-station JSON files in {args.output.parent.relative_to(REPO_ROOT)}/")

    print("\nRecipes by type (top 20):")
    for recipe_type, count in type_counts.most_common(20):
        print(f"  {count:>6,}  {recipe_type}")
    if len(type_counts) > 20:
        print(f"  ... + {len(type_counts) - 20} more types")


if __name__ == "__main__":
    main()
