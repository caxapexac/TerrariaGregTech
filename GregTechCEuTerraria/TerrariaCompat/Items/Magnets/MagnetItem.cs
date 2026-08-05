#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Cover.Filter;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.TerrariaCompat.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Magnets;

public sealed class MagnetItem : ModItem, IElectricItem, ITextureWarmUp
{
	private readonly string? _id;
	private readonly string? _label;
	private readonly VoltageTier _tier;
	private readonly long _maxEu;
	private readonly int _rangeTiles;
	private readonly int _rangePixels;
	private readonly long _energyDraw;

	private long _storedEu;
	private bool _isActive;
	private int _filterOrdinal; // 0 = SIMPLE, 1 = TAG
	private SimpleItemFilter _simpleFilter = new();
	private TagItemFilter _tagFilter = new();

	public MagnetItem() { }

	public MagnetItem(string id, string label, VoltageTier tier, long maxEu, int rangeTiles)
	{
		_id = id;
		_label = label;
		_tier = tier;
		_maxEu = maxEu;
		_rangeTiles = rangeTiles;
		_rangePixels = rangeTiles * 16;
		_energyDraw = VoltageTiers.Voltage(rangeTiles > 8 ? VoltageTier.HV : VoltageTier.LV);
	}

	public override bool IsLoadingEnabled(Mod mod) => _id != null;
	public override string Name => _id ?? nameof(MagnetItem);
	protected override bool CloneNewInstances => true;

	public override string Texture => _id == null
		? "Terraria/Images/Item_22"
		: $"GregTechCEuTerraria/Content/Textures/item/{_id}";

	public bool MagnetActive { get => _isActive; set => _isActive = value; }
	public int  FilterOrdinal { get => _filterOrdinal; set => _filterOrdinal = Math.Clamp(value, 0, 1); }
	public SimpleItemFilter SimpleFilter => _simpleFilter;
	public TagItemFilter    TagFilter    => _tagFilter;
	public int RangeTiles => _rangeTiles;

	public IItemFilter ActiveFilter() => _filterOrdinal == 1 ? _tagFilter : _simpleFilter;

	public long StoredEu
	{
		get => _storedEu;
		set => _storedEu = Math.Clamp(value, 0, _maxEu);
	}

	public bool CanProvideChargeExternally() => false;
	public bool Chargeable() => true;
	public long GetTransferLimit() => VoltageTiers.Voltage(_tier);
	public long GetMaxCharge() => _maxEu;
	public long GetCharge() => System.Math.Min(_storedEu, _maxEu);
	public int  GetTier() => (int)_tier;

	public long Charge(long amount, int chargerTier, bool ignoreTransferLimit, bool simulate)
	{
		if (Item.stack != 1) return 0L;
		int tier = (int)_tier;
		if (chargerTier >= tier && amount > 0L)
		{
			long canReceive = GetMaxCharge() - GetCharge();
			if (!ignoreTransferLimit)
				amount = Math.Min(amount, GetTransferLimit());
			long charged = Math.Min(amount, canReceive);
			if (!simulate)
				_storedEu += charged;
			return charged;
		}
		return 0;
	}

	public long Discharge(long amount, int dischargerTier, bool ignoreTransferLimit, bool externally, bool simulate)
	{
		if (Item.stack != 1) return 0L;
		int tier = (int)_tier;
		if ((!externally || amount == long.MaxValue) && (dischargerTier >= tier) && amount > 0L)
		{
			if (!ignoreTransferLimit)
				amount = Math.Min(amount, GetTransferLimit());
			long charge = GetCharge();
			long discharged = Math.Min(amount, charge);
			if (!simulate)
				_storedEu = charge - discharged;
			return discharged;
		}
		return 0;
	}

	private bool DrainEnergy(long amount, bool simulate) =>
		Discharge(amount, int.MaxValue, ignoreTransferLimit: true, externally: false, simulate) >= amount;

	public override void SetStaticDefaults()
	{
		base.SetStaticDefaults();
		if (_label != null)
			Terraria.Localization.Language.GetOrRegister(
				$"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName", () => _label);
	}

	public override void SetDefaults()
	{
		Item.maxStack = 1;
		Item.width = 32;
		Item.height = 32;
		Item.rare = ItemRarityID.White;
		Item.useStyle = ItemUseStyleID.HoldUp;
		Item.useTime = 20;
		Item.useAnimation = 20;
		Item.autoReuse = false;
		Item.noMelee = true;
		Item.UseSound = null;
		Item.accessory = true;
	}

	public override bool AltFunctionUse(Player player) => true;
	public override bool CanUseItem(Player player) => player.altFunctionUse == 2;

	public override bool? UseItem(Player player)
	{
		if (player.altFunctionUse != 2 || player.whoAmI != Main.myPlayer || Main.dedServ)
			return true;

		MagnetUISystem.OpenFor(Item);
		return true;
	}

