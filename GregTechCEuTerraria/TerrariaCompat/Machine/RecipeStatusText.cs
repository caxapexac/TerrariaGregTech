#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Machine.Trait;
using Terraria.Localization;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public static class RecipeStatusText
{
	private const string LocPrefix       = "Mods.GregTechCEuTerraria.RecipeStatus.";
	private const string ConditionPrefix = "gtceu.recipe.condition.";

	private static readonly string[] InternalPrefixes =
	{
		"gtceu.recipe_modifier.",
		"gtceu.recipe_logic.",
		"gtceu.recipe.",
	};

	public static string Resolve(string reasonKey)
	{
		if (reasonKey.StartsWith(ConditionPrefix, StringComparison.Ordinal))
		{
			int sep = reasonKey.IndexOf('|');
			if (sep >= 0) return reasonKey[(sep + 1)..];
			string locKeyG = LocPrefix + "condition";
			string textG = Language.GetTextValue(locKeyG);
			return textG == locKeyG ? reasonKey : textG;
		}

		string suffix = reasonKey;
		foreach (var pfx in InternalPrefixes)
		{
			if (reasonKey.StartsWith(pfx, StringComparison.Ordinal))
			{
				suffix = reasonKey[pfx.Length..];
				break;
			}
		}

		string locKey = LocPrefix + suffix;
		string text = Language.GetTextValue(locKey);
		return text == locKey ? reasonKey : text;
	}

	public static string StatusLine(RecipeLogic? rl, string workingVerb = "Running")
	{
		if (rl is null) return "Idle";
		if (rl.IsWorking())
			return $"{workingVerb} {FormatProgressSeconds(rl.GetProgress(), rl.GetMaxProgress())}";
		if (rl.IsWaiting() && rl.GetWaitingReason() is { } reason)
			return $"Waiting: {Resolve(reason)}";
		if (rl.IsSuspend())
			return "Suspended";
		return "Idle";
	}

	public static string FormatProgressSeconds(int progressTicks, int maxTicks)
	{
		double cur = progressTicks / (double)Api.TickScale.McTickRate;
		double max = maxTicks      / (double)Api.TickScale.McTickRate;
		return $"{cur:0.00} / {max:0.00}s";
	}

	public static string StatusLineForMulti(
		Multiblock.MultiblockControllerMachine controller,
		RecipeLogic? rl,
		string workingVerb = "Running")
	{
		if (!controller.IsFormed)
		{
			if (controller.GetUnformedErrorCell() is not null)
				return "[c/FFAA44:Structure not formed:][c/FF8888: see highlighted block]";
			var reason = controller.GetUnformedReason();
			if (!string.IsNullOrEmpty(reason))
				return $"[c/FFAA44:Structure not formed:][c/FF8888: {reason}]";
			return "[c/FFAA44:Structure not formed]";
		}
		return StatusLine(rl, workingVerb);
	}

	public static void AppendUnformedStatus(
		Multiblock.MultiblockControllerMachine controller,
		RecipeLogic? rl,
		List<string> lines)
	{
		lines.Add(StatusLineForMulti(controller, rl));
		if (Api.Machine.Multiblock.MultiblockDisplayText.UnformedHint is { } hint)
			lines.Add(hint);
	}

	public static void AppendFailureDetail(RecipeLogic? rl, List<string> lines)
	{
		if (rl is null || rl.GetStatus() != RecipeLogicStatus.IDLE) return;
		HashSet<string>? seen = null;
		foreach (var reason in rl.GetFailureReasons())
		{
			string text = Resolve(reason);
			seen ??= new HashSet<string>();
			if (seen.Add(text))
				lines.Add($"[c/FF8888:{text}]");
		}
	}
}
