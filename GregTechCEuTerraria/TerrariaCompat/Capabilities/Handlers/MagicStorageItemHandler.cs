#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Capability;
using MagicStorage;
using MagicStorage.Common.Systems;
using MagicStorage.Components;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Capabilities.Handlers;

[JITWhenModsEnabled("MagicStorage")]
public sealed class MagicStorageItemHandler : IItemHandler
{
	private readonly TEStorageHeart _heart;
	private List<Item>? _snapshot;

	private MagicStorageItemHandler(TEStorageHeart heart) => _heart = heart;

	private List<Item> Snapshot =>
		_snapshot ??= _heart.GetStoredItems().Where(static i => !i.IsAir).ToList();

	public static IItemHandler? At(int x, int y)
	{
		if (x < 1 || y < 1 || x >= Main.maxTilesX - 1 || y >= Main.maxTilesY - 1) return null;
		Tile tile = Main.tile[x, y];
		if (!tile.HasTile || TileLoader.GetTile(tile.TileType) is not StorageAccess) return null;

		int left = x - (tile.TileFrameX % 36) / 18;
		int top = y - (tile.TileFrameY % 36) / 18;
		Point16 origin = new(left, top);
		Point16 center = TileEntity.ByPosition.TryGetValue(origin, out TileEntity? oe) && oe is TEStorageCenter
			? origin
			: TEStorageComponent.FindStorageCenter(origin);
		if (center.X < 0 || center.Y < 0) return null;
		if (!TileEntity.ByPosition.TryGetValue(center, out TileEntity? te) || te is not TEStorageCenter sc) return null;

		TEStorageHeart? heart = sc.GetHeart();
		return heart is null ? null : new MagicStorageItemHandler(heart);
	}

	public int SlotCount => Snapshot.Count + 1;

	public Item GetSlot(int slot) => slot <= 0 ? new Item() : Snapshot[slot - 1];

	public Item Insert(int slot, Item item, bool simulate)
	{
		if (item is null || item.IsAir) return new Item();

		if (MagicStorageNbtGuard.HoldsCustomData(item)) return item.Clone();

		int canFit = ComputeCanFit(item);
		if (canFit <= 0) return item.Clone();

		if (simulate)
		{
			Item preview = item.Clone();
			preview.stack = item.stack - canFit;
			if (preview.stack <= 0) preview.TurnToAir();
			return preview;
		}

		Item deposit = item.Clone();
		deposit.stack = canFit;
		_heart.DepositItem(deposit);
		int deposited = canFit - deposit.stack;

		Item leftover = item.Clone();
		leftover.stack = item.stack - deposited;
		if (leftover.stack <= 0) leftover.TurnToAir();

		if (deposited > 0) HandleStorageItemChange();
		return leftover;
	}

	public Item Extract(int slot, int maxAmount, bool simulate)
	{
		if (slot <= 0 || maxAmount <= 0) return new Item();
		int idx = slot - 1;
		if (idx >= Snapshot.Count) return new Item();

		Item stored = Snapshot[idx];
		if (stored.IsAir) return new Item();

		if (MagicStorageNbtGuard.HoldsCustomData(stored)) return new Item();

		int take = Math.Min(stored.stack, maxAmount);
		if (take <= 0) return new Item();

		if (simulate)
		{
			Item preview = stored.Clone();
			preview.stack = take;
			return preview;
		}

		Item lookFor = stored.Clone();
		lookFor.stack = take;
		Item withdrawn = _heart.Withdraw(lookFor, keepOneIfFavorite: true);
		if (!withdrawn.IsAir && withdrawn.stack > 0)
			HandleStorageItemChange();
		return withdrawn;
	}

	private int ComputeCanFit(Item item)
	{
		foreach (TEAbstractStorageUnit unit in _heart.GetStorageUnits())
			if (unit is TEStorageUnit { Inactive: false, IsFull: false })
				return Math.Min(item.stack, item.maxStack);
		return 0;
	}

	private void HandleStorageItemChange()
	{
		if (Main.netMode == NetmodeID.Server)
			NetHelper.SendRefreshNetworkItems(_heart.Position);
		else if (Main.netMode == NetmodeID.SinglePlayer)
			MagicUI.RefreshItems();
	}
}
