#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using GregTechCEuTerraria.Api.Tool;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Items.Cables;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Net.Actions;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Tools;

public sealed class ToolItem : ModItem, IElectricItem
{
	private readonly string? _id;
	private readonly string? _label;
	private readonly GTToolType? _type;
	private readonly ToolLayer[] _layers = System.Array.Empty<ToolLayer>();
	private readonly string _texture = "Terraria/Images/Item_22";

	private readonly int _pick, _axe, _hammer, _damage, _useTime;
	private readonly int _harvestLevel;
	private readonly int _tier;

	private readonly long _maxCharge;
	private readonly long _euPerUse;
	private long _storedEu;

	public ToolItem() { }

	public ToolItem(string id, string label, GTToolType type, Material material, ToolLayer[] layers, string texture)
	{
		_id = id;
		_label = label;
		_type = type;
		_layers = layers;
		_texture = texture;

		var def = type.Definition;
		var tp = material.Tool!;

		_harvestLevel = tp.HarvestLevel + def.BaseQuality;

		bool isHammer = ReferenceEquals(type, GTToolType.HARD_HAMMER);
		bool isPick = !isHammer && (type.ToolClassNames.Contains("pickaxe") || type.Name.Contains("drill"));
		bool isAxe = type.ToolClassNames.Contains("axe") || type.Name.Contains("chainsaw");

		int tier = ToolTier.For(material);
		_tier = tier;
		var anchor = ToolTier.AnchorFor(tier);
		_pick    = isPick   ? anchor.Pick   : 0;
		_axe     = isAxe    ? anchor.Axe    : 0;
		_hammer  = isHammer ? anchor.Hammer : 0;
		_damage  = Math.Max(1, anchor.Damage);
		_useTime = Math.Max(2, ToolTier.SpeedUseTime(material));

		string n = type.Name;
		if (n == "shovel" || n == "spade")
		{
			_pick = Math.Max(35, anchor.Pick / 4);
			_axe = 0; _hammer = 0;
			_useTime = Math.Max(2, (int)Math.Round(_useTime / (n == "shovel" ? 3.0 : 2.0)));
		}
		else if (n == "saw" || n == "buzzsaw")
		{
			_axe = anchor.Axe;
		}
		else if (n == "mortar")
		{
			_pick = Math.Max(1, (int)Math.Round(anchor.Pick * 0.75));
		}
		else if (n == "screwdriver" || n == "file")
		{
			_damage = Math.Max(1, (int)Math.Round(anchor.Damage * (n == "file" ? 0.75 : 1.0)));
			_useTime = n == "file" ? 22 : 14;
		}
		else if (n == "plunger")
		{
			_useTime = Math.Clamp(24 - 5 * tier, 2, 60);
		}
		else if (n == "hoe")
		{
			_pick = 50;
			_axe = 0; _hammer = 0; _damage = 0;
		}

		if (type.IsElectric)
		{
			_maxCharge = 100_000L * (long)Math.Pow(4, type.ElectricTier - 1);
			_euPerUse = Math.Max(1, _maxCharge / 4000);
		}
	}

	public override bool IsLoadingEnabled(Mod mod) => _id != null;
	public override string Name => _id ?? nameof(ToolItem);
	public override string Texture => _texture;
	protected override bool CloneNewInstances => true;

	public GTToolType ToolType => _type!;
	public int AoeColumn => _type?.Definition.Aoe.Column ?? 0;
	public int AoeRow => _type?.Definition.Aoe.Row ?? 0;
	public bool IsElectric => _type?.IsElectric ?? false;

	public long StoredEu
	{
		get => _storedEu;
		set => _storedEu = Math.Clamp(value, 0, _maxCharge);
	}

	public bool CanProvideChargeExternally() => false;
	public bool Chargeable() => IsElectric;
	public long GetTransferLimit() => IsElectric ? VoltageTiers.Voltage((VoltageTier)_type!.ElectricTier) : 0;
	public long GetMaxCharge() => _maxCharge;
	public long GetCharge() => Math.Min(_storedEu, _maxCharge);
	public int GetTier() => _type?.ElectricTier ?? 0;

