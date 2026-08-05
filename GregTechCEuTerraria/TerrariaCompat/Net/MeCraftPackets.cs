#nullable enable
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Core;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public readonly record struct CpuInfo(Point16 Pos, long Bytes, int CoProcessors, bool Busy);

public readonly record struct CraftCpuStatus(Point16 Pos, bool Busy, AEKey? Output, long OutputAmount,
	long Bytes, int CoProcessors, float Progress, bool CantStore);

public readonly record struct CraftStatusEntry(AEKey What, long Stored, long Active, long Pending);

public sealed class CraftStatusSnapshot
{
	public readonly List<CraftCpuStatus> Cpus;
	public readonly int SelectedIndex;
	public readonly long ElapsedNs;
	public readonly long RemainingItemCount;
	public readonly long StartItemCount;
	public readonly bool CantStore;
	public readonly List<CraftStatusEntry> Entries;
	public readonly string StallReason;

	public CraftStatusSnapshot(List<CraftCpuStatus> cpus, int selectedIndex, long elapsedNs,
		long remaining, long start, bool cantStore, List<CraftStatusEntry> entries, string stallReason = "")
	{
		Cpus = cpus; SelectedIndex = selectedIndex; ElapsedNs = elapsedNs;
		RemainingItemCount = remaining; StartItemCount = start; CantStore = cantStore; Entries = entries;
		StallReason = stallReason ?? "";
	}

	public static readonly CraftStatusSnapshot Empty = new(new(), -1, 0, 0, 0, false, new());

	public int BusyCount { get { int n = 0; foreach (var c in Cpus) if (c.Busy) n++; return n; } }
}

public static class MeCraftPackets
{
	private static MeNetwork? NetAt(Point16 termPos)
	{
		if (TileEntity.ByPosition.TryGetValue(termPos, out var te) && te is MeTerminalMachine term)
			return term.Network;
		return null;
	}

	private static List<CpuInfo> GatherCpus(MeNetwork net)
	{
		var list = new List<CpuInfo>();
		foreach (var dev in net.Devices)
			if (dev is QuantumComputerMachine cpu)
				list.Add(new CpuInfo(cpu.Position, cpu.AvailableStorage, cpu.CoProcessors, cpu.Logic.HasJob));
		return list;
	}

