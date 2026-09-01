#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Machines;

public class SuperTankItem : TieredMachineItem, IFluidHandlerItem
{
	public SuperTankItem() { }
	public SuperTankItem(VoltageTier tier, MachineDefinition def) : base(tier, def) { }

	private long MaxAmount => Tiles.Machines.SuperTankTileEntity.MaxAmountForTier(_tier);

	private MachinePortableData? Blob =>
		Item.TryGetGlobalItem<MachinePortableData>(out var g) ? g : null;

	private long StoredAmount =>
		Blob?.Data is { } d && d.ContainsKey("fluidAmount") ? d.GetLong("fluidAmount") : 0L;

	private FluidStack StoredFluid()
	{
		if (Blob?.Data is not { } d || !d.ContainsKey("fluidId")) return FluidStack.Empty;
		if (!FluidRegistry.TryGet(d.GetString("fluidId"), out var type)) return FluidStack.Empty;
		long amt = d.GetLong("fluidAmount");
		if (amt <= 0) return FluidStack.Empty;
		return new FluidStack(type, (int)Math.Min(amt, int.MaxValue),
			d.ContainsKey("fluidNbt") ? d.GetCompound("fluidNbt") : null);
	}

	public Item Container => Item;
	public int TankCount => 1;
	public int GetCapacity(int tank) => (int)Math.Min(MaxAmount, int.MaxValue);
	public FluidStack GetTank(int tank) => StoredFluid();
	public bool IsFluidValid(int tank, FluidStack fluid) => true;

	public int Fill(FluidStack resource, bool simulate)
	{
		if (resource.IsEmpty) return 0;
		var existing = StoredFluid();
		if (!existing.IsEmpty && !existing.SameTypeAs(resource)) return 0;
		long room = MaxAmount - StoredAmount;
		if (room <= 0) return 0;
		int accepted = (int)Math.Min(room, resource.Amount);
		if (accepted <= 0) return 0;
		if (!simulate)
			SetStored(existing.IsEmpty ? resource.Type! : existing.Type!,
				StoredAmount + accepted,
				existing.IsEmpty ? resource.Nbt : existing.Nbt);
		return accepted;
	}

	public FluidStack Drain(int maxAmount, bool simulate)
	{
		var existing = StoredFluid();
		if (existing.IsEmpty || maxAmount <= 0) return FluidStack.Empty;
		long stored = StoredAmount;
		int drained = (int)Math.Min(stored, maxAmount);
		var result = new FluidStack(existing.Type!, drained, existing.Nbt);
		if (!simulate)
			SetStored(existing.Type!, stored - drained, existing.Nbt);
		return result;
	}

	public FluidStack Drain(FluidStack fluidStack, bool simulate)
	{
		var existing = StoredFluid();
		if (existing.IsEmpty || !existing.SameTypeAs(fluidStack)) return FluidStack.Empty;
		return Drain(fluidStack.Amount, simulate);
	}

	private void SetStored(FluidType type, long amount, TagCompound? nbt)
	{
		var g = Blob;
		if (g is null) return;
		if (amount <= 0)
		{
			if (g.Data is { } empty)
			{
				empty.Remove("fluidId");
				empty.Remove("fluidAmount");
				empty.Remove("fluidNbt");
				if (empty.Count == 0) g.Data = null;
			}
			return;
		}
		var d = g.Data ??= new TagCompound();
		d["fluidId"]     = type.Id;
		d["fluidAmount"] = amount;
		if (nbt != null) d["fluidNbt"] = nbt; else d.Remove("fluidNbt");
	}

	protected override void AppendTierTooltip(List<TooltipLine> tooltips)
	{
		long cap = MaxAmount;
		string capStr = cap > int.MaxValue ? "~2.1G (cap)" : $"{cap:N0}";
		tooltips.Add(new TooltipLine(Mod, "TierLine",
			$"{VoltageTiers.ShortName(_tier)} - capacity {capStr} mB"));
		var stored = StoredFluid();
		if (!stored.IsEmpty)
			tooltips.Add(new TooltipLine(Mod, "TankContents",
				$"Contains {stored.Amount:N0} mB of {stored.Type!.DisplayName}"));
	}

	private const float FluidInsetFrac = Tiles.Machines.SuperTankTile.FluidInsetArtPx / 16f;

	public override void PostDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame,
		Color drawColor, Color itemColor, Vector2 origin, float scale)
	{
		base.PostDrawInInventory(sb, position, frame, drawColor, itemColor, origin, scale);
		var stored = StoredFluid();
		if (stored.IsEmpty) return;
		DrawFluidOverlay(sb, position - origin * scale, frame.Size() * scale, stored.Type!, drawColor);
	}

	public override void PostDrawInWorld(SpriteBatch sb, Color lightColor, Color alphaColor,
		float rotation, float scale, int whoAmI)
	{
		base.PostDrawInWorld(sb, lightColor, alphaColor, rotation, scale, whoAmI);
		var stored = StoredFluid();
		if (stored.IsEmpty) return;
		var tex = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
		Vector2 size = new Vector2(tex.Width, tex.Height) * scale;
		Vector2 center = Item.Center - Main.screenPosition;
		DrawFluidOverlay(sb, center - size * 0.5f, size, stored.Type!, lightColor);
	}

	private static void DrawFluidOverlay(SpriteBatch sb, Vector2 topLeft, Vector2 size,
		FluidType fluid, Color light)
	{
		FluidIconRenderer.Draw(sb, fluid, topLeft + size * FluidInsetFrac,
			size * (1f - 2f * FluidInsetFrac), light: light);
	}
}
