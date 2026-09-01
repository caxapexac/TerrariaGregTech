#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Fluids;

public sealed class FluidBucketItem : ModItem, ITextureWarmUp, IFluidHandlerItem
{
	private const int BucketCapacity = 1000;

	private static readonly HashSet<string> GimmickFluids = new()
	{
		"steam", "air", "sulfuric_acid", "neutronium"
	};

	internal static int GimmickHeadSlot = -1;

	[CloneByReference] private readonly FluidType? _fluid;

	public FluidBucketItem() { }
	public FluidBucketItem(FluidType fluid) { _fluid = fluid; }

	public FluidType? Fluid => _fluid;

	private bool IsGimmick => _fluid != null && GimmickFluids.Contains(_fluid.Id);

	public Item Container => Item;
	public int TankCount => 1;
	public FluidStack GetTank(int tank) =>
		_fluid is null ? default : new FluidStack(_fluid, BucketCapacity);
	public int GetCapacity(int tank) => BucketCapacity;
	public int Fill(FluidStack fluid, bool simulate) => 0;
	public FluidStack Drain(int maxAmount, bool simulate) => default;
	public FluidStack Drain(FluidStack fluidStack, bool simulate) => default;

	public override string Name => _fluid != null ? $"{_fluid.Id}_bucket" : nameof(FluidBucketItem);
	public override string Texture => "GregTechCEuTerraria/Content/TerrariaCompat/TooManyItemsItem";
	public override bool IsLoadingEnabled(Mod mod) => _fluid != null;
	protected override bool CloneNewInstances => true;

	public override void SetStaticDefaults()
	{
		if (_fluid is null) return;
		Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName",
			() => $"{_fluid.DisplayName} Bucket");
	}

	public override void SetDefaults()
	{
		Item.CloneDefaults(ItemID.EmptyBucket);
		Item.maxStack = Item.CommonMaxStack;
		Item.value = 0;
		Item.rare = ItemRarityID.Blue;
		Item.defense = 0;
		Item.headSlot = IsGimmick ? GimmickHeadSlot : -1;
	}

	public override void UpdateEquip(Player player)
	{
		if (_fluid is null) return;
		switch (_fluid.Id)
		{
			case "steam":
				player.slowFall = true;
				break;
			case "air":
				player.accFlipper = true;
				player.accDivingHelm = true;
				break;
			case "sulfuric_acid":
				player.AddBuff(BuffID.OnFire3, 6);
				break;
			case "neutronium":
				player.AddBuff(BuffID.Regeneration, 6);
				break;
		}
	}

	public void WarmUpTexture()
	{
		if (_fluid != null)
			FluidBucketRenderer.EnsureItemTexture(Item.type, _fluid);
	}

	public override bool PreDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame,
		Color drawColor, Color itemColor, Vector2 origin, float scale)
	{
		WarmUpTexture();
		return true;
	}

	public override bool PreDrawInWorld(SpriteBatch sb, Color lightColor, Color alphaColor,
		ref float rotation, ref float scale, int whoAmI)
	{
		WarmUpTexture();
		return true;
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		if (_fluid is null) return;
		tooltips.Add(new TooltipLine(Mod, "FluidContents", $"Contains 1000 mB {_fluid.DisplayName}"));
		tooltips.Add(new TooltipLine(Mod, "FluidTemperature", UI.FluidTooltips.Temperature(_fluid))
		{
			OverrideColor = UI.FluidTooltips.TemperatureColor,
		});
		foreach (var attr in _fluid.Attributes)
			attr.AppendFluidTooltips(s => tooltips.Add(new TooltipLine(Mod, "FluidAttribute", s)));
	}
}
