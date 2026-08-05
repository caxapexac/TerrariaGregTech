#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Terminal;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;

public sealed class MeModularTerminalMachine : MeTerminalMachine, IMePatternAccessHost, IMePatternEncodingHost, IMeCraftingHost
{
	public MetaMachine Machine => this;

	public IReadOnlyList<IMePatternProvider> Providers =>
		Network?.Providers ?? System.Array.Empty<IMePatternProvider>();

	private readonly PatternEncodingState _encoding = new();
	public PatternEncodingState Encoding => _encoding;
	public SlotGroup BlankSlotGroup => SlotGroup.PatternBlank;
	public SlotGroup EncodedSlotGroup => SlotGroup.InventoryOutput;
	public bool IsEncodingActive => HasUpgrade("pattern_encoding");

	private readonly CraftingStationState _crafting = new();
	public CraftingStationState Crafting => _crafting;
	public SlotGroup StationSlotGroup => SlotGroup.CraftingStation;
	public bool IsCraftingActive => HasUpgrade("crafting");

	public const int UpgradeSlots = 4;

	private readonly Item[] _upgrades = NewSlots();
	private static Item[] NewSlots()
	{
		var a = new Item[UpgradeSlots];
		for (int i = 0; i < a.Length; i++) a[i] = new Item();
		return a;
	}

	public MeModularTerminalMachine() { }

	protected override string Label => "ME Terminal";

	public override Item[]? GetSlotGroup(SlotGroup group) => group switch
	{
		SlotGroup.InventoryInput => _upgrades,
		SlotGroup.PatternBlank => _encoding.Blank,
		SlotGroup.InventoryOutput => _encoding.Encoded,
		SlotGroup.CraftingStation => _crafting.Stations,
		_ => null,
	};

	public override bool AcceptsOutputDeposit(int index, Item item) =>
		_encoding.AcceptsEncodedDeposit(index, item);

	public override void NotifySlotGroupChanged(SlotGroup group)
	{
		base.NotifySlotGroupChanged(group);
		if (group == SlotGroup.InventoryOutput && IsServer)
			_encoding.OnEncodedSlotChanged();
	}

	public override bool IsItemValidForSlot(SlotGroup group, int index, Item item)
	{
		if (group == SlotGroup.CraftingStation)
			return item.IsAir || RecipeNetworkCrafting.IsCraftingStationItem(item);
		if (group != SlotGroup.InventoryInput) return true;
		if (item.IsAir) return true;
		if (MeTerminalUpgrades.ByItemType(item.type) == null) return false;
		for (int i = 0; i < UpgradeSlots; i++)
			if (i != index && !_upgrades[i].IsAir && _upgrades[i].type == item.type)
				return false;
		return true;
	}

	public bool HasUpgrade(string id)
	{
		var upgrade = MeTerminalUpgrades.ById(id);
		if (upgrade == null || upgrade.CardItemType < 0) return false;
		for (int i = 0; i < UpgradeSlots; i++)
			if (!_upgrades[i].IsAir && _upgrades[i].type == upgrade.CardItemType)
				return true;
		return false;
	}

	public IEnumerable<MeTerminalUpgrade> InstalledUpgrades()
	{
		for (int i = 0; i < UpgradeSlots; i++)
		{
			if (_upgrades[i].IsAir) continue;
			var upgrade = MeTerminalUpgrades.ByItemType(_upgrades[i].type);
			if (upgrade != null) yield return upgrade;
		}
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		for (int i = 0; i < UpgradeSlots; i++)
			if (!_upgrades[i].IsAir) tag[$"up{i}"] = ItemIO.Save(_upgrades[i]);
		_encoding.Save(tag);
		_crafting.Save(tag);
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		for (int i = 0; i < UpgradeSlots; i++)
			_upgrades[i] = tag.ContainsKey($"up{i}") ? ItemIO.Load(tag.GetCompound($"up{i}")) : new Item();
		_encoding.Load(tag);
		_crafting.Load(tag);
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		int installed = 0;
		for (int i = 0; i < UpgradeSlots; i++) if (!_upgrades[i].IsAir) installed++;
		lines.Add($"Upgrades: {installed} / {UpgradeSlots}");
	}
}
