// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (appeng.crafting.CraftingCalculation), Forge 1.20.1. LGPL-3.0-only. See AE2 LICENSE.

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Core;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;

public enum CalculationStrategy
{
	ReportMissingItems,
	CraftLess
}

public sealed class CraftingCalculation
{
	private readonly NetworkCraftingSimulationState _networkInv;
	private readonly KeyCounter _missing = new();
	private readonly object _monitor = new();
	private readonly Stopwatch _watch = new();
	private readonly CraftingTreeNode _tree;
	private readonly AEKey _output;
	private readonly long _requestedAmount;
	private readonly CalculationStrategy _strategy;
	private bool _simulate = false;
	internal readonly IActionSource SimRequester;
	private bool _running = false;
	private bool _done = false;
	private int _time = 5;
	private int _incTime = int.MaxValue;
	private volatile bool _interrupted;
	private readonly List<CraftAttempt>? _attempts = AELog.IsCraftingLogEnabled() ? new List<CraftAttempt>() : null;

	public CraftingCalculation(MeNetwork net, GenericStack output, CalculationStrategy strategy, IActionSource src)
	{
		_output = output.What;
		_requestedAmount = output.Amount;
		_strategy = strategy;
		SimRequester = src;

		_networkInv = new NetworkCraftingSimulationState(net, src);
		_tree = new CraftingTreeNode(net, this, _output, 1, null, -1);
	}

	internal void AddMissing(AEKey what, long amount) => _missing.Add(what, amount);

	public CraftingPlan Run()
	{
		try
		{
			MeCraftingTickSystem.RegisterCraftingSimulation(this);
			HandlePausing();

			var plan = ComputePlan();
			LogCraftingJob(plan);
			return plan;
		}
		catch (Exception ex)
		{
			AELog.Info(ex, "Exception during crafting calculation.");
			throw;
		}
		finally
		{
			Finish();
		}
	}

	private CraftingPlan ComputePlan()
	{
		var fullAmountPlan = RunCraftAttempt(false, _requestedAmount);
		if (fullAmountPlan != null)
			return fullAmountPlan;

		if (_strategy == CalculationStrategy.CraftLess)
		{
			long successfulAmount = 0;
			CraftingPlan? successfulPlan = null;
			for (long increment = HighestOneBit(_requestedAmount); increment > 0; increment /= 2)
			{
				long testAmount = successfulAmount + increment;
				if (testAmount < _requestedAmount)
				{
					var plan = RunCraftAttempt(false, testAmount);
					if (plan != null)
					{
						successfulAmount = testAmount;
						successfulPlan = plan;
					}
				}
			}
			if (successfulPlan != null)
				return successfulPlan;
		}

		return RunCraftAttempt(true, _requestedAmount)!;
	}

	private CraftingPlan? RunCraftAttempt(bool simulate, long amount)
	{
		_simulate = simulate;

		var timer = Stopwatch.StartNew();

		var craftingInventory = new ChildCraftingSimulationState(_networkInv);
		craftingInventory.Ignore(_output);

		try
		{
			_tree.Request(craftingInventory, amount, null);
		}
		catch (CraftBranchFailure)
		{
			_attempts?.Add(new CraftAttempt(amount + " failed", timer));
			return null;
		}

		craftingInventory.AddBytes(_tree.GetNodeCount() * 8);

		var plan = CraftingSimulationState.BuildCraftingPlan(craftingInventory, this, amount);
		if (_attempts != null)
		{
			string type = simulate ? "simulated" : "succeeded";
			_attempts.Add(new CraftAttempt($"{amount} {type} ({plan.Bytes} bytes)", timer));
		}
		return plan;
	}

	internal void HandlePausing()
	{
		if (_incTime > 100)
		{
			_incTime = 0;

			lock (_monitor)
			{
				if (ElapsedMicroseconds() > _time)
				{
					_running = false;
					_watch.Stop();
					Monitor.Pulse(_monitor);
				}

				if (!_running)
				{
					AELog.CraftingDebug("crafting job will now sleep");

					while (!_running)
						Monitor.Wait(_monitor);

					AELog.CraftingDebug("crafting job now active");
				}
			}

			if (_interrupted)
				throw new ThreadInterruptedException();
		}
		_incTime++;
	}

	private long ElapsedMicroseconds() => _watch.ElapsedTicks * 1_000_000L / Stopwatch.Frequency;

	private void Finish()
	{
		lock (_monitor)
		{
			_running = false;
			_done = true;
			Monitor.Pulse(_monitor);
		}
	}

	internal void Interrupt()
	{
		_interrupted = true;
		lock (_monitor)
		{
			_running = true;
			Monitor.Pulse(_monitor);
		}
	}

	public bool IsSimulation => _simulate;
	public AEKey Output => _output;
	public KeyCounter GetMissingItems() => _missing;
	public bool HasMultiplePaths => _tree.HasMultiplePaths();

	public bool SimulateFor(int micros)
	{
		_time = micros;

		lock (_monitor)
		{
			if (_done)
				return false;

			_watch.Reset();
			_watch.Start();
			_running = true;

			AELog.CraftingDebug("main thread is now going to sleep");

			Monitor.Pulse(_monitor);

			while (_running)
			{
				try { Monitor.Wait(_monitor); }
				catch (ThreadInterruptedException) { }
			}

			AELog.CraftingDebug("main thread is now active");
		}

		return true;
	}

	private void LogCraftingJob(CraftingPlan plan)
	{
		if (_attempts == null)
			return;

		var player = SimRequester.GetPlayer();
		string actionSourceName = player != null ? player.name : "[unknown source]";

		var message = new StringBuilder();
		message.Append($"CraftingCalculation issued by {actionSourceName} requesting [{_requestedAmount}x{_output}] breakdown:\n");
		foreach (var attempt in _attempts)
			message.Append($" - {attempt.Description} in {attempt.Stopwatch.ElapsedMilliseconds} ms\n");
		message.Append($" - final plan: {plan.FinalOutput.Amount} ({plan.Bytes} bytes)");

		AELog.Crafting(message.ToString());
	}

	private static long HighestOneBit(long value)
	{
		if (value <= 0) return 0;
		long bit = 1;
		while ((bit << 1) > 0 && (bit << 1) <= value)
			bit <<= 1;
		return bit;
	}

	private readonly record struct CraftAttempt(string Description, Stopwatch Stopwatch);
}
