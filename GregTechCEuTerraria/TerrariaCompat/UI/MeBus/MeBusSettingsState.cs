#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using GregTechCEuTerraria.TerrariaCompat.UI.Widgets;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.MeBus;

public sealed class MeBusSettingsState : UIModalWindow
{
	public int CellX { get; private set; }
	public int CellY { get; private set; }

	private UIElement? _outer;
	private readonly SideSignature[] _builtSig = new SideSignature[4];

	private readonly struct SideSignature : System.IEquatable<SideSignature>
	{
		public readonly bool Cable;
		public readonly bool Inventory;
		public readonly MeBusKind Kind;
		public SideSignature(bool cable, bool inv, MeBusKind kind) { Cable = cable; Inventory = inv; Kind = kind; }
		public bool Equals(SideSignature o) => Cable == o.Cable && Inventory == o.Inventory && Kind == o.Kind;
		public override bool Equals(object? obj) => obj is SideSignature s && Equals(s);
		public override int GetHashCode() => System.HashCode.Combine(Cable, Inventory, (int)Kind);
		public static bool operator ==(SideSignature a, SideSignature b) => a.Equals(b);
		public static bool operator !=(SideSignature a, SideSignature b) => !a.Equals(b);
	}

	public void Bind(int x, int y)
	{
		CellX = x;
		CellY = y;
		Rebuild();
	}

	public void Unbind()
	{
		RemoveAllChildren();
		_outer = null;
		for (int i = 0; i < 4; i++) _builtSig[i] = default;
	}

	public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
	{
		base.Update(gameTime);
		if (Main.mouseLeft || Main.mouseRight) return;
		if (!NeedsRebuild()) return;
		Rebuild();
	}

	private bool NeedsRebuild()
	{
		for (int i = 0; i < 4; i++)
			if (CaptureSignature(MeBusLayer.SideFromIndex(i)) != _builtSig[i]) return true;
		return false;
	}

	private SideSignature CaptureSignature(IODirection side) =>
		new(CableConnected(side), HasInventory(side), KindAt(side));

	private const int CellW = 280;
	private const int CellH = 250;
	private const int CellPad = 8;

	private void Rebuild()
	{
		RemoveAllChildren();

		int outerW = CellW * 3 + CellPad * 4;
		int outerH = CellH * 3 + CellPad * 4;

		_outer = new PlusHitElement
		{
			HAlign = 0.5f,
			VAlign = 0.5f,
			Width  = StyleDimension.FromPixels(outerW),
			Height = StyleDimension.FromPixels(outerH),
		};

		PlaceSideCell(IODirection.Up,    col: 1, row: 0);
		PlaceSideCell(IODirection.Left,  col: 0, row: 1);
		PlaceCenterCell(                 col: 1, row: 1);
		PlaceSideCell(IODirection.Right, col: 2, row: 1);
		PlaceSideCell(IODirection.Down,  col: 1, row: 2);

		Append(_outer);
		Recalculate();

		for (int i = 0; i < 4; i++)
			_builtSig[i] = CaptureSignature(MeBusLayer.SideFromIndex(i));
	}

	private void PlaceSideCell(IODirection side, int col, int row)
	{
		if (!HasInventory(side) && KindAt(side) == MeBusKind.None) return;

		var cell = BuildSideCell(side);
		cell.Left = StyleDimension.FromPixels(CellPad + col * (CellW + CellPad));
		cell.Top  = StyleDimension.FromPixels(CellPad + row * (CellH + CellPad));
		_outer!.Append(cell);
	}

	private void PlaceCenterCell(int col, int row)
	{
		var cell = BuildCenterCell();
		cell.Left = StyleDimension.FromPixels(CellPad + col * (CellW + CellPad));
		cell.Top  = StyleDimension.FromPixels(CellPad + row * (CellH + CellPad));
		_outer!.Append(cell);
	}

	private UIElement BuildSideCell(IODirection side)
	{
		var cell = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(CellW),
			Height = StyleDimension.FromPixels(CellH),
		};

