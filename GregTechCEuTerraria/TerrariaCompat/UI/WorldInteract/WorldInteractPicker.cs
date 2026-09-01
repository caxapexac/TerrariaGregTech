#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.WorldInteract;

public sealed class WorldInteractPickerSystem : ModSystem
{
	private UserInterface? _ui;
	private WorldInteractPickerState? _state;
	private static WorldInteractPickerSystem? _inst;

	internal static bool ConsumeTileInteract;

	public override void Load()
	{
		_inst = this;
		On_Player.TileInteractionsUse += SkipWhilePicking;
		if (Main.dedServ) return;
		_ui = new UserInterface();
		_state = new WorldInteractPickerState();
		_state.Activate();
	}

	public override void Unload()
	{
		On_Player.TileInteractionsUse -= SkipWhilePicking;
		_ui = null; _state = null; _inst = null;
	}

	private static void SkipWhilePicking(On_Player.orig_TileInteractionsUse orig, Player self, int myX, int myY)
	{
		if (IsOpen || ConsumeTileInteract) return;
		orig(self, myX, myY);
	}

	public static bool IsOpen => _inst?._ui?.CurrentState != null;
	public static void CloseMenu() { if (_inst?._ui != null) _inst._ui.SetState(null); }

	public override void PostUpdateInput()
	{
		if (Main.dedServ) return;
		ConsumeTileInteract = false;
		var p = Main.LocalPlayer;
		if (p is null) return;

		if (IsOpen)
		{
			if (_state!.ContainsCursor())
			{
				p.mouseInterface = true;
				p.controlUseItem = false;
				p.controlUseTile = false;
			}
			if (ModalEscape.EscJustPressed) { CloseMenu(); ModalEscape.ConsumeEscape(); }
			return;
		}

		if (WorldInteractables.IsClickTool(p.HeldItem)) return;
		if (UILayers.IsCursorOverAnyModal()) return;
		if (PipeSettings.PipeSettingsSystem.IsOpen || MeBus.MeBusSettingsSystem.IsOpen) return;
		if (!(Main.mouseRight && Main.mouseRightRelease)) return;

		WorldCursor.RawCell(out int x, out int y);
		var list = new List<WorldInteractable>();
		WorldInteractables.ProbeAt(x, y, list);
		if (list.Count == 0)
		{
			x = Player.tileTargetX; y = Player.tileTargetY;
			WorldInteractables.ProbeAt(x, y, list);
			if (list.Count == 0) return;
		}
		if (!p.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple)) return;

		Main.mouseRightRelease = false;
		p.controlUseTile = false;
		p.releaseUseTile = false;
		ConsumeTileInteract = true;

		if (list.Count == 1) { list[0].Open(); return; }

		_state!.Bind(list, x, y);
		_ui!.SetState(_state);
		SoundEngine.PlaySound(SoundID.MenuTick);
	}

	public override void UpdateUI(GameTime gameTime)
	{
		if (_ui?.CurrentState is not WorldInteractPickerState st) return;
		var p = Main.LocalPlayer;
		if (p is null || !p.IsInTileInteractionRange(st.TileX, st.TileY, TileReachCheckSettings.Simple))
		{
			CloseMenu();
			return;
		}
		_ui.Update(gameTime);
		if (st.OpenedThisFrame) { st.OpenedThisFrame = false; return; }
		if ((MouseClick.LeftPressed || MouseClick.RightPressed) && !st.ContainsCursor()) CloseMenu();
	}

	private GameInterfaceLayer? _layer;

	public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
	{
		if (_ui?.CurrentState == null) return;
		_layer ??= new LegacyGameInterfaceLayer(
			"GregTechCEuTerraria: Interact Picker",
			() =>
			{
				if (_ui?.CurrentState is WorldInteractPickerState st)
				{
					st.Reposition();
					_ui.Draw(Main.spriteBatch, new GameTime());
				}
				return true;
			},
			InterfaceScaleType.UI);
		int idx = layers.FindIndex(l => l.Name == "Vanilla: Mouse Text");
		if (idx < 0) idx = layers.Count;
		layers.Insert(idx, _layer);
	}
}

public sealed class WorldInteractPickerState : UIModalWindow
{
	public bool OpenedThisFrame;
	public int TileX, TileY;

	private UITerrariaPanel? _panel;

	public void Bind(List<WorldInteractable> items, int tileX, int tileY)
	{
		RemoveAllChildren();
		OpenedThisFrame = true;
		TileX = tileX; TileY = tileY;

		const int rowH = 24, w = 184, pad = 5, gap = 2;
		int h = pad * 2 + items.Count * rowH + System.Math.Max(0, items.Count - 1) * gap;

		var panel = new UITerrariaPanel
		{
			Width = StyleDimension.FromPixels(w),
			Height = StyleDimension.FromPixels(h),
		};

		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];
			panel.Append(new UITextButton(
				label: () => item.Label,
				onLeft: () => { WorldInteractPickerSystem.CloseMenu(); item.Open(); },
				tooltip: null,
				width: w - pad * 2, height: rowH, textScale: 0.8f)
			{
				Left = StyleDimension.FromPixels(pad),
				Top = StyleDimension.FromPixels(pad + i * (rowH + gap)),
			});
		}

		Append(panel);
		_panel = panel;
	}

	public void Reposition()
	{
		if (_panel is null) return;
		WorldCursor.WorldToUi(TileX * 16f, TileY * 16f, out float ux, out float uy);
		WorldCursor.UiScreenSize(out float screenW, out float screenH);
		float maxX = System.Math.Max(0f, screenW - _panel.Width.Pixels);
		float maxY = System.Math.Max(0f, screenH - _panel.Height.Pixels);
		ux = MathHelper.Clamp(ux, 0f, maxX);
		uy = MathHelper.Clamp(uy, 0f, maxY);
		_panel.Left = StyleDimension.FromPixels(ux);
		_panel.Top = StyleDimension.FromPixels(uy);
		_panel.Recalculate();
	}
}
