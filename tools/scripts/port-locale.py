#!/usr/bin/env python3
"""Generates GregTechCEuTerraria/Localization/<culture>/*.hjson from the data
dumps + upstream's lang files.

Output is prefix-scoped (one file per section, folder = culture): a single flat
file hung tML's dev-time localization rewriter under non-English collation.

en-US is full; de-DE/pt-BR/ru-RU/zh-Hans are sparse - only keys upstream
translates, everything else falls back to en-US at runtime.
"""

import glob
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MAT_DIR = os.path.join(REPO, "GregTechCEuTerraria", "Data", "Materials")
REGISTRY_ITEMS = os.path.join(REPO, "GregTechCEuTerraria", "Data", "Registry", "items.json")
OUT = os.path.join(REPO, "GregTechCEuTerraria", "Localization", "en-US.hjson")
# Gitignored; produced by `./gradlew runData` in GregTech-Modern-1.20.1.
UPSTREAM_LANG = os.path.join(
    REPO, "GregTech-Modern-1.20.1", "src", "generated", "resources",
    "assets", "gtceu", "lang", "en_us.json")

UPSTREAM_LANG_DIR = os.path.join(
    REPO, "GregTech-Modern-1.20.1", "src", "main", "resources",
    "assets", "gtceu", "lang")
TRANSLATIONS = {
    "de-DE":   "de_de.json",
    "pt-BR":   "pt_br.json",
    "ru-RU":   "ru_ru.json",
    "zh-Hans": "zh_cn.json",
}
LOCALE_DIR = os.path.join(REPO, "GregTechCEuTerraria", "Localization")
MULTIBLOCK_LOCALE_CS = os.path.join(
    REPO, "GregTechCEuTerraria", "TerrariaCompat", "Machine", "Multiblock",
    "MultiblockLocale.cs")

TIERS = ["ULV", "LV", "MV", "HV", "EV", "IV", "LuV", "ZPM", "UV",
         "UHV", "UEV", "UIV", "UXV", "OpV", "MAX"]

def has_form(m, f):  return f in (m.get("forms") or [])

PREFIXES = [
    ("raw_ore",           "raw_{m}",                "Raw {0}"),
    ("ingot",             "{m}_ingot",              "{0} Ingot"),
    ("nugget",            "{m}_nugget",             "{0} Nugget"),
    ("gem",               "{m}_gem",                "{0}"),
    ("dust",              "{m}_dust",               "{0} Dust"),
    ("small_dust",        "small_{m}_dust",         "Small Pile of {0} Dust"),
    ("tiny_dust",         "tiny_{m}_dust",          "Tiny Pile of {0} Dust"),
    ("plate",             "{m}_plate",              "{0} Plate"),
    ("double_plate",      "double_{m}_plate",       "Double {0} Plate"),
    ("dense_plate",       "dense_{m}_plate",        "Dense {0} Plate"),
    ("foil",              "{m}_foil",               "{0} Foil"),
    ("rod",               "{m}_rod",                "{0} Rod"),
    ("long_rod",          "long_{m}_rod",           "Long {0} Rod"),
    ("bolt",              "{m}_bolt",               "{0} Bolt"),
    ("screw",             "{m}_screw",              "{0} Screw"),
    ("ring",              "{m}_ring",               "{0} Ring"),
    ("round",             "{m}_round",              "{0} Round"),
    ("gear",              "{m}_gear",               "{0} Gear"),
    ("small_gear",        "small_{m}_gear",         "Small {0} Gear"),
    ("spring",            "{m}_spring",             "{0} Spring"),
    ("small_spring",      "small_{m}_spring",       "Small {0} Spring"),
    ("rotor",             "{m}_rotor",              "{0} Rotor"),
    ("fine_wire",         "fine_{m}_wire",          "Fine {0} Wire"),
    ("crushed",           "crushed_{m}_ore",        "Crushed {0} Ore"),
    ("crushed_purified",  "purified_{m}_ore",       "Purified Crushed {0} Ore"),
    ("crushed_refined",   "refined_{m}_ore",        "Refined {0} Ore"),
    ("pure_dust",         "pure_{m}_dust",          "Purified Pile of {0} Dust"),
    ("impure_dust",       "impure_{m}_dust",        "Impure Pile of {0} Dust"),
    ("chipped_gem",       "chipped_{m}_gem",        "Chipped {0}"),
    ("flawed_gem",        "flawed_{m}_gem",         "Flawed {0}"),
    ("flawless_gem",      "flawless_{m}_gem",       "Flawless {0}"),
    ("exquisite_gem",     "exquisite_{m}_gem",      "Exquisite {0}"),
    ("hot_ingot",         "hot_{m}_ingot",          "Hot {0} Ingot"),
    ("double_ingot",      "double_{m}_ingot",       "Double {0} Ingot"),
    ("lens",              "{m}_lens",               "{0} Lens"),
    ("turbine_blade",     "{m}_turbine_blade",      "{0} Turbine Blade"),
    ("buzz_saw_blade",    "{m}_buzz_saw_blade",     "{0} Buzzsaw Blade"),
    ("chainsaw_head",     "{m}_chainsaw_head",      "{0} Chainsaw Head"),
    ("drill_head",        "{m}_drill_head",         "{0} Drill Head"),
    ("screwdriver_tip",   "{m}_screwdriver_tip",    "{0} Screwdriver Tip"),
    ("wire_cutter_head",  "{m}_wire_cutter_head",   "{0} Wire Cutter Head"),
    ("wrench_tip",        "{m}_wrench_tip",         "{0} Wrench Tip"),
    ("planks",            "{m}_planks",             "{0} Planks"),
    ("block",             "{m}_block",              "Block of {0}"),
    ("raw_ore_block",     "raw_{m}_block",          "Block of Raw {0}"),
    ("frame",             "{m}_frame",              "{0} Frame"),
]

