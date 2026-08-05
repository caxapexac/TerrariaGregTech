#nullable enable
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait.Hpca;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part.Hpca;

public class HPCAComponentPartMachine : MultiblockPartMachine
{
	protected override string Label => "HPCA Component";

	public HpcaComponentKind Kind { get; protected set; } = HpcaComponentKind.Empty;
	public HPCAComponentTrait? ComponentTrait { get; protected set; }

	public HPCAComponentPartMachine() : base() { }

	public bool IsAdvanced() => Kind is HpcaComponentKind.AdvancedComputation
		or HpcaComponentKind.ActiveCooler or HpcaComponentKind.Bridge;

	protected override void OnDefinitionBound()
	{
		base.OnDefinitionBound();
		if (Definition?.HpcaKind is { } k)
		{
			Kind = k;
			EnsureTrait();
		}
	}

	protected override void OnTick()
	{
		BindDefinition();
		base.OnTick();
	}

	private static int VA(VoltageTier t) => VoltageTiers.VA((int)t);

	private void EnsureTrait()
	{
		if (ComponentTrait != null) return;
		ComponentTrait = Kind switch
		{
			HpcaComponentKind.Computation =>
				new HPCAComputationProviderTrait(VA(VoltageTier.EV), VA(VoltageTier.LuV), true, false, 4, 2),
			HpcaComponentKind.AdvancedComputation =>
				new HPCAComputationProviderTrait(VA(VoltageTier.IV), VA(VoltageTier.ZPM), true, false, 16, 4),
			HpcaComponentKind.HeatSink =>
				new HPCACoolantProviderTrait(0, 0, false, false, 1, 0, false),
			HpcaComponentKind.ActiveCooler =>
				new HPCACoolantProviderTrait(VA(VoltageTier.IV), VA(VoltageTier.IV), false, false, 2, 8, true),
			HpcaComponentKind.Bridge =>
				new HPCAComponentTrait(VA(VoltageTier.IV), VA(VoltageTier.IV), false, true),
			_ => // Empty
				new HPCAComponentTrait(0, 0, false, false),
		};
		Traits.Attach(ComponentTrait);
		Traits.RegisterPersistent("HpcaComponent", ComponentTrait);
	}

	public bool CanShared() => false;

	public override bool IsActive => ComponentTrait?.IsActive ?? false;

	public override void AppendTooltip(System.Collections.Generic.List<string> lines)
	{
		base.AppendTooltip(lines);
		var t = ComponentTrait;
		if (t == null) return;

		switch (t)
		{
			case HPCAComputationProviderTrait c:
				lines.Add($"[c/55FFFF:Computation: {c.GetCWUPerTick()} CWU/t]");
				lines.Add($"[c/FFAA55:Cooling demand: {c.GetCoolingPerTick()}]");
				break;
			case HPCACoolantProviderTrait cool:
				lines.Add($"[c/55FF55:Cooling: {cool.CoolingAmount}]");
				if (cool.IsActiveCooler)
					lines.Add($"[c/FFFF55:Active cooler - {cool.MaxCoolantPerTick} mB/t PCB Coolant]");
				else
					lines.Add("[c/AAAAAA:Passive heat sink (no coolant)]");
				break;
			default:
				if (t.AllowBridging) lines.Add("[c/55FF55:Enables HPCA bridging]");
				else                 lines.Add("[c/AAAAAA:Empty slot (no function)]");
				break;
		}

		if (t.MaxEUt > 0)
			lines.Add($"[c/AAAAAA:Energy: {t.UpkeepEUt:N0} idle / {t.MaxEUt:N0} max EU/t]");
		if (t.CanBeDamaged)
			lines.Add(t.IsDamaged ? "[c/FF5555:DAMAGED - replace or cool the array]"
			                      : "[c/AAAAAA:Can overheat if cooling is insufficient]");
	}

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["hpca_kind"] = (byte)Kind;
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("hpca_kind"))
			Kind = (HpcaComponentKind)tag.GetByte("hpca_kind");
		EnsureTrait();
		Traits.Load(tag);
	}
}
