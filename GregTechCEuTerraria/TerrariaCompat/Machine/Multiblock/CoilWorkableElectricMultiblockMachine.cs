#nullable enable
using GregTechCEuTerraria.Api.Block;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

public class CoilWorkableElectricMultiblockMachine : WorkableElectricMultiblockMachine
{
	public ICoilType CoilType { get; private set; } = DefaultCoilType.CUPRONICKEL;

	public CoilWorkableElectricMultiblockMachine() : base() { }

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		var type = GetMultiblockState().MatchContext.Get<object>("CoilType");
		if (type is ICoilType coil)
			CoilType = coil;
	}

	public int GetCoilTier() => CoilType.Tier;

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["coilName"] = CoilType.Name;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		if (!tag.ContainsKey("coilName")) return;
		string name = tag.GetString("coilName");
		var resolved = Api.Block.CoilType.GetByName(name);
		if (resolved is not null) CoilType = resolved;
	}
}