WIRE_SIZES = [
    (1,  "single",    None),
    (2,  "double",    "Double"),
    (4,  "quadruple", "Quadruple"),
    (8,  "octal",     "Octal"),
    (16, "hex",       "Hex"),
]


# _TOOL_FORMATS = upstream item.gtceu.tool.<name> verbatim; _TOOL_ID mirrors
# GTToolType.idFormat (electric tools carry a tier prefix; electric wirecutter
# id base is wire_cutter, not wirecutter).
_TOOL_FORMATS = {
    "sword": "{0} Sword", "pickaxe": "{0} Pickaxe", "shovel": "{0} Shovel",
    "axe": "{0} Axe", "hoe": "{0} Hoe", "mining_hammer": "{0} Mining Hammer",
    "spade": "{0} Spade", "scythe": "{0} Scythe", "saw": "{0} Saw",
    "hammer": "{0} Hammer", "mallet": "{0} Soft Mallet", "wrench": "{0} Wrench",
    "file": "{0} File", "crowbar": "{0} Crowbar", "screwdriver": "{0} Screwdriver",
    "mortar": "{0} Mortar", "wire_cutter": "{0} Wire Cutter", "knife": "{0} Knife",
    "butchery_knife": "{0} Butchery Knife", "plunger": "{0} Plunger",
    "buzzsaw": "{0} Buzzsaw (LV)",
    "lv_drill": "{0} Drill (LV)", "mv_drill": "{0} Drill (MV)",
    "hv_drill": "{0} Drill (HV)", "ev_drill": "{0} Drill (EV)",
    "iv_drill": "{0} Drill (IV)",
    "lv_chainsaw": "{0} Chainsaw (LV)", "hv_chainsaw": "{0} Chainsaw (HV)",
    "iv_chainsaw": "{0} Chainsaw (IV)",
    "lv_wrench": "{0} Wrench (LV)", "hv_wrench": "{0} Wrench (HV)",
    "iv_wrench": "{0} Wrench (IV)",
    "lv_wirecutter": "{0} Wire Cutter (LV)", "hv_wirecutter": "{0} Wire Cutter (HV)",
    "iv_wirecutter": "{0} Wire Cutter (IV)",
    "lv_screwdriver": "{0} Screwdriver (LV)", "hv_screwdriver": "{0} Screwdriver (HV)",
    "iv_screwdriver": "{0} Screwdriver (IV)",
}
_TOOL_ID = {
    "lv_drill": "lv_{m}_drill", "mv_drill": "mv_{m}_drill", "hv_drill": "hv_{m}_drill",
    "ev_drill": "ev_{m}_drill", "iv_drill": "iv_{m}_drill",
    "lv_chainsaw": "lv_{m}_chainsaw", "hv_chainsaw": "hv_{m}_chainsaw",
    "iv_chainsaw": "iv_{m}_chainsaw",
    "lv_wrench": "lv_{m}_wrench", "hv_wrench": "hv_{m}_wrench", "iv_wrench": "iv_{m}_wrench",
    "lv_wirecutter": "lv_{m}_wire_cutter", "hv_wirecutter": "hv_{m}_wire_cutter",
    "iv_wirecutter": "iv_{m}_wire_cutter",
    "lv_screwdriver": "lv_{m}_screwdriver", "hv_screwdriver": "hv_{m}_screwdriver",
    "iv_screwdriver": "iv_{m}_screwdriver",
}


def humanize(snake: str) -> str:
    """`annealed_copper` -> `Annealed Copper` (mirror of MaterialItemRegistry.Humanize)."""
    out, cap = [], True
    for c in snake:
        if c == "_":
            out.append(" "); cap = True
        else:
            out.append(c.upper() if cap else c); cap = False
    return "".join(out)


