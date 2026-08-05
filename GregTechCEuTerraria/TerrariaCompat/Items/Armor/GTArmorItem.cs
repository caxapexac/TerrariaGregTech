#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Armor;

public sealed class GTArmorItem : ModItem, IElectricItem
{
	private readonly ArmorSpec? _spec;
	private long _storedEu;

	public GTArmorItem() { }
	public GTArmorItem(ArmorSpec spec) { _spec = spec; }

	public override bool IsLoadingEnabled(Mod mod) => _spec != null;
	public override string Name => _spec?.Id ?? nameof(GTArmorItem);
	protected override bool CloneNewInstances => true;

	public override string Texture => _spec?.IconPath ?? "Terraria/Images/Item_22";

	public ArmorSpec Spec => _spec!;
	public ArmorSuite Suite => _spec!.Suite;
	public bool IsCharged => _storedEu >= _spec!.EnergyPerUse;

	public bool HasFlight => _spec?.HasFlight ?? false;
	public bool FlightReady => HasFlight && _storedEu >= _spec!.EnergyPerUse;

	public float FlightAscentMult => Suite == ArmorSuite.Quark ? 2.5f : 1.5f;
	public float FlightRunSpeed   => Suite == ArmorSuite.Quark ? 8.0f : 6.25f;

	public void DrainForFlight() =>
		Discharge(_spec?.EnergyPerUse ?? 0, int.MaxValue, ignoreTransferLimit: true, externally: false, simulate: false);

	public long StoredEu
	{
		get => _storedEu;
		set => _storedEu = Math.Clamp(value, 0, _spec?.Capacity ?? 0);
	}

	public bool CanProvideChargeExternally() => false;
	public bool Chargeable() => true;
	public long GetTransferLimit() => VoltageTiers.Voltage(_spec!.Tier);
	public long GetMaxCharge() => _spec?.Capacity ?? 0;
	public long GetCharge() => Math.Min(_storedEu, _spec?.Capacity ?? 0);
	public int  GetTier() => (int)(_spec?.Tier ?? 0);

	public long Charge(long amount, int chargerTier, bool ignoreTransferLimit, bool simulate)
	{
		if (Item.stack != 1 || _spec == null) return 0L;
		int tier = (int)_spec.Tier;
		if (chargerTier >= tier && amount > 0L)
		{
			long canReceive = GetMaxCharge() - GetCharge();
			if (!ignoreTransferLimit) amount = Math.Min(amount, GetTransferLimit());
			long charged = Math.Min(amount, canReceive);
			if (!simulate) _storedEu += charged;
			return charged;
		}
		return 0L;
	}

	public long Discharge(long amount, int dischargerTier, bool ignoreTransferLimit, bool externally, bool simulate)
	{
		if (Item.stack != 1 || _spec == null || externally) return 0L;
		int tier = (int)_spec.Tier;
		if (dischargerTier >= tier && amount > 0L)
		{
			if (!ignoreTransferLimit) amount = Math.Min(amount, GetTransferLimit());
			long discharged = Math.Min(amount, _storedEu);
			if (!simulate) _storedEu -= discharged;
			return discharged;
		}
		return 0L;
	}

	private const int HitDrainScale = 16;
	public void DrainOnHit(int damage)
	{
		if (_spec == null) return;
		long cost = Math.Max(1, (long)_spec.EnergyPerUse * HitDrainScale / 100L * Math.Max(1, damage));
		Discharge(cost, int.MaxValue, ignoreTransferLimit: true, externally: false, simulate: false);
	}

	public override void SetStaticDefaults()
	{
		if (_spec != null)
			Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName", () => _spec.Label);
	}

	public override void SetDefaults()
	{
		if (_spec == null) return;
		Item.maxStack = 1;
		Item.width = Item.height = 32;
		Item.value = Item.sellPrice(gold: _spec.Suite == ArmorSuite.Quark ? 8 : 3);
		Item.rare = _spec.Rarity;
		Item.defense = _spec.FullDefense;
	}

	public override void UpdateEquip(Player player)
	{
		if (_spec == null) return;

		if (!IsCharged)
		{
			player.statDefense -= _spec.FullDefense - _spec.FloorDefense;
			return;
		}

		switch (_spec.Suite, _spec.Piece)
		{
			case (ArmorSuite.Nano, ArmorPiece.Helmet):
				player.nightVision = true;
				break;
			case (ArmorSuite.Nano, ArmorPiece.Legs):
				player.noFallDmg = true;
				break;
			case (ArmorSuite.Quark, ArmorPiece.Helmet):
				player.nightVision = true;
				player.gills = true;
				player.buffImmune[BuffID.Suffocation] = true;
				break;
			case (ArmorSuite.Quark, ArmorPiece.Chest):
				player.buffImmune[BuffID.OnFire] = true;
				player.buffImmune[BuffID.OnFire3] = true;
				player.buffImmune[BuffID.Burning] = true;
				player.buffImmune[BuffID.Frostburn] = true;
				player.buffImmune[BuffID.Frostburn2] = true;
				player.buffImmune[BuffID.Frozen] = true;
				player.buffImmune[BuffID.Chilled] = true;
				player.fireWalk = true;
				break;
			case (ArmorSuite.Quark, ArmorPiece.Legs):
				player.moveSpeed += 0.25f;
				player.noFallDmg = true;
				break;
		}

		if (_spec.HasFlight && player.accRunSpeed < FlightRunSpeed)
			player.accRunSpeed = FlightRunSpeed;
	}

