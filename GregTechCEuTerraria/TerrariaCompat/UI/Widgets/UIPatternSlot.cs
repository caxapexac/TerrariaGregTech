#nullable enable
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Net.Actions;
using GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;
using Terraria.UI.Chat;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Widgets;

public sealed class UIPatternSlot : UIElement
{
	private readonly IMePatternEncodingHost _term;
	private readonly bool _output;
	private readonly int _slot;
	private const float VanillaNativeSlotPixels = 52f;
	private static readonly Item[] _temp = { new() };

	public UIPatternSlot(IMePatternEncodingHost term, bool output, int slot, int sizePx)
	{
		_term = term;
		_output = output;
		_slot = slot;
		Width = StyleDimension.FromPixels(sizePx);
		Height = StyleDimension.FromPixels(sizePx);
	}

	private GenericStack? Stack() =>
		(_output ? _term.Encoding.Outputs : _term.Encoding.Inputs).GetStack(_slot);

	private bool _membersCached;
	private string? _cacheTag;
	private System.Collections.Generic.IReadOnlyList<int> _cacheItems = System.Array.Empty<int>();
	private System.Collections.Generic.IReadOnlyList<string> _cacheFluids = System.Array.Empty<string>();

	private void RefreshMembers()
	{
		var tag = _term.Encoding.GetTag(_slot);
		if (_membersCached && tag == _cacheTag) return;
		_membersCached = true;
		_cacheTag = tag;
		_cacheItems  = tag == null ? System.Array.Empty<int>()    : _term.Encoding.GetAlternatives(_slot);
		_cacheFluids = tag == null ? System.Array.Empty<string>() : _term.Encoding.GetFluidAlternatives(_slot);
	}

	private static System.Collections.Generic.HashSet<AEKey>? _craftCache;
	private static long _craftCacheTick = -1;
	private bool IsCraftable(AEKey what)
	{
		if (_craftCacheTick != UiFrame.Count)
		{
			_craftCacheTick = UiFrame.Count;
			_craftCache = _term.Network?.GetCraftables();
		}
		return _craftCache != null && _craftCache.Contains(what);
	}