def _camel_to_snake(s: str) -> str:
    """items.json `prefix` (camelCase) -> upstream `tagprefix.<name>` lang key
    (snake_case). Idempotent on already-snake input."""
    return re.sub(r"(?<!^)(?=[A-Z])", "_", s).lower()


def _template_to_braces(s: str) -> str:
    """Upstream `%s` template (may carry MC color codes) -> our `{0}` string."""
    return (_mc_to_terraria(s)
            .replace("%1$s", "{0}")
            .replace("%s", "{0}"))


def _fill(template: str, value: str) -> str:
    """{0} substitution WITHOUT str.format - translated strings carry stray
    braces in color tags that str.format would choke on."""
    return template.replace("{0}", value)


class Tr:
    """A loaded upstream translation. Accessors return None when the key is
    absent -> caller emits nothing -> tML falls back to en-US.hjson at runtime.
    Translated files are therefore sparse."""

    def __init__(self, culture: str, lang: dict):
        self.culture = culture
        self.lang = lang

    def material(self, mid: str):
        return self.lang.get(f"material.gtceu.{mid}")

    def tagprefix(self, camel_prefix: str):
        v = self.lang.get(f"tagprefix.{_camel_to_snake(camel_prefix)}")
        return _template_to_braces(v) if v is not None else None

    def tool(self, tn: str):
        v = self.lang.get(f"item.gtceu.tool.{tn}")
        return _template_to_braces(v) if v is not None else None


def load_materials() -> dict:
    materials = {}
    for fname in sorted(os.listdir(MAT_DIR)):
        if not fname.endswith(".json"): continue
        with open(os.path.join(MAT_DIR, fname), encoding="utf-8") as f:
            data = json.load(f)
        if isinstance(data, list):
            for m in data:
                materials[m["id"]] = m
    return materials


def emit_materials(materials, lines, indent, tr=None):
    lines.append(f"{indent}Materials: {{")
    for mid in sorted(materials):
        if tr is None:
            lines.append(f"{indent}\t{mid}: {humanize(mid)}")
        else:
            name = tr.material(mid)
            if name is not None:
                # Quote: translated prose can carry commas / leading digits /
                # color-tag brackets that hjson mis-parses as a JSON literal.
                lines.append(f"{indent}\t{mid}: {json.dumps(name, ensure_ascii=False)}")
    lines.append(f"{indent}}}")


# Mirror of MaterialItemRegistry.PrefixNameOverrides (C#) - the prefixes whose
# camelCase<->snake_case mapping isn't mechanical.
_PREFIX_NAME_OVERRIDES = {
    "raw_ore":          "raw",
    "crushed":          "crushedOre",
    "crushed_purified": "purifiedOre",
    "crushed_refined":  "refinedOre",
}


def _snake_to_camel(s):
    p = s.split("_")
    return p[0] + "".join(w[:1].upper() + w[1:] for w in p[1:])


def _prefix_templates():
    """camelCase prefix (items.json) -> display template ({0} = material)."""
    t = {}
    for prefix_id, _id_pattern, template in PREFIXES:
        t[_PREFIX_NAME_OVERRIDES.get(prefix_id, _snake_to_camel(prefix_id))] = template
    wire_prefix  = {1: "wireGtSingle", 2: "wireGtDouble", 4: "wireGtQuadruple",
                    8: "wireGtOctal", 16: "wireGtHex"}
    cable_prefix = {1: "cableGtSingle", 2: "cableGtDouble", 4: "cableGtQuadruple",
                    8: "cableGtOctal", 16: "cableGtHex"}
    for size, _word, label in WIRE_SIZES:
        t[wire_prefix[size]]  = "{0} Wire"  if label is None else f"{label} {{0}} Wire"
        t[cable_prefix[size]] = "{0} Cable" if label is None else f"{label} {{0}} Cable"
    t.update(_pipe_en_templates())
    return t


_PIPE_TEMPLATES_CACHE = None


def _pipe_en_templates():
    """Pipe templates from upstream en_us tagprefix.pipe_*, keyed by items.json's
    camelCase prefix. Always English; translations come via Tr.tagprefix."""
    global _PIPE_TEMPLATES_CACHE
    if _PIPE_TEMPLATES_CACHE is None:
        out = {}
        for k, v in _load_lang(UPSTREAM_LANG).items():
            if k.startswith("tagprefix.pipe_"):
                out[_snake_to_camel(k[len("tagprefix."):])] = _template_to_braces(v)
        _PIPE_TEMPLATES_CACHE = out
    return _PIPE_TEMPLATES_CACHE


