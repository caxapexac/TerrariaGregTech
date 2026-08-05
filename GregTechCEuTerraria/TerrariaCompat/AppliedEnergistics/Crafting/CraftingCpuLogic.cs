// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (appeng.crafting.execution.CraftingCpuLogic), Forge 1.20.1. LGPL-3.0-only. See AE2 LICENSE.
//
// Per-CPU crafting logic: holds the working inventory + current job, pushes pattern inputs
// to providers each tick within an op budget, routes returned outputs (waitingFor ->
// intermediate buffer; final output -> requester/network), and dumps leftovers when idle.
#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Core;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;

public sealed class CraftingCpuLogic
{
	private readonly ICraftingCpuHost _host;
	private ExecutingCraftingJob? _job;
	private readonly ListCraftingInventory _inventory;
	private readonly int[] _usedOps = new int[3];
	private readonly HashSet<Action<AEKey>> _listeners = new();
	private bool _cantStoreItems = false;
	private long _lastModifiedOnTick = Main.GameUpdateCount;

	public CraftingCpuLogic(ICraftingCpuHost host)
	{
		_host = host;
		_inventory = new ListCraftingInventory(PostChange);
	}

	public CraftingSubmitResult TrySubmitJob(CraftingPlan plan, IActionSource src,
		IMeCraftingRequester? requester, int? playerId)
	{
		if (_job != null) return CraftingSubmitResult.CpuBusy;
		if (!_host.IsOnline) return CraftingSubmitResult.CpuOffline;
		if (_host.AvailableStorage < plan.Bytes) return CraftingSubmitResult.CpuTooSmall;

		var net = _host.Network;
		if (net == null) return CraftingSubmitResult.NoCpuFound;

		var missing = CraftingCpuHelper.TryExtractInitialItems(plan, net.GetStorage(), _inventory, src);
		if (missing != null) return CraftingSubmitResult.Missing(missing);

		var craftId = Guid.NewGuid();
		var linkCpu = new CraftingLink(craftId, standalone: requester == null, cpu: this);
		_job = new ExecutingCraftingJob(plan, PostChange, linkCpu, playerId);
		_host.UpdateOutput(plan.FinalOutput);
		_host.MarkDirty();
		_host.NotifyJobStatus(_job, CraftingJobStatus.Started);

		if (requester != null)
		{
			var linkReq = new CraftingLink(craftId, standalone: false, req: requester);
			CraftingLinkManager.AddLink(linkCpu);
			CraftingLinkManager.AddLink(linkReq);
			return CraftingSubmitResult.Successful(linkReq);
		}
		return CraftingSubmitResult.Successful(null);
	}

	public void TickCraftingLogic()
	{
		if (!_host.IsOnline) return;
		_cantStoreItems = false;

		if (_job == null)
		{
			StoreItems();
			if (!_inventory.List.IsEmpty())
				_cantStoreItems = true;
			return;
		}

		if (_job.Link.IsCanceled)
		{
			Cancel();
			return;
		}

		var remainingOperations = _host.CoProcessors + 1 - (_usedOps[0] + _usedOps[1] + _usedOps[2]);
		var started = remainingOperations;

		if (remainingOperations > 0)
		{
			do
			{
				var pushedPatterns = ExecuteCrafting(remainingOperations);
				if (pushedPatterns > 0)
					remainingOperations -= pushedPatterns;
				else
					break;
			} while (remainingOperations > 0);
		}

		_usedOps[2] = _usedOps[1];
		_usedOps[1] = _usedOps[0];
		_usedOps[0] = started - remainingOperations;
	}