	public override bool CanRightClick() => true;

	public override void RightClick(Player player)
	{
		_isActive = !_isActive;
		if (player.whoAmI == Main.myPlayer && !Main.dedServ)
			Main.NewText(_isActive ? "Item Magnet enabled" : "Item Magnet disabled");
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	public override bool ConsumeItem(Player player) => false;

	public override void UpdateAccessory(Player player, bool hideVisual)
	{
		if (Main.dedServ || player.whoAmI != Main.myPlayer) return;
		if (!_isActive) return;
		if (Main.GameUpdateCount % global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(10) != 0) return;

		if (!DrainEnergy(_energyDraw, simulate: true)) return;

		var filter = ActiveFilter();
		Vector2 center = player.Center;
		bool didMove = false;

		for (int i = 0; i < Main.maxItems; i++)
		{
			Item it = Main.item[i];
			if (it is null || !it.active || it.IsAir) continue;
			if (it.noGrabDelay != 0) continue;
			int reserved = it.playerIndexTheItemIsReservedFor;
			if (reserved >= 0 && reserved != player.whoAmI) continue;
			if (Math.Abs(it.Center.X - center.X) > _rangePixels) continue;
			if (Math.Abs(it.Center.Y - center.Y) > _rangePixels) continue;
			if (!filter.Test(it)) continue;

			it.velocity = Vector2.Zero;
			it.position = new Vector2(
				center.X - it.width * 0.5f + Main.rand.NextFloat(-3.2f, 3.2f),
				center.Y - it.height * 0.5f + Main.rand.NextFloat(-3.2f, 3.2f));
			didMove = true;
		}

		if (didMove)
		{
			SoundEngine.PlaySound(SoundID.Grab, player.Center);
			DrainEnergy(_energyDraw, simulate: false);
		}
	}

	public override ModItem Clone(Item newEntity)
	{
		var c = (MagnetItem)base.Clone(newEntity);
		c._storedEu = _storedEu;
		c._isActive = _isActive;
		c._filterOrdinal = _filterOrdinal;
		var s = _simpleFilter.SaveFilter();
		c._simpleFilter = s != null ? SimpleItemFilter.LoadFilter(s) : new SimpleItemFilter();
		var t = _tagFilter.SaveFilter();
		c._tagFilter = t != null ? TagItemFilter.LoadFilter(t) : new TagItemFilter();
		return c;
	}

	public override void SaveData(TagCompound tag)
	{
		tag["eu"] = _storedEu;
		tag["active"] = _isActive;
		tag["filterOrdinal"] = _filterOrdinal;
		var s = _simpleFilter.SaveFilter();
		if (s != null) tag["simple"] = s;
		var t = _tagFilter.SaveFilter();
		if (t != null) tag["tag"] = t;
	}

	public override void LoadData(TagCompound tag)
	{
		_storedEu = tag.ContainsKey("eu") ? Math.Clamp(tag.GetLong("eu"), 0, _maxEu) : 0;
		_isActive = tag.GetBool("active");
		_filterOrdinal = Math.Clamp(tag.GetInt("filterOrdinal"), 0, 1);
		_simpleFilter = tag.ContainsKey("simple")
			? SimpleItemFilter.LoadFilter(tag.GetCompound("simple")) : new SimpleItemFilter();
		_tagFilter = tag.ContainsKey("tag")
			? TagItemFilter.LoadFilter(tag.GetCompound("tag")) : new TagItemFilter();
	}

	public override void NetSend(BinaryWriter writer)
	{
		var tag = new TagCompound();
		SaveData(tag);
		TagIO.Write(tag, writer);
	}

	public override void NetReceive(BinaryReader reader) => LoadData(TagIO.Read(reader));

	void ITextureWarmUp.WarmUpTexture() => ItemIconBaker.Install(Item.type, Texture);

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		tooltips.ApplyTierColor(_tier);
		float pct = _maxEu > 0 ? (float)_storedEu / _maxEu * 100 : 0;
		tooltips.Add(new TooltipLine(Mod, "MagnetCharge",
			$"{_storedEu:N0} / {_maxEu:N0} EU  ({pct:F0}%)"));
		tooltips.Add(new TooltipLine(Mod, "MagnetState",
			_isActive ? "Enabled" : "Disabled"));
		if (_filterOrdinal == 0 && UI.FilterWarning.IsEmptyWhitelist(_simpleFilter))
			tooltips.Add(new TooltipLine(Mod, "MagnetFilterWarn",
				"! Empty whitelist - pulls nothing")
			{ OverrideColor = UI.FilterWarning.Color });
		tooltips.Add(new TooltipLine(Mod, "MagnetHint",
			$"Equip in an accessory slot to activate   Use (right-click held): filter settings   Right-click in inventory: pause/resume   Range {_rangeTiles}"));
	}
}