def emit_items(materials, lines, indent, tr=None, lang=None):
    """DisplayNames for every dump item. Material/prefix items, wires and tools
    are composed from templates + the material name (as upstream composes them at
    runtime); everything else (inert components, casings, machines, armour, ...)
    is emitted straight from `item.gtceu.<id>` / `block.gtceu.<id>`.
    tr set -> sparse translated values."""
    lines.append(f"{indent}Items: {{")
    i2 = indent + "\t"
    emitted = set()

    def write_entry(key, display, quote=None):
        emitted.add(key)
        # Quote anything that may carry commas / leading digits / color-tag
        # brackets (translated + upstream-sourced values); plain en template
        # output stays unquoted (byte-identical to pre-i18n output).
        q = (tr is not None) if quote is None else quote
        val = json.dumps(display, ensure_ascii=False) if q else display
        lines.append(f"{i2}{key}: {{")
        lines.append(f"{i2}\tDisplayName: {val}")
        lines.append(f'{i2}\tTooltip: ""')
        lines.append(f"{i2}}}")

    dump = _load_dump()
    templates = _prefix_templates()

    # Material-composed items: TagPrefixItem, MaterialPipeBlockItem (wireGt*/
    # cableGt*), MaterialBlockItem (block/frame/rawOreBlock). Prefixes absent
    # from `templates` (ore-host stone, etc.) fall through unmatched - same as
    # the C# registries.
    for e in sorted(dump, key=lambda x: x.get("id", "")):
        eid = e.get("id", "")
        if not eid.startswith("gtceu:"):
            continue
        cls = e.get("class", "")
        is_block = cls.endswith("MaterialBlockItem") and e.get("prefix") in ("block", "frame", "rawOreBlock")
        if not (cls.endswith("TagPrefixItem") or cls.endswith("MaterialPipeBlockItem") or is_block):
            continue
        prefix, mat = e.get("prefix"), e.get("material")
        if prefix is None or mat is None or mat not in materials:
            continue
        en_template = templates.get(prefix)
        if en_template is None:
            continue
        is_wire = prefix.startswith("wireGt") or prefix.startswith("cableGt")

        if tr is None:
            display = en_template.format(humanize(mat))
            # DEVIATION: wires/cables get a voltage-tier prefix ("MV Copper
            # Wire"); tier = material.cableTier (ULV if absent).
            if is_wire:
                display = f"{materials[mat].get('cableTier') or 'ULV'} {display}"
            write_entry(eid[len("gtceu:"):], display)
            continue

        # Translated: localize material (+ prefix template for non-wires); emit
        # only when a piece is translated, else fall back to en-US.
        mat_tr = tr.material(mat)
        mat_name = mat_tr if mat_tr is not None else humanize(mat)
        if is_wire:
            # Localize material only; keep en template + tier prefix.
            if mat_tr is None:
                continue
            display = en_template.format(mat_name)
            display = f"{materials[mat].get('cableTier') or 'ULV'} {display}"
        else:
            tmpl_tr = tr.tagprefix(prefix)
            if mat_tr is None and tmpl_tr is None:
                continue
            display = _fill(tmpl_tr if tmpl_tr is not None else en_template, mat_name)
        write_entry(eid[len("gtceu:"):], display)

    # Tools - one per (material x tool.types), enumerated like ToolItemLoader.
    for mid in sorted(materials):
        tool = materials[mid].get("tool")
        if not tool:
            continue
        for tn in sorted(tool.get("types") or []):
            fmt = _TOOL_FORMATS.get(tn)
            if fmt is None:
                continue
            tool_id = _TOOL_ID.get(tn, "{m}_" + tn).replace("{m}", mid)
            if tr is None:
                write_entry(tool_id, fmt.format(humanize(mid)))
                continue
            tmpl_tr, mat_tr = tr.tool(tn), tr.material(mid)
            if tmpl_tr is None and mat_tr is None:
                continue
            mat_name = mat_tr if mat_tr is not None else humanize(mid)
            write_entry(tool_id, _fill(tmpl_tr if tmpl_tr is not None else fmt, mat_name))

    # Every remaining dump item with its own upstream key (inert components,
    # casings, machines - which adopt upstream's tier-coloured "Advanced Bender"
    # naming - armour, dye, ...). Material-composed items already deduped via
    # `emitted`. en-US `lang` = upstream en_us.json; translations = that
    # culture's (sparse).
    src = lang or {}
    for e in sorted(dump, key=lambda x: x.get("id", "")):
        eid = e.get("id", "")
        if not eid.startswith("gtceu:"):
            continue
        iid = eid[len("gtceu:"):]
        if iid in emitted:
            continue
        cls = e.get("class", "").split(".")[-1]
        is_block = cls.endswith("BlockItem")
        val = src.get(("block.gtceu." if is_block else "item.gtceu.") + iid)
        if val is None:                       # try the other namespace
            val = src.get(("item.gtceu." if is_block else "block.gtceu.") + iid)
        if val is None:
            continue                          # no upstream name -> stays GetOrRegister
        write_entry(iid, _mc_to_terraria(val), quote=True)

    # Fluid buckets - upstream `item.gtceu.bucket` template + the fluid name.
    # When the fluid id == a material id the name IS the material name; the ~10
    # plasma/cryo variants have none here and fall back to en-US.
    bucket_raw = src.get("item.gtceu.bucket")
    bucket_tmpl = _to_braces_positional(bucket_raw) if bucket_raw else "{0} Bucket"
    for e in sorted(dump, key=lambda x: x.get("id", "")):
        eid = e.get("id", "")
        if not (eid.startswith("gtceu:") and eid.endswith("_bucket")):
            continue
        iid = eid[len("gtceu:"):]
        if iid in emitted:
            continue
        stem = iid[:-len("_bucket")]
        if stem not in materials:
            continue                          # plasma/cryo variant -> en-US fallback
        if tr is None:
            write_entry(iid, _fill(bucket_tmpl, humanize(stem)), quote=True)
        else:
            mat_tr = tr.material(stem)
            if mat_tr is None:
                continue
            write_entry(iid, _fill(bucket_tmpl, mat_tr), quote=True)

    lines.append(f"{indent}}}")