	private int ExecuteCrafting(int maxPatterns)
	{
		var job = _job;
		if (job == null) return 0;
		var net = _host.Network;
		if (net == null) return 0;

		var pushedPatterns = 0;

		var taskKeys = new List<MePattern>(job.Tasks.Keys);
		foreach (var details in taskKeys)
		{
			if (!job.Tasks.TryGetValue(details, out var progress))
				continue;
			if (progress.Value <= 0)
			{
				job.Tasks.Remove(details);
				continue;
			}

			var expectedOutputs = new KeyCounter();
			var expectedContainerItems = new KeyCounter();
			var craftingContainer = CraftingCpuHelper.ExtractPatternInputs(details, _inventory, expectedOutputs, expectedContainerItems);

			foreach (var providerRead in net.GetProviders(details))
			{
				if (craftingContainer == null) break;
				if (providerRead is not IMeCraftingProvider provider) continue;
				if (provider.IsBusy) continue;

				if (provider.PushPattern(details, craftingContainer))
				{
					pushedPatterns++;

					foreach (var expectedOutput in expectedOutputs)
						job.WaitingFor.Insert(expectedOutput.Key, expectedOutput.Value, Actionable.MODULATE);
					foreach (var expectedContainerItem in expectedContainerItems)
					{
						job.WaitingFor.Insert(expectedContainerItem.Key, expectedContainerItem.Value, Actionable.MODULATE);
						job.TimeTracker.AddMaxItems(expectedContainerItem.Value, expectedContainerItem.Key.KeyType);
					}

					_host.MarkDirty();

					progress.Value--;
					if (progress.Value <= 0)
					{
						job.Tasks.Remove(details);
						craftingContainer = null;
						break;
					}

					if (pushedPatterns == maxPatterns)
						return pushedPatterns;

					expectedOutputs.Reset();
					expectedContainerItems.Reset();
					craftingContainer = CraftingCpuHelper.ExtractPatternInputs(details, _inventory, expectedOutputs, expectedContainerItems);
				}
			}

			if (craftingContainer != null)
				CraftingCpuHelper.ReinjectPatternInputs(_inventory, craftingContainer);
		}

		return pushedPatterns;
	}

	public long Insert(AEKey what, long amount, Actionable type)
	{
		if (what == null || _job == null) return 0;

		var waitingFor = _job.WaitingFor.Extract(what, amount, Actionable.SIMULATE);
		if (waitingFor <= 0) return 0;
		if (amount > waitingFor) amount = waitingFor;

		if (type == Actionable.MODULATE)
		{
			_job.TimeTracker.DecrementItems(amount, what.KeyType);
			_job.WaitingFor.Extract(what, amount, Actionable.MODULATE);
			_host.MarkDirty();
		}

		long inserted = amount;
		if (what.Equals(_job.FinalOutput.What))
		{
			inserted = _job.Link.Insert(what, amount, type);

			if (type == Actionable.MODULATE)
			{
				PostChange(what);
				_job.RemainingAmount = Math.Max(0, _job.RemainingAmount - amount);
				if (_job.RemainingAmount <= 0)
				{
					FinishJob(true);
					_host.UpdateOutput(null);
				}
				else
				{
					_host.UpdateOutput(new GenericStack(_job.FinalOutput.What, _job.RemainingAmount));
				}
			}
		}
		else
		{
			if (type == Actionable.MODULATE)
				_inventory.Insert(what, amount, Actionable.MODULATE);
		}

		return inserted;
	}

	private void FinishJob(bool success)
	{
		if (_job == null) return;
		if (success) _job.Link.MarkDone();
		else _job.Link.Cancel();

		if (success) BroadcastCompletion(_job);

		_job.WaitingFor.Clear();
		foreach (var entry in _job.Tasks)
			foreach (var (what, _) in entry.Key.Outputs)
				PostChange(what);

		_host.NotifyJobStatus(_job, success ? CraftingJobStatus.Finished : CraftingJobStatus.Cancelled);
		_job = null;
		StoreItems();
	}

	private static void BroadcastCompletion(ExecutingCraftingJob job)
	{
		if (job.PlayerId is not int pid) return;

		string who = pid >= 0 && pid < Main.maxPlayers && Main.player[pid] is { active: true } pl
			? pl.name : Terraria.Localization.Language.GetTextValue(AELocale.CraftDoneSomeone);
		string what = job.FinalOutput.What?.GetDisplayName()
			?? Terraria.Localization.Language.GetTextValue(AELocale.CraftDoneItems);
		long amount = job.FinalOutput.Amount;
		long elapsedMs = job.TimeTracker.GetElapsedTime() / 1_000_000;
		string units = job.TimeTracker.DescribeTotalStarted();

		string msg = Terraria.Localization.Language.GetTextValue(AELocale.CraftDone,
			who, amount.ToString("N0"), what, FormatDuration(elapsedMs), units);
		var color = new Microsoft.Xna.Framework.Color(124, 255, 156);

		if (Main.netMode == Terraria.ID.NetmodeID.Server)
			Terraria.Chat.ChatHelper.BroadcastChatMessage(
				Terraria.Localization.NetworkText.FromLiteral(msg), color);
		else
			Main.NewText(msg, color.R, color.G, color.B);
	}

