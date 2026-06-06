#nullable enable
using GregTechCEuTerraria.Api.Capability;
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI;

// UIState that hosts a machine's panel + widgets, bound to a specific entity.
// Rebuilds children whenever Bind is called with a fresh entity/layout.
public sealed class MachineUIState : UIState
{
	private const float SlotGap = 1f;

	private MetaMachine? _entity;
	private MachineUILayout? _layout;
	private float _leftStackOffsetPx;
	private CoverSide? _openCoverSide;
	private UIElement? _coverPopup;
	private CoverSide? _pendingCoverOpen;
	private int _coverPopupFilterSig; // (0/1/2 = none/simple/tag)
	private UIElement? _machinePanel;

	public MetaMachine? Entity => _entity;

	public void Bind(MetaMachine entity, MachineUILayout layout)
	{
		_entity = entity;
		_layout = layout;
		_openCoverSide = null;
		_coverPopup = null;
		_pendingCoverOpen = null;
		_machinePanel = null;
		RemoveAllChildren();
		BuildPanel();
	}

	public void Unbind()
	{
		_entity = null;
		_layout = null;
		_openCoverSide = null;
		_coverPopup = null;
		_pendingCoverOpen = null;
		_machinePanel = null;
		RemoveAllChildren();
	}

	public void RequestCoverSettings(CoverSide side) => _pendingCoverOpen = side;

	// RMB toggles - opens the popup, or closes if already open
	private void ToggleCoverSettings(CoverSide side)
	{
		if (_entity is null || _layout is null) return;
		bool wasSame = _openCoverSide == side;
		CloseCoverSettings();
		if (wasSame) return;
		OpenCoverSettings(side);
	}

	private void OpenCoverSettings(CoverSide side)
	{
		if (_entity is null || _layout is null) return;

		var popup = CoverSettingsUI.Build(_entity, side, _layout.Scale);
		if (popup is null) return;
		popup.HAlign = 0.5f;
		popup.VAlign = 0f;
		float panelBottom = 0f;
		if (_machinePanel is not null)
		{
			var d = _machinePanel.GetDimensions();
			panelBottom = d.Y + d.Height;
		}
		popup.Top = StyleDimension.FromPixels(panelBottom + 8f);
		Append(popup);
		_coverPopup = popup;
		_openCoverSide = side;
		_coverPopupFilterSig = CoverSettingsUI.FilterEditorSignature(_entity, side);
	}

	private void CloseCoverSettings()
	{
		Widgets.UITextField.UnfocusAll();
		_coverPopup?.Remove();
		_coverPopup = null;
		_openCoverSide = null;
	}

	public override void Update(GameTime gameTime)
	{
		base.Update(gameTime);
		if (_pendingCoverOpen is { } pending)
		{
			_pendingCoverOpen = null;
			ToggleCoverSettings(pending);
		}
		if (_openCoverSide is { } open)
		{
			if (_entity is null || _entity.GetCoverAtSide(open) is null)
				CloseCoverSettings();
			else if (!Main.mouseLeft && !Main.mouseRight
			         && CoverSettingsUI.FilterEditorSignature(_entity, open) != _coverPopupFilterSig)
			{
				CloseCoverSettings();
				OpenCoverSettings(open);
			}
		}
	}


