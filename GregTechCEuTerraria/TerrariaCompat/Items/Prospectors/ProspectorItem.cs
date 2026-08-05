#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.TerrariaCompat.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Prospectors;

public sealed class ProspectorItem : ModItem, IElectricItem, ITextureWarmUp
{
	private readonly string? _id;
	private readonly string? _label;
	private readonly VoltageTier _tier;
	private readonly long _maxEu;
	private readonly int _rangeTiles;
	private readonly long _energyDrawBase;

	private long _storedEu;
	private bool _isActive;
	private bool _hasTarget;
	private int  _targetX;
	private int  _targetY;

	private bool _powered;

	public ProspectorItem() { }

	public ProspectorItem(string id, string label, VoltageTier tier, long maxEu, int rangeTiles)
	{
		_id = id;
		_label = label;
		_tier = tier;
		_maxEu = maxEu;
		_rangeTiles = rangeTiles;
		_energyDrawBase = VoltageTiers.Voltage(tier);
	}

	public override bool IsLoadingEnabled(Mod mod) => _id != null;
	public override string Name => _id ?? nameof(ProspectorItem);
	protected override bool CloneNewInstances => true;

	public override string Texture => _id == null
		? "Terraria/Images/Item_22"
		: $"GregTechCEuTerraria/Content/Textures/item/{_id}";

	public bool Active   { get => _isActive; set => _isActive = value; }
	public int  RangeTiles => _rangeTiles;
	public long MaxEu      => _maxEu;
	public bool HasTarget  => _hasTarget;
	public long ScanCost => _maxEu / 20;

	public long StoredEu
	{
		get => _storedEu;
		set => _storedEu = Math.Clamp(value, 0, _maxEu);
	}

	public void SetTarget(int x, int y) { _hasTarget = true; _targetX = x; _targetY = y; }
	public void ClearTarget() { _hasTarget = false; }

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

	public bool TryScan(Player player, out List<ProspectorScan.OreHit> hits, out string status)
	{
		hits = new List<ProspectorScan.OreHit>();
		long cost = ScanCost;
		if (_storedEu < cost)
		{
			status = $"Insufficient energy to scan ({_storedEu:N0} / {cost:N0} EU)";
			return false;
		}
		DrainEnergy(cost, simulate: false);
		ClearTarget();
		hits = ProspectorScan.ScanAround(player, _rangeTiles);
		status = hits.Count == 0 ? "No ores found in range" : $"{hits.Count} ore types found";
		return true;
	}

	public override void SetStaticDefaults()
	{
		base.SetStaticDefaults();
		if (_label != null)
			Terraria.Localization.Language.GetOrRegister(
				$"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName", () => _label);

		if (Main.dedServ || _id == null) return;

		var tex = ModContent.Request<Texture2D>(Texture, AssetRequestMode.ImmediateLoad).Value;
		if (tex.Width > 0 && tex.Height > tex.Width && tex.Height % tex.Width == 0)
		{
			int frames = tex.Height / tex.Width;
			Main.RegisterItemAnimation(Type, new Terraria.DataStructures.DrawAnimationVertical(
				MachineRenderer.AnimationTicksPerFrame, frames));
		}
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
		ProspectorUISystem.OpenFor(Item);
		return true;
	}

	public override bool CanRightClick() => true;

	public override void RightClick(Player player)
	{
		_isActive = !_isActive;
		if (!_isActive) { ClearTarget(); _powered = false; }
		if (player.whoAmI == Main.myPlayer && !Main.dedServ)
			Main.NewText(_isActive ? "Ore Scanner enabled" : "Ore Scanner disabled");
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	public override bool ConsumeItem(Player player) => false;

	public override void UpdateInventory(Player player) => Tick(player);
	public override void UpdateAccessory(Player player, bool hideVisual) => Tick(player);

	private void Tick(Player player)
	{
		if (Main.dedServ || player.whoAmI != Main.myPlayer) return;
		if (!_isActive) { _powered = false; return; }

		if (_hasTarget && !ProspectorScan.IsOreTile(_targetX, _targetY))
			ClearTarget();

		if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(20) == 0)
		{
			long rate = _hasTarget ? 4L * _energyDrawBase : _energyDrawBase;
			_powered = DrainEnergy(rate, simulate: true);
			if (_powered) DrainEnergy(rate, simulate: false);
		}

		if (!_powered) return;

		player.AddBuff(BuffID.Spelunker, 5);

		if (_hasTarget) EmitTracer(player);
	}

