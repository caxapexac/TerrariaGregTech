#nullable enable
using GregTechCEuTerraria.Common.Energy;
using System;
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.Api.Blockentity;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Cover;
using GregTechCEuTerraria.Api.Cover.Data;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Capabilities.Handlers;
using GregTechCEuTerraria.TerrariaCompat.Cover;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

// Base for any in-world machine. Mirrors upstream MetaMachine + TieredMachine flattened
public abstract class MetaMachine : ModTileEntity, ITickSubscription, ICoverable, IFancyTooltip, ISidedCapabilityProvider
{
	private Common.Energy.VoltageTier? _persistedTier;
	protected Common.Energy.VoltageTier ResolveTier(Common.Energy.VoltageTier fallback) => _persistedTier ?? fallback;

	private string? _persistedMdefId;
	private MachineDefinition? _definition;

	public MachineDefinition? Definition => _definition;

	// safe to call from any Ensure* hook
	public void BindDefinition()
	{
		if (_definition != null) return;
		string? id = _persistedMdefId;
		if (id == null && Position.X > 0 && Position.X < Main.maxTilesX
		    && Position.Y > 0 && Position.Y < Main.maxTilesY)
		{
			int tileType = Main.tile[Position.X, Position.Y].TileType;
			if (MachineRegistry.TryResolveTile(tileType, out var rid, out var rtier))
			{
				id = rid;
				_persistedMdefId = rid;
				_persistedTier ??= rtier;
			}
		}
		if (id != null && MachineRegistry.TryGet(id, out var def))
		{
			_definition = def;
			OnDefinitionBound();
		}
	}

	internal void OverrideIdentity(string mdefId, Common.Energy.VoltageTier tier)
	{
		_persistedMdefId = mdefId;
		_persistedTier   = tier;
		_definition      = MachineRegistry.TryGet(mdefId, out var def) ? def : null;
		if (_definition != null) OnDefinitionBound();
	}

	protected virtual void OnDefinitionBound() { }

	public string? MachineId => _definition?.Id;
	public virtual string MachineKey => MachineId != null
		? (_definition!.Tiered
			? $"{Common.Energy.VoltageTiers.Id(Tier)}_{MachineId}"
			: MachineId)
		: GetType().Name;

	// Activator-ctor fallback; real tier is _persistedTier via Tier
	protected readonly Common.Energy.VoltageTier _tier;

	protected MetaMachine()
	{
		_tier = Common.Energy.VoltageTier.LV;
		Traits = new MachineTraitHolder(this);
	}

	public static MetaMachine? GetMachineAt(int x, int y)
	{
		var pos = new Terraria.DataStructures.Point16(x, y);
		if (Terraria.DataStructures.TileEntity.ByPosition.TryGetValue(pos, out var te) && te is MetaMachine machine)
			return machine;
		return null;
	}

	protected MetaMachine(Common.Energy.VoltageTier tier)
	{
		_tier = tier;
		Traits = new MachineTraitHolder(this);
	}

	public MachineTraitHolder Traits { get; }

	protected abstract string Label { get; }

	public override string Name => GetType().Name;

	public virtual Common.Energy.VoltageTier Tier => ResolveTier(_tier);

	public System.Collections.Generic.IReadOnlyDictionary<object, int>? GetOutputLimits() => Definition?.OutputLimits?.Invoke(Tier);

	public virtual string DisplayName
	{
		get
		{
			if (Definition == null) return Label;
			string key = $"Mods.GregTechCEuTerraria.Items.{MachineKey}.DisplayName";
			var text = Terraria.Localization.Language.GetText(key);
			if (text.Value != key)
				return StripColorTags(text.Value);
			return Definition.Tiered
				? $"{Common.Energy.VoltageTiers.ShortName(Tier)} {Definition.Label}"
				: Definition.Label;
		}
	}