	private static string FormatDuration(long ms)
	{
		var ts = System.TimeSpan.FromMilliseconds(ms);
		if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}";
		if (ts.TotalMinutes >= 1) return $"{ts.Minutes}:{ts.Seconds:00}";
		return $"{ts.TotalSeconds:0.0}s";
	}

	public void Cancel()
	{
		if (_job == null) return;
		_host.UpdateOutput(null);
		FinishJob(false);
	}

	public void StoreItems()
	{
		if (_job != null) throw new InvalidOperationException("CPU should not have a job while dumping items");
		if (_inventory.List.IsEmpty()) return;

		var net = _host.Network;
		if (net == null) return;
		var storage = net.GetStorage();

		var snapshot = new List<KeyValuePair<AEKey, long>>(_inventory.List);
		foreach (var entry in snapshot)
		{
			PostChange(entry.Key);
			var inserted = storage.Insert(entry.Key, entry.Value, Actionable.MODULATE, _host.Src);
			_inventory.List.Set(entry.Key, entry.Value - inserted);
		}
		_inventory.List.RemoveZeros();

		_host.MarkDirty();
	}

	private void PostChange(AEKey what)
	{
		_lastModifiedOnTick = Main.GameUpdateCount;
		foreach (var listener in _listeners)
			listener(what);
	}

	public long LastModifiedOnTick => _lastModifiedOnTick;
	public bool HasJob => _job != null;
	public GenericStack? FinalJobOutput => _job?.FinalOutput;
	public ExecutingCraftingJob? Job => _job;
	public bool IsCantStoreItems => _cantStoreItems;
	public ListCraftingInventory Inventory => _inventory;

	public void AddListener(Action<AEKey> listener) => _listeners.Add(listener);
	public void RemoveListener(Action<AEKey> listener) => _listeners.Remove(listener);

	public void GetAllWaitingFor(ISet<AEKey> waitingFor)
	{
		if (_job == null) return;
		foreach (var entry in _job.WaitingFor.List)
			waitingFor.Add(entry.Key);
	}

	public ElapsedTimeTracker ElapsedTimeTracker => _job != null ? _job.TimeTracker : new ElapsedTimeTracker();

	public IEnumerable<MePattern> PendingTaskPatterns()
	{
		if (_job == null) yield break;
		foreach (var t in _job.Tasks)
			if (t.Value.Value > 0) yield return t.Key;
	}

	public long GetStored(AEKey template) => _inventory.Extract(template, long.MaxValue, Actionable.SIMULATE);

	public long GetWaitingFor(AEKey template)
		=> _job != null ? _job.WaitingFor.Extract(template, long.MaxValue, Actionable.SIMULATE) : 0;

	public long GetPendingOutputs(AEKey template)
	{
		long count = 0;
		if (_job != null)
			foreach (var t in _job.Tasks)
				foreach (var (what, amount) in t.Key.Outputs)
					if (template.Equals(what))
						count += amount * t.Value.Value;
		return count;
	}

	public void GetAllItems(KeyCounter @out)
	{
		@out.AddAll(_inventory.List);
		if (_job != null)
		{
			@out.AddAll(_job.WaitingFor.List);
			foreach (var t in _job.Tasks)
				foreach (var (what, amount) in t.Key.Outputs)
					@out.Add(what, amount * t.Value.Value);
		}
	}

	public bool IsAlive => _host.IsOnline;
	public Pipelike.Me.MeNetwork? Network => _host.Network;
	public CraftingLink? GetLastLink() => _job?.Link;

	public void ReadFromNBT(TagCompound data)
	{
		_inventory.ReadFromNBT(data.GetList<TagCompound>("inventory"));
		if (data.ContainsKey("job"))
		{
			_job = new ExecutingCraftingJob(data.GetCompound("job"), PostChange, this);
			CraftingLinkManager.AddLink(_job.Link);
			_host.UpdateOutput(new GenericStack(_job.FinalOutput.What, _job.RemainingAmount));
		}
		else
		{
			_host.UpdateOutput(null);
		}
	}

	public void WriteToNBT(TagCompound data)
	{
		data["inventory"] = _inventory.WriteToNBT();
		if (_job != null)
			data["job"] = _job.WriteToNBT();
	}
}