	public static void Begin(Point16 termPos, AEKey what, long amount)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) { DoBegin(termPos, what, amount, Main.myPlayer); return; }
		var p = NetRouter.NewPacket(PacketType.MeCraftPlanBegin);
		p.Write(termPos.X); p.Write(termPos.Y);
		AEKey.WriteKey(p, what);
		p.Write7BitEncodedInt64(amount);
		p.Send();
	}

	public static void HandleBegin(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server) return;
		var pos = new Point16(r.ReadInt16(), r.ReadInt16());
		var what = AEKey.ReadKey(r);
		long amount = r.Read7BitEncodedInt64();
		if (what != null) DoBegin(pos, what, amount, whoAmI);
	}

	private sealed class PendingPlan
	{
		public Point16 TermPos;
		public AEKey What = null!;
		public long Amount;
		public CraftingJob Job = null!;
	}

	private sealed class ReadyPlan
	{
		public Point16 TermPos;
		public AEKey What = null!;
		public long Amount;
		public CraftingPlan Plan = null!;
	}

	private static readonly Dictionary<int, PendingPlan> _pending = new();
	private static readonly Dictionary<int, ReadyPlan> _ready = new();

	private static bool StillConnected(int player)
	{
		if (Main.netMode != NetmodeID.Server) return true;
		return player >= 0 && player < Main.maxPlayers && Main.player[player].active;
	}

	private static IActionSource SourceOf(int whoAmI) =>
		whoAmI >= 0 && whoAmI < Main.maxPlayers && Main.player[whoAmI].active
			? IActionSource.OfPlayer(Main.player[whoAmI])
			: IActionSource.Empty();

	public static void ClearPlans()
	{
		foreach (var p in _pending.Values) p.Job.Cancel();
		_pending.Clear();
		_ready.Clear();
	}

	public static void PollPendingPlans()
	{
		if (_pending.Count == 0) return;

		List<int>? finished = null;
		foreach (var (player, pending) in _pending)
		{
			if (!StillConnected(player))
			{
				pending.Job.Cancel();
				(finished ??= new List<int>()).Add(player);
				_ready.Remove(player);
				continue;
			}

			if (!pending.Job.IsDone) continue;
			(finished ??= new List<int>()).Add(player);

			var plan = pending.Job.Get();
			if (plan == null)
			{
				if (!pending.Job.IsCanceled && pending.Job.Error is { } error)
				{
					Notify(player, "Error: " + error.Message);
					AELog.Warn(error, "Failed to start crafting job.");
				}
				continue;
			}

			_ready[player] = new ReadyPlan
			{
				TermPos = pending.TermPos,
				What = pending.What,
				Amount = pending.Amount,
				Plan = plan,
			};
			SendPlanResult(pending.TermPos, pending.What, pending.Amount, plan, player);
		}

		if (finished != null)
			foreach (int player in finished)
				_pending.Remove(player);
	}

	private static void DoBegin(Point16 termPos, AEKey what, long amount, int whoAmI)
	{
		var net = NetAt(termPos);
		if (net == null) return;

		if (_pending.TryGetValue(whoAmI, out var existing))
			existing.Job.Cancel();
		_ready.Remove(whoAmI);

		var job = MeCraftingService.BeginCraftingCalculation(
			net, what, amount, CalculationStrategy.ReportMissingItems, SourceOf(whoAmI));
		if (job == null) return;

		_pending[whoAmI] = new PendingPlan
		{
			TermPos = termPos,
			What = what,
			Amount = amount,
			Job = job,
		};
	}

	public static void Abandon()
	{
		if (Main.gameMenu) return;
		if (Main.netMode == NetmodeID.SinglePlayer) { DoAbandon(Main.myPlayer); return; }
		NetRouter.NewPacket(PacketType.MeCraftPlanAbandon).Send();
	}

	public static void HandleAbandon(int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server) return;
		DoAbandon(whoAmI);
	}

	private static void DoAbandon(int whoAmI)
	{
		if (_pending.TryGetValue(whoAmI, out var pending))
		{
			pending.Job.Cancel();
			_pending.Remove(whoAmI);
		}
		_ready.Remove(whoAmI);
	}

	private static void SendPlanResult(Point16 termPos, AEKey what, long amount, CraftingPlan plan, int whoAmI)
	{
		var net = NetAt(termPos);
		if (net == null) return;
		var summary = CraftingPlanSummary.FromPlan(plan, net.GetStorage(), SourceOf(whoAmI));
		var cpus = GatherCpus(net);
		var invalid = MeCraftingService.CollectUnfulfillableByOutput(net, plan);

		if (Main.netMode == NetmodeID.SinglePlayer)
		{
			MeCraftConfirmSystem.OpenFor(termPos, what, amount, summary, cpus, invalid);
			return;
		}
		var p = NetRouter.NewPacket(PacketType.MeCraftPlanResult);
		p.Write(termPos.X); p.Write(termPos.Y);
		AEKey.WriteKey(p, what);
		p.Write7BitEncodedInt64(amount);
		summary.Write(p);
		p.Write7BitEncodedInt(cpus.Count);
		foreach (var c in cpus) { p.Write(c.Pos.X); p.Write(c.Pos.Y); p.Write7BitEncodedInt64(c.Bytes); p.Write7BitEncodedInt(c.CoProcessors); p.Write(c.Busy); }
		p.Write7BitEncodedInt(invalid.Count);
		foreach (var (iw, reason) in invalid) { AEKey.WriteKey(p, iw); p.Write(reason); }
		p.Send(toClient: whoAmI);
	}

	public static void HandleResult(BinaryReader r)
	{
		var termPos = new Point16(r.ReadInt16(), r.ReadInt16());
		var what = AEKey.ReadKey(r);
		long amount = r.Read7BitEncodedInt64();
		var summary = CraftingPlanSummary.Read(r);
		int n = r.Read7BitEncodedInt();
		var cpus = new List<CpuInfo>(n);
		for (int i = 0; i < n; i++)
			cpus.Add(new CpuInfo(new Point16(r.ReadInt16(), r.ReadInt16()),
				r.Read7BitEncodedInt64(), r.Read7BitEncodedInt(), r.ReadBoolean()));
		int wn = r.Read7BitEncodedInt();
		var invalid = new List<(AEKey what, string reason)>(wn);
		for (int i = 0; i < wn; i++)
		{
			var iw = AEKey.ReadKey(r);
			string reason = r.ReadString();
			if (iw != null) invalid.Add((iw, reason));
		}
		if (what != null) MeCraftConfirmSystem.OpenFor(termPos, what, amount, summary, cpus, invalid);
	}

	public static void Submit(Point16 termPos, AEKey what, long amount, bool automatic, Point16 cpuPos)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) { DoSubmit(termPos, what, amount, automatic, cpuPos, Main.myPlayer); return; }
		var p = NetRouter.NewPacket(PacketType.MeCraftSubmit);
		p.Write(termPos.X); p.Write(termPos.Y);
		AEKey.WriteKey(p, what);
		p.Write7BitEncodedInt64(amount);
		p.Write(automatic);
		p.Write(cpuPos.X); p.Write(cpuPos.Y);
		p.Send();
	}

	public static void HandleSubmit(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server) return;
		var termPos = new Point16(r.ReadInt16(), r.ReadInt16());
		var what = AEKey.ReadKey(r);
		long amount = r.Read7BitEncodedInt64();
		bool automatic = r.ReadBoolean();
		var cpuPos = new Point16(r.ReadInt16(), r.ReadInt16());
		if (what != null) DoSubmit(termPos, what, amount, automatic, cpuPos, whoAmI);
	}

	private static void DoSubmit(Point16 termPos, AEKey what, long amount, bool automatic, Point16 cpuPos, int whoAmI)
	{
		var net = NetAt(termPos);
		if (net == null) { Notify(whoAmI, "Not connected to an ME network"); return; }

		if (!_ready.TryGetValue(whoAmI, out var ready)) return;
		_ready.Remove(whoAmI);

		var plan = ready.Plan;
		if (plan.Simulation) { Notify(whoAmI, "Missing items - cannot craft"); return; }
		var invalid = MeCraftingService.CollectUnfulfillableByOutput(net, plan);
		if (invalid.Count > 0) { Notify(whoAmI, "Cannot craft - " + invalid[0].reason); return; }

		var src = SourceOf(whoAmI);

		CraftingSubmitResult result;
		if (automatic)
		{
			result = MeCraftingService.Submit(net, plan, null, whoAmI, src);
		}
		else if (TileEntity.ByPosition.TryGetValue(cpuPos, out var te) && te is QuantumComputerMachine cpu)
		{
			result = cpu.Logic.TrySubmitJob(plan, src, null, whoAmI);
		}
		else
		{
			result = CraftingSubmitResult.NoCpuFound;
		}
		Notify(whoAmI, result.Describe());
	}

	public static void RequestStatus(Point16 termPos, int selectedCpu)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) { DoStatus(termPos, selectedCpu, Main.myPlayer); return; }
		var p = NetRouter.NewPacket(PacketType.MeCraftStatusRequest);
		p.Write(termPos.X); p.Write(termPos.Y);
		p.Write7BitEncodedInt(selectedCpu);
		p.Send();
	}

	public static void HandleStatusRequest(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server) return;
		var termPos = new Point16(r.ReadInt16(), r.ReadInt16());
		int selected = r.Read7BitEncodedInt();
		DoStatus(termPos, selected, whoAmI);
	}

	private static void DoStatus(Point16 termPos, int selected, int whoAmI)
	{
		var net = NetAt(termPos);
		var cpus = new List<QuantumComputerMachine>();
		if (net != null)
			foreach (var dev in net.Devices)
				if (dev is QuantumComputerMachine cpu) cpus.Add(cpu);

		var cpuStatuses = new List<CraftCpuStatus>(cpus.Count);
		foreach (var cpu in cpus)
		{
			var o = cpu.Logic.FinalJobOutput;
			float prog = cpu.Logic.HasJob ? cpu.Logic.ElapsedTimeTracker.GetProgress() : 0f;
			cpuStatuses.Add(new CraftCpuStatus(cpu.Position, cpu.Logic.HasJob, o?.What, o?.Amount ?? 0,
				cpu.AvailableStorage, cpu.CoProcessors, prog, cpu.Logic.IsCantStoreItems));
		}

		int sel = cpus.Count == 0 ? -1 : System.Math.Clamp(selected, 0, cpus.Count - 1);
		long elapsed = 0, remaining = 0, start = 0;
		bool cantStore = false;
		string stallReason = "";
		var entries = new List<CraftStatusEntry>();
		if (sel >= 0)
		{
			var logic = cpus[sel].Logic;
			var tracker = logic.ElapsedTimeTracker;
			elapsed = tracker.GetElapsedTime();
			remaining = tracker.GetRemainingItemCount();
			start = tracker.GetStartItemCount();
			cantStore = logic.IsCantStoreItems;

			if (net != null && logic.HasJob)
				foreach (var pattern in logic.PendingTaskPatterns())
				{
					var reason = MeCraftingService.UnfulfillableReason(net, pattern);
					if (reason != null) { stallReason = reason; break; }
				}

			var all = new KeyCounter();
			logic.GetAllItems(all);
			foreach (var kv in all)
			{
				var what = kv.Key;
				long stored = logic.GetStored(what);
				long active = logic.GetWaitingFor(what);
				long pending = logic.GetPendingOutputs(what);
				if (stored == 0 && active == 0 && pending == 0) continue;
				entries.Add(new CraftStatusEntry(what, stored, active, pending));
			}
			entries.Sort((a, b) =>
			{
				int c = (b.Active + b.Pending).CompareTo(a.Active + a.Pending);
				return c != 0 ? c : b.Stored.CompareTo(a.Stored);
			});
		}

		var snapshot = new CraftStatusSnapshot(cpuStatuses, sel, elapsed, remaining, start, cantStore, entries, stallReason);
		if (Main.netMode == NetmodeID.SinglePlayer) { MeCraftStatusSystem.Receive(snapshot); return; }

		var pkt = NetRouter.NewPacket(PacketType.MeCraftStatusResult);
		WriteSnapshot(pkt, snapshot);
		pkt.Send(toClient: whoAmI);
	}

	public static void HandleStatusResult(BinaryReader r)
	{
		MeCraftStatusSystem.Receive(ReadSnapshot(r));
	}

	private static void WriteSnapshot(BinaryWriter p, CraftStatusSnapshot s)
	{
		p.Write7BitEncodedInt(s.Cpus.Count);
		foreach (var c in s.Cpus)
		{
			p.Write(c.Pos.X); p.Write(c.Pos.Y);
			p.Write(c.Busy);
			AEKey.WriteOptionalKey(p, c.Output);
			p.Write7BitEncodedInt64(c.OutputAmount);
			p.Write7BitEncodedInt64(c.Bytes);
			p.Write7BitEncodedInt(c.CoProcessors);
			p.Write(c.Progress);
			p.Write(c.CantStore);
		}
		p.Write7BitEncodedInt(s.SelectedIndex);
		p.Write7BitEncodedInt64(s.ElapsedNs);
		p.Write7BitEncodedInt64(s.RemainingItemCount);
		p.Write7BitEncodedInt64(s.StartItemCount);
		p.Write(s.CantStore);
		p.Write(s.StallReason);
		p.Write7BitEncodedInt(s.Entries.Count);
		foreach (var e in s.Entries)
		{
			AEKey.WriteKey(p, e.What);
			p.Write7BitEncodedInt64(e.Stored);
			p.Write7BitEncodedInt64(e.Active);
			p.Write7BitEncodedInt64(e.Pending);
		}
	}

	private static CraftStatusSnapshot ReadSnapshot(BinaryReader r)
	{
		int nc = r.Read7BitEncodedInt();
		var cpus = new List<CraftCpuStatus>(nc);
		for (int i = 0; i < nc; i++)
			cpus.Add(new CraftCpuStatus(
				new Point16(r.ReadInt16(), r.ReadInt16()), r.ReadBoolean(),
				AEKey.ReadOptionalKey(r), r.Read7BitEncodedInt64(),
				r.Read7BitEncodedInt64(), r.Read7BitEncodedInt(), r.ReadSingle(), r.ReadBoolean()));
		int sel = r.Read7BitEncodedInt();
		long elapsed = r.Read7BitEncodedInt64();
		long remaining = r.Read7BitEncodedInt64();
		long start = r.Read7BitEncodedInt64();
		bool cantStore = r.ReadBoolean();
		string stallReason = r.ReadString();
		int ne = r.Read7BitEncodedInt();
		var entries = new List<CraftStatusEntry>(ne);
		for (int i = 0; i < ne; i++)
		{
			var what = AEKey.ReadKey(r);
			long stored = r.Read7BitEncodedInt64();
			long active = r.Read7BitEncodedInt64();
			long pending = r.Read7BitEncodedInt64();
			if (what != null) entries.Add(new CraftStatusEntry(what, stored, active, pending));
		}
		return new CraftStatusSnapshot(cpus, sel, elapsed, remaining, start, cantStore, entries, stallReason);
	}

	public static void Cancel(Point16 cpuPos)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) { DoCancel(cpuPos); return; }
		var p = NetRouter.NewPacket(PacketType.MeCraftCancel);
		p.Write(cpuPos.X); p.Write(cpuPos.Y);
		p.Send();
	}

	public static void HandleCancel(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server) return;
		DoCancel(new Point16(r.ReadInt16(), r.ReadInt16()));
	}

	private static void DoCancel(Point16 cpuPos)
	{
		if (TileEntity.ByPosition.TryGetValue(cpuPos, out var te) && te is QuantumComputerMachine cpu)
			cpu.Logic.Cancel();
	}

	private static void Notify(int whoAmI, string message)
	{
		var color = new Color(120, 200, 255);
		if (Main.netMode == NetmodeID.Server)
			Terraria.Chat.ChatHelper.SendChatMessageToClient(
				Terraria.Localization.NetworkText.FromLiteral(message), color, whoAmI);
		else
			Main.NewText(message, color.R, color.G, color.B);
	}
}