	public long Charge(long amount, int chargerTier, bool ignoreTransferLimit, bool simulate)
	{
		if (Item.stack != 1 || !IsElectric) return 0L;
		int tier = _type!.ElectricTier;
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
		if (Item.stack != 1 || !IsElectric || externally) return 0L;
		int tier = _type!.ElectricTier;
		if (dischargerTier >= tier && amount > 0L)
		{
			if (!ignoreTransferLimit) amount = Math.Min(amount, GetTransferLimit());
			long discharged = Math.Min(amount, _storedEu);
			if (!simulate) _storedEu -= discharged;
			return discharged;
		}
		return 0L;
	}

	public override void SetStaticDefaults()
	{
		if (_label != null)
			Terraria.Localization.Language.GetOrRegister(
				$"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName", () => _label);
	}

	public bool DrillLike => _type != null && (_type.Name.Contains("drill") || _type.Name.Contains("chainsaw"));

	public bool IsWrench => _type != null && _type.ToolClassNames.Contains("wrench");
	public bool IsWireCutter => _type != null && _type.ToolClassNames.Contains("wire_cutter");
	public bool IsMallet => _type != null && _type.Name == "mallet";
	public bool IsCrowbar => _type != null && _type.Name == "crowbar";
	public bool IsKnife => _type != null && _type.Name == "knife";
	public bool IsButcheryKnife => _type != null && _type.Name == "butchery_knife";

	public int Tier => _tier;
	public bool IsSoftDigger => _type != null && (_type.Name == "shovel" || _type.Name == "spade");
	public bool IsShovel => _type != null && _type.Name == "shovel";
	public bool IsSawLike => _type != null && (_type.Name == "saw" || _type.Name == "buzzsaw");
	public bool IsMortar => _type != null && _type.Name == "mortar";
	public bool IsHoe => _type != null && _type.Name == "hoe";
	public bool IsScythe => _type != null && _type.Name == "scythe";
	public bool IsSword => _type != null && _type.Name == "sword";
	public bool IsPoker => _type != null && (_type.Name == "screwdriver" || _type.Name == "file");
	public bool IsPlunger => _type != null && _type.Name == "plunger";

	public override void SetDefaults()
	{
		Item.maxStack = 1;
		Item.width = Item.height = 32;
		Item.DamageType = DamageClass.Melee;
		Item.damage = _damage;
		Item.knockBack = IsCrowbar ? 9.5f : 3f;
		Item.useTime = Item.useAnimation = _useTime;
		bool isUtilityOneShot = IsWrench || IsWireCutter || IsMallet || (_type != null && _type.Name == "soft_hammer");
		Item.autoReuse = !isUtilityOneShot;
		Item.pick = _pick;
		Item.axe = _axe;
		Item.hammer = _hammer;
		Item.rare = Math.Clamp(_harvestLevel, ItemRarityID.White, ItemRarityID.Purple);
		Item.value = Item.sellPrice(silver: 5 + _harvestLevel * 3);

		if (DrillLike)
		{
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.DamageType = DamageClass.MeleeNoSpeed;
			Item.shoot = ModContent.ProjectileType<DrillHeldProjectile>();
			Item.shootSpeed = 32f;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.channel = true;
		}
		else if (IsButcheryKnife)
		{
			Item.useStyle = ItemUseStyleID.Swing;
			Item.UseSound = SoundID.Item1;
			Item.shoot = ModContent.ProjectileType<ButcheryKnifeProjectile>();
			Item.shootSpeed = 10f;
			Item.noUseGraphic = true;
			Item.noMelee = true;
			Item.autoReuse = true;
			Item.consumable = false;
		}
		else if (IsKnife)
		{
			Item.useStyle = ItemUseStyleID.Swing;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			Item.useTurn = true;
		}
		else if (IsPoker)
		{
			Item.useStyle = ItemUseStyleID.Shoot;
			Item.DamageType = DamageClass.Melee;
			Item.shoot = ModContent.ProjectileType<ToolPokeProjectile>();
			Item.shootSpeed = 3.6f;
			Item.knockBack = 4.5f;
			Item.noMelee = true;
			Item.noUseGraphic = true;
			Item.autoReuse = true;
			Item.UseSound = SoundID.Item1;
		}
		else if (IsScythe)
		{
			Item.useStyle = ItemUseStyleID.Swing;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			Item.useTurn = false;
			Item.scale = 1.5f;
		}
		else if (IsHoe)
		{
			Item.useStyle = ItemUseStyleID.Swing;
			Item.useTurn = true;
			Item.UseSound = SoundID.Item1;
			Item.noMelee = true;
			Item.autoReuse = true;
		}
		else
		{
			Item.useStyle = ItemUseStyleID.Swing;
			Item.useTurn = true;
			Item.UseSound = SoundID.Item1;
			if (IsSword) Item.scale = MathHelper.Clamp(0.8f + 0.1f * _tier, 0.8f, 2.0f);
		}
	}

