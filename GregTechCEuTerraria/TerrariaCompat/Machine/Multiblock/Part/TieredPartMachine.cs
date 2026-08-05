#nullable enable
using GregTechCEuTerraria.Api.Machine.Feature;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;

public abstract class TieredPartMachine : MultiblockPartMachine, ITieredMachine
{
	public new int Tier { get; protected set; }

	protected TieredPartMachine() : base() { }

	public int GetTier() => Tier;

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["tier"] = Tier;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.LoadData(tag);
		if (tag.ContainsKey("tier")) Tier = tag.GetInt("tier");
	}
}
