#nullable enable
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Cover.Data;

namespace GregTechCEuTerraria.Api.Cover;

public interface IIOCover
{
	int TransferRate { get; }

	IO Io { get; }

	ManualIOMode ManualIOMode { get; }
}
