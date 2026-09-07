#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

namespace GregTechCEuTerraria.TerrariaCompat.UI.Layouts;

public static class FluidHatchLayout
{
	private const int SlotSize = 22;
	private const int Padding  = 12;
	private const int TitleH   = 14;

	public static MachineUILayout Build(FluidHatchPartMachine hatch)
	{
		int slots = hatch.Tank?.Storages.Length ?? 1;
		bool showCircuit = hatch.Io == IO.IN
			&& hatch is IHasCircuitSlot icc && icc.IsCircuitSlotEnabled()
			&& hatch.CircuitInventory != null && hatch.CircuitInventory.SlotCount > 0;
		return slots == 1 ? BuildSingle(hatch, showCircuit) : BuildGrid(hatch, slots, showCircuit);
	}

	private static int EmitCircuitSlot(MachineUILayout layout, FluidHatchPartMachine hatch, bool show, int contentRightX, int contentBaseY, int contentHeight)
	{
		if (!show) return 0;
		int circuitX = contentRightX + Padding;
		int circuitY = contentBaseY + System.Math.Max(0, (contentHeight - SlotSize) / 2);
		layout.Widgets.Add(new CircuitButtonWidgetSpec(X: circuitX, Y: circuitY));
		return Padding + SlotSize;
	}

	private static MachineUILayout BuildSingle(FluidHatchPartMachine hatch, bool showCircuit)
	{
		const int TankW = 18;
		const int TankH = 60;
		const int ReadoutW = 110;
		const int LockRowDy   = 42;
		const int LockSlotSize = 22;
		const int LockButtonSize = 18;

		bool showLock = hatch.Io == IO.OUT;

		int contentW = ReadoutW + 6 + TankW;
		int contentH = showLock ? System.Math.Max(TankH, LockRowDy + LockSlotSize) : TankH;
		int circuitW = showCircuit ? (Padding + SlotSize) : 0;

		var layout = new MachineUILayout
		{
			Width  = Padding + contentW + circuitW + Padding,
			Height = Padding + TitleH + contentH + Padding,
			Title  = hatch.DisplayName,
		};

		int readoutX = Padding;
		int tankX    = Padding + ReadoutW + 6;
		int baseY    = Padding + TitleH;

		layout.Widgets.Add(new LabelWidgetSpec(X: readoutX, Y: baseY + 2,  Text: "Fluid Amount", Scale: 0.8f));
		layout.Widgets.Add(new DynamicLabelWidgetSpec(X: readoutX, Y: baseY + 16, Getter: () =>
		{
			var t = hatch.Tank?.Storages[0];
			if (t is null) return "0 / 0 mB";
			return $"{t.Fluid.Amount:N0} / {t.Capacity:N0} mB";
		}));
		layout.Widgets.Add(new DynamicLabelWidgetSpec(X: readoutX, Y: baseY + 30, Getter: () =>
		{
			var tank = hatch.Tank;
			if (tank is null) return "(no fluid)";
			var stored = tank.Storages[0].Fluid;
			if (!stored.IsEmpty) return stored.Type!.DisplayName;
			return tank.LockedFluid.Fluid.Type?.DisplayName ?? "(no fluid)";
		}));

		layout.Widgets.Add(new FluidSlotWidgetSpec(X: tankX, Y: baseY,
			Width: TankW, Height: TankH,
			Direction: hatch.Io, TankIndex: 0));

		if (showLock)
		{
			layout.Widgets.Add(new ToggleButtonWidgetSpec(
				X: readoutX,
				Y: baseY + LockRowDy + (LockSlotSize - LockButtonSize) / 2,
				IconAssetPath: "GregTechCEuTerraria/Content/Textures/gui/widget/button_lock",
				Getter: () => hatch.Tank?.IsLocked ?? false,
				Setter: v => TerrariaCompat.Net.Actions.MachineActions.Send(
					TerrariaCompat.Net.Actions.FluidLockSetAction.Toggle(v), hatch),
				Tooltip: "Fluid Locking Disabled")
			{
				TooltipFor = on => on ? "Fluid Locking Enabled" : "Fluid Locking Disabled",
			});

			layout.Widgets.Add(new CreativeSourceFluidSlotWidgetSpec(
				X: readoutX + LockButtonSize + 6,
				Y: baseY + LockRowDy,
				Getter: () => hatch.Tank?.LockedFluid.Fluid.Type,
				Setter: t => TerrariaCompat.Net.Actions.MachineActions.Send(
					TerrariaCompat.Net.Actions.FluidLockSetAction.Phantom(t), hatch)));
		}

		EmitCircuitSlot(layout, hatch,
			show: showCircuit,
			contentRightX: tankX + TankW,
			contentBaseY: baseY,
			contentHeight: contentH);

		return layout;
	}

	private static MachineUILayout BuildGrid(FluidHatchPartMachine hatch, int slots, bool showCircuit)
	{
		int rows = (int)System.Math.Sqrt(slots);
		int cols = rows == 0 ? 0 : (slots + rows - 1) / rows;

		int stripW = cols * SlotSize;
		int stripH = rows * SlotSize;
		int circuitW = showCircuit ? (Padding + SlotSize) : 0;

		var layout = new MachineUILayout
		{
			Width  = Padding + stripW + circuitW + Padding,
			Height = Padding + TitleH + stripH + Padding,
			Title  = hatch.DisplayName,
		};

		int baseY = Padding + TitleH;

		for (int idx = 0; idx < slots; idx++)
		{
			int c = idx % cols, r = idx / cols;
			layout.Widgets.Add(new FluidSlotWidgetSpec(
				X: Padding + c * SlotSize,
				Y: baseY + r * SlotSize,
				Width: SlotSize, Height: SlotSize,
				Direction: hatch.Io,
				TankIndex: idx));
		}

		EmitCircuitSlot(layout, hatch,
			show: showCircuit,
			contentRightX: Padding + stripW,
			contentBaseY: baseY,
			contentHeight: stripH);

		return layout;
	}
}
