#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Pipenet;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Optical;

public sealed class OpticalPipeNet : PipeNet<OpticalPipeProperties>
{
	private readonly Dictionary<((int x, int y) pipe, IODirection face, OpticalRouteKind kind), OpticalRoutePath> _netData = new();

	public OpticalPipeNet(ILevelPipeNet<OpticalPipeProperties> level) : base(level) { }

	public OpticalRoutePath? GetNetData((int x, int y) pipePos, IODirection facing, OpticalRouteKind kind)
	{
		var key = (pipePos, facing, kind);
		if (_netData.TryGetValue(key, out var data)) return data;

		var fresh = OpticalNetWalker.CreateNetData(this, pipePos, facing, kind);
		if (fresh == OpticalNetWalker.FAILED_MARKER)
		{
			return null;
		}
		if (fresh != null)
			_netData[key] = fresh;
		return fresh;
	}

	public override void OnNeighbourUpdate((int x, int y) fromPos) => _netData.Clear();
	public override void OnPipeConnectionsUpdate()                  => _netData.Clear();

	protected override void WriteNodeData(OpticalPipeProperties nodeData, TagCompound tag) { }
	protected override OpticalPipeProperties ReadNodeData(TagCompound tag) => OpticalPipeProperties.INSTANCE;
}
