#nullable enable
using System;
using GregTechCEuTerraria.Api.Cover;

namespace GregTechCEuTerraria.Api.Machine;

public sealed class ConditionalSubscriptionHandler
{
	private readonly ICoverable _holder;
	private readonly Action _runnable;
	private readonly Func<bool> _condition;

	private TickableSubscription? _subscription;

	public ConditionalSubscriptionHandler(ICoverable holder, Action runnable, Func<bool> condition)
	{
		_holder = holder;
		_runnable = runnable;
		_condition = condition;
	}

	public void Initialize()
	{
		TickableSubscription? init = null;
		init = _holder.SubscribeServerTick(() =>
		{
			init?.Unsubscribe();
			UpdateSubscription();
		});
	}

	public void UpdateSubscription()
	{
		if (_condition())
		{
			if (_subscription is null || !_subscription.StillSubscribed)
				_subscription = _holder.SubscribeServerTick(_runnable);
		}
		else if (_subscription != null)
		{
			_subscription.Unsubscribe();
			_subscription = null;
		}
	}

	public void Unsubscribe()
	{
		if (_subscription != null)
		{
			_subscription.Unsubscribe();
			_subscription = null;
		}
	}
}