		cell.Append(new UIText($"{SideWord(side)}: {NeighbourName(side)}", 0.75f)
		{
			Left = StyleDimension.FromPixels(6),
			Top  = StyleDimension.FromPixels(4),
		});

		const int btnH = 18, btnGap = 4, rowX = 6, rowY = 26;
		int btnW = (CellW - rowX * 2 - btnGap * 3) / 4;
		AppendKindButton(cell, side, MeBusKind.None,    "None",    rowX + (btnW + btnGap) * 0, rowY, btnW, btnH);
		AppendKindButton(cell, side, MeBusKind.Storage, "Storage", rowX + (btnW + btnGap) * 1, rowY, btnW, btnH);
		AppendKindButton(cell, side, MeBusKind.Import,  "Import",  rowX + (btnW + btnGap) * 2, rowY, btnW, btnH);
		AppendKindButton(cell, side, MeBusKind.Export,  "Export",  rowX + (btnW + btnGap) * 3, rowY, btnW, btnH);

		var k = KindAt(side);
		if (k == MeBusKind.Storage)
			BuildStorageZone(cell, side, x: 6, y: 52, w: CellW - 12);
		else if (k == MeBusKind.Import || k == MeBusKind.Export)
			BuildFilterZone(cell, side, x: 6, y: 52, w: CellW - 12, kind: k);