# Keyed by the suffix RecipeStatusText.Resolve looks up (gtceu.recipe.* ids from
# ActionResult.Fail). Granular per-capability, unlike upstream's lumped reasons.
_RECIPE_STATUS = {
    "no_input":          "Missing input items",
    "no_fluid":          "Missing input fluid",
    "no_capabilities_item_in":   "No item input bus",
    "no_capabilities_item_out":  "No item output bus",
    "no_capabilities_fluid_in":  "No fluid input hatch",
    "no_capabilities_fluid_out": "No fluid output hatch",
    "no_capabilities_eu_in":     "No energy input hatch",
    "no_capabilities_eu_out":    "No energy output hatch",
    "no_capabilities_any_in":    "No input handler",
    "no_capabilities_any_out":   "No output handler",
    "output_full":       "Output slots full",
    "fluid_output_full": "Output tank full",
    "eu_too_high":       "Recipe voltage too high for this machine",
    "insufficient_eu":   "Not enough power",
    "eu_buffer_full":    "Energy buffer full",
    "circuit_mismatch":  "Circuit setting mismatch",
    "condition":         "Conditions not met",
    # Generator-multi modifier rejections (via ModifierFunction.Cancel).
    "no_rotor":                  "Install a rotor in the rotor holder",
    "turbine_voltage_too_low":   "Rotor too weak for this recipe's voltage",
    "no_lubricant":              "Out of lubricant",
    # Fusion rejections - split from upstream's single insufficient_eu_to_start_fusion.
    "fusion_tier_too_low":       "Reactor tier too low for this recipe",
    "fusion_capacity_too_small": "Capacitor too small - install more energy hatches",
    "fusion_capacitor_charging": "Capacitor charging - waiting for startup energy",
    "insufficient_eu_to_start_fusion": "Recipe missing eu_to_start data",
}


def emit_recipe_status(lines, indent, lang, include_curated=True):
    """_RECIPE_STATUS (curated, en only) plus every gtceu.recipe_logic.* /
    gtceu.recipe_modifier.* suffix from the lang file. Translated files pass
    include_curated=False (curated suffixes are en-only -> runtime fallback)."""
    lines.append(f"{indent}RecipeStatus: {{")
    out = dict(_RECIPE_STATUS) if include_curated else {}
    for k, v in (lang or {}).items():
        for pfx in ("gtceu.recipe_logic.", "gtceu.recipe_modifier."):
            if k.startswith(pfx):
                suffix = k[len(pfx):]
                if "." in suffix:
                    continue                # nested keys (category.*) - skip
                out.setdefault(suffix, _mc_to_terraria(v))
    for key in sorted(out):
        lines.append(f"{indent}\t{key}: {json.dumps(out[key], ensure_ascii=False)}")
    print(f"  recipe statuses: {len(out)}")
    lines.append(f"{indent}}}")


# our id <- upstream machine id, for the handful whose ids differ.
_MACHINE_TOOLTIP_ALIASES = {
    "combustion_generator":  "combustion",
}


# Formatting codes (l/o/n/m/k) and reset (r) close any open color tag and drop.
_MC_COLORS = {
    "0": "000000", "1": "0000AA", "2": "00AA00", "3": "00AAAA",
    "4": "AA0000", "5": "AA00AA", "6": "FFAA00", "7": "AAAAAA",
    "8": "555555", "9": "5555FF", "a": "55FF55", "b": "55FFFF",
    "c": "FF5555", "d": "FF55FF", "e": "FFFF55", "f": "FFFFFF",
}


