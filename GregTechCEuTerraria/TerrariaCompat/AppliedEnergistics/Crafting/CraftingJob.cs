// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (the Future<ICraftingPlan> returned by appeng.me.service.CraftingService#beginCraftingCalculation,
// backed by its CRAFTING_POOL daemon thread factory), Forge 1.20.1. LGPL-3.0-only. See AE2 LICENSE.

#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;

public sealed class CraftingJob
{
	private readonly CraftingCalculation _calculation;
	private readonly Task<CraftingPlan> _task;
	private volatile bool _canceled;

	internal CraftingJob(CraftingCalculation calculation)
	{
		_calculation = calculation;
		_task = Task.Factory.StartNew(
			Execute, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}

	private CraftingPlan Execute()
	{
		Thread.CurrentThread.Name ??= "AE Crafting Calculator";
		return _calculation.Run();
	}

	public bool IsDone => _task.IsCompleted;

	public bool IsCanceled => _canceled;

	public Exception? Error => _task.IsFaulted ? _task.Exception?.InnerException : null;

	public CraftingPlan? Get() => _task.Status == TaskStatus.RanToCompletion ? _task.Result : null;

	public void Cancel()
	{
		_canceled = true;
		_calculation.Interrupt();
	}
}
