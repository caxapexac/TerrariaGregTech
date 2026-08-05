#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Tiles.Machines;

namespace GregTechCEuTerraria.TerrariaCompat.Net.Actions;

public sealed class FisherFilterAction : IMachineAction
{
	public PacketType Type => PacketType.FisherFilterSet;

	private byte _filter;
	private bool _filtered;

	public FisherFilterAction() { }

	public FisherFilterAction(FisherFilter filter, bool filtered)
	{
		_filter   = (byte)filter;
		_filtered = filtered;
	}

	public void Write(BinaryWriter w)
	{
		w.Write(_filter);
		w.Write(_filtered);
	}

	public void Read(BinaryReader r)
	{
		_filter   = r.ReadByte();
		_filtered = r.ReadBoolean();
	}

	public void Apply(MetaMachine entity, int byWhoAmI)
	{
		if (entity is FisherMachine fisher)
			fisher.SetFiltered((FisherFilter)_filter & FisherFilter.All, _filtered);
	}
}
