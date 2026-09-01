#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Terraria.Localization;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public static class MachineTooltipLookup
{
	public static void AppendDescriptionAndBuilder(List<string> lines, string? machineKey, string? machineId)
	{
		AppendDescriptionAndBuilder(lines, machineKey, machineId, definition: null);
	}

	public static void AppendDescriptionAndBuilder(List<string> lines, string? machineKey, string? machineId, MachineDefinition? definition)
	{
		AppendBuilder(lines, machineKey, machineId, definition);
		string? text = Lookup(machineKey) ?? (machineId != null ? Lookup(machineId) : null);
		if (text != null && AcceptOrSubstitute(ref text, definition))
			AppendWrapped(lines, CollapsePercentEscape(text));
		AppendAvailableRecipeMaps(lines, definition);
	}

	private const int WrapWidth = 110;

	private static void AppendWrapped(List<string> lines, string text)
	{
		if (text.Length <= WrapWidth || text.Contains("[c/"))
		{
			lines.Add(text);
			return;
		}
		int start = 0;
		while (start < text.Length)
		{
			if (text.Length - start <= WrapWidth)
			{
				lines.Add(text.Substring(start));
				return;
			}
			int split = text.LastIndexOf(' ', start + WrapWidth, WrapWidth);
			if (split <= start) split = start + WrapWidth;
			lines.Add(text.Substring(start, split - start));
			start = split;
			while (start < text.Length && text[start] == ' ') start++;
		}
	}

	private static void AppendAvailableRecipeMaps(List<string> lines, MachineDefinition? def)
	{
		var rts = def?.RecipeTypes;
		if (rts == null || rts.Length < 2) return;
		string? template = Lookup($"available_recipe_map_{rts.Length}");
		if (template == null) return;
		template = NormalizePlaceholders(template);
		var names = new object[rts.Length];
		for (int i = 0; i < rts.Length; i++) names[i] = RecipeTypeDisplayName(rts[i]);
		string filled;
		try { filled = string.Format(template, names); }
		catch { return; }
		AppendWrapped(lines, CollapsePercentEscape(filled));
	}

	public static string RecipeTypeDisplayName(Api.Recipe.GTRecipeType recipeType)
	{
		string key = $"Mods.GregTechCEuTerraria.RecipeTypeName.{recipeType.RegistryName}";
		string text = Language.GetTextValue(key);
		if (text != key) return text;
		return Humanize(recipeType.RegistryName);
	}

	private static string Humanize(string snake)
	{
		var parts = snake.Split('_');
		for (int i = 0; i < parts.Length; i++)
			if (parts[i].Length > 0)
				parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
		return string.Join(" ", parts);
	}

	private static void AppendBuilder(List<string> lines, string? machineKey, string? machineId, MachineDefinition? def)
	{
		string? id = HasBuilder(machineKey) ? machineKey
		           : HasBuilder(machineId)  ? machineId
		           : null;
		if (id == null) return;

		var collected = new List<string>();
		for (int n = 0; ; n++)
		{
			string? line = Lookup($"{id}_{n}");
			if (line == null) break;
			collected.Add(NormalizePlaceholders(line));
		}
		RunBuilder(collected, def, machineId);
		foreach (var line in collected)
			if (!_unfilledFormat.IsMatch(line))
				AppendWrapped(lines, CollapsePercentEscape(line));
	}

	private static string CollapsePercentEscape(string text) => text.Replace("%%", "%");

	public static bool HasBuilder(string? id) => id != null && Lookup($"{id}_0") != null;

	private static readonly Regex _unfilledFormat = new(@"%(?:\d+\$)?[sdf]", RegexOptions.Compiled);

	private static string NormalizePlaceholders(string text)
	{
		int auto = 0;
		return Regex.Replace(text, @"%(?:(\d+)\$)?([sdf])", m =>
		{
			if (m.Groups[1].Success)
				return "{" + (int.Parse(m.Groups[1].Value) - 1) + "}";
			return "{" + (auto++) + "}";
		});
	}

	private static void RunBuilder(List<string> lines, MachineDefinition? def, string? machineId)
	{
		var builder = def?.TooltipBuilder
		           ?? MachineTooltipBuilders.Get(def?.Id ?? machineId);
		if (builder == null || def == null) return;
		builder(lines, def);
	}

	private static bool AcceptOrSubstitute(ref string line, MachineDefinition? def)
	{
		if (!_unfilledFormat.IsMatch(line)) return true;
		line = NormalizePlaceholders(line);
		var single = new List<string> { line };
		RunBuilder(single, def, def?.Id);
		line = single.Count > 0 ? single[0] : line;
		return !_unfilledFormat.IsMatch(line);
	}

	public static string? Lookup(string? id)
	{
		if (id == null) return null;
		string key  = $"Mods.GregTechCEuTerraria.MachineTooltip.{id}";
		string text = Language.GetTextValue(key);
		return text == key ? null : text;
	}
}