def _mc_to_terraria(s):
    """Convert §X color codes to Terraria [c/RRGGBB:...] tags."""
    out, i, open_tag = [], 0, False
    def close():                       # Terraria dislikes trailing space in a tag
        while out and out[-1] == " ":
            out.pop()
        out.append("]")
    while i < len(s):
        if s[i] == "§" and i + 1 < len(s):
            code = s[i + 1].lower()
            if open_tag:
                close(); open_tag = False
            if code in _MC_COLORS:
                out.append(f"[c/{_MC_COLORS[code]}:"); open_tag = True
            i += 2
        else:
            out.append(s[i]); i += 1
    if open_tag:
        close()
    # Drop empty colored segments ("[c/XXXXXX:]") left when two §X codes touch.
    text = "".join(out)
    while True:
        cleaned = re.sub(r"\[c/[0-9A-Fa-f]{6}:\]", "", text)
        if cleaned == text:
            break
        text = cleaned
    return text.strip()


def emit_machine_tooltips(lines, indent, lang):
    lines.append(f"{indent}MachineTooltip: {{")
    if not lang:
        print(f"  WARN: lang missing - MachineTooltip section empty "
              f"(run ./gradlew runData in GregTech-Modern-1.20.1)")
        lines.append(f"{indent}}}")
        return
    # "gtceu.machine.<id>.tooltip" -> "<id>"; numbered ".tooltip.<N>" -> "<id>_<N>"
    # (hjson nests on '.', so flatten with '_'). %s/%d kept raw -
    # MachineTooltipLookup substitutes at runtime.
    prefix, suffix = "gtceu.machine.", ".tooltip"
    out = {}
    n_numbered = 0
    for k, v in lang.items():
        if not k.startswith(prefix):
            continue
        rest = k[len(prefix):]
        if rest.endswith(suffix):
            mid = rest[:-len(suffix)]
            if not mid or "." in mid:
                continue
            # includes available_recipe_map_N templates (appended by
            # MachineTooltipLookup to multi-type defs).
            out[_MACHINE_TOOLTIP_ALIASES.get(mid, mid)] = _mc_to_terraria(v)
            continue
        m = re.fullmatch(r"([a-z0-9_]+)\.tooltip\.(\d+)", rest)
        if not m:
            continue
        mid, n = m.group(1), int(m.group(2))
        mid = _MACHINE_TOOLTIP_ALIASES.get(mid, mid)
        out[f"{mid}_{n}"] = _mc_to_terraria(v)
        n_numbered += 1
    for mid in sorted(out):
        lines.append(f"{indent}\t{mid}: {json.dumps(out[mid], ensure_ascii=False)}")
    print(f"  machine tooltips: {len(out) - n_numbered} single + {n_numbered} numbered")
    lines.append(f"{indent}}}")


# Single-segment `gtceu.<id>` keys (gtceu.centrifuge = "Centrifuge"); fills the
# available_recipe_map_N template.
def emit_recipe_type_names(lines, indent, lang):
    lines.append(f"{indent}RecipeTypeName: {{")
    if not lang:
        lines.append(f"{indent}}}"); return
    pat = re.compile(r"^gtceu\.[a-z][a-z0-9_]*$")
    out = {}
    for k, v in lang.items():
        if pat.match(k):
            out[k[len("gtceu."):]] = _mc_to_terraria(v)
    for rt in sorted(out):
        lines.append(f"{indent}\t{rt}: {json.dumps(out[rt], ensure_ascii=False)}")
    print(f"  recipe type names: {len(out)}")
    lines.append(f"{indent}}}")


# GTConfig.cs has no [Label]/[Tooltip] attributes, so without these the config
# menu shows raw field names.
_CONFIGS = {
    "GTConfig": {
        "DisplayName": "GregTech",
        "EnableBossDrops": {
            "Label":   "Enable GregTech boss drops",
            "Tooltip": "If enabled, vanilla bosses drop tier-appropriate "
                       "GregTech raw ores, dusts, and circuit components.",
        },
        "SimulationSpeed": {
            "Label":   "Simulation Speed",
            "Tooltip": "",
        },
        "NetworkSyncPeriod": {
            "Label":   "Network Sync Period",
            "Tooltip": "",
        },
    },
}

_BOSSDROPS_CONDITION_DESCRIPTION = (
    "Requires boss drops enabled in config."
)


