#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;

public sealed class MeStorageMachine : MetaMachine, IMeStorageDevice
{
	private const long Capacity = int.MaxValue;

	private readonly SingleTypeMeStorage _store = new(Capacity);

	public MeStorageMachine() { }

	protected override string Label => "ME Storage";

	public override bool SupportsCovers => false;

	public MEStorage GetMeStorage() => _store;

	public AEKey? StoredKey => _store.What;
	public long StoredAmount => _store.Amount;
	public int StoredTypeCount => _store.What != null ? 1 : 0;
	public long TotalStored => _store.Amount;
	public long MaxAmount => Capacity;

	public Item FirstStoredStack() => _store.What is AEItemKey ik ? ik.ToStack(1) : new Item();
	public long FirstStoredAmount() => _store.Amount;

	public Item InsertCursor(Item cursor)
	{
		if (cursor is null || cursor.IsAir) return new Item();
		var key = AEItemKey.Of(cursor);
		if (key is null) return cursor;
		long inserted = _store.Insert(key, cursor.stack, Actionable.MODULATE, IActionSource.Empty());
		long leftover = cursor.stack - inserted;
		if (leftover <= 0) return new Item();
		var rem = cursor.Clone();
		rem.stack = (int)leftover;
		return rem;
	}

	public void DumpFirstTo(Player player)
	{
		if (_store.What is not AEItemKey ik) return;
		long taken = _store.Extract(ik, ik.GetMaxStackSize(), Actionable.MODULATE, IActionSource.Empty());
		if (taken <= 0) return;
		var give = ik.ToStack((int)taken);
		global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(
			player, player.GetSource_OpenItem(give.type), give);
	}

	public override void WritePortableData(TagCompound tag) => tag["store"] = _store.Save();
	public override void ReadPortableData(TagCompound tag)
	{
		if (tag.ContainsKey("store")) _store.Load(tag.GetCompound("store"));
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["store"] = _store.Save();
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("store")) _store.Load(tag.GetCompound("store"));
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		var net = MeNetworkSystem.NetOf(this);
		lines.Add(net == null ? "[c/FF8888:Not connected]" : "ME Network: connected");
		lines.Add(_store.What == null
			? $"Stored: empty  (cap {Capacity:N0})"
			: $"Stored: {_store.What.GetDisplayName()}  {_store.Amount:N0} / {Capacity:N0}");
		lines.Add("Holds one type. Right-click to open / deposit");
	}

	private sealed class SingleTypeMeStorage : MEStorage
	{
		public AEKey? What;
		public long Amount;
		private readonly long _cap;

		public SingleTypeMeStorage(long cap) => _cap = cap;

		public string GetDescription() => "ME Storage";

		public bool IsPreferredStorageFor(AEKey what, IActionSource source) =>
			What != null && Amount > 0 && What.Equals(what);

		public long Insert(AEKey what, long amount, Actionable mode, IActionSource source)
		{
			if (amount <= 0) return 0;
			if (What != null && !What.Equals(what)) return 0;
			long room = _cap - Amount;
			if (room <= 0) return 0;
			long ins = System.Math.Min(amount, room);
			if (mode == Actionable.MODULATE)
			{
				if (What == null) What = what;
				Amount += ins;
			}
			return ins;
		}

		public long Extract(AEKey what, long amount, Actionable mode, IActionSource source)
		{
			if (What == null || !What.Equals(what) || amount <= 0) return 0;
			long ext = System.Math.Min(amount, Amount);
			if (mode == Actionable.MODULATE)
			{
				Amount -= ext;
				if (Amount <= 0) { What = null; Amount = 0; }
			}
			return ext;
		}

		public void GetAvailableStacks(KeyCounter @out)
		{
			if (What != null && Amount > 0) @out.Add(What, Amount);
		}

		public TagCompound Save()
		{
			var tag = new TagCompound();
			if (What != null && Amount > 0)
			{
				tag["what"] = What.ToTagGeneric();
				tag["amt"] = Amount;
			}
			return tag;
		}

		public void Load(TagCompound tag)
		{
			if (tag.ContainsKey("what"))
			{
				What = AEKey.FromTagGeneric(tag.GetCompound("what"));
				Amount = What != null ? tag.GetLong("amt") : 0;
			}
			else { What = null; Amount = 0; }
		}
	}
}
