#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Net.Actions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

public sealed class UIFluidSlot : UIElement
{
	private readonly MetaMachine _entity;
	private readonly IFluidHandler _handler;
	private readonly int _tankIndex;
	private readonly bool _allowFill;
	private readonly bool _allowDrain;
	private readonly bool _fillBar;

	public UIFluidSlot(MetaMachine entity, IO direction, int localTankIndex, int width, int height,
		bool fillBar = false)
	{
		_entity = entity;
		_handler = (IFluidHandler)entity;
		_tankIndex = entity.ResolveFluidTank(direction, localTankIndex);
		(_allowFill, _allowDrain) = _handler.GetTankClickCaps(_tankIndex);
		_fillBar = fillBar;
		Width = StyleDimension.FromPixels(width);
		Height = StyleDimension.FromPixels(height);
	}

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		var bounds = GetDimensions().ToRectangle();
		var stored = _handler.GetTank(_tankIndex);
		int capacity = _handler.GetCapacity(_tankIndex);

		if (_fillBar)
			DrawFillBar(spriteBatch, bounds, stored, capacity);
		else if (stored.IsEmpty)
			spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, new Color(25, 30, 50) * 0.9f);
		else
			BrowserFluidSlot.Draw(spriteBatch, bounds, stored.Type, stored.Amount,
				amountBottomInset: 16);

		var border = IsMouseHovering
			? Color.Lerp(TankFrame.BorderColor, Color.White, 0.5f)
			: TankFrame.BorderColor;
		TankFrame.DrawBorder(spriteBatch, bounds, border);

		if (IsMouseHovering)
		{
			Main.LocalPlayer.mouseInterface = true;
			Main.LocalPlayer.cursorItemIconEnabled = false;
			string hint =
				  _allowFill  && _allowDrain ? "R-click with a bucket to fill or empty"
				: _allowFill                 ? "R-click with a bucket to fill"
				: _allowDrain                ? "R-click with an empty bucket to drain"
				:                              "";
			string label = stored.IsEmpty
				? $"Empty  (0 / {capacity:N0} mB)\n{hint}"
				: $"{stored.Type!.DisplayName}: {stored.Amount:N0} / {capacity:N0} mB\n"
				  + $"{FluidTooltips.Temperature(stored.Type!)}\n{hint}";
			Main.instance.MouseText(label);
			if (!stored.IsEmpty)
				HoverItemTracker.SetFluid(stored.Type!.Id);
		}
	}

	public override void RightMouseDown(UIMouseEvent evt)
	{
		base.RightMouseDown(evt);
		var held = Main.mouseItem;
		if (held is null || held.IsAir) return;
		if (!WouldTransfer(held)) return;

		MachineActions.Send(new FluidSlotAction(_tankIndex, held), _entity);
		Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Splash);
	}

	private static void DrawFillBar(SpriteBatch sb, Rectangle bounds, FluidStack stored, int capacity)
	{
		var tex = TextureAssets.MagicPixel.Value;
		sb.Draw(tex, bounds, new Color(25, 30, 50) * 0.9f);

		if (stored.IsEmpty || capacity <= 0) return;
		float fill = System.Math.Clamp((float)stored.Amount / capacity, 0f, 1f);
		int fillH = (int)(bounds.Height * fill);
		if (fillH <= 0) return;

		var fillRect = new Rectangle(bounds.X, bounds.Y + bounds.Height - fillH, bounds.Width, fillH);
		if (!FluidIconRenderer.Draw(sb, stored.Type!, fillRect))
			sb.Draw(tex, fillRect, FluidIconRenderer.RgbColor(stored.Type!.Color));
	}

	private bool WouldTransfer(Item held)
	{
		var tank = _handler.GetTankAccess(_tankIndex);

		var vanilla = VanillaFluidBridge.StackFor(held.type);
		if (!vanilla.IsEmpty)
			return _allowFill && tank.Fill(vanilla, simulate: true) >= vanilla.Amount;

		if (held.ModItem is Items.Fluids.FluidBucketItem gtBucket && gtBucket.Fluid is { } gf)
			return _allowFill
				&& tank.Fill(new FluidStack(gf, VanillaFluidBridge.BucketAmount),
					simulate: true) >= VanillaFluidBridge.BucketAmount;

		if (held.type == Terraria.ID.ItemID.EmptyBucket)
		{
			if (!_allowDrain) return false;
			var stored = tank.GetTank(0);
			if (stored.IsEmpty || stored.Amount < VanillaFluidBridge.BucketAmount) return false;
			return VanillaFluidBridge.FilledVersion(Terraria.ID.ItemID.EmptyBucket, stored.Type!) != 0
			    || Items.Fluids.FluidBucketRegistry.Get(stored.Type!.Id) != null;
		}

		if (held.ModItem is IFluidHandlerItem container)
		{
			var contents = container.GetTank(0);
			if (contents.IsEmpty)
			{
				if (!_allowDrain) return false;
				var pulled = tank.Drain(container.GetCapacity(0), simulate: true);
				return !pulled.IsEmpty && container.Fill(pulled, simulate: true) > 0;
			}
			return _allowFill && tank.Fill(contents, simulate: true) > 0;
		}

		return false;
	}
}