	public override bool IsArmorSet(Item head, Item body, Item legs) =>
		_spec != null && _spec.Piece == ArmorPiece.Helmet &&
		body.ModItem is GTArmorItem b && b.Suite == _spec.Suite &&
		legs.ModItem is GTArmorItem l && l.Suite == _spec.Suite;

	public override void UpdateArmorSet(Player player)
	{
		if (_spec == null) return;
		bool powered = AllPiecesCharged(player);
		if (_spec.Suite == ArmorSuite.Nano)
		{
			player.setBonus = Language.GetTextValue("Mods.GregTechCEuTerraria.ArmorSet.Nano");
			if (!powered) return;
			player.blackBelt = true;
			player.moveSpeed += 0.1f;
			player.GetDamage(DamageClass.Generic) += 0.1f;
		}
		else
		{
			player.setBonus = Language.GetTextValue("Mods.GregTechCEuTerraria.ArmorSet.Quark");
			if (!powered) return;
			player.GetDamage(DamageClass.Generic) += 0.15f;
		}
	}

	private static bool AllPiecesCharged(Player player)
	{
		for (int i = 0; i < 3; i++)
			if (player.armor[i].ModItem is not GTArmorItem g || !g.IsCharged)
				return false;
		return true;
	}

	public override ModItem Clone(Item newEntity)
	{
		var c = (GTArmorItem)base.Clone(newEntity);
		c._storedEu = _storedEu;
		return c;
	}

	public override void SaveData(TagCompound tag) => tag["eu"] = _storedEu;
	public override void LoadData(TagCompound tag) =>
		_storedEu = tag.ContainsKey("eu") ? Math.Clamp(tag.GetLong("eu"), 0, _spec?.Capacity ?? 0) : 0;
	public override void NetSend(System.IO.BinaryWriter writer) => writer.Write(_storedEu);
	public override void NetReceive(System.IO.BinaryReader reader) => _storedEu = reader.ReadInt64();

	public override void PostDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame,
		Color drawColor, Color itemColor, Vector2 origin, float scale) => DrawChargeBar(sb, position, scale);

	private void DrawChargeBar(SpriteBatch sb, Vector2 center, float scale)
	{
		if (_spec == null || _spec.Capacity <= 0) return;
		float pct = Math.Clamp((float)_storedEu / _spec.Capacity, 0f, 1f);
		var px = TextureAssets.MagicPixel.Value;
		float iconHalf = 16f * scale, barW = 26f * scale, barH = 3f * scale;
		float left = center.X - barW * 0.5f;
		float top = center.Y + iconHalf - barH - 2f * scale;
		var bg = new Rectangle((int)left, (int)top, (int)barW, (int)barH);
		sb.Draw(px, bg, Color.Black * 0.7f);
		var col = pct < 0.5f
			? Color.Lerp(Color.Red, Color.Yellow, pct * 2f)
			: Color.Lerp(Color.Yellow, Color.LimeGreen, (pct - 0.5f) * 2f);
		sb.Draw(px, new Rectangle(bg.X, bg.Y, (int)(barW * pct), bg.Height), col);
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		if (_spec == null) return;
		tooltips.ApplyTierColor(_spec.Tier);
		float pct = _spec.Capacity > 0 ? (float)_storedEu / _spec.Capacity * 100 : 0;
		tooltips.Add(new TooltipLine(Mod, "GTArmorCharge",
			$"{_storedEu:N0} / {_spec.Capacity:N0} EU  ({pct:F0}%)"));
		tooltips.Add(new TooltipLine(Mod, "GTArmorState",
			IsCharged
				? "Powered - full protection"
				: $"Drained - only {_spec.FloorDefense} defense; charge in a machine charger slot")
			{ OverrideColor = IsCharged ? new Color(120, 230, 140) : new Color(235, 170, 120) });
		if (HasFlight)
			tooltips.Add(new TooltipLine(Mod, "GTArmorFlight", "Hold Jump to fly while charged")
				{ OverrideColor = new Color(140, 200, 245) });
	}
}