def emit_configs(lines, indent):
    lines.append(f"{indent}Configs: {{")
    i2 = indent + "\t"
    for cfg_name in sorted(_CONFIGS):
        cfg = _CONFIGS[cfg_name]
        lines.append(f"{i2}{cfg_name}: {{")
        i3 = i2 + "\t"
        if "DisplayName" in cfg:
            lines.append(f"{i3}DisplayName: {cfg['DisplayName']}")
        for field in sorted(k for k in cfg if k != "DisplayName"):
            entry = cfg[field]
            lines.append(f"{i3}{field}: {{")
            lines.append(f"{i3}\tLabel: {json.dumps(entry['Label'], ensure_ascii=False)}")
            lines.append(f"{i3}\tTooltip: {json.dumps(entry['Tooltip'], ensure_ascii=False)}")
            lines.append(f"{i3}}}")
        lines.append(f"{i2}}}")
    lines.append(f"{indent}}}")


def emit_bossdrops(lines, indent):
    desc = json.dumps(_BOSSDROPS_CONDITION_DESCRIPTION, ensure_ascii=False)
    lines.append(f"{indent}BossDrops.ConditionDescription: {desc}")


def emit_tiles(materials, lines, indent, tr=None, lang=None):
    lines.append(f"{indent}Tiles: {{")
    i2 = indent + "\t"
    emitted = set()

    # Storage blocks - materials with INGOT or GEM form (the `block` prefix gate).
    for mid in sorted(materials):
        m = materials[mid]
        if not (has_form(m, "INGOT") or has_form(m, "GEM")): continue
        emitted.add(f"{mid}_block")
        if tr is None:
            lines.append(f"{i2}{mid}_block.MapEntry: Block of {humanize(mid)}")
        else:
            tmpl, mat_tr = tr.tagprefix("block"), tr.material(mid)
            if tmpl is None and mat_tr is None:
                continue
            mat_name = mat_tr if mat_tr is not None else humanize(mid)
            tmpl = tmpl if tmpl is not None else "Block of {0}"
            lines.append(f"{i2}{mid}_block.MapEntry: {json.dumps(_fill(tmpl, mat_name), ensure_ascii=False)}")

    # Ore tiles - en only: upstream has no "%s Ore" template, so translations
    # fall back to en-US ("<Material> Ore").
    if tr is None:
        for mid in sorted(materials):
            if not has_form(materials[mid], "ORE"): continue
            emitted.add(f"{mid}_ore")
            lines.append(f"{i2}{mid}_ore.MapEntry: {humanize(mid)} Ore")

    # Machine + casing MapEntries (placed-block hover name) from block.gtceu.<id>;
    # material blocks/ores above are deduped via `emitted`. No block.gtceu key
    # -> not a tile here.
    src = lang or {}
    for e in sorted(_load_dump(), key=lambda x: x.get("id", "")):
        eid = e.get("id", "")
        if not eid.startswith("gtceu:"):
            continue
        iid = eid[len("gtceu:"):]
        if iid in emitted:
            continue
        val = src.get("block.gtceu." + iid)
        if val is None:
            continue
        emitted.add(iid)
        lines.append(f"{i2}{iid}.MapEntry: {json.dumps(_mc_to_terraria(val), ensure_ascii=False)}")

    lines.append(f"{indent}}}")


def _load_lang(path):
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as f:
        return json.load(f)


_DUMP_CACHE = None


def _load_dump():
    global _DUMP_CACHE
    if _DUMP_CACHE is None:
        with open(REGISTRY_ITEMS, encoding="utf-8") as f:
            _DUMP_CACHE = json.load(f)
    return _DUMP_CACHE