	private static readonly System.Text.RegularExpressions.Regex _colorTagRe =
		new(@"\[c/[0-9A-Fa-f]+:(.*?)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

	internal static string StripColorTags(string s) => _colorTagRe.Replace(s, "$1");

	protected virtual int OwnerTileType => Mod.TryFind<ModTile>(MachineKey, out var t) ? t.Type : 0;

	public virtual (int Width, int Height) Size => Definition?.Size ?? (1, 1);

	public virtual bool IsActive => false;

	public virtual bool DisplayActive => IsActive;

	public IEnumerable<(int x, int y)> Cells()
	{
		var (w, h) = Size;
		int ox = Position.X, oy = Position.Y;
		for (int dx = 0; dx < w; dx++)
			for (int dy = 0; dy < h; dy++)
				yield return (ox + dx, oy + dy);
	}

	public override bool IsTileValidForEntity(int x, int y)
	{
		BindDefinition();
		Tile tile = Main.tile[x, y];
		return tile.HasTile && tile.TileType == OwnerTileType;
	}

	public virtual void AppendTooltip(List<string> lines)
	{
		lines.Add(DisplayName);
		OnAddFancyInformationTooltip(lines);
		if (SupportsWorkingEnabledToggle && !WorkingEnabled)
			lines.Add("[c/FF4444:Disabled]");
	}

	void IFancyTooltip.AppendFancyTooltip(List<string> lines) =>
		OnAddFancyInformationTooltip(lines);

	public virtual void OnAddFancyInformationTooltip(List<string> lines) =>
		MachineTooltipLookup.AppendDescriptionAndBuilder(lines, MachineKey, MachineId, Definition);

	public static bool IsServer       => Main.netMode != NetmodeID.MultiplayerClient;
	public static bool IsClient       => Main.netMode == NetmodeID.MultiplayerClient;
	public static bool IsMultiplayer  => Main.netMode != NetmodeID.SinglePlayer;

	// Server-only - periodic state-sync only broadcasts to machines with viewers > 0
	private readonly HashSet<int> _viewers = new();
	public IEnumerable<int> Viewers => _viewers;
	public int  ViewerCount          => _viewers.Count;
	public bool HasViewers           => _viewers.Count > 0;
	public bool HasViewer(int whoAmI) => _viewers.Contains(whoAmI);

	public byte[]? LastBroadcastBlob { get; set; }
	public long? LastBroadcastEnergy { get; set; }

	public virtual bool HasSyncEnergy => false;
	public virtual long SyncEnergyStored => 0;
	public virtual void ApplySyncEnergy(long energy) { }

	public void AddViewer(int whoAmI)
	{
		if (!IsServer) return;
		_viewers.Add(whoAmI);
	}
	public void RemoveViewer(int whoAmI)
	{
		if (!IsServer) return;
		_viewers.Remove(whoAmI);
	}

	public void PruneViewers()
	{
		if (!IsServer || _viewers.Count == 0) return;
		_viewers.RemoveWhere(w => w < 0 || w >= Main.maxPlayers || !Main.player[w].active);
	}

	public override void NetSend(BinaryWriter writer)
	{
		var tag = new TagCompound();
		SaveData(tag);
		TagIO.Write(tag, writer);
	}

	public override void NetReceive(BinaryReader reader)
	{
		var tag = TagIO.Read(reader);
		LoadData(tag);
	}

	private readonly List<TickableSubscription> _serverTicks  = new();
	private readonly List<TickableSubscription> _waitingToAdd = new();

	public TickableSubscription? SubscribeServerTick(Action runnable)
	{
		if (IsClient) return null;
		var sub = new TickableSubscription(runnable);
		_waitingToAdd.Add(sub);
		return sub;
	}

	public void Unsubscribe(TickableSubscription? current) => current?.Unsubscribe();

	// Driven by EnergyNetSystem.PostUpdateWorld. Server-only - clients receive snapshots via MachineStateSyncPacket
	internal virtual void SystemTick()
	{
		if (IsClient) return;
		EnsureLoaded();

		if (_waitingToAdd.Count > 0)
		{
			_serverTicks.AddRange(_waitingToAdd);
			_waitingToAdd.Clear();
		}
		for (int i = _serverTicks.Count - 1; i >= 0; i--)
		{
			var sub = _serverTicks[i];
			if (sub.StillSubscribed) sub.Run();
			if (!sub.StillSubscribed) _serverTicks.RemoveAt(i);
		}

		if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) != 0) return;

