#nullable enable
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Items.MeCables;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class MeCableLayerHandle : IGridLayerHandle
{
	public static readonly MeCableLayerHandle Instance = new();
	private MeCableLayerHandle() { }

	public bool Has(int x, int y) => MeCableLayerSystem.Cables.Has(x, y);

	public bool TryPlace(MeCableCell cell, int x, int y, Player placer)
	{
		if (!MeCableLayerSystem.Cables.CanPlaceAt(x, y)) return false;
		var existing = MeCableLayerSystem.Cables.CellAt(x, y);
		if (existing.HasValue && existing.Value.Equals(cell)) return false;
		if (existing.HasValue) Refund(placer, existing.Value);
		MeCableLayerSystem.Cables.Set(x, y, cell);
		MeCablePackets.SendSet(x, y, cell);
		return true;
	}

	public bool CutAt(int x, int y, Player remover)
	{
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1) return false;
		var existing = MeCableLayerSystem.Cables.CellAt(x, y);
		if (existing is not { } cell) return false;
		MeCableLayerSystem.Cables.Remove(x, y);
		MeCablePackets.SendRemove(x, y);
		if (MeBusLayerSystem.Buses.HasAny(x, y))
			foreach (var (side, _, _) in Api.Capability.IODirectionExtensions.Cardinal4)
				if (MeBusLayerSystem.Buses.Get(x, y, side) is not null)
					MeBusPackets.SetSide(x, y, side, null);
		Refund(remover, cell);
		SoundEngine.PlaySound(SoundID.Tink, new Vector2(x * 16f, y * 16f));
		return true;
	}

	private static void Refund(Player player, MeCableCell cell)
	{
		int? type = MeCableItemRegistry.Get(cell.Color);
		if (type is null) return;
		global::GregTechCEuTerraria.TerrariaCompat.Utils.PlayerGive.Give(
			player, player.GetSource_ItemUse(player.HeldItem), type.Value, 1);
	}
}
