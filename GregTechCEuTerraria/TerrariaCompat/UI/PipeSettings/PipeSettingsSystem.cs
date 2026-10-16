#nullable enable
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Cover;
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Fluid;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.ItemPipe;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace GregTechCEuTerraria.TerrariaCompat.UI.PipeSettings;

public sealed class PipeSettingsSystem : ModalUISystem
{
	private PipeSettingsState? _state;

	private const string LayerNameStr = "GregTechCEuTerraria: Pipe Settings";
	protected override string LayerName => LayerNameStr;
	protected override bool CloseOnEscape => false;

	public override void Load()
	{
		base.Load();
		if (!Main.dedServ)
		{
			_state = new PipeSettingsState();
			_state.Activate();
		}
	}

	public override void Unload()
	{
		_state = null;
		base.Unload();
	}

	public static bool IsOpen
		=> ModContent.GetInstance<PipeSettingsSystem>()?.IsOpenInternal ?? false;

	public static void OpenFor(int x, int y, PipeKind layer)
	{
		var sys = ModContent.GetInstance<PipeSettingsSystem>();
		if (sys?.Ui is null || sys._state is null) return;
		ModUIRegistry.OnOpen(Close);
		sys._state.Bind(x, y, layer);
		sys.Ui.SetState(sys._state);
		sys.PushModal();
		Main.playerInventory = true;
		SoundEngine.PlaySound(SoundID.MenuOpen);
	}

	public static void Close() => ModContent.GetInstance<PipeSettingsSystem>()?.CloseInternal();