	private void BuildPanel()
	{
		if (_entity is null || _layout is null) return;

		float s = _layout.Scale;
		_leftStackOffsetPx = 0f;

		var panel = new UITerrariaPanel
		{
			Width = StyleDimension.FromPixels(_layout.Width * s),
			Height = StyleDimension.FromPixels(_layout.Height * s),
			HAlign = 0.5f,
			VAlign = 0.3f,
		};

		if (!string.IsNullOrEmpty(_layout.Title))
		{
			var title = new UIText(_layout.Title, 1.0f, large: false)
			{
				Left = StyleDimension.FromPixels(8 * s),
				Top = StyleDimension.FromPixels(6 * s),
			};
			panel.Append(title);
		}

		foreach (var spec in _layout.Widgets)
		{
			var element = spec.Create(_entity);
			float w = element.Width.Pixels * s;
			float h = element.Height.Pixels * s;
			if (element is Widgets.UISlot or Widgets.UIFluidSlot)
			{
				w -= SlotGap;
				h -= SlotGap;
			}
			element.Width = StyleDimension.FromPixels(w);
			element.Height = StyleDimension.FromPixels(h);
			element.Left = StyleDimension.FromPixels(spec.X * s);
			element.Top = StyleDimension.FromPixels(spec.Y * s);
			panel.Append(element);
		}

		Append(panel);
		_machinePanel = panel;

		if (_layout.LeftPanel is not null)
			AppendLeftSidePanel(_layout.LeftPanel);
		if (_layout.TopPanel is not null)
			AppendTopSidePanel(_layout.TopPanel);
		if (_layout.BottomPanel is not null)
			AppendBottomSidePanel(_layout.BottomPanel);

		AppendIOConfigPanel(_entity, panel);

		if (_entity is TieredEnergyMachine em)
			AppendChargerSlot(em, panel);

		if (_entity.SupportsWorkingEnabledToggle)
			AppendPowerTogglePanel(_entity, panel);

		if (_entity is Machine.Multiblock.MultiblockControllerMachine)
			AppendCoverPanelLeft(_entity);

		if (_entity is Api.Machine.Feature.Multiblock.IDistinctPart dp
			&& _entity is Machine.Multiblock.Part.TieredIOPartMachine iop
			&& iop.Io == Api.Capability.Recipe.IO.IN)
			AppendDistinctTogglePanel(_entity, dp);

		if (_entity is TerrariaCompat.Machine.Multiblock.WorkableMultiblockMachine wmm
			&& wmm.GetRecipeTypes().Length > 1)
			AppendModeSelectPanel(wmm, panel);

		if (_entity is IRecipeLogicMachine proc)
			AppendRecipeBrowser(proc);
	}

