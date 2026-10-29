#nullable enable
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public abstract class ModalUISystem : ModSystem, IModalHost
{
	protected UserInterface? Ui;

	protected abstract string LayerName { get; }

	protected virtual bool CloseOnEscape => true;

	protected virtual bool CloseOnClickOutside => false;

	public virtual bool PinSupported => false;
	private bool _pinned;
	public bool Pinned
	{
		get => _pinned;
		set => _pinned = value && PinSupported;
	}
	void IModalHost.RequestClose() => CloseInternal();

	private UIState? _pinHidden;

	protected virtual bool ShouldAutoClose() => false;

	protected virtual void OnClose() { }

	protected virtual void AddExtraLayers(List<GameInterfaceLayer> layers) { }

	protected bool IsOpenInternal => Ui?.CurrentState != null;

	private uint _openedTick = uint.MaxValue;
	private bool OpenedThisFrame => _openedTick == UiFrame.Count;

	private bool _openPressHeld;
	private uint _openPressTick = uint.MaxValue;

	private string? _parentLayer;

	private bool _pendingClose;

	protected void PushModal(string? parentLayer = null)
	{
		_pendingClose = false;
		_parentLayer = parentLayer;
		UILayers.Push(LayerName);
		_openedTick = UiFrame.Count;
		_openPressHeld = Main.mouseLeft || Main.mouseRight;
		_openPressTick = UiFrame.Count;
	}

	protected void CloseInternal()
	{
		if (Ui?.CurrentState == null && _pinHidden == null) return;
		_pendingClose = true;
	}

	private void DoCloseNow()
	{
		_pendingClose = false;
		if (Ui?.CurrentState == null && _pinHidden == null) return;
		Pinned = false;
		_pinHidden = null;
		_parentLayer = null;
		OnClose();
		Ui?.SetState(null);
	}

	public override void Load()
	{
		if (Main.dedServ) return;
		Ui = new UserInterface();
		UILayers.RegisterModal(LayerName, () => IsOpenInternal);
		UILayers.RegisterModalCursorProbe(LayerName,
			() => Ui?.CurrentState is UIModalWindow w && w.ContainsCursor());
		UILayers.RegisterModalBoundsProbe(LayerName,
			() => Ui?.CurrentState is UIModalWindow w ? w.OccupiedRects() : System.Array.Empty<Rectangle>());
	}

	public override void Unload() => Ui = null;

	public override void UpdateUI(GameTime gameTime)
	{
		if (_pendingClose) DoCloseNow();

		if (PinSupported && Pinned)
		{
			bool blocked = Main.ingameOptionsWindow || Main.gameMenu;
			bool show = Main.playerInventory && !blocked;
			if (show && Ui?.CurrentState == null && _pinHidden != null)
			{ Ui!.SetState(_pinHidden); _pinHidden = null; PushModal(); }
			else if (!show && Ui?.CurrentState is { } cur)
			{ _pinHidden = cur; Ui.SetState(null); }
		}

		if (Ui?.CurrentState is not { } state) return;
		if (state is FreeModalWindow fmw) fmw.Host ??= this;
		bool pinned = PinSupported && Pinned;
		if (!pinned && (!Main.playerInventory || Main.ingameOptionsWindow || Main.gameMenu
			|| ShouldAutoClose()))
		{ CloseInternal(); return; }
		if (!pinned && _parentLayer != null && !UILayers.IsModalOpen(_parentLayer))
		{ CloseInternal(); return; }
		if (CloseOnClickOutside && !pinned && !OpenedThisFrame && MouseClick.LeftPressed
			&& state is UIModalWindow modal
			&& (!modal.ContainsCursor() || UILayers.IsCursorOverHigherModal(LayerName)))
		{ CloseInternal(); return; }
		ModalEscape.SuppressVanillaUIClicks(state);
		if (state is UIModalWindow scrollModal && scrollModal.ContainsCursor())
			PlayerInput.LockVanillaMouseScroll("GregTechCEuTerraria/Modal");
		if (InputBlocked()) ModalEscape.WithCursorParked(() => Ui.Update(gameTime));
		else                Ui.Update(gameTime);
	}

	private bool InputBlocked()
	{
		MaintainOpenPress();
		return UILayers.IsCursorOverHigherModal(LayerName) || OpenedThisFrame || _openPressHeld
			|| UILayers.PressBelongsToAnotherModal(LayerName);
	}

	private void MaintainOpenPress()
	{
		if (!_openPressHeld || _openPressTick == UiFrame.Count) return;
		_openPressTick = UiFrame.Count;
		if (!Main.mouseLeft && !Main.mouseRight) _openPressHeld = false;
	}

	public override void PostUpdateInput()
	{
		if (Main.dedServ || Ui?.CurrentState is not { } state) return;
		ModalEscape.SuppressItemUse(state);
		if (CloseOnEscape && !Pinned && ModalEscape.EscJustPressed && UILayers.IsTopModal(LayerName))
		{
			CloseInternal();
			ModalEscape.ConsumeEscape();
		}
	}

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		AddExtraLayers(layers);
		UILayers.InsertModal(layers, LayerName, () =>
		{
			if (Ui?.CurrentState != null)
			{
				if (InputBlocked()) ModalEscape.WithCursorParked(() => Ui.Draw(Main.spriteBatch, new GameTime()));
				else                Ui.Draw(Main.spriteBatch, new GameTime());
			}
			return true;
		});
	}
}
