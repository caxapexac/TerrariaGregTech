#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Utils;

namespace GregTechCEuTerraria.Api.Machine.Trait;

public interface INotifiableRecipeHandler
{
	void SetDistinct(bool distinct);
	void NotifyListeners();
	ISubscription AddChangedListener(Action listener);
}

public abstract class NotifiableRecipeHandlerTrait<T> : MachineTrait, IRecipeHandlerTrait<T>, INotifiableRecipeHandler
{
	protected readonly List<Action> _listeners = new();

	public bool IsDistinct { get; private set; }

	public void SetDistinct(bool distinct)
	{
		IsDistinct = distinct;
	}

	public ISubscription AddChangedListener(Action listener)
	{
		_listeners.Add(listener);
		return new ListenerSubscription(_listeners, listener);
	}

	public void NotifyListeners()
	{
		foreach (var l in _listeners.ToArray()) l();
	}

	public abstract List<T>? HandleRecipeInner(IO io, GTRecipe recipe, List<T> left, bool simulate);
	public abstract IReadOnlyList<object> GetContents();
	public abstract double GetTotalContentAmount();
	public abstract RecipeCapability<T> GetCapability();
	public abstract IO GetHandlerIO();

	public virtual bool Test(T ingredient) => true;

	public virtual int GetPriority() => IFilteredHandler<T>.NORMAL;

	bool IRecipeHandler<T>.IsDistinct() => IsDistinct;

	private sealed class ListenerSubscription : ISubscription
	{
		private readonly List<Action> _list;
		private readonly Action _listener;
		public ListenerSubscription(List<Action> list, Action listener)
		{
			_list = list;
			_listener = listener;
		}
		public void Unsubscribe() => _list.Remove(_listener);
	}
}