	// IO & cover panel pinned above the machine panel, centered
	private void AppendIOConfigPanel(MetaMachine entity, UITerrariaPanel machinePanel)
	{
		bool wantsItem  = entity.SupportsAutoOutputItems;
		bool wantsFluid = entity.SupportsAutoOutputFluids;
		bool wantsCover = entity.SupportsCovers
			&& entity is not Machine.Multiblock.MultiblockControllerMachine;
		var part        = entity as Machine.Multiblock.Part.TieredIOPartMachine;
		bool wantsPart  = part is not null;

		float s = _layout!.Scale;
		const int ClusterSize = UIDirectionSelector.ClusterSize; // 54
		const int ToggleSize  = 18;
		const int Gap         = 4;
		const int Padding     = 6;

		int clusterCount = (wantsCover ? 1 : 0) + (wantsItem ? 1 : 0) + (wantsFluid ? 1 : 0) + (wantsPart ? 1 : 0);
		if (clusterCount == 0) return;
		int innerW = ClusterSize * clusterCount + Gap * (clusterCount - 1);
		int innerH = ClusterSize + Gap + ToggleSize;
		int outerW = innerW + Padding * 2;
		int outerH = innerH + Padding * 2;

		float uiH = Main.screenHeight / (Main.UIScale <= 0 ? 1f : Main.UIScale);
		float machineH  = _layout.Height * s;
		float machineTop = 0.3f * (uiH - machineH);
		float ioH       = outerH * s;
		const float GapAbove = 6f;
		float ioTop = System.Math.Max(8f, machineTop - ioH - GapAbove);

		var ioPanel = new UITerrariaPanel
		{
			Width = StyleDimension.FromPixels(outerW * s),
			Height = StyleDimension.FromPixels(ioH),
			HAlign = 0.5f,
			VAlign = 0f,
			Top = StyleDimension.FromPixels(ioTop),
		};

		int colX = Padding;

		if (wantsCover)
		{
			ioPanel.Append(new UICoverPanel(entity, RequestCoverSettings)
			{
				Left   = StyleDimension.FromPixels(colX * s),
				Top    = StyleDimension.FromPixels(Padding * s),
				Width  = StyleDimension.FromPixels(ClusterSize * s),
				Height = StyleDimension.FromPixels(ClusterSize * s),
			});
			colX += ClusterSize + Gap;
		}

		if (wantsItem)
		{
			var itemCluster = new UIDirectionSelector(
				UIDirectionSelector.Mode.Items,
				() => entity.AutoOutput?.ItemOutputDirection ?? IODirection.None,
				d => TerrariaCompat.Net.Actions.MachineActions.Send(TerrariaCompat.Net.Actions.IOConfigSetAction.OfDirection(TerrariaCompat.Net.Actions.IOConfigSetAction.Field.ItemOutputSide, d), entity),
				() => entity.AutoOutput?.IsAutoOutputItems ?? false,
				b => TerrariaCompat.Net.Actions.MachineActions.Send(TerrariaCompat.Net.Actions.IOConfigSetAction.OfBool(TerrariaCompat.Net.Actions.IOConfigSetAction.Field.ItemAutoOutput, b), entity))
			{
				Left = StyleDimension.FromPixels(colX * s),
				Top  = StyleDimension.FromPixels(Padding * s),
				Width = StyleDimension.FromPixels(ClusterSize * s),
				Height = StyleDimension.FromPixels(ClusterSize * s),
			};
			ioPanel.Append(itemCluster);

			var allowItemIn = new UIToggleButton(
				"GregTechCEuTerraria/Content/Textures/gui/overlay/tool_allow_input",
				() => entity.AutoOutput?.AllowItemInputFromOutputSide ?? false,
				v => TerrariaCompat.Net.Actions.MachineActions.Send(TerrariaCompat.Net.Actions.IOConfigSetAction.OfBool(TerrariaCompat.Net.Actions.IOConfigSetAction.Field.AllowItemInputFromOutput, v), entity),
				"Allow item insertion from the output side\n(pipes/hoppers can backfill output slots)")
			{
				Left = StyleDimension.FromPixels((colX + (ClusterSize - ToggleSize) / 2) * s),
				Top  = StyleDimension.FromPixels((Padding + ClusterSize + Gap) * s),
				Width = StyleDimension.FromPixels(ToggleSize * s),
				Height = StyleDimension.FromPixels(ToggleSize * s),
			};
			ioPanel.Append(allowItemIn);
			colX += ClusterSize + Gap;
		}

		if (wantsFluid)
		{
			var fluidCluster = new UIDirectionSelector(
				UIDirectionSelector.Mode.Fluids,
				() => entity.AutoOutput?.FluidOutputDirection ?? IODirection.None,
				d => TerrariaCompat.Net.Actions.MachineActions.Send(TerrariaCompat.Net.Actions.IOConfigSetAction.OfDirection(TerrariaCompat.Net.Actions.IOConfigSetAction.Field.FluidOutputSide, d), entity),
				() => entity.AutoOutput?.IsAutoOutputFluids ?? false,
				b => TerrariaCompat.Net.Actions.MachineActions.Send(TerrariaCompat.Net.Actions.IOConfigSetAction.OfBool(TerrariaCompat.Net.Actions.IOConfigSetAction.Field.FluidAutoOutput, b), entity))
			{
				Left = StyleDimension.FromPixels(colX * s),
				Top  = StyleDimension.FromPixels(Padding * s),
				Width = StyleDimension.FromPixels(ClusterSize * s),
				Height = StyleDimension.FromPixels(ClusterSize * s),
			};
			ioPanel.Append(fluidCluster);

			var allowFluidIn = new UIToggleButton(
				"GregTechCEuTerraria/Content/Textures/gui/overlay/tool_allow_input",
				() => entity.AutoOutput?.AllowFluidInputFromOutputSide ?? false,
				v => TerrariaCompat.Net.Actions.MachineActions.Send(TerrariaCompat.Net.Actions.IOConfigSetAction.OfBool(TerrariaCompat.Net.Actions.IOConfigSetAction.Field.AllowFluidInputFromOutput, v), entity),
				"Allow fluid insertion from the output side\n(pipes can backfill output tanks)")
			{
				Left = StyleDimension.FromPixels((colX + (ClusterSize - ToggleSize) / 2) * s),
				Top  = StyleDimension.FromPixels((Padding + ClusterSize + Gap) * s),
				Width = StyleDimension.FromPixels(ToggleSize * s),
				Height = StyleDimension.FromPixels(ToggleSize * s),
			};
			ioPanel.Append(allowFluidIn);
		}

		if (wantsPart)
		{
			var partCluster = new UIDirectionSelector(
				part!.PartIoConfigMode,
				() => part.IoDirection,
				d => TerrariaCompat.Net.Actions.MachineActions.Send(new TerrariaCompat.Net.Actions.PartIoDirectionSetAction(d), entity),
				() => part.WorkingEnabled,
				b => TerrariaCompat.Net.Actions.MachineActions.Send(new TerrariaCompat.Net.Actions.PowerToggleAction(b), entity))
			{
				Left = StyleDimension.FromPixels(colX * s),
				Top  = StyleDimension.FromPixels(Padding * s),
				Width = StyleDimension.FromPixels(ClusterSize * s),
				Height = StyleDimension.FromPixels(ClusterSize * s),
			};
			ioPanel.Append(partCluster);
		}

		Append(ioPanel);
	}