	private void EmitTracer(Player player)
	{
		Vector2 from = player.Center;
		var to = new Vector2(_targetX * 16f + 8f, _targetY * 16f + 8f);
		Vector2 dir = to - from;
		if (dir == Vector2.Zero) return;
		dir.Normalize();

		for (int k = 0; k < 2; k++)
		{
			Vector2 vel = dir * Main.rand.NextFloat(6f, 10f);
			var d = Dust.NewDustPerfect(from + dir * 12f, DustID.TreasureSparkle, vel, 100, default, 1.1f);
			d.noGravity = true;
			d.fadeIn = 0.4f;
		}
		if (Main.GameUpdateCount % 12 == 0)
		{
			var m = Dust.NewDustPerfect(to, DustID.TreasureSparkle, Vector2.Zero, 80, default, 1.5f);
			m.noGravity = true;
		}
	}

	public override ModItem Clone(Item newEntity)
	{
		var c = (ProspectorItem)base.Clone(newEntity);
		c._storedEu = _storedEu;
		c._isActive = _isActive;
		c._hasTarget = _hasTarget;
		c._targetX = _targetX;
		c._targetY = _targetY;
		return c;
	}

	public override void SaveData(TagCompound tag)
	{
		tag["eu"] = _storedEu;
		tag["active"] = _isActive;
		if (_hasTarget)
		{
			tag["tx"] = _targetX;
			tag["ty"] = _targetY;
		}
	}

	public override void LoadData(TagCompound tag)
	{
		_storedEu = tag.ContainsKey("eu") ? Math.Clamp(tag.GetLong("eu"), 0, _maxEu) : 0;
		_isActive = tag.GetBool("active");
		if (tag.ContainsKey("tx") && tag.ContainsKey("ty"))
		{
			_hasTarget = true;
			_targetX = tag.GetInt("tx");
			_targetY = tag.GetInt("ty");
		}
		else _hasTarget = false;
	}

	public override void NetSend(BinaryWriter writer)
	{
		var tag = new TagCompound();
		SaveData(tag);
		TagIO.Write(tag, writer);
	}

	public override void NetReceive(BinaryReader reader) => LoadData(TagIO.Read(reader));

	void ITextureWarmUp.WarmUpTexture() => ItemIconBaker.Install(Item.type, Texture);

	private const float ItemRenderScale = 2f;

	public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
	{
		base.PostDrawInInventory(spriteBatch, position, frame, drawColor, itemColor, origin, scale);
		DrawChargeBar(spriteBatch, position, scale);
	}

	public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
	{
		base.PostDrawInWorld(spriteBatch, lightColor, alphaColor, rotation, scale, whoAmI);
		DrawChargeBar(spriteBatch, Item.Center - Main.screenPosition, scale);
	}

	private void DrawChargeBar(SpriteBatch sb, Vector2 center, float scale)
	{
		if (_maxEu <= 0) return;
		float pct = Math.Clamp((float)_storedEu / _maxEu, 0f, 1f);
		var px = Terraria.GameContent.TextureAssets.MagicPixel.Value;

		float u = ItemRenderScale * scale;
		float iconHalf = 8f * u;
		float barW = 14f * u;
		float barH = 1f * u;
		float left = center.X - barW * 0.5f;
		float top = center.Y + iconHalf - barH - u;

		var bg = new Rectangle((int)left, (int)top, (int)barW, (int)barH);
		sb.Draw(px, bg, Color.Black * 0.7f);
		int fillW = (int)(barW * pct);
		var col = pct < 0.5f
			? Color.Lerp(Color.Red, Color.Yellow, pct * 2f)
			: Color.Lerp(Color.Yellow, Color.LimeGreen, (pct - 0.5f) * 2f);
		sb.Draw(px, new Rectangle(bg.X, bg.Y, fillW, bg.Height), col);
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		tooltips.ApplyTierColor(_tier);
		float pct = _maxEu > 0 ? (float)_storedEu / _maxEu * 100 : 0;
		tooltips.Add(new TooltipLine(Mod, "ProspectorCharge",
			$"{_storedEu:N0} / {_maxEu:N0} EU  ({pct:F0}%)"));
		tooltips.Add(new TooltipLine(Mod, "ProspectorState",
			_isActive ? "Enabled" : "Disabled"));
		tooltips.Add(new TooltipLine(Mod, "ProspectorHint1",
			"Ore scanner - works in your inventory or an accessory slot"));
		tooltips.Add(new TooltipLine(Mod, "ProspectorHint2",
			"Right-click in inventory: enable / disable"));
		tooltips.Add(new TooltipLine(Mod, "ProspectorHint3",
			"Use (right-click held): scan & pick an ore to track"));
		tooltips.Add(new TooltipLine(Mod, "ProspectorHint4",
			$"While enabled: grants Spelunker (highlights all ores), drains {_energyDrawBase:N0} EU/s"));
		tooltips.Add(new TooltipLine(Mod, "ProspectorHint5",
			$"While tracking an ore: 4x drain ({4L * _energyDrawBase:N0} EU/s), streams particles to it"));
		tooltips.Add(new TooltipLine(Mod, "ProspectorHint6",
			$"Each scan costs {ScanCost:N0} EU (5%)   Scan radius: {_rangeTiles} tiles"));
	}
}
