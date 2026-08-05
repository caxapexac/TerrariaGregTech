#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Tool;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Items.Tools;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public class MaintenanceHatchPartMachine : TieredPartMachine, IMaintenanceMachine
{
	public const float MAX_DURATION_MULTIPLIER = 1.1f;
	public const float MIN_DURATION_MULTIPLIER = 0.9f;
	public const float DURATION_ACTION_AMOUNT  = 0.01f;

	protected override string Label => "Maintenance Hatch";

	public bool IsConfigurable { get; private set; }

	private bool  _isTaped;
	private int   _timeActive;
	private byte  _maintenanceProblems;
	private float _durationMultiplier = 1f;

	public MaintenanceHatchPartMachine() : base() { }

	public void Configure(bool isConfigurable)
	{
		IsConfigurable = isConfigurable;
		Tier = isConfigurable
			? (int)VoltageTier.HV
			: (int)VoltageTier.LV;
		_maintenanceProblems = ((IMaintenanceMachine)this).StartProblems();
	}

	protected override void OnDefinitionBound()
	{
		base.OnDefinitionBound();
		if (Definition == null) return;
		Configure(Definition.PartConfigurable);
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		var imm = (IMaintenanceMachine)this;
		int problems = imm.GetNumMaintenanceProblems();
		lines.Add(problems == 0 ? "[c/55FF55:No problems]" : $"[c/FF5555:Problems: {problems}/6]");
		if (IsConfigurable)
			lines.Add($"Duration Multiplier: {_durationMultiplier:F2}");
	}

	public bool IsFullAuto() => false;
	public bool IsTaped()    => _isTaped;

	public byte StartProblems() => IMaintenanceMachine.ALL_PROBLEMS;

	public byte GetMaintenanceProblems() => _maintenanceProblems;

	public void SetMaintenanceProblems(byte problems)
	{
		_maintenanceProblems = problems;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public int GetTimeActive() => _timeActive;
	public void SetTimeActive(int time) => _timeActive = time;

	public float GetDurationMultiplier() => _durationMultiplier;

	public void SetDurationMultiplier(float value)
	{
		if (value < MIN_DURATION_MULTIPLIER) value = MIN_DURATION_MULTIPLIER;
		if (value > MAX_DURATION_MULTIPLIER) value = MAX_DURATION_MULTIPLIER;
		_durationMultiplier = value;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public void SetTaped(bool isTaped)
	{
		if (_isTaped == isTaped) return;
		_isTaped = isTaped;
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public float GetTimeMultiplier()
	{
		float result = 1f;
		if (_durationMultiplier < 1.0f)
			result = -20f * _durationMultiplier + 21f;
		else
			result =  -8f * _durationMultiplier +  9f;
		return (float)System.Math.Round(result, 2, System.MidpointRounding.AwayFromZero);
	}

	public void FixAllMaintenanceProblems()
	{
		var imm = (IMaintenanceMachine)this;
		for (int i = 0; i < 6; i++) imm.SetMaintenanceFixed(i);
	}

	public bool TryFixFromPlayerInventory(Player player)
	{
		var imm = (IMaintenanceMachine)this;
		if (!imm.HasMaintenanceProblems()) return false;

		if (player.creativeGodMode) { FixAllMaintenanceProblems(); return true; }

		FixProblemsWithTools(_maintenanceProblems, player);
		return true;
	}

	private void FixProblemsWithTools(byte problems, Player player)
	{
		var needed = new GTToolType?[6];
		bool anyMissing = false;
		for (int i = 0; i < 6; i++)
		{
			if (((problems >> i) & 1) == 0)
			{
				anyMissing = true;
				needed[i] = i switch
				{
					0 => GTToolType.WRENCH,
					1 => GTToolType.SCREWDRIVER,
					2 => GTToolType.SOFT_MALLET,
					3 => GTToolType.HARD_HAMMER,
					4 => GTToolType.WIRE_CUTTER,
					5 => GTToolType.CROWBAR,
					_ => null,
				};
			}
		}
		if (!anyMissing) return;

		var imm = (IMaintenanceMachine)this;
		for (int idx = 0; idx < 6; idx++)
		{
			var toolType = needed[idx];
			if (toolType == null) continue;
			for (int s = 0; s < player.inventory.Length; s++)
			{
				var stack = player.inventory[s];
				if (stack == null || stack.IsAir) continue;
				if (stack.ModItem is ToolItem t && ReferenceEquals(t.ToolType, toolType))
				{
					imm.SetMaintenanceFixed(idx);
					SetTaped(false);
					break;
				}
			}
		}
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["isConfigurable"]      = IsConfigurable;
		tag["isTaped"]             = _isTaped;
		tag["timeActive"]          = _timeActive;
		tag["maintenanceProblems"] = (byte)_maintenanceProblems;
		tag["durationMultiplier"]  = _durationMultiplier;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		IsConfigurable       = tag.GetBool("isConfigurable");
		_isTaped             = tag.GetBool("isTaped");
		_timeActive          = tag.GetInt("timeActive");
		_maintenanceProblems = tag.GetByte("maintenanceProblems");
		_durationMultiplier  = tag.ContainsKey("durationMultiplier") ? tag.GetFloat("durationMultiplier") : 1f;
		Tier = IsConfigurable ? (int)VoltageTier.HV : (int)VoltageTier.LV;
	}
}