def _write_section(culture_dir, section, frag):
    """Strip the `["\t\tName: {", "\t\t\t...", "\t\t}"]` wrapper + dedent 3 tabs so
    entries sit at column 0, then write Mods.GregTechCEuTerraria.<section>.hjson.
    tML derives the key prefix from folder (=culture) + filename. Skips empty."""
    body = [L.removeprefix("\t\t\t") for L in frag[1:-1]]
    if not body:
        return 0
    with open(os.path.join(culture_dir, f"Mods.GregTechCEuTerraria.{section}.hjson"),
              "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(body) + "\n")
    return len(body)


_MB_PAIRS_CACHE = None


def _multiblock_pairs():
    """Parse MultiblockLocale.cs `Register("gtceu.X", "english")` -> (raw_key,
    english). These English strings are the source of truth; the C# GetOrRegister
    calls remain only as a default-culture fallback."""
    global _MB_PAIRS_CACHE
    if _MB_PAIRS_CACHE is not None:
        return _MB_PAIRS_CACHE
    pairs = []
    if os.path.exists(MULTIBLOCK_LOCALE_CS):
        pat = re.compile(r'Register\("(gtceu\.[^"]+)",\s*"((?:[^"\\]|\\.)*)"\)')
        with open(MULTIBLOCK_LOCALE_CS, encoding="utf-8") as f:
            for line in f:
                m = pat.search(line)
                if m:
                    pairs.append((m.group(1), m.group(2).replace('\\"', '"')))
    _MB_PAIRS_CACHE = pairs
    return pairs


def _to_braces_positional(s):
    """Like _template_to_braces but multi-arg: %s / %d (and %N$s / %N$d) ->
    positional {0}/{1}/... matching the hand-converted English arg order.
    %d is upstream's integer arg (throttle / temperature / heat-time); without
    handling it the translated line renders a literal "%d"."""
    s = _mc_to_terraria(s)
    s = re.sub(r"%(\d+)\$[sd]", lambda m: "{" + str(int(m.group(1)) - 1) + "}", s)
    counter = [0]
    def _seq(_):
        i = counter[0]; counter[0] += 1
        return "{" + str(i) + "}"
    return re.sub(r"%[sd]", _seq, s)


def emit_multiblock_ui(culture_dir, tr, lang):
    """Multiblock display-text keys, emitted as namespaced FILE keys - they
    survive a language switch (raw GetOrRegister'd keys don't) and are
    translatable. en-US = MultiblockLocale English verbatim; translations =
    upstream gtceu.* (sparse). One file per section, matching the runtime
    KeyResolver mapping (strip `gtceu.`)."""
    sections = {}
    for raw, english in _multiblock_pairs():
        parts = raw.split(".")              # gtceu . <section> . <subkey...>
        if len(parts) < 3:
            continue                        # single-segment -> RecipeTypeName
        section, subkey = parts[1], ".".join(parts[2:])
        if tr is None:
            value = english
        else:
            up = lang.get(raw)
            if up is None:
                continue                    # sparse - en-US fallback at runtime
            value = _to_braces_positional(up)
        sections.setdefault(section, []).append((subkey, value))

    total = 0
    for section in sorted(sections):
        frag = [f"\t\t{section}: {{"]
        for subkey, value in sections[section]:
            frag.append(f"\t\t\t{subkey}: {json.dumps(value, ensure_ascii=False)}")
        frag.append("\t\t}")
        total += _write_section(culture_dir, section, frag)
    return total


def build_locale(materials, lang, tr, culture):
    """Write one culture as one file per section under Localization/<culture>/.
    Splitting keeps each file small: tML's dev-time rewriter
    (UpdateLocalizationFiles -> AddEntryToHJSON) is O(n^2) per file with
    culture-sensitive StartsWith, so a single flat ~20k-entry file hung under
    non-English collation. tr=None -> en-US (full, incl. Configs/BossDrops)."""
    culture_dir = os.path.join(LOCALE_DIR, culture)
    os.makedirs(culture_dir, exist_ok=True)
    for old in glob.glob(os.path.join(culture_dir, "Mods.GregTechCEuTerraria.*.hjson")):
        os.remove(old)
    is_en = tr is None
    I = "\t\t"

    def section(name, emit):
        frag = []
        emit(frag)
        return _write_section(culture_dir, name, frag)

    total = 0
    total += section("Materials",      lambda l: emit_materials(materials, l, I, tr))
    total += section("Items",          lambda l: emit_items(materials, l, I, tr, lang))
    total += section("Tiles",          lambda l: emit_tiles(materials, l, I, tr, lang))
    total += section("RecipeStatus",   lambda l: emit_recipe_status(l, I, lang, include_curated=is_en))
    total += section("MachineTooltip", lambda l: emit_machine_tooltips(l, I, lang))
    total += section("RecipeTypeName", lambda l: emit_recipe_type_names(l, I, lang))
    total += emit_multiblock_ui(culture_dir, tr, lang)
    if is_en:
        total += section("Configs", lambda l: emit_configs(l, I))
        # BossDrops is a single bare "BossDrops.<key>: val" line, not a section.
        bl = []
        emit_bossdrops(bl, I)
        leaf = [L.removeprefix(I).removeprefix("BossDrops.") for L in bl]
        with open(os.path.join(culture_dir, "Mods.GregTechCEuTerraria.BossDrops.hjson"),
                  "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(leaf) + "\n")
        total += len(leaf)
    print(f"  wrote {culture_dir}  ({total} content lines)")


def main():
    materials = load_materials()
    print(f"loaded {len(materials)} materials")

    # Remove legacy flat files (replaced by per-culture folders).
    for old in glob.glob(os.path.join(LOCALE_DIR, "*.hjson")):
        os.remove(old)

    print("== en-US ==")
    build_locale(materials, _load_lang(UPSTREAM_LANG), None, "en-US")

    for culture, fname in TRANSLATIONS.items():
        lang_path = os.path.join(UPSTREAM_LANG_DIR, fname)
        if not os.path.exists(lang_path):
            print(f"== {culture} ==  SKIP (missing {lang_path})")
            continue
        print(f"== {culture} ==")
        lang = _load_lang(lang_path)
        build_locale(materials, lang, Tr(culture, lang), culture)


if __name__ == "__main__":
    sys.exit(main() or 0)
