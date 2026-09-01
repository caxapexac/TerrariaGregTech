#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Machines;

public class DrumItem : TieredMachineItem, IFluidHandlerItem
{
	public DrumItem() { }
	public DrumItem(VoltageTier tier, MachineDefinition def) : base(tier, def) { }

	public int Capacity => _def?.Capacity ?? 0;

	public override void WarmUpTexture()
	{
		DrumRenderer.EnsureItemTexture(Item.type, _def?.MaterialId);
		if (Mod.TryFind<ModTile>(Name, out var t))
			DrumRenderer.EnsureTileTexture(t.Type, _def?.MaterialId);
	}

	private const float FluidBorderFrac = 0.22f;

	public override void PostDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame,
		Color drawColor, Color itemColor, Vector2 origin, float scale)
	{
		var stored = GetTank(0);
		if (stored.IsEmpty || stored.Type is null) return;
		DrawFluidOverlay(sb, position - origin * scale, frame.Size() * scale, stored.Type, drawColor);
	}

	public override void PostDrawInWorld(SpriteBatch sb, Color lightColor, Color alphaColor,
		float rotation, float scale, int whoAmI)
	{
		var stored = GetTank(0);
		if (stored.IsEmpty || stored.Type is null) return;
		var tex = TextureAssets.Item[Item.type].Value;
		Vector2 size = new Vector2(tex.Width, tex.Height) * scale;
		Vector2 center = Item.Center - Main.screenPosition;
		DrawFluidOverlay(sb, center - size * 0.5f, size, stored.Type, lightColor);
	}

	private static void DrawFluidOverlay(SpriteBatch sb, Vector2 topLeft, Vector2 size,
		Api.Fluids.FluidType fluid, Color light)
	{
		int bx = (int)(size.X * FluidBorderFrac), by = (int)(size.Y * FluidBorderFrac);
		var inner = new Rectangle((int)topLeft.X + bx, (int)topLeft.Y + by,
			(int)size.X - 2 * bx, (int)size.Y - 2 * by);
		FluidIconRenderer.Draw(sb, fluid, inner, light: light);
	}

	protected override void AppendTierTooltip(List<TooltipLine> tooltips)
	{
		tooltips.Add(new TooltipLine(Mod, "DrumCapacity", $"Fluid capacity: {Capacity:N0} mB"));

		var stored = GetTank(0);
		if (!stored.IsEmpty)
			tooltips.Add(new TooltipLine(Mod, "DrumContents",
				$"Contains {stored.Amount:N0} mB of {stored.Type!.DisplayName}"));
	}

	public Item Container => Item;
	public int TankCount => 1;
	public int GetCapacity(int tank) => Capacity;

	public FluidStack GetTank(int tank)
	{
		if (Blob?.Data is not { } d || !d.ContainsKey("fluidId")) return FluidStack.Empty;
		if (!FluidRegistry.TryGet(d.GetString("fluidId"), out var type)) return FluidStack.Empty;
		int amt = d.GetInt("fluidAmount");
		if (amt <= 0) return FluidStack.Empty;
		return new FluidStack(type, amt, d.ContainsKey("fluidNbt") ? d.GetCompound("fluidNbt") : null);
	}

	public bool IsFluidValid(int tank, FluidStack fluid) => true;

	public int Fill(FluidStack resource, bool simulate)
	{
		if (resource.IsEmpty) return 0;
		var existing = GetTank(0);
		if (!existing.IsEmpty && !existing.SameTypeAs(resource)) return 0;
		int room = Capacity - existing.Amount;
		if (room <= 0) return 0;
		int accepted = Math.Min(room, resource.Amount);
		if (!simulate)
			SetStored(existing.IsEmpty
				? new FluidStack(resource.Type!, accepted, resource.Nbt)
				: existing.WithAmount(existing.Amount + accepted));
		return accepted;
	}

	public FluidStack Drain(int maxAmount, bool simulate)
	{
		var existing = GetTank(0);
		if (existing.IsEmpty || maxAmount <= 0) return FluidStack.Empty;
		int drained = Math.Min(existing.Amount, maxAmount);
		var result = new FluidStack(existing.Type!, drained, existing.Nbt);
		if (!simulate)
			SetStored(drained == existing.Amount
				? FluidStack.Empty
				: existing.WithAmount(existing.Amount - drained));
		return result;
	}

	public FluidStack Drain(FluidStack fluidStack, bool simulate)
	{
		var existing = GetTank(0);
		if (existing.IsEmpty || !existing.SameTypeAs(fluidStack)) return FluidStack.Empty;
		return Drain(fluidStack.Amount, simulate);
	}

	private MachinePortableData? Blob =>
		Item.TryGetGlobalItem<MachinePortableData>(out var g) ? g : null;

	private void SetStored(FluidStack stack)
	{
		var g = Blob;
		if (g is null) return;
		if (stack.IsEmpty)
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
		d["fluidId"]     = stack.Type!.Id;
		d["fluidAmount"] = stack.Amount;
		if (stack.Nbt != null) d["fluidNbt"] = stack.Nbt; else d.Remove("fluidNbt");
	}
}