	public override bool CanUseItem(Player player) =>
		!IsElectric || _storedEu >= _euPerUse;

	public override bool? CanHitNPC(Player player, NPC target)
	{
		if (target.townNPC) return IsKnife;
		return null;
	}

	public override bool Shoot(Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source,
		Vector2 position, Vector2 velocity, int type, int damage, float knockback)
	{
		if (!IsButcheryKnife) return true;
		Projectile.NewProjectile(source, position, velocity, type, damage, knockback,
			player.whoAmI, ai0: Item.type);
		return false;
	}

	public override bool? UseItem(Player player)
	{
		if (IsElectric && _storedEu > 0)
			_storedEu = Math.Max(0, _storedEu - _euPerUse);

		if (player.whoAmI == Main.myPlayer && (IsWrench || IsMallet || IsWireCutter))
			DoMachineInteraction(player);

		if (player.whoAmI == Main.myPlayer && IsPlunger)
			AbsorbLiquid(player);

		return true;
	}

	// Port of vanilla absorbing-sponge (Player.cs:45724)
	private void AbsorbLiquid(Player player)
	{
		int tx = Player.tileTargetX, ty = Player.tileTargetY;
		if (!WorldGen.InWorld(tx, ty, 2)) return;
		var center = Main.tile[tx, ty];
		if (center.LiquidAmount <= 0) return;

		int liquidType = center.LiquidType;
		int collected = center.LiquidAmount;
		center.LiquidAmount = 0;
		center.LiquidType = LiquidID.Water;
		WorldGen.SquareTileFrame(tx, ty, resetFrame: false);
		if (Main.netMode == NetmodeID.MultiplayerClient) NetMessage.sendWater(tx, ty);
		else Liquid.AddWater(tx, ty);
		SoundEngine.PlaySound(SoundID.Splash, new Vector2(tx * 16f, ty * 16f));

		if (collected >= 255) return;
		for (int k = tx - 1; k <= tx + 1; k++)
		for (int l = ty - 1; l <= ty + 1; l++)
		{
			if (k == tx && l == ty) continue;
			if (!WorldGen.InWorld(k, l, 2)) continue;
			var t = Main.tile[k, l];
			if (t.LiquidAmount <= 0 || t.LiquidType != liquidType) continue;
			int take = t.LiquidAmount;
			if (take + collected > 255) take = 255 - collected;
			collected += take;
			t.LiquidAmount -= (byte)take;
			if (t.LiquidAmount == 0) t.LiquidType = LiquidID.Water;
			WorldGen.SquareTileFrame(k, l, resetFrame: false);
			if (Main.netMode == NetmodeID.MultiplayerClient) NetMessage.sendWater(k, l);
			else Liquid.AddWater(k, l);
		}
	}