		OnTick();
	}

	private bool _machineLoaded;
	private void EnsureLoaded()
	{
		if (_machineLoaded) return;
		_machineLoaded = true;
		Traits.OnMachineLoad();
		OnMachineLoaded();
	}

	protected virtual void OnMachineLoaded() { }

	protected virtual void OnTick() { }

	public virtual Microsoft.Xna.Framework.Vector3 WorkingLightColor => Common.Energy.VoltageTiers.LightColor(Tier);

	public virtual void DrawCustomOverlay(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, int i, int j) { }

	public virtual void OnClientFrame() { }

	internal virtual void OnClientSync() { }

	public virtual Item[]? GetSlotGroup(SlotGroup group) => null;

	public virtual void NotifySlotGroupChanged(SlotGroup group)
	{
		if (group == SlotGroup.Charger) return;
		foreach (var trait in Traits.AllTraits)
		{
			if (trait is not Api.Machine.Trait.NotifiableItemStackHandler nish) continue;
			bool match = group switch
			{
				SlotGroup.Inventory       => true,
				SlotGroup.InventoryInput  => nish.HandlerIO.Supports(Api.Capability.Recipe.IO.IN),
				SlotGroup.InventoryOutput => nish.HandlerIO.Supports(Api.Capability.Recipe.IO.OUT),
				_                         => false,
			};
			if (match) nish.OnContentsChanged();
		}
	}

	public virtual int ResolveFluidTank(Api.Capability.Recipe.IO direction, int localIndex) => localIndex;

	public bool WorkingEnabled
	{
		get => this is IControllable c ? c.IsWorkingEnabled() : true;
		set { if (this is IControllable c) c.SetWorkingEnabled(value); }
	}

	public virtual bool SupportsWorkingEnabledToggle => this is IControllable;
	public virtual bool SupportsAutoOutputItems  => false;
	public virtual bool SupportsAutoOutputFluids => false;
	public virtual AutoOutputTrait? AutoOutput => null;

	// Per-side capability resolver - pipe / adjacent-transfer entry point, player interaction bypasses it
	public virtual IItemHandler? GetItemHandlerCap(IODirection side, bool useCoverCapability = true)
	{
		if (this is not IItemHandler handler) return null;
		IItemHandler? result = AutoOutput is { } ao && ao.BlocksInputFrom(side, fluid: false)
			? new ExtractOnlyItemHandler(handler)
			: handler;
		if (useCoverCapability && CoverSideOf(side) is { } cs && GetCoverAtSide(cs) is { } cover)
			result = cover.GetItemHandlerCap(result);
		return result;
	}

	public virtual IFluidHandler? GetFluidHandlerCap(IODirection side, bool useCoverCapability = true)
	{
		if (this is not IFluidHandler handler) return null;
		IFluidHandler? result = AutoOutput is { } ao && ao.BlocksInputFrom(side, fluid: true)
			? new ExtractOnlyFluidHandler(handler)
			: handler;
		if (useCoverCapability && CoverSideOf(side) is { } cs && GetCoverAtSide(cs) is { } cover)
			result = cover.GetFluidHandlerCap(result);
		return result;
	}

	IItemHandler? ISidedCapabilityProvider.GetItemHandler(IODirection side) =>
		GetItemHandlerCap(side, useCoverCapability: true);

	IFluidHandler? ISidedCapabilityProvider.GetFluidHandler(IODirection side) =>
		GetFluidHandlerCap(side, useCoverCapability: true);

	private static CoverSide? CoverSideOf(IODirection side) => side switch
	{
		IODirection.Up    => CoverSide.Up,
		IODirection.Down  => CoverSide.Down,
		IODirection.Left  => CoverSide.Left,
		IODirection.Right => CoverSide.Right,
		_                 => null,
	};

	public System.Func<Item, bool> GetItemCapFilter(IODirection side, IO io)
	{
		if (CoverSideOf(side) is { } cs && GetCoverAtSide(cs) is ItemFilterCover fc)
		{
			if (!fc.FilterMode.Filters(io))
			{
				if (fc.AllowFlow == ManualIOMode.Disabled)   return _ => false;
				if (fc.AllowFlow == ManualIOMode.Unfiltered) return _ => true;
			}
			return fc.GetItemFilter().Test;
		}
		return _ => true;
	}

	public System.Func<FluidStack, bool> GetFluidCapFilter(IODirection side, IO io)
	{
		if (CoverSideOf(side) is { } cs && GetCoverAtSide(cs) is FluidFilterCover fc)
		{
			if (!fc.FilterMode.Filters(io))
			{
				if (fc.AllowFlow == ManualIOMode.Disabled)   return _ => false;
				if (fc.AllowFlow == ManualIOMode.Unfiltered) return _ => true;
			}
			return fc.GetFluidFilter().Test;
		}
		return _ => true;
	}

	private readonly CoverBehavior?[] _covers = new CoverBehavior?[CoverSides.Count];

	public CoverBehavior? GetCoverAtSide(CoverSide side) => _covers[(int)side];
	public void SetCoverAtSide(CoverBehavior? cover, CoverSide side) => _covers[(int)side] = cover;

	public virtual bool SupportsCovers => true;

	public virtual bool CanPlaceCoverOnSide(CoverDefinition definition, CoverSide side) => SupportsCovers;

	public void NotifyBlockUpdate() { }

	public bool IsRemote => IsClient;

	public Point16 GetBlockPos() => Position;

	// Per-position offset so covers on different machines don't sync up
	public long GetOffsetTimer() => (long)Main.GameUpdateCount + ((Position.X * 13L + Position.Y * 7L) & 0x3FF);

	public long GetMcOffsetTimer() =>
		(long)(Main.GameUpdateCount / (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1))
		+ ((Position.X * 13L + Position.Y * 7L) & 0x3FF);

	private ulong _lastWirePulseTick;

	public bool TryConsumeWirePulse()
	{
		if (_lastWirePulseTick == Main.GameUpdateCount) return false;
		_lastWirePulseTick = Main.GameUpdateCount;
		return true;
	}

	protected bool HasPowerFailPreventingCover()
	{
		foreach (var cover in ((ICoverable)this).GetCovers())
			if (cover is MachineControllerCover { PreventPowerFail: true })
				return true;
		return false;
	}

	public override void SaveData(TagCompound tag)
	{
		tag["mte_tier"] = (byte)Tier;
		if (_persistedMdefId != null) tag["mdef"] = _persistedMdefId;
		Traits.Save(tag);
		((ICoverable)this).SaveCovers(tag);
	}

	public override void LoadData(TagCompound tag)
	{
		if (tag.ContainsKey("mte_tier"))
			_persistedTier = (Common.Energy.VoltageTier)tag.GetByte("mte_tier");
		if (tag.ContainsKey("mdef"))
		{
			_persistedMdefId = tag.GetString("mdef");
			BindDefinition();
		}
		Traits.Load(tag);
		((ICoverable)this).LoadCovers(tag);
	}

	public virtual void SaveDataForSync(TagCompound tag)
	{
		SaveData(tag);
		Traits.SaveForSync(tag);
	}

	internal void OnClientPostUpdate()
	{
		Traits.OnClientTick();
	}

	public virtual void WritePortableData(TagCompound tag) { }
	public virtual void ReadPortableData(TagCompound tag) { }

	public override void OnKill()
	{
		if (!IsServer) return;

		var (tw, th) = Size;
		int worldX = Position.X * 16, worldY = Position.Y * 16;
		int boxW   = tw * 16,           boxH   = th * 16;
		var src    = new Terraria.DataStructures.EntitySource_TileBreak(Position.X, Position.Y);

		bool inventoryIsView = GetSlotGroup(SlotGroup.InventoryInput) != null
		                    || GetSlotGroup(SlotGroup.InventoryOutput) != null;
		foreach (SlotGroup group in System.Enum.GetValues(typeof(SlotGroup)))
		{
			if (group == SlotGroup.Inventory && inventoryIsView) continue;
			var inv = GetSlotGroup(group);
			if (inv is null) continue;
			DropInventory(inv, src, worldX, worldY, boxW, boxH);
		}

		foreach (var side in CoverSides.All)
			foreach (var drop in ((ICoverable)this).RemoveCover(side))
				SpawnItem(drop, src, worldX, worldY, boxW, boxH);

		Traits.OnMachineDestroyed();
	}

	private static void DropInventory(Terraria.Item[] inv,
		Terraria.DataStructures.IEntitySource src,
		int worldX, int worldY, int boxW, int boxH)
	{
		for (int i = 0; i < inv.Length; i++)
		{
			var item = inv[i];
			if (item is null || item.IsAir) continue;
			SpawnItem(item, src, worldX, worldY, boxW, boxH);
			inv[i] = new Terraria.Item();
		}
	}

	private static void SpawnItem(Terraria.Item item,
		Terraria.DataStructures.IEntitySource src,
		int worldX, int worldY, int boxW, int boxH)
	{
		if (item is null || item.IsAir) return;
		int idx = Terraria.Item.NewItem(src, worldX, worldY, boxW, boxH,
			item.type, item.stack, noBroadcast: true);
		Main.item[idx]          = item.Clone();
		Main.item[idx].position = new Microsoft.Xna.Framework.Vector2(
			worldX + boxW / 2f - item.width / 2f,
			worldY + boxH / 2f - item.height / 2f);
		Main.item[idx].whoAmI   = idx;
		if (Main.netMode == NetmodeID.Server)
			NetMessage.SendData(MessageID.SyncItem, number: idx, number2: 1f);
	}
}
