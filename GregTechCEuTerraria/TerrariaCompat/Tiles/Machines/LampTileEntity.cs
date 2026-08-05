#nullable enable
using System;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

public sealed class LampTileEntity : TieredEnergyMachine
{
	public LampTileEntity() { }
	public LampTileEntity(VoltageTier tier) : base(tier) { }

	protected override string  Label       => "Lamp";

	public override bool CanAccept => true;

	public override long EnergyCapacity => VoltageTiers.Voltage(Tier);

	public long DrawPerTick => Math.Max(1L, VoltageTiers.Voltage(Tier) / 32L);

	private bool _isActive;
	public override bool IsActive => _isActive;

	protected override void OnTick()
	{
		long stored = EnergyContainer.EnergyStored;
		if (stored <= 0)
		{
			_isActive = false;
			return;
		}
		long draw = DrawPerTick;
		long actualDraw = System.Math.Min(stored, draw);
		EnergyContainer.SetEnergyStored(stored - actualDraw);
		_isActive = true;
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["lampActive"] = _isActive;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("lampActive")) _isActive = tag.GetBool("lampActive");
	}

	private static readonly float[] _brightnessMultByTier =
	{
		1.00f, // ULV - vanilla torch
		1.50f, // LV
		1.90f, // MV
		2.20f, // HV
		2.50f, // EV
		2.80f, // IV
		3.00f, // LuV
		3.10f, // ZPM
		3.20f, // UV
		3.25f, // UHV
		3.30f, // UEV
		3.35f, // UIV
		3.40f, // UXV
		3.45f, // OpV
		3.50f, // MAX
	};

	public Vector3 LitColor
	{
		get
		{
			int idx = Math.Clamp((int)Tier, 0, _brightnessMultByTier.Length - 1);
			float m = _brightnessMultByTier[idx];
			return Common.Energy.VoltageTiers.LightColor(Tier) * m;
		}
	}

	public override Vector3 WorkingLightColor => LitColor;
}