	private void DoMachineInteraction(Player player)
	{
		int x = (int)(Main.MouseWorld.X / 16f);
		int y = (int)(Main.MouseWorld.Y / 16f);
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1) return;

		if (IsWireCutter)
		{
			if (WireItem.CutCableAt(player, x, y)) return;
			Pipelike.Laser.LaserPipeLayerHandle.Instance.CutAt(x, y, player);
			return;
		}

		if (Vector2.Distance(player.Center, new Vector2(x * 16f + 8f, y * 16f + 8f)) > 16f * 12f)
			return;

		if (IsWrench)
		{
			if (Pipelike.ItemPipe.ItemPipeLayerHandle.Instance.CutAt(x, y, player)) return;
			if (Pipelike.Fluid.FluidPipeLayerHandle.Instance.CutAt(x, y, player)) return;
			if (Pipelike.LongDistance.LongDistancePipeLayerHandle.Item.CutAt(x, y, player)) return;
		}

		if (!MachineCellResolver.TryFindMachineAt(x, y, out var machine)) return;

		if (IsWrench)
		{
			var pos = machine.Position;
			WorldGen.KillTile(pos.X, pos.Y);
			if (Main.netMode == NetmodeID.MultiplayerClient)
				NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, pos.X, pos.Y);
		}
		else // IsMallet
		{
			bool newState = !machine.WorkingEnabled;
			MachineActions.Send(new PowerToggleAction(newState), machine);

			var p = machine.Position;
			var worldPos = new Vector2(p.X * 16f, p.Y * 16f);
			SoundEngine.PlaySound(SoundID.Mech, worldPos);
			CombatText.NewText(new Rectangle(p.X * 16, p.Y * 16, 32, 32),
				newState ? Color.LightGreen : Color.OrangeRed,
				newState ? "Enabled" : "Disabled");
		}
	}

	public override ModItem Clone(Item newEntity)
	{
		var clone = (ToolItem)base.Clone(newEntity);
		clone._storedEu = _storedEu;
		return clone;
	}

	public override void SaveData(TagCompound tag)
	{
		if (IsElectric) tag["eu"] = _storedEu;
	}

	public override void LoadData(TagCompound tag)
	{
		_storedEu = IsElectric && tag.ContainsKey("eu") ? tag.GetLong("eu") : 0;
	}

	public override void NetSend(System.IO.BinaryWriter writer)
	{
		if (IsElectric) writer.Write(_storedEu);
	}

	public override void NetReceive(System.IO.BinaryReader reader)
	{
		if (IsElectric) _storedEu = reader.ReadInt64();
	}

	public override bool PreDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame,
		Color drawColor, Color itemColor, Vector2 origin, float scale)
	{
		EnsureTextureBaked();
		return true;
	}

	public override bool PreDrawInWorld(SpriteBatch sb, Color lightColor, Color alphaColor,
		ref float rotation, ref float scale, int whoAmI)
	{
		EnsureTextureBaked();
		return true;
	}

	public override void HoldItem(Player player)
	{
		EnsureTextureBaked();
		if (IsScythe) player.cordage = true; // 50% Vine drop
	}

	public void EnsureTextureBaked()
	{
		if (_layers.Length == 0) return;
		var iconLayers = new IconLayer[_layers.Length];
		for (int i = 0; i < _layers.Length; i++)
			iconLayers[i] = new IconLayer(_layers[i].TexturePath, _layers[i].Tint);
		ItemIconBaker.Install(Item.type, mirrorDiagonal: !IsMortar, iconLayers);
	}

	public override void PostDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame,
		Color drawColor, Color itemColor, Vector2 origin, float scale)
	{
		if (IsElectric) DrawChargeBar(sb, position, scale);
	}

	public override void PostDrawInWorld(SpriteBatch sb, Color lightColor, Color alphaColor,
		float rotation, float scale, int whoAmI)
	{
		if (IsElectric) DrawChargeBar(sb, Item.Center - Main.screenPosition, scale);
	}

	private void DrawChargeBar(SpriteBatch sb, Vector2 center, float scale)
	{
		if (_maxCharge <= 0) return;
		float pct = Math.Clamp((float)_storedEu / _maxCharge, 0f, 1f);
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

	private string? ToolDescription()
	{
		if (_type == null) return null;
		string n = _type.Name;
		int aoeLen = 2 * Math.Max(AoeColumn, AoeRow) + 1;

		switch (n)
		{
			case "shovel":
			{
				int wormPct = (int)System.Math.Round((0.10f + System.Math.Clamp(_tier / 9f, 0f, 1f) * 0.90f) * 100f);
				return $"Quickly digs soft ground (~3x pickaxe speed). Cannot break stone. {wormPct}% chance get a Worm when digging grass.";
			}
			case "spade":          return $"Digs a {aoeLen}-tile line of soft ground (~2x pickaxe speed). Cannot break stone.";
			case "pickaxe":        return "Mines blocks and ore.";
			case "mining_hammer":  return $"Mines a {aoeLen}-tile line of blocks.";
			case "axe":            return "Chops trees.";
			case "saw":            return "Chops trees - logs come out as Rubber Wood.";
			case "buzzsaw":        return "Powered saw - chops trees into Rubber Wood.";
			case "hammer":         return "Smashes walls and shapes blocks.";
			case "sword":          return "Melee weapon with slightly extended reach.";
			case "knife":          return "Fast auto-swinging melee weapon.";
			case "crowbar":        return "Heavy melee weapon with strong knockback.";
			case "scythe":         return "Wide auto-swinging weapon. Collects Vine while cutting plants.";
			case "butchery_knife": return "Thrown like a knife and never consumed.";
			case "screwdriver":    return "Pokes like a spear. Right-click a transformer/machine to reconfigure it.";
			case "file":           return "Pokes like a spear (0.75x a screwdriver's power).";
			case "mortar":         return "Weak pickaxe - grinds mined ground: stone -> silt -> dirt -> clay -> sand.";
			case "wrench":         return "Dismantles machines and cuts item/fluid pipes.";
			case "mallet":         return "Pauses or resumes a machine.";
			case "wire_cutter":    return "Cuts cables and laser pipes.";
			case "plunger":        return $"Absorbs liquid in a 3x3 area. Cooldown: {_useTime} ticks.";
			case "hoe":
			{
				int seedCap = Math.Max(1, (int)Math.Round(5.0 * _tier / 2.0));
				return _tier >= 2
					? $"Harvests herbs for up to {seedCap} bonus seeds and an extra herb."
					: $"Harvests herbs for up to {seedCap} bonus seeds.";
			}
		}

		if (n.Contains("drill"))      return $"Powered drill - mines a {aoeLen}-tile line.";
		if (n.Contains("chainsaw"))   return $"Powered chainsaw - chops a {aoeLen}-tile line of trees.";
		if (n.Contains("wirecutter")) return "Powered wire cutter - cuts cables and laser pipes.";
		if (n.EndsWith("wrench"))     return "Powered wrench - dismantles machines and cuts pipes.";
		if (n.Contains("screwdriver"))return "Powered screwdriver - right-click a transformer/machine to reconfigure it.";
		return null;
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		if (_type == null) return;
		string? desc = ToolDescription();
		if (desc != null)
			tooltips.Add(new TooltipLine(Mod, "GTToolDesc", desc)
				{ OverrideColor = new Color(170, 210, 235) });
		if (IsKnife)
			tooltips.Add(new TooltipLine(Mod, "GTFriendlyFire", "Can harm friendly NPCs.")
				{ OverrideColor = new Color(220, 60, 60) });
		if (IsElectric)
		{
			float pct = _maxCharge > 0 ? (float)_storedEu / _maxCharge * 100 : 0;
			tooltips.Add(new TooltipLine(Mod, "GTCharge", $"{_storedEu:N0} / {_maxCharge:N0} EU  ({pct:F0}%)"));
		}
	}
}
