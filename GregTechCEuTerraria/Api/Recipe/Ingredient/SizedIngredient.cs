#nullable enable
using System.Collections.Generic;
using Terraria;

namespace GregTechCEuTerraria.Api.Recipe.Ingredient;

// port of com.gregtechceu.gtceu.api.recipe.ingredient.SizedIngredient.
public class SizedIngredient : Ingredient
{
	public Ingredient Inner { get; }

	private int _amount;
	public int Amount
	{
		get => _amount;
		set { _amount = value; _changed = true; }
	}

	private Item[]? _itemStacks;
	private bool _changed = true;

	public SizedIngredient(Ingredient inner, int amount)
	{
		Inner = inner;
		_amount = amount;
	}

	public static SizedIngredient Create(Ingredient inner, int amount) => new(inner, amount);
	public static SizedIngredient Create(Ingredient inner) => new(inner, 1);

	public override bool Test(Item item) => Inner.Test(item);

	public override IReadOnlyList<Item> GetItems()
	{
		if (Inner is IntProviderIngredient ipi) return ipi.GetItems();
		if (_changed || _itemStacks is null)
		{
			var innerStacks = Inner.GetItems();
			_itemStacks = new Item[innerStacks.Count];
			for (int i = 0; i < _itemStacks.Length; i++)
			{
				var copy = innerStacks[i].Clone();
				copy.stack = _amount;
				_itemStacks[i] = copy;
			}
			_changed = false;
		}
		return _itemStacks;
	}

	public override bool IsEmpty => Inner.IsEmpty;
	public override string GetTypeName() => "gtceu:sized";
}