		return cell;
	}

	private int AppendFilterGrid(UIElement cell, IODirection side, int x, int y, string label)
	{
		cell.Append(new UIText(label, 0.6f)
		{ Left = StyleDimension.FromPixels(x), Top = StyleDimension.FromPixels(y) });

		const int Slot = 26, Gap = 2, PerRow = 9;
		int gridY = y + 16;
		for (int i = 0; i < MeBusAttachment.FilterSize; i++)
		{
			int gx = x + (i % PerRow) * (Slot + Gap);
			int gy = gridY + (i / PerRow) * (Slot + Gap);
			cell.Append(new MeBusFilterSlot(this, side, i, Slot)
			{ Left = StyleDimension.FromPixels(gx), Top = StyleDimension.FromPixels(gy) });
		}
		int rows = (MeBusAttachment.FilterSize + PerRow - 1) / PerRow;
		return gridY + rows * (Slot + Gap) + 6;
	}

	private void BuildFilterZone(UIElement cell, IODirection side, int x, int y, int w, MeBusKind kind)
	{
		int rowY = AppendFilterGrid(cell, side, x, y,
			kind == MeBusKind.Import ? "Filter (empty = all)" : "Export list");

		const int btnH = 16;
		cell.Append(new UINumericStepper("Speed",
			() => SpeedAt(side), v => SetSpeed(side, (int)v),
			min: 0, max: MeBusAttachment.MaxSpeed, step: 1, labelWidth: 44)
		{ Left = StyleDimension.FromPixels(x), Top = StyleDimension.FromPixels(rowY) });
		cell.Append(new UIDynamicLabel(
			() => $"{MeBusAttachment.OperationsForSpeed(SpeedAt(side))} items per tick", 0.65f)
		{ Left = StyleDimension.FromPixels(x + 150), Top = StyleDimension.FromPixels(rowY + 3),
		  Width = StyleDimension.FromPixels(System.Math.Max(40, w - 150)) });

		if (kind == MeBusKind.Export)
		{
			AppendCraftCardRow(cell, side, x, rowY + btnH + 8, w);
			AppendSchedulingRow(cell, side, x, rowY + btnH * 2 + 12, w);
		}
	}

	private void AppendSchedulingRow(UIElement cell, IODirection side, int x, int y, int w)
	{
		cell.Append(new UITextButton(
			label: () => "Order: " + SchedulingAt(side) switch
			{
				MeBusSchedulingMode.RoundRobin => "Round Robin",
				MeBusSchedulingMode.Random     => "Random",
				_                              => "Default",
			},
			onLeft: () => CycleScheduling(side),
			tooltip: "Export list traversal order",
			width: w, height: 16)
		{ Left = StyleDimension.FromPixels(x), Top = StyleDimension.FromPixels(y),
		  IsVisible = () => FilledFilterCount(side) > 1 });
	}

	private int FilledFilterCount(IODirection side)
	{
		var f = MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.Filter;
		if (f is null) return 0;
		int n = 0;
		foreach (var k in f)
			if (k != null) n++;
		return n;
	}

	private MeBusSchedulingMode SchedulingAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.Scheduling ?? MeBusSchedulingMode.Default;

	private void CycleScheduling(IODirection side)
	{
		if (KindAt(side) != MeBusKind.Export) return;
		var a = CloneOrNew(side, MeBusKind.Export);
		a.Scheduling = (MeBusSchedulingMode)(((byte)a.Scheduling + 1) % 3);
		Apply(side, a);
	}

	private void BuildStorageZone(UIElement cell, IODirection side, int x, int y, int w)
	{
		const int btnH = 16, btnGap = 3, rowGap = 6;

		cell.Append(new UIText("Network access", 0.6f)
		{
			Left = StyleDimension.FromPixels(x),
			Top  = StyleDimension.FromPixels(y),
		});

		int rowY = y + 16;
		int abW = (w - btnGap * 2) / 3;
		AppendAccessButton(cell, side, AccessRestriction.READ_WRITE, "Read+Write", x + (abW + btnGap) * 0, rowY, abW, btnH);
		AppendAccessButton(cell, side, AccessRestriction.READ,       "Read Only",  x + (abW + btnGap) * 1, rowY, abW, btnH);
		AppendAccessButton(cell, side, AccessRestriction.WRITE,      "Write Only", x + (abW + btnGap) * 2, rowY, abW, btnH);

		rowY += btnH + rowGap;
		cell.Append(new UINumericStepper("Priority",
			() => PriorityAt(side), v => SetPriority(side, (int)v),
			min: -9999, max: 9999, step: 1, labelWidth: 50)
		{ Left = StyleDimension.FromPixels(x), Top = StyleDimension.FromPixels(rowY) });

		rowY += btnH + rowGap;
		int afterGrid = AppendFilterGrid(cell, side, x, rowY, "Partition (empty = any)");
		AppendStorageToggle(cell, side, x, afterGrid, w, filterExtract: true);
		AppendStorageToggle(cell, side, x, afterGrid + 18, w, filterExtract: false);
	}

	private void AppendStorageToggle(UIElement cell, IODirection side, int x, int y, int w, bool filterExtract)
	{
		cell.Append(new UITextButton(
			label: () => filterExtract
				? "Filter on Extract: " + (FilterOnExtractAt(side) ? "On" : "Off")
				: "Show: " + (ExtractableOnlyAt(side) ? "Extractable" : "All"),
			onLeft: () => ToggleStorageFlag(side, filterExtract),
			tooltip: filterExtract
				? "Whether the partition filter also restricts what the network can extract."
				: "Whether input-only slots are visible to network",
			width: w, height: 16)
		{
			Left = StyleDimension.FromPixels(x),
			Top = StyleDimension.FromPixels(y),
			IsActive = () => filterExtract ? FilterOnExtractAt(side) : !ExtractableOnlyAt(side),
		});
	}

	private bool FilterOnExtractAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.FilterOnExtract ?? true;

	private bool ExtractableOnlyAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.ExtractableOnly ?? false;

	private void ToggleStorageFlag(IODirection side, bool filterExtract)
	{
		if (KindAt(side) != MeBusKind.Storage) return;
		var a = CloneOrNew(side, MeBusKind.Storage);
		if (filterExtract) a.FilterOnExtract = !a.FilterOnExtract;
		else a.ExtractableOnly = !a.ExtractableOnly;
		Apply(side, a);
	}

	private void AppendCraftCardRow(UIElement cell, IODirection side, int x, int y, int w)
	{
		const int btnH = 16;
		cell.Append(new UITextButton(
			label: () => "Request Crafting: " + CraftModeLabel(side),
			onLeft: () => CycleCraftMode(side),
			width: w, height: btnH)
		{
			Left = StyleDimension.FromPixels(x),
			Top  = StyleDimension.FromPixels(y),
			IsActive = () => CraftMissingAt(side),
		});
	}

	private void AppendAccessButton(UIElement cell, IODirection side, AccessRestriction access,
		string label, int x, int y, int w, int h)
	{
		cell.Append(new UITextButton(
			label: () => label,
			onLeft: () => { var a = CloneOrNew(side, MeBusKind.Storage); a.Access = access; Apply(side, a); },
			tooltip: access switch
			{
				AccessRestriction.READ_WRITE => "Network can insert and extract through this inventory",
				AccessRestriction.READ       => "Network can only extract from this inventory",
				AccessRestriction.WRITE      => "Network can only insert into this inventory",
				_                            => "",
			},
			width: w, height: h)
		{
			Left = StyleDimension.FromPixels(x),
			Top  = StyleDimension.FromPixels(y),
			IsActive = () => AccessAt(side) == access,
		});
	}

	private MeBusAttachment CloneOrNew(IODirection side, MeBusKind kind)
	{
		var cur = MeBusLayerSystem.Buses.Get(CellX, CellY, side);
		var att = cur?.Clone() ?? new MeBusAttachment(kind);
		att.Kind = kind;
		return att;
	}

	private void Apply(IODirection side, MeBusAttachment? att) =>
		MeBusPackets.SetSide(CellX, CellY, side, att);

	private void SetPriority(IODirection side, int value)
	{
		var a = CloneOrNew(side, MeBusKind.Storage);
		a.Priority = value;
		Apply(side, a);
	}

	private AccessRestriction AccessAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.Access ?? AccessRestriction.READ_WRITE;

	private int PriorityAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.Priority ?? 0;

	private int SpeedAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.Speed ?? 0;

	private bool CraftMissingAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.CraftMissing ?? false;

	private bool CraftOnlyAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.CraftOnly ?? false;

	private void SetSpeed(IODirection side, int level)
	{
		var kind = KindAt(side);
		if (kind != MeBusKind.Import && kind != MeBusKind.Export) return;
		var a = CloneOrNew(side, kind);
		a.Speed = System.Math.Clamp(level, 0, MeBusAttachment.MaxSpeed);
		Apply(side, a);
	}

	private string CraftModeLabel(IODirection side) =>
		!CraftMissingAt(side) ? "OFF" : CraftOnlyAt(side) ? "Craft Only" : "Craft Missing";

	private void CycleCraftMode(IODirection side)
	{
		var kind = KindAt(side);
		if (kind == MeBusKind.None) return;
		var a = CloneOrNew(side, kind);
		if (!a.CraftMissing) { a.CraftMissing = true; a.CraftOnly = false; }
		else if (!a.CraftOnly) { a.CraftOnly = true; }
		else { a.CraftMissing = false; a.CraftOnly = false; }
		Apply(side, a);
	}

	public AEKey? FilterKeyAt(IODirection side, int slot)
	{
		var f = MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.Filter;
		return f != null && slot >= 0 && slot < f.Length ? f[slot] : null;
	}

	public void SetFilterSlot(IODirection side, int slot, AEKey? key)
	{
		var kind = KindAt(side);
		if (kind == MeBusKind.None) return;
		var a = CloneOrNew(side, kind);
		if (slot >= 0 && slot < a.Filter.Length) a.Filter[slot] = key;
		Apply(side, a);
	}

	private void AppendKindButton(UIElement cell, IODirection side, MeBusKind kind,
		string label, int x, int y, int w, int h)
	{
		cell.Append(new UITextButton(
			label: () => label,
			onLeft: () => Apply(side, kind == MeBusKind.None ? null : CloneOrNew(side, kind)),
			tooltip: kind switch
			{
				MeBusKind.None    => null,
				MeBusKind.Storage => "Expose container contents to the network",
				MeBusKind.Import  => "Pull from container into network",
				MeBusKind.Export  => "Push from container into network",
				_                 => "",
			},
			width: w, height: h)
		{
			Left = StyleDimension.FromPixels(x),
			Top  = StyleDimension.FromPixels(y),
			IsActive = () => KindAt(side) == kind,
		});
	}

	private UIElement BuildCenterCell()
	{
		var cell = new UITerrariaPanel
		{
			Width  = StyleDimension.FromPixels(CellW),
			Height = StyleDimension.FromPixels(CellH),
		};

		cell.Append(new UIText("ME Network", 0.85f)
		{
			HAlign = 0.5f,
			Top    = StyleDimension.FromPixels(6),
		});
		cell.Append(new UIText($"({CellX}, {CellY})", 0.55f)
		{
			HAlign = 0.5f,
			Top    = StyleDimension.FromPixels(26),
		});

		cell.Append(new UIDynamicLabel(() =>
		{
			var net = MeNetworkSystem.NetAt(CellX, CellY);
			return net is null ? "Cables: -" : $"Cables: {net.Cells.Count}";
		}, 0.6f) { Left = StyleDimension.FromPixels(8), Top = StyleDimension.FromPixels(54) });

		cell.Append(new UIDynamicLabel(() =>
		{
			var net = MeNetworkSystem.NetAt(CellX, CellY);
			return net is null ? "Storage devices: -" : $"Storage devices: {net.MountedStorageCount}";
		}, 0.6f) { Left = StyleDimension.FromPixels(8), Top = StyleDimension.FromPixels(74) });

		cell.Append(new UIDynamicLabel(() =>
		{
			var net = MeNetworkSystem.NetAt(CellX, CellY);
			return net is null ? "Providers: -" : $"Providers: {net.Providers.Count}";
		}, 0.6f) { Left = StyleDimension.FromPixels(8), Top = StyleDimension.FromPixels(94) });

		cell.Append(new UIDynamicLabel(() =>
		{
			var net = MeNetworkSystem.NetAt(CellX, CellY);
			return net is null ? "Interfaces: -" : $"Interfaces: {net.InterfaceCount}";
		}, 0.6f) { Left = StyleDimension.FromPixels(8), Top = StyleDimension.FromPixels(114) });

		cell.Append(new UIDynamicLabel(() =>
		{
			var net = MeNetworkSystem.NetAt(CellX, CellY);
			return net is null ? "Types: -" : $"Types: {net.GetStorage().GetAvailableStacks().Size()}";
		}, 0.6f) { Left = StyleDimension.FromPixels(8), Top = StyleDimension.FromPixels(134) });

		return cell;
	}

	private bool CableConnected(IODirection side)
	{
		var (dx, dy) = side.Offset();
		return MeCableLayerSystem.Cables.Connects(CellX, CellY, CellX + dx, CellY + dy);
	}

	private bool HasInventory(IODirection side)
	{
		var (dx, dy) = side.Offset();
		return WorldCapability.HasInventoryAt(CellX + dx, CellY + dy, side.Opposite());
	}

	private MeBusKind KindAt(IODirection side) =>
		MeBusLayerSystem.Buses.Get(CellX, CellY, side)?.Kind ?? MeBusKind.None;

	private string NeighbourName(IODirection side)
	{
		var (dx, dy) = side.Offset();
		return WorldCapability.TileDisplayName(CellX + dx, CellY + dy);
	}

	private static string SideWord(IODirection d) => d switch
	{
		IODirection.Up => "Up", IODirection.Down => "Down",
		IODirection.Left => "Left", IODirection.Right => "Right", _ => "?",
	};

	private sealed class PlusHitElement : UIElement
	{
		public override bool ContainsPoint(Microsoft.Xna.Framework.Vector2 point)
		{
			if (!base.ContainsPoint(point)) return false;
			foreach (var child in Children)
				if (child.ContainsPoint(point)) return true;
			return false;
		}
	}
}
