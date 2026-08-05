#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Trait;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public class OpticalComputationHatchMachine : MultiblockPartMachine, IOpticalComputationHatch
{
	protected override string Label => "Optical Computation Hatch";

	public bool Transmitter { get; protected set; }
	public NotifiableComputationContainer? ComputationContainer { get; protected set; }

	private int _syncAvailableCwu;

	public OpticalComputationHatchMachine() : base() { }

	public bool IsTransmitter() => Transmitter;

	public int RequestCWUt(int cwut, bool simulate, ICollection<IOpticalComputationProvider> seen)
	{
		if (seen.Contains(this)) return 0;
		seen.Add(this);
		return ComputationContainer?.RequestCWUt(cwut, simulate, seen) ?? 0;
	}

	public int GetMaxCWUt(ICollection<IOpticalComputationProvider> seen)
	{
		if (seen.Contains(this)) return 0;
		seen.Add(this);
		return ComputationContainer?.GetMaxCWUt(seen) ?? 0;
	}

	public bool CanBridge(ICollection<IOpticalComputationProvider> seen)
	{
		if (seen.Contains(this)) return false;
		seen.Add(this);
		return ComputationContainer?.CanBridge(seen) ?? false;
	}

	protected override void OnDefinitionBound()
	{
		base.OnDefinitionBound();
		Configure(Definition?.OpticalTransmitter ?? false);
	}

	public void Configure(bool transmitter)
	{
		Transmitter = transmitter;
		EnsureContainer();
	}

	private void EnsureContainer()
	{
		if (ComputationContainer != null) return;
		ComputationContainer = new NotifiableComputationContainer(IO.IN, Transmitter);
		Traits.Attach(ComputationContainer);
		Traits.RegisterPersistent("ComputationContainer", ComputationContainer);
	}

	public bool CanShared() => false;

	public int GetAvailableCwu() => ComputationContainer?.GetMaxCWUt() ?? 0;

	public int GetAllocatableCwu() => ComputationContainer?.RequestCWUt(int.MaxValue, simulate: true) ?? 0;

	public override void AppendTooltip(System.Collections.Generic.List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add(Transmitter
			? "[c/55AAFF:Computation Transmitter] - sends CWU/t to the optical network"
			: "[c/FFAA55:Computation Receiver] - pulls CWU/t from an HPCA into this multi");
		int max = IsClient ? _syncAvailableCwu : ComputationContainer?.GetMaxCWUt() ?? 0;
		lines.Add($"[c/55FFFF:Available: {max} CWU/t]");
		if (max == 0)
			lines.Add(Transmitter ? DiagnoseTransmitter()
				: "[c/FFAA44:Not linked to an HPCA - connect via an optical pipe or place it adjacent to a transmitter hatch]");
		else if (Transmitter)
			AppendUnderpoweredSourceWarning(lines);
	}

	private void AppendUnderpoweredSourceWarning(System.Collections.Generic.List<string> lines)
	{
		foreach (var controller in GetControllers())
		{
			if (controller is Multiblock.IPowerDiagnostics pd
				&& !Multiblock.ResearchPowerDiagnostics.IsSufficient(pd))
			{
				lines.Add("[c/FFAA44:Source is underpowered - output is intermittent until it gets enough EU/t]");
				return;
			}
		}
	}

	private string DiagnoseTransmitter()
	{
		Multiblock.Electric.Research.HPCAMachine? hpca = null;
		bool anyController = false;
		foreach (var controller in GetControllers())
		{
			anyController = true;
			if (controller is Multiblock.Electric.Research.HPCAMachine h) { hpca = h; break; }
			if (controller is Api.Capability.IOpticalComputationProvider)
				return "[c/FFAA44:Computation source is idle or unpowered]";
		}
		if (!anyController)
			return "[c/FFAA44:Not bound to a multiblock - place this hatch as part of an HPCA's casing wall]";
		if (hpca == null)
			return "[c/FFAA44:Bound multiblock provides no computation]";
		if (!hpca.IsFormed)
			return "[c/FFAA44:HPCA structure incomplete - fill ALL nine grid slots (use Empty Components for unused slots)]";
		if (hpca.DisplayMaxCWUt == 0)
			return "[c/FFAA44:HPCA grid has no Computation Components - add at least one]";
		if (!hpca.IsActive)
			return "[c/FFAA44:HPCA has no power - wire EU into its Energy Hatch]";
		return "[c/FFAA44:HPCA is present but not providing - check power / working state]";
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["transmitter"] = Transmitter;
	}

	public override void SaveDataForSync(TagCompound tag)
	{
		if (IsServer) _syncAvailableCwu = GetAvailableCwu();
		base.SaveDataForSync(tag);
		tag["availCwu"] = _syncAvailableCwu;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		Transmitter = tag.GetBool("transmitter");
		if (tag.ContainsKey("availCwu")) _syncAvailableCwu = tag.GetInt("availCwu");
		EnsureContainer();
		Traits.Load(tag);
	}
}