	// Power toggle panel - left of the machine panel, top of the left stack
	private void AppendPowerTogglePanel(MetaMachine entity, UITerrariaPanel machinePanel)
	{
		float s = _layout!.Scale;
		const int BtnSize = 22;
		const int LabelHeight = 9;
		const int Padding = 4;
		int outerW = BtnSize + Padding * 2;
		int outerH = LabelHeight + BtnSize + Padding * 2 + 2;
		const float Gap = 6f;

		var (machineLeft, machineTop, _) = MachinePanelScreenAnchor();

		var togglePanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW * s),
			Height = StyleDimension.FromPixels(outerH * s),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(machineLeft - outerW * s - Gap),
			Top  = StyleDimension.FromPixels(machineTop + _leftStackOffsetPx),
		};
		Append(togglePanel);
		_leftStackOffsetPx += outerH * s + Gap;

		var label = new UIText("Power", 0.55f)
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels(Padding * s),
		};
		togglePanel.Append(label);

		var toggle = new Widgets.UIPowerToggle(
			() => entity.WorkingEnabled,
			v => TerrariaCompat.Net.Actions.MachineActions.Send(new TerrariaCompat.Net.Actions.PowerToggleAction(v), entity),
			width: (int)(BtnSize * s),
			height: (int)(BtnSize * s))
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels((Padding + LabelHeight + 2) * s),
		};
		togglePanel.Append(toggle);
	}

	private void AppendTopSidePanel(MachineUILayout.SatellitePanelSpec spec)
	{
		const int Padding = 6, LabelHeight = 11;
		const float Gap = 6f;
		bool hasTitle = !string.IsNullOrEmpty(spec.Title);
		int innerTop = Padding + (hasTitle ? LabelHeight + 2 : 0);
		int outerW = spec.Width + Padding * 2;
		int outerH = innerTop + spec.Height + Padding;

		var (machineLeft, machineTop, machineW) = MachinePanelScreenAnchor();
		float left = machineLeft + (machineW - outerW) / 2f;
		float top = System.Math.Max(8f, machineTop - outerH - Gap);

		var sidePanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW),
			Height = StyleDimension.FromPixels(outerH),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(left),
			Top  = StyleDimension.FromPixels(top),
		};

		if (hasTitle)
			sidePanel.Append(new UIText(spec.Title, 0.6f)
			{
				Left = StyleDimension.FromPixels(Padding),
				Top  = StyleDimension.FromPixels(Padding),
			});

		var el = spec.Element;
		el.Left   = StyleDimension.FromPixels(Padding);
		el.Top    = StyleDimension.FromPixels(innerTop);
		el.Width  = StyleDimension.FromPixels(spec.Width);
		el.Height = StyleDimension.FromPixels(spec.Height);
		sidePanel.Append(el);

		Append(sidePanel);
	}

	private void AppendBottomSidePanel(MachineUILayout.SatellitePanelSpec spec)
	{
		const int Padding = 6, LabelHeight = 11;
		const float Gap = 6f;
		bool hasTitle = !string.IsNullOrEmpty(spec.Title);
		int innerTop = Padding + (hasTitle ? LabelHeight + 2 : 0);
		int outerW = spec.Width + Padding * 2;
		int outerH = innerTop + spec.Height + Padding;

		float s = _layout!.Scale;
		var (machineLeft, machineTop, machineW) = MachinePanelScreenAnchor();
		float machineH = _layout.Height * s;
		float left = machineLeft + (machineW - outerW) / 2f;
		float top = machineTop + machineH + Gap;

		var sidePanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW),
			Height = StyleDimension.FromPixels(outerH),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(left),
			Top  = StyleDimension.FromPixels(top),
		};

		if (hasTitle)
			sidePanel.Append(new UIText(spec.Title, 0.6f)
			{
				Left = StyleDimension.FromPixels(Padding),
				Top  = StyleDimension.FromPixels(Padding),
			});

		var el = spec.Element;
		el.Left   = StyleDimension.FromPixels(Padding);
		el.Top    = StyleDimension.FromPixels(innerTop);
		el.Width  = StyleDimension.FromPixels(spec.Width);
		el.Height = StyleDimension.FromPixels(spec.Height);
		sidePanel.Append(el);

		Append(sidePanel);
	}

	private void AppendLeftSidePanel(MachineUILayout.SatellitePanelSpec spec)
	{
		const int Padding = 6, LabelHeight = 11;
		const float Gap = 6f;
		bool hasTitle = !string.IsNullOrEmpty(spec.Title);
		int innerTop = Padding + (hasTitle ? LabelHeight + 2 : 0);
		int outerW = spec.Width + Padding * 2;
		int outerH = innerTop + spec.Height + Padding;

		var (machineLeft, machineTop, _) = MachinePanelScreenAnchor();

		var sidePanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW),
			Height = StyleDimension.FromPixels(outerH),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(machineLeft - outerW - Gap),
			Top  = StyleDimension.FromPixels(machineTop + _leftStackOffsetPx),
		};

		if (hasTitle)
			sidePanel.Append(new UIText(spec.Title, 0.6f)
			{
				Left = StyleDimension.FromPixels(Padding),
				Top  = StyleDimension.FromPixels(Padding),
			});

		var el = spec.Element;
		el.Left   = StyleDimension.FromPixels(Padding);
		el.Top    = StyleDimension.FromPixels(innerTop);
		el.Width  = StyleDimension.FromPixels(spec.Width);
		el.Height = StyleDimension.FromPixels(spec.Height);
		sidePanel.Append(el);

		Append(sidePanel);
		_leftStackOffsetPx += outerH + Gap;
	}


	private void AppendCoverPanelLeft(MetaMachine entity)
	{
		float s = _layout!.Scale;
		const int ClusterSize = UIDirectionSelector.ClusterSize; // 54
		const int LabelHeight = 9;
		const int Padding = 4;
		int outerW = ClusterSize + Padding * 2;
		int outerH = LabelHeight + ClusterSize + Padding * 2 + 2;
		const float Gap = 6f;

		var (machineLeft, machineTop, _) = MachinePanelScreenAnchor();

		var coverPanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW * s),
			Height = StyleDimension.FromPixels(outerH * s),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(machineLeft - outerW * s - Gap),
			Top  = StyleDimension.FromPixels(machineTop + _leftStackOffsetPx),
		};

		coverPanel.Append(new UIText("Covers", 0.55f)
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels(Padding * s),
		});

		coverPanel.Append(new UICoverPanel(entity, RequestCoverSettings)
		{
			Left   = StyleDimension.FromPixels(Padding * s),
			Top    = StyleDimension.FromPixels((Padding + LabelHeight + 2) * s),
			Width  = StyleDimension.FromPixels(ClusterSize * s),
			Height = StyleDimension.FromPixels(ClusterSize * s),
		});

		Append(coverPanel);

		_leftStackOffsetPx += outerH * s + Gap;
	}

	private void AppendDistinctTogglePanel(MetaMachine entity, Api.Machine.Feature.Multiblock.IDistinctPart part)
	{
		float s = _layout!.Scale;
		const int BtnSize = 22;
		const int LabelHeight = 9;
		const int Padding = 4;
		int outerW = BtnSize + Padding * 2;
		int outerH = LabelHeight + BtnSize + Padding * 2 + 2;
		const float Gap = 6f;

		var (machineLeft, machineTop, _) = MachinePanelScreenAnchor();

		var distinctPanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW * s),
			Height = StyleDimension.FromPixels(outerH * s),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(machineLeft - outerW * s - Gap),
			Top  = StyleDimension.FromPixels(machineTop + _leftStackOffsetPx),
		};
		Append(distinctPanel);
		_leftStackOffsetPx += outerH * s + Gap;

		var label = new UIText("Distinct", 0.55f)
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels(Padding * s),
		};
		distinctPanel.Append(label);

		var btn = new Widgets.UIToggleButton(
			"GregTechCEuTerraria/Content/Textures/gui/widget/button_distinct_buses",
			() => part.IsDistinct(),
			v => TerrariaCompat.Net.Actions.MachineActions.Send(new TerrariaCompat.Net.Actions.DistinctSetAction(v), entity),
			"")
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels((Padding + LabelHeight + 2) * s),
			Width  = StyleDimension.FromPixels(BtnSize * s),
			Height = StyleDimension.FromPixels(BtnSize * s),
		};
		btn.IconSrcRectFor = on =>
		{
			var tex = ModContent.Request<Texture2D>(
				"GregTechCEuTerraria/Content/Textures/gui/widget/button_distinct_buses",
				ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
			int half = tex.Height / 2;
			return on
				? new Rectangle(0, 0,    tex.Width, half)
				: new Rectangle(0, half, tex.Width, half);
		};
		btn.TooltipFor = on => "Distinct Buses: " + (on ? "Yes" : "No");
		distinctPanel.Append(btn);
	}

	private void AppendModeSelectPanel(
		TerrariaCompat.Machine.Multiblock.WorkableMultiblockMachine multi,
		UITerrariaPanel machinePanel)
	{
		float s = _layout!.Scale;
		var recipeTypes = multi.GetRecipeTypes();
		const int RowH    = 16;
		const int RowGap  = 2;
		const int LabelH  = 9;
		const int Padding = 4;
		const int InnerW  = 86;
		int innerH  = LabelH + 2 + recipeTypes.Length * RowH + (recipeTypes.Length - 1) * RowGap;
		int outerW  = InnerW + Padding * 2;
		int outerH  = innerH + Padding * 2;
		const float Gap = 6f;

		var (machineLeft, machineTop, _) = MachinePanelScreenAnchor();

		var modePanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW * s),
			Height = StyleDimension.FromPixels(outerH * s),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(machineLeft - outerW * s - Gap),
			Top  = StyleDimension.FromPixels(machineTop + _leftStackOffsetPx),
		};
		Append(modePanel);
		_leftStackOffsetPx += outerH * s + Gap;

		var label = new UIText("Mode", 0.55f)
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels(Padding * s),
		};
		modePanel.Append(label);

		int rowsTop = Padding + LabelH + 2;
		for (int i = 0; i < recipeTypes.Length; i++)
		{
			int captured = i;
			string typeName = recipeTypes[i].RegistryName;
			var btn = new Widgets.UITextButton(
				() => multi.ActiveRecipeType == captured
					? $"[c/55FFFF:{typeName}]" : typeName,
				onLeft: () => TerrariaCompat.Net.Actions.MachineActions.Send(
					new TerrariaCompat.Net.Actions.ActiveRecipeTypeSetAction(captured), multi),
				tooltip: $"Switch to {typeName}",
				width: (int)(InnerW * s),
				height: (int)(RowH * s))
			{
				Left = StyleDimension.FromPixels(Padding * s),
				Top  = StyleDimension.FromPixels((rowsTop + captured * (RowH + RowGap)) * s),
			};
			modePanel.Append(btn);
		}
	}

	private (float Left, float Top, float MachineWidth) MachinePanelScreenAnchor()
	{
		float s = _layout!.Scale;
		float uiScale = Main.UIScale <= 0 ? 1f : Main.UIScale;
		float uiW = Main.screenWidth / uiScale;
		float uiH = Main.screenHeight / uiScale;
		float machineW = _layout.Width * s;
		float machineH = _layout.Height * s;
		float machineLeft = 0.5f * (uiW - machineW);
		float machineTop  = 0.3f * (uiH - machineH);
		return (machineLeft, machineTop, machineW);
	}

	private void AppendChargerSlot(MetaMachine entity, UITerrariaPanel machinePanel)
	{
		var slots = entity.GetSlotGroup(Machine.SlotGroup.Charger);
		if (slots is null) return;
		float s = _layout!.Scale;
		const int SlotSize = 22;
		const int LabelHeight = 9;
		const int Padding = 4;
		int outerW = SlotSize + Padding * 2;
		int outerH = LabelHeight + SlotSize + Padding * 2 + 2;
		const float Gap = 6f;
		var (machineLeft, machineTop, machineW) = MachinePanelScreenAnchor();
		var chargerPanel = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(outerW * s),
			Height = StyleDimension.FromPixels(outerH * s),
			HAlign = 0f,
			VAlign = 0f,
			Left = StyleDimension.FromPixels(machineLeft + machineW + Gap),
			Top  = StyleDimension.FromPixels(machineTop),
		};
		Append(chargerPanel);

		var label = new UIText("Charge", 0.55f)
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels(Padding * s),
		};
		chargerPanel.Append(label);
		var bgTex = ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(
			"GregTechCEuTerraria/Content/Textures/gui/overlay/charger_slot_overlay");
		var bg = new UIImage(bgTex)
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels((Padding + LabelHeight + 2) * s),
			Width  = StyleDimension.FromPixels(SlotSize * s),
			Height = StyleDimension.FromPixels(SlotSize * s),
			ScaleToFit = true,
			AllowResizingDimensions = false,
		};
		chargerPanel.Append(bg);

		var charger = new Widgets.UISlot(entity, Machine.SlotGroup.Charger, slotIndex: 0)
		{
			Left = StyleDimension.FromPixels(Padding * s),
			Top  = StyleDimension.FromPixels((Padding + LabelHeight + 2) * s),
			Width  = StyleDimension.FromPixels(SlotSize * s),
			Height = StyleDimension.FromPixels(SlotSize * s),
		};
		chargerPanel.Append(charger);
	}

	private void AppendRecipeBrowser(IRecipeLogicMachine proc)
	{
		string currentStation = proc.GetRecipeType().RegistryName;
		var allRecipes = RecipeRegistry.ForStation(currentStation)
			.Where(proc.ShowsInRecipeBrowser).ToList();
		bool isMultiMode = proc is TerrariaCompat.Machine.Multiblock.WorkableMultiblockMachine wmmMode
			&& wmmMode.GetRecipeTypes().Length > 1;
		if (allRecipes.Count == 0 && !isMultiMode) return;

		float uiScale = Main.UIScale <= 0 ? 1f : Main.UIScale;
		float uiW = Main.screenWidth / uiScale;
		float uiH = Main.screenHeight / uiScale;
		float browserW = System.Math.Clamp(uiW * 0.28f, 380f, 560f);
		float browserTop = uiH * 0.08f;
		float browserHalfH = (uiH * 0.78f - 8f) / 2f;

		ModLoader.GetMod("GregTechCEuTerraria").Logger.Info(
			$"[recipe-browser] open for {proc.GetRecipeType().RegistryName} (recipes={allRecipes.Count}) " +
			$"uiW={uiW:F0} uiH={uiH:F0} panelW={browserW:F0} halfH={browserHalfH:F0}");

		string[] queryTokens = System.Array.Empty<string>();
		List<GTRecipe> searchFiltered = allRecipes;
		void ApplySearchFilter()
		{
			if (queryTokens.Length == 0) { searchFiltered = allRecipes; return; }
			searchFiltered = new List<GTRecipe>();
			foreach (var r in allRecipes)
				if (RecipeSearch.Matches(r, queryTokens)) searchFiltered.Add(r);

			if (searchFiltered.Count > 1)
			{
				var ranks = new int[searchFiltered.Count];
				for (int i = 0; i < searchFiltered.Count; i++)
					ranks[i] = RecipeSearch.MatchesOutputs(searchFiltered[i], queryTokens) ? 0 : 1;
				for (int i = 1; i < searchFiltered.Count; i++)
				{
					var r = searchFiltered[i];
					int rank = ranks[i];
					int j = i - 1;
					while (j >= 0 && ranks[j] > rank)
					{
						searchFiltered[j + 1] = searchFiltered[j];
						ranks[j + 1] = ranks[j];
						j--;
					}
					searchFiltered[j + 1] = r;
					ranks[j + 1] = rank;
				}
			}
		}
		void OnSearchChanged(string text)
		{
			queryTokens = RecipeSearch.Tokenize(text);
			ApplySearchFilter();
		}
		bool RebuildIfStationChanged()
		{
			string s = proc.GetRecipeType().RegistryName;
			if (s == currentStation) return false;
			currentStation = s;
			allRecipes = RecipeRegistry.ForStation(s).Where(proc.ShowsInRecipeBrowser).ToList();
			ApplySearchFilter();
			return true;
		}

		var allPanel = BuildBrowserPanelWithSearch(
			browserTop, browserW, browserHalfH,
			countLabel: () => $"{searchFiltered.Count} / {allRecipes.Count}  *  {proc.GetRecipeType().RegistryName}",
			list: new UIRecipeList(() => { RebuildIfStationChanged(); return searchFiltered; }, emptyHint: "No recipes match this search"),
			searchPlaceholder: "Search...  *  RMB to clear",
			onSearchChanged: OnSearchChanged);
		Append(allPanel);
		HoverItemTracker.Kind cachedKind = HoverItemTracker.Kind.None;
		int cachedType = -1;
		string? cachedFluid = null;
		List<GTRecipe>? cachedFiltered = null;
		IReadOnlyList<GTRecipe> FilteredSource()
		{
			bool stationChanged = RebuildIfStationChanged();
			var kind = HoverItemTracker.LastKind;
			int t = HoverItemTracker.LastHoveredItemType;
			string? fid = HoverItemTracker.LastHoveredFluidId;

			bool sameAsCache = !stationChanged
				&& kind == cachedKind
				&& (kind != HoverItemTracker.Kind.Item || t == cachedType)
				&& (kind != HoverItemTracker.Kind.Fluid || fid == cachedFluid);
			if (sameAsCache && cachedFiltered != null) return cachedFiltered;

			cachedKind = kind;
			cachedType = t;
			cachedFluid = fid;
			cachedFiltered = new List<GTRecipe>();

			switch (kind)
			{
				case HoverItemTracker.Kind.Item when t > 0:
					foreach (var r in allRecipes)
						if (RecipeRowRenderer.ItemTypesInRecipe(r).Contains(t))
							cachedFiltered.Add(r);
					break;
				case HoverItemTracker.Kind.Fluid when !string.IsNullOrEmpty(fid):
					foreach (var r in allRecipes)
						if (RecipeRowRenderer.FluidIdsInRecipe(r).Contains(fid!))
							cachedFiltered.Add(r);
					break;
			}
			return cachedFiltered;
		}

		var hoverPanel = BuildBrowserPanel(
			browserTop + browserHalfH + 8f, browserW, browserHalfH,
			title: "Hover item to see relevant recipes",
			list: new UIRecipeList(FilteredSource, emptyHint: "Hover an item anywhere to filter"));
		Append(hoverPanel);
	}

	private static UIElement BuildBrowserPanel(float y, float w, float h, string title, UIRecipeList list)
	{
		var panel = new UITerrariaPanel
		{
			HAlign = 1f,
			Left = StyleDimension.FromPixels(-8f),
			Top = StyleDimension.FromPixels(y),
			Width = StyleDimension.FromPixels(w),
			Height = StyleDimension.FromPixels(h),
		};

		var titleText = new UIText(title, 0.85f)
		{
			Left = StyleDimension.FromPixels(8),
			Top = StyleDimension.FromPixels(6),
		};
		panel.Append(titleText);

		list.Left = StyleDimension.FromPixels(4);
		list.Top = StyleDimension.FromPixels(28);
		list.Width = StyleDimension.FromPixels(w - 8);
		list.Height = StyleDimension.FromPixels(h - 32);
		panel.Append(list);
		return panel;
	}

	private static UIElement BuildBrowserPanelWithSearch(
		float y, float w, float h,
		System.Func<string> countLabel,
		UIRecipeList list,
		string searchPlaceholder,
		System.Action<string> onSearchChanged)
	{
		var panel = new UITerrariaPanel
		{
			HAlign = 1f,
			Left = StyleDimension.FromPixels(-8f),
			Top = StyleDimension.FromPixels(y),
			Width = StyleDimension.FromPixels(w),
			Height = StyleDimension.FromPixels(h),
		};

		const int SearchH    = 22;
		const int CountW     = 110;
		const int HeaderPad  = 6;

		var search = new Widgets.UISearchBar(searchPlaceholder, onSearchChanged)
		{
			Left = StyleDimension.FromPixels(HeaderPad),
			Top  = StyleDimension.FromPixels(HeaderPad),
			Width = StyleDimension.FromPixels(w - HeaderPad * 2 - CountW - 6),
			Height = StyleDimension.FromPixels(SearchH),
		};
		panel.Append(search);
		list.OnStationFilter = s => search.SetText("@" + s);

		var count = new Widgets.UIDynamicLabel(countLabel, 0.75f)
		{
			HAlign = 1f,
			Left   = StyleDimension.FromPixels(-HeaderPad),
			Top    = StyleDimension.FromPixels(HeaderPad + 3),
			Width  = StyleDimension.FromPixels(CountW),
			Height = StyleDimension.FromPixels(SearchH),
		};
		panel.Append(count);

		list.Left = StyleDimension.FromPixels(4);
		list.Top  = StyleDimension.FromPixels(HeaderPad + SearchH + 4);
		list.Width  = StyleDimension.FromPixels(w - 8);
		list.Height = StyleDimension.FromPixels(h - (HeaderPad + SearchH + 8));
		panel.Append(list);
		return panel;
	}
}