	public static bool IsOpenable(int x, int y, PipeKind kind)
	{
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1) return false;
		return kind switch
		{
			PipeKind.Item  => ItemPipeLayerSystem .Pipes.Has(x, y) && PipeNeighborProbe.HasAnyLive(x, y, PipeKind.Item),
			PipeKind.Fluid => FluidPipeLayerSystem.Pipes.Has(x, y) && PipeNeighborProbe.HasAnyLive(x, y, PipeKind.Fluid),
			_              => false,
		};
	}

	protected override void OnClose()
	{
		_state?.Unbind();
		ModUIRegistry.OnClose(Close);
		SoundEngine.PlaySound(SoundID.MenuClose);
	}

	protected override bool ShouldAutoClose()
	{
		if (_state is null) return false;
		bool stillThere = _state.Layer == PipeKind.Fluid
			? FluidPipeLayerSystem.Pipes.Has(_state.PipeX, _state.PipeY)
			: ItemPipeLayerSystem.Pipes.Has(_state.PipeX, _state.PipeY);
		if (!stillThere) return true;
		return !Main.LocalPlayer.IsInTileInteractionRange(
			_state.PipeX, _state.PipeY, TileReachCheckSettings.Simple);
	}

	private static int _hoverX, _hoverY;
	private static PipeKind? _hoverLayer;
	private static bool _hoverHasOpenable;

	private static bool IsLayerAffectingItem(Item item)
	{
		if (item.IsAir) return false;
		if (item.ModItem is Items.Cables.WireItem) return true;
		if (item.ModItem is Items.Pipes.PipeItem)  return true;
		if (item.ModItem is Items.Pipes.SimpleItemPipeItem)  return true;
		if (item.ModItem is Items.Pipes.SimpleFluidPipeItem) return true;
		if (item.ModItem is Items.Pipes.LaserPipeItem)       return true;
		if (item.ModItem is Items.Tools.ToolItem tool && tool.IsWireCutter) return true;
		return false;
	}

	public override void PostUpdateInput()
	{
		_hoverLayer = null;
		if (Main.dedServ) return;
		base.PostUpdateInput();

		var p = Main.LocalPlayer;
		if (p is null) return;
		if (IsLayerAffectingItem(p.HeldItem))
		{
			if (Main.mouseRight)
			{
				p.controlUseTile  = false;
				p.releaseUseTile  = false;
			}
			return;
		}

		if (UILayers.IsCursorOverAnyModal()) return;

		WorldInteract.WorldCursor.RawCell(out int rawX, out int rawY);

		int x, y;
		bool TryPipeAt(int tx, int ty, out PipeKind k)
		{
			if (tx <= 0 || tx >= Main.maxTilesX - 1 || ty <= 0 || ty >= Main.maxTilesY - 1) { k = default; return false; }
			if (ItemPipeLayerSystem .Pipes.Has(tx, ty)) { k = PipeKind.Item;  return true; }
			if (FluidPipeLayerSystem.Pipes.Has(tx, ty)) { k = PipeKind.Fluid; return true; }
			if (Pipelike.Laser.LaserPipeLayerSystem.Pipes.Has(tx, ty)) { k = PipeKind.Laser; return true; }
			if (Pipelike.Optical.OpticalPipeLayerSystem.Pipes.Has(tx, ty)) { k = PipeKind.Optical; return true; }
			k = default; return false;
		}
		PipeKind? layer;
		if (TryPipeAt(rawX, rawY, out var k1)) { x = rawX; y = rawY; layer = k1; }
		else if (TryPipeAt(Player.tileTargetX, Player.tileTargetY, out var k2))
		{
			x = Player.tileTargetX; y = Player.tileTargetY; layer = k2;
		}
		else return;

		if (!p.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple)) return;

		bool hasLiveNeighbour = layer.Value != PipeKind.Laser && layer.Value != PipeKind.Optical
			&& PipeNeighborProbe.HasAnyLive(x, y, layer.Value);

		bool sameAsBound = IsOpen && _state is not null
			&& _state.PipeX == x && _state.PipeY == y && _state.Layer == layer.Value;
		if (!sameAsBound) { _hoverX = x; _hoverY = y; _hoverLayer = layer; _hoverHasOpenable = hasLiveNeighbour; }
	}

	public override void UpdateUI(GameTime gameTime)
	{
		base.UpdateUI(gameTime);

		if (_hoverLayer is not null)
		{
			WorldHoverTooltip.Set(BuildHoverTooltip(_hoverLayer.Value, _hoverX, _hoverY));
			if (_hoverHasOpenable)
				WorldHoverTooltip.SetHighlight(_hoverX, _hoverY, new Color(255, 220, 60, 200));
		}
	}

	private static string BuildHoverTooltip(PipeKind layer, int x, int y)
	{
		string contents = layer switch
		{
			PipeKind.Fluid   => FluidContentsLine(x, y),
			PipeKind.Laser   => LaserStatusLine(x, y),
			PipeKind.Optical => OpticalStatusLine(x, y),
			_                => ItemThroughputLine(x, y),
		};
		string netLine = NetIdLine(layer, x, y);
		var sb = new System.Text.StringBuilder();
		if (!string.IsNullOrEmpty(contents)) { sb.Append(contents); sb.Append('\n'); }
		else if (layer == PipeKind.Fluid)    { sb.Append("Pipe: empty\n"); }
		if (!string.IsNullOrEmpty(netLine))  { sb.Append(netLine);  sb.Append('\n'); }
		string warn = EmptyWhitelistWarningLine(layer, x, y);
		if (!string.IsNullOrEmpty(warn))     { sb.Append(warn);     sb.Append('\n'); }
		if (_hoverHasOpenable) sb.Append("RMB to setup the pipe");
		else                   { if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length--; }
		return sb.ToString();
	}

	private static string LaserStatusLine(int x, int y)
	{
		bool active = Pipelike.Laser.LaserPipeLayerSystem.IsActive(x, y);
		var net = Pipelike.Laser.LaserPipeNetSystem.Level.GetNetFromPos((x, y));
		bool hasEndpoint = false;
		if (net is not null)
		{
			foreach (var side in new[]
				{ IODirection.Up, IODirection.Down,
				  IODirection.Left, IODirection.Right })
			{
				if (net.GetNetData((x, y), side) is not null) { hasEndpoint = true; break; }
			}
		}
		string state = active ? "[c/55FF55:Active]" : "[c/AAAAAA:Idle]";
		string ep    = hasEndpoint ? "endpoint reached" : "[c/FFAA44:no endpoint]";
		return $"Laser Pipe: {state}   {ep}";
	}

	private static string OpticalStatusLine(int x, int y)
	{
		bool active = Pipelike.Optical.OpticalPipeLayerSystem.IsActive(x, y);
		var net = Pipelike.Optical.OpticalPipeNetSystem.Level.GetNetFromPos((x, y));
		bool hasEndpoint = false;
		if (net is not null)
		{
			foreach (var side in new[]
				{ IODirection.Up, IODirection.Down,
				  IODirection.Left, IODirection.Right })
			{
				if (net.GetNetData((x, y), side, Pipelike.Optical.OpticalRouteKind.Computation) is not null
					|| net.GetNetData((x, y), side, Pipelike.Optical.OpticalRouteKind.Data) is not null)
				{
					hasEndpoint = true;
					break;
				}
			}
		}
		string state = active ? "[c/55FF55:Active]" : "[c/AAAAAA:Idle]";
		string ep    = hasEndpoint ? "endpoint reached" : "[c/FFAA44:no endpoint]";
		return $"Optical Pipe: {state}   {ep}";
	}

	private static string EmptyWhitelistWarningLine(PipeKind layer, int x, int y)
	{
		bool fluid = layer == PipeKind.Fluid;
		PipeCoverable? pcv = fluid              ? FluidPipeLayerSystem.GetSides(x, y)
		                   : layer == PipeKind.Item ? ItemPipeLayerSystem.GetSides(x, y)
		                   : null;
		if (pcv is null) return "";
		foreach (var side in CoverSides.All)
			if (PipeSettingsState.IsEmptyBlockingWhitelist(pcv.GetCoverAtSide(side), fluid))
				return "[c/FF4646:! Empty whitelist - this side blocks everything]";
		return "";
	}

	private static string ItemThroughputLine(int x, int y)
	{
		int v = Main.netMode == NetmodeID.MultiplayerClient
			? (ItemPipeNetSystem.ClientTransferStats.TryGetValue((x, y), out int cv) ? cv : 0)
			: (ItemPipeLayerSystem.GetSides(x, y)?.TransferredItems ?? 0);
		return v > 0 ? $"Last 1s: {v} items" : "";
	}

	private static string FluidContentsLine(int x, int y)
	{
		global::GregTechCEuTerraria.Api.Fluids.FluidStack[]? fluids;
		if (Main.netMode == NetmodeID.MultiplayerClient)
		{
			FluidPipeLayerSystem.ClientTankSnapshots.TryGetValue((x, y), out fluids);
		}
		else
		{
			fluids = FluidPipeLayerSystem.GetState(x, y)?.GetContainedFluids();
		}
		if (fluids is null) return "";
		var sb = new System.Text.StringBuilder();
		int channels = fluids.Length;
		for (int i = 0; i < channels; i++)
		{
			var f = fluids[i];
			if (f.IsEmpty) continue;
			string name = f.Type?.DisplayName ?? f.Type?.Id ?? "?";
			if (sb.Length > 0) sb.Append('\n');
			if (channels > 1) sb.Append($"ch{i}: ");
			else              sb.Append("Pipe: ");
			sb.Append(f.Amount); sb.Append(" mB "); sb.Append(name);
		}
		return sb.ToString();
	}

	private static string NetIdLine(PipeKind layer, int x, int y)
	{
		(int x, int y)? anchor = null;
		int nodeCount = 0;
		if (layer == PipeKind.Fluid)
		{
			var net = FluidPipeNetSystem.Level.GetNetFromPos((x, y));
			if (net is null) return "";
			nodeCount = net.AllNodes.Count;
			foreach (var k in net.AllNodes.Keys)
				if (anchor is null || k.x < anchor.Value.x || (k.x == anchor.Value.x && k.y < anchor.Value.y))
					anchor = k;
		}
		else if (layer == PipeKind.Laser)
		{
			var net = Pipelike.Laser.LaserPipeNetSystem.Level.GetNetFromPos((x, y));
			if (net is null) return "";
			nodeCount = net.AllNodes.Count;
			foreach (var k in net.AllNodes.Keys)
				if (anchor is null || k.x < anchor.Value.x || (k.x == anchor.Value.x && k.y < anchor.Value.y))
					anchor = k;
		}
		else if (layer == PipeKind.Optical)
		{
			var net = Pipelike.Optical.OpticalPipeNetSystem.Level.GetNetFromPos((x, y));
			if (net is null) return "";
			nodeCount = net.AllNodes.Count;
			foreach (var k in net.AllNodes.Keys)
				if (anchor is null || k.x < anchor.Value.x || (k.x == anchor.Value.x && k.y < anchor.Value.y))
					anchor = k;
		}
		else
		{
			var net = ItemPipeNetSystem.Level.GetNetFromPos((x, y));
			if (net is null) return "";
			nodeCount = net.AllNodes.Count;
			foreach (var k in net.AllNodes.Keys)
				if (anchor is null || k.x < anchor.Value.x || (k.x == anchor.Value.x && k.y < anchor.Value.y))
					anchor = k;
		}
		if (anchor is null) return "";
		uint h = 2166136261u;
		unchecked { h ^= (uint)anchor.Value.x; h *= 16777619u; h ^= (uint)anchor.Value.y; h *= 16777619u; }
		return $"Net #{(h & 0xFFFF):X4} ({nodeCount} pipes)";
	}
}
