#nullable enable
using GregTechCEuTerraria.Api.Capability;
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Cable;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines.Transformers;

public class TransformerMachine : TieredEnergyMachine
{
	public const IODirection HvFace = IODirection.Up;
	public const IODirection LvFace = IODirection.Down;

	public TransformerMachine() { }
	public TransformerMachine(VoltageTier tier) : base(tier) { }

	protected override string Label => Definition?.Label ?? "Transformer";

	public virtual int BaseAmp => Definition?.BaseAmp ?? 1;

	public static readonly VoltageTier[] Tiers =
	{
		VoltageTier.ULV, VoltageTier.LV,  VoltageTier.MV,  VoltageTier.HV,
		VoltageTier.EV,  VoltageTier.IV,  VoltageTier.LuV, VoltageTier.ZPM,
		VoltageTier.UV,  VoltageTier.UHV, VoltageTier.UEV, VoltageTier.UIV,
		VoltageTier.UXV, VoltageTier.OpV,
	};

	private bool _isTransformUp;
	public bool IsTransformUp => _isTransformUp;

	public override long EnergyCapacity =>
		VoltageTiers.Voltage(Tier) * 8L * (BaseAmp * 4L);

	public override bool CanAccept  => true;
	public override bool CanExtract => true;

	public override IODirection EnergyFaceForCell(int cx, int cy) =>
		cy == Position.Y ? HvFace : LvFace;

	protected override NotifiableEnergyContainer CreateEnergyContainer()
	{
		long v = VoltageTiers.Voltage(Tier);
		var c = new NotifiableEnergyContainer(v * 8L, v * 4L, BaseAmp, v, 4L * BaseAmp);
		ApplyTransformConfig(c, _isTransformUp);
		return c;
	}

	private void ApplyTransformConfig(NotifiableEnergyContainer c, bool up)
	{
		long v = VoltageTiers.Voltage(Tier);
		int lowAmperage = BaseAmp * 4;
		if (up)
		{
			c.ResetBasicInfo(v * 8L * lowAmperage, v, lowAmperage, v * 4L, BaseAmp);
			c.SideInputCondition  = s => s == LvFace && WorkingEnabled;
			c.SideOutputCondition = s => s == HvFace && WorkingEnabled;
		}
		else
		{
			c.ResetBasicInfo(v * 8L * lowAmperage, v * 4L, BaseAmp, v, lowAmperage);
			c.SideInputCondition  = s => s == HvFace && WorkingEnabled;
			c.SideOutputCondition = s => s == LvFace && WorkingEnabled;
		}
	}

	public void SetTransformUp(bool up)
	{
		if (_isTransformUp == up || !IsServer) return;
		_isTransformUp = up;
		ApplyTransformConfig(EnergyContainer, up);
		EnergyNetSystem.MarkEndpointsDirty();
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["transformUp"] = _isTransformUp;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		_isTransformUp = tag.ContainsKey("transformUp") && tag.GetBool("transformUp");
		ApplyTransformConfig(EnergyContainer, _isTransformUp);
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		var c = EnergyContainer;
		string inFace  = _isTransformUp ? "bottom" : "top";
		string outFace = _isTransformUp ? "top"    : "bottom";
		string arrow   = _isTransformUp ? "Step Up"   : "Step Down";
		lines.Add($"{arrow}: IN {inFace} {c.InputVoltage:N0}V @{c.InputAmperage}A  ->  OUT {outFace} {c.OutputVoltage:N0}V @{c.OutputAmperage}A");
		lines.Add("Screwdriver-RMB to flip direction");
	}
}