	public override void LeftMouseDown(UIMouseEvent evt)
	{
		base.LeftMouseDown(evt);

		var k = Main.keyState;
		if (k.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftAlt)
			|| k.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightAlt))
		{
			var s = Stack();
			if (s?.What is AEItemKey aik) FavoritesPlayer.Local.BringItemToFront(aik.GetItem());
			else if (s?.What is AEFluidKey afk) { var f = afk.GetFluid(); FavoritesPlayer.Local.BringFluidToFront(f.Id, f.DisplayName); }
			return;
		}

		if (!_output && _term.Encoding.HasAlternatives(_slot))
		{
			var tagStack = Stack();
			if (tagStack != null)
				MeCraftSystem.OpenForAmount(tagStack.What, tagStack.Amount, 1, "Set Amount", "Set",
					"Amount for this pattern slot (keeps the tag)",
					amt => MachineActions.Send(MePatternEncodingAction.SetTagAmount(_slot, amt), _term.Machine));
			return;
		}

		if (_term.Encoding.Mode != MePatternType.Processing) return;
		var cursor = Main.mouseItem;
		if (!cursor.IsAir)
		{
			var key = AEItemKey.Of(cursor);
			if (key != null)
				MachineActions.Send(MePatternEncodingAction.SetSlot(_output, _slot, key, cursor.stack), _term.Machine);
			return;
		}
		var existing = Stack();
		if (existing != null)
			MeCraftSystem.OpenForAmount(existing.What, existing.Amount, 1, "Set Amount", "Set",
				"Amount for this pattern slot",
				amt => MachineActions.Send(MePatternEncodingAction.SetSlot(_output, _slot, existing.What, amt), _term.Machine));
		else
			ItemPickerSystem.Open(
				itemType => MachineActions.Send(MePatternEncodingAction.SetSlot(_output, _slot, AEItemKey.OfType(itemType), 1), _term.Machine),
				fluidId =>
				{
					var fluid = Api.Fluids.FluidRegistry.Get(fluidId);
					if (fluid != null)
						MachineActions.Send(MePatternEncodingAction.SetSlot(_output, _slot, AEFluidKey.Of(fluid), 1), _term.Machine);
				});
	}

	public override void RightMouseDown(UIMouseEvent evt)
	{
		base.RightMouseDown(evt);
		if (_term.Encoding.Mode != MePatternType.Processing) return;
		MachineActions.Send(MePatternEncodingAction.SetSlot(_output, _slot, null, 0), _term.Machine);
	}

	public override void MiddleMouseDown(UIMouseEvent evt)
	{
		base.MiddleMouseDown(evt);
		if (_output || !_term.Encoding.HasAlternatives(_slot)) return;

		if (Stack()?.What is AEFluidKey)
			ItemPickerSystem.Open(
				_ => { },
				fluidId =>
				{
					var fluid = Api.Fluids.FluidRegistry.Get(fluidId);
					if (fluid != null)
						MachineActions.Send(MePatternEncodingAction.SetSlot(false, _slot, AEFluidKey.Of(fluid), 1), _term.Machine);
				},
				System.Array.Empty<int>(),
				_term.Encoding.GetFluidAlternatives(_slot));
		else
			ItemPickerSystem.Open(
				itemType => MachineActions.Send(
					MePatternEncodingAction.SetSlot(false, _slot, AEItemKey.OfType(itemType), 1), _term.Machine),
				null,
				_term.Encoding.GetAlternatives(_slot));
	}

	protected override void DrawSelf(SpriteBatch sb)
	{
		var bounds = GetDimensions().ToRectangle();

		if (ItemDrag.TryDropItem(bounds, out int droppedItem))
			MachineActions.Send(MePatternEncodingAction.SetSlot(
				_output, _slot, AEItemKey.OfType(droppedItem), 1), _term.Machine);
		else if (ItemDrag.TryDropFluid(bounds, out var droppedFluid, out _))
		{
			var fluid = Api.Fluids.FluidRegistry.Get(droppedFluid);
			if (fluid != null)
				MachineActions.Send(MePatternEncodingAction.SetSlot(
					_output, _slot, AEFluidKey.Of(fluid), 1), _term.Machine);
		}

		sb.Draw(TextureAssets.MagicPixel.Value, bounds, new Color(24, 28, 56));

		var stack = Stack();
		bool tagged = !_output && _term.Encoding.HasAlternatives(_slot);
		if (tagged) RefreshMembers();
		if (stack?.What is AEItemKey itemKey)
		{
			Item disp;
			if (tagged)
			{
				disp = new Item();
				disp.SetDefaults(_cacheItems.Count > 0
					? _cacheItems[TagCycle.Index(_cacheItems.Count)]
					: itemKey.GetItem());
			}
			else disp = itemKey.GetReadOnlyStack().Clone();
			disp.stack = 1;
			_temp[0] = disp;
			float old = Main.inventoryScale;
			Main.inventoryScale = bounds.Width / VanillaNativeSlotPixels;
			try
			{
				ItemSlot.Draw(sb, _temp, ItemSlot.Context.CraftingMaterial, 0, new Vector2(bounds.X, bounds.Y));
			}
			finally { Main.inventoryScale = old; }
			SlotRender.DrawAmount(sb, bounds, stack.What, stack.Amount, hideAtOrBelow: 1);
		}
		else if (stack?.What is AEFluidKey fluidKey)
		{
			var drawFluid = fluidKey.GetFluid();
			if (tagged && _cacheFluids.Count > 0
				&& Api.Fluids.FluidRegistry.Get(_cacheFluids[TagCycle.Index(_cacheFluids.Count)]) is { } f)
				drawFluid = f;
			BrowserFluidSlot.Draw(sb, bounds, drawFluid);
			SlotRender.DrawAmount(sb, bounds, stack.What, stack.Amount, hideAtOrBelow: 1);
		}

		if (stack?.What != null && (tagged || IsCraftable(stack.What)))
		{
			var cfont = FontAssets.ItemStack.Value;
			ChatManager.DrawColorCodedStringWithShadow(sb, cfont, "*",
				new Vector2(bounds.X + 3, bounds.Y - 1),
				new Color(120, 255, 120), 0f, Vector2.Zero, new Vector2(0.9f));
		}

		if (IsMouseHovering)
		{
			Main.LocalPlayer.mouseInterface = true;
			if (stack?.What is AEItemKey ik) BrowserHover.SetItem(ik.GetItem());
			else if (stack?.What is AEFluidKey fk)
			{
				var f = fk.GetFluid();
				BrowserHover.SetFluid(f.Id, f.DisplayName);
			}
			EmitHint(stack);
		}
	}

	private void EmitHint(GenericStack? stack)
	{
		var body = new System.Text.StringBuilder();
		body.Append(stack != null
			? stack.What.GetDisplayName() + (stack.Amount > 1 ? $"  x{stack.Amount}" : "")
			: (_output ? "Pattern output" : "Pattern input"));
		if (!_output && _term.Encoding.HasAlternatives(_slot))
		{
			body.Append($"\n[c/AAAAAA:Tag: {_term.Encoding.GetTag(_slot)}]");
			body.Append("\n[c/AAAAAA:LMB: set quantity]");
			body.Append("\n[c/AAAAAA:MMB: pin a specific item (or leave as the tag)]");
		}
		else if (_term.Encoding.Mode == MePatternType.Processing)
		{
			body.Append(_output
				? "\n[c/AAAAAA:Put the produced items/fluids here]"
				: "\n[c/AAAAAA:Put the required items/fluids here]");
			body.Append("\n[c/AAAAAA:LMB: set / change quantity]");
			body.Append("\n[c/AAAAAA:RMB: remove]");
		}
		else
		{
			body.Append("\n[c/AAAAAA:Filled from the selected recipe]");
		}
		Main.instance.MouseText(body.ToString());
	}

}
