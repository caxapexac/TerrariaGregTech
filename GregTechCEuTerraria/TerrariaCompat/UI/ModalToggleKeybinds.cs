#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Questbook;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

public sealed class ModalToggleKeybinds : ModSystem
{
	public static ModKeybind? OpenRecipeBrowser;
	public static ModKeybind? ToggleDockedBrowser;
	public static ModKeybind? ToggleSidePanels;
	public static ModKeybind? OpenQuestbook;

	private static readonly InputMode[] KeyboardModes = { InputMode.Keyboard, InputMode.KeyboardUI };

	private bool _ownBrowser;
	private bool _ownDocked;
	private bool _ownSidePanels;
	private bool _ownQuestbook;

	public override void Load()
	{
		Language.GetOrRegister(
			"Mods.GregTechCEuTerraria.Keybinds.OpenRecipeBrowser.DisplayName",
			() => "Open recipe browser");
		Language.GetOrRegister(
			"Mods.GregTechCEuTerraria.Keybinds.ToggleDockedBrowser.DisplayName",
			() => "Toggle docked recipe browser");
		Language.GetOrRegister(
			"Mods.GregTechCEuTerraria.Keybinds.ToggleSidePanels.DisplayName",
			() => "Toggle favorites and history panels");
		Language.GetOrRegister(
			"Mods.GregTechCEuTerraria.Keybinds.OpenQuestbook.DisplayName",
			() => "Open questbook");

		OpenRecipeBrowser   = KeybindLoader.RegisterKeybind(Mod, "OpenRecipeBrowser", Keys.OemQuestion);
		ToggleDockedBrowser = KeybindLoader.RegisterKeybind(Mod, "ToggleDockedBrowser", Keys.OemTilde);
		ToggleSidePanels    = KeybindLoader.RegisterKeybind(Mod, "ToggleSidePanels", Keys.None);
		OpenQuestbook       = KeybindLoader.RegisterKeybind(Mod, "OpenQuestbook", Keys.Q);
	}

	public override void Unload()
	{
		OpenRecipeBrowser = null;
		ToggleDockedBrowser = null;
		ToggleSidePanels = null;
		OpenQuestbook = null;
	}

	public override void PostUpdateInput()
	{
		if (Main.dedServ) return;
		if (UISearchBar.AnyFocused) return;

		Handle(OpenRecipeBrowser, GlobalRecipeBrowserSystem.Toggle, ref _ownBrowser);
		Handle(ToggleDockedBrowser, GlobalRecipeBrowserSystem.ToggleDocked, ref _ownDocked);
		Handle(ToggleSidePanels, GlobalRecipeBrowserSystem.ToggleSidePanels, ref _ownSidePanels);
		Handle(OpenQuestbook, QuestbookUISystem.Toggle, ref _ownQuestbook);
	}

	private static void Handle(ModKeybind? kb, Action toggle, ref bool owned)
	{
		if (kb is null) return;

		bool held, justPressed;
		try { held = kb.Current; justPressed = kb.JustPressed; }
		catch (KeyNotFoundException) { return; }

		if (!held) owned = false;

		if (justPressed)
		{
			toggle();
			owned = true;
		}

		if (owned && held)
			ConsumeKey(kb);
	}

	private static void ConsumeKey(ModKeybind kb)
	{
		foreach (var mode in KeyboardModes)
		{
			List<string> keys;
			try { keys = kb.GetAssignedKeys(mode); }
			catch { continue; }
			ModalEscape.ConsumePhysicalKeys(keys, mode);
		}
	}
}
