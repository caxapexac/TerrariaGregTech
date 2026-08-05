#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Recipe.Chance.Boost;
using GregTechCEuTerraria.Api.Recipe.Content;

namespace GregTechCEuTerraria.Api.Recipe.Chance.Logic;

public abstract class ChanceLogic
{
	public string Name { get; }
	protected ChanceLogic(string name) { Name = name; Register(this); }

	public abstract IReadOnlyList<Recipe.Content.Content> Roll(
		object cap,
		IReadOnlyList<Recipe.Content.Content> chancedEntries,
		ChanceBoostFunction function,
		int recipeTier,
		int chanceTier,
		IDictionary<object, int>? cache,
		int times);

	private static readonly List<ChanceLogic> _registry = new();
	private static void Register(ChanceLogic c) { lock (_registry) _registry.Add(c); }
	public static IReadOnlyList<ChanceLogic> All { get { lock (_registry) return _registry.ToArray(); } }

	public static readonly ChanceLogic OR    = new OrLogic();
	public static readonly ChanceLogic AND   = new AndLogic();
	public static readonly ChanceLogic XOR   = new XorLogic();
	public static readonly ChanceLogic NONE  = new NoneLogic();

	public static int GetMaxChancedValue() => 10_000;

	public static int GetChance(Recipe.Content.Content entry, ChanceBoostFunction function, int recipeTier, int chanceTier)
	{
		return function.GetBoostedChance(entry, recipeTier, chanceTier);
	}

	private static readonly Random _rng = new();

	public static int GetCachedChance(Recipe.Content.Content entry, IDictionary<object, int>? cache)
	{
		if (cache is null || !cache.TryGetValue(entry.Payload, out var v))
			return _rng.Next(entry.MaxChance);
		return v;
	}

	public static void UpdateCachedChance(object payload, IDictionary<object, int>? cache, int value)
	{
		if (cache is null) return;
		cache[payload] = value;
	}

	public static bool PassesChance(int chance, int maxChance) => chance >= maxChance;

	private sealed class OrLogic : ChanceLogic
	{
		public OrLogic() : base("or") { }
		public override IReadOnlyList<Recipe.Content.Content> Roll(
			object cap, IReadOnlyList<Recipe.Content.Content> entries,
			ChanceBoostFunction function, int recipeTier, int chanceTier,
			IDictionary<object, int>? cache, int times)
		{
			var result = new List<Recipe.Content.Content>();
			foreach (var entry in entries)
			{
				int maxChance = entry.MaxChance;
				int newChance = GetChance(entry, function, recipeTier, chanceTier);
				int totalChance = times * newChance;
				int guaranteed = totalChance / maxChance;
				if (guaranteed > 0)
					result.Add(entry.CopyChanced(cap, ContentModifier.Multiplier_(guaranteed)));
				newChance = totalChance % maxChance;

				int cached = GetCachedChance(entry, cache);
				int chance = newChance + cached;
				while (PassesChance(chance, maxChance))
				{
					result.Add(entry);
					chance -= maxChance;
					newChance -= maxChance;
				}
				UpdateCachedChance(entry.Payload, cache, newChance / 2 + cached);
			}
			return result;
		}
	}

	private sealed class AndLogic : ChanceLogic
	{
		public AndLogic() : base("and") { }
		public override IReadOnlyList<Recipe.Content.Content> Roll(
			object cap, IReadOnlyList<Recipe.Content.Content> entries,
			ChanceBoostFunction function, int recipeTier, int chanceTier,
			IDictionary<object, int>? cache, int times)
		{
			var result = new List<Recipe.Content.Content>();
			for (int i = 0; i < times; ++i)
			{
				bool failed = false;
				foreach (var entry in entries)
				{
					int newChance = GetChance(entry, function, recipeTier, chanceTier);
					int cached = GetCachedChance(entry, cache);
					int chance = newChance + cached;
					if (PassesChance(chance, entry.MaxChance)) newChance -= entry.MaxChance;
					else failed = true;
					UpdateCachedChance(entry.Payload, cache, newChance / 2 + cached);
					if (failed) break;
				}
				if (!failed) result.AddRange(entries);
			}
			return result;
		}
	}

	private sealed class XorLogic : ChanceLogic
	{
		public XorLogic() : base("xor") { }
		public override IReadOnlyList<Recipe.Content.Content> Roll(
			object cap, IReadOnlyList<Recipe.Content.Content> entries,
			ChanceBoostFunction function, int recipeTier, int chanceTier,
			IDictionary<object, int>? cache, int times)
		{
			var chancesOutOfTenThousand = new List<int>();
			foreach (var orig in entries)
			{
				if (orig.MaxChance == GetMaxChancedValue())
					chancesOutOfTenThousand.Add(orig.Chance);
				else
					chancesOutOfTenThousand.Add((int)((orig.Chance / (float)orig.MaxChance) * GetMaxChancedValue()));
			}

			int chanceTotal = 0;
			foreach (int chance in chancesOutOfTenThousand) chanceTotal += chance;

			if (chanceTotal != GetMaxChancedValue())
			{
				int chanceTotalDecremented = GetMaxChancedValue();
				for (int i = 0; i < chancesOutOfTenThousand.Count; i++)
				{
					int newChance = (int)(chancesOutOfTenThousand[i] *
						((float)GetMaxChancedValue() / (float)chanceTotal));
					if (i == chancesOutOfTenThousand.Count - 1)
						chancesOutOfTenThousand[i] = chanceTotalDecremented;
					else
						chancesOutOfTenThousand[i] = newChance;
					chanceTotalDecremented -= newChance;
				}
			}

			var normalizedEntries = new List<Recipe.Content.Content>();
			for (int i = 0; i < chancesOutOfTenThousand.Count; i++)
				normalizedEntries.Add(new Recipe.Content.Content(entries[i].Payload,
					chancesOutOfTenThousand[i], GetMaxChancedValue(), entries[i].TierChanceBoost));

			var result = new List<Recipe.Content.Content>();
			int nonGuaranteedTimes = times;
			if (times > 1)
			{
				foreach (var entry in normalizedEntries)
				{
					int newChance = GetChance(entry, function, recipeTier, chanceTier);
					int totalChance = times * newChance;
					int guaranteed = totalChance / 10000;
					if (guaranteed > 0)
					{
						result.Add(entry.CopyChanced(cap, ContentModifier.Multiplier_(guaranteed)));
						nonGuaranteedTimes -= guaranteed;
					}
				}
			}
			for (int i = 0; i < nonGuaranteedTimes; ++i)
			{
				Recipe.Content.Content? selected = null;
				int maxChance = GetMaxChancedValue();
				foreach (var entry in normalizedEntries)
				{
					int newChance = GetChance(entry, function, recipeTier, chanceTier);
					int cached = GetCachedChance(entry, cache);
					int chance = newChance + cached;
					if (PassesChance(chance, maxChance))
					{
						selected = entry;
						newChance -= maxChance;
					}
					UpdateCachedChance(entry.Payload, cache, newChance / 2 + cached);
					if (selected != null) break;
					maxChance -= newChance;
				}
				if (selected != null) result.Add(selected);
			}
			return result;
		}
	}

	private sealed class NoneLogic : ChanceLogic
	{
		public NoneLogic() : base("none") { }
		public override IReadOnlyList<Recipe.Content.Content> Roll(
			object cap, IReadOnlyList<Recipe.Content.Content> entries,
			ChanceBoostFunction function, int recipeTier, int chanceTier,
			IDictionary<object, int>? cache, int times) =>
				System.Array.Empty<Recipe.Content.Content>();
	}
}
