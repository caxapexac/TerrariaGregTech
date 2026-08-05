#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.Items.Patterns;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net.Actions;

public sealed class MePatternAccessAction : IMachineAction
{
	public enum Kind : byte
	{
		PickupOrSetDown    = 0,
		SplitOrPlaceSingle = 1,
		ShiftClick         = 2,
		CreativeDuplicate  = 3,
	}

	public PacketType Type => PacketType.MePatternAccess;

	private Point16 _providerPos;
	private int _slot;
	private Kind _kind;
	private Item _cursor = new();

	public MePatternAccessAction() { }
	public MePatternAccessAction(Point16 providerPos, int slot, Kind kind, Item cursor)
	{
		_providerPos = providerPos;
		_slot = slot;
		_kind = kind;
		_cursor = cursor.Clone();
	}

	public void Write(BinaryWriter w)
	{
		w.WritePoint16(_providerPos);
		w.Write((byte)_slot);
		w.Write((byte)_kind);
		w.WriteItem(_cursor);
	}

	public void Read(BinaryReader r)
	{
		_providerPos = r.ReadPoint16();
		_slot = r.ReadByte();
		_kind = (Kind)r.ReadByte();
		_cursor = r.ReadItem();
	}

	public void Apply(MetaMachine entity, int byWhoAmI)
	{
		if (entity is not IMePatternAccessHost term) return;
		var net = term.Network;
		if (net is null) return;

		if (!TileEntity.ByPosition.TryGetValue(_providerPos, out var te)
			|| te is not PatternProviderMachine provider) return;

		bool inNet = false;
		foreach (var p in net.Providers)
			if (ReferenceEquals(p, provider)) { inNet = true; break; }
		if (!inNet) return;

		var slots = provider.GetSlotGroup(SlotGroup.InventoryInput);
		if (slots is null || _slot < 0 || _slot >= slots.Length) return;

		switch (_kind)
		{
			case Kind.PickupOrSetDown:    PickupOrSetDown(slots, byWhoAmI); break;
			case Kind.SplitOrPlaceSingle: SplitOrPlaceSingle(slots, byWhoAmI); break;
			case Kind.ShiftClick:         ShiftClick(slots, byWhoAmI); break;
			case Kind.CreativeDuplicate:  CreativeDuplicate(slots, byWhoAmI); break;
		}

		provider.NotifySlotGroupChanged(SlotGroup.InventoryInput);

		if (Main.netMode == NetmodeID.Server)
			MachineStateSyncPacket.Broadcast(provider);
	}

	private static bool IsPattern(Item it) =>
		!it.IsAir && it.ModItem is EncodedPatternItem e && e.Pattern != null;

	private void PickupOrSetDown(Item[] slots, int byWhoAmI)
	{
		ref Item slot = ref slots[_slot];
		if (!_cursor.IsAir)
		{
			if (!IsPattern(_cursor)) { CursorUpdatePacket.SetCursor(byWhoAmI, _cursor); return; }
			if (slot.IsAir)
			{
				slot = _cursor.Clone();
				CursorUpdatePacket.SetCursor(byWhoAmI, new Item());
			}
			else
			{
				var old = slot;
				slot = _cursor.Clone();
				CursorUpdatePacket.SetCursor(byWhoAmI, old);
			}
		}
		else
		{
			var taken = slot;
			slot = new Item();
			CursorUpdatePacket.SetCursor(byWhoAmI, taken ?? new Item());
		}
	}

	private void SplitOrPlaceSingle(Item[] slots, int byWhoAmI)
	{
		ref Item slot = ref slots[_slot];
		if (!_cursor.IsAir)
		{
			if (!IsPattern(_cursor)) { CursorUpdatePacket.SetCursor(byWhoAmI, _cursor); return; }
			if (slot.IsAir)
			{
				var one = _cursor.Clone();
				one.stack = 1;
				slot = one;
				_cursor.stack -= 1;
				CursorUpdatePacket.SetCursor(byWhoAmI, _cursor.stack <= 0 ? new Item() : _cursor);
			}
			else
			{
				CursorUpdatePacket.SetCursor(byWhoAmI, _cursor);
			}
		}
		else if (!slot.IsAir)
		{
			int take = (slot.stack + 1) / 2;
			var taken = slot.Clone();
			taken.stack = take;
			slot.stack -= take;
			if (slot.stack <= 0) slot = new Item();
			CursorUpdatePacket.SetCursor(byWhoAmI, taken);
		}
		else
		{
			CursorUpdatePacket.SetCursor(byWhoAmI, new Item());
		}
	}

	private void CreativeDuplicate(Item[] slots, int byWhoAmI)
	{
		if (!Main.GameModeInfo.IsJourneyMode) return;
		if (!_cursor.IsAir) return;
		var slot = slots[_slot];
		if (slot.IsAir) return;
		CursorUpdatePacket.SetCursor(byWhoAmI, slot.Clone());
	}

	private void ShiftClick(Item[] slots, int byWhoAmI)
	{
		ref Item slot = ref slots[_slot];
		if (slot.IsAir) return;
		var moved = slot;
		slot = new Item();
		var player = Main.player[byWhoAmI];
		PlayerGive.Give(player, player.GetSource_Misc("gtceu_pattern_access"), moved);
	}

}
