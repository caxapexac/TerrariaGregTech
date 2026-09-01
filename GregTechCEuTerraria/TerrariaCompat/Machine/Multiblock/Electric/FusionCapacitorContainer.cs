#nullable enable
using GregTechCEuTerraria.Api.Machine.Trait;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

public sealed class FusionCapacitorContainer : NotifiableEnergyContainer
{
	private const string NbtCapacity = "energyCapacity";

	public FusionCapacitorContainer() : base(0, 0, 0, 0, 0) { }

	public override void Save(TagCompound tag)
	{
		base.Save(tag);
		tag[NbtCapacity] = _energyCapacity;
	}

	public override void Load(TagCompound tag)
	{
		base.Load(tag);
		_energyCapacity = tag.GetLong(NbtCapacity);
	}

	public override void SaveForSync(TagCompound tag) => Save(tag);
}
