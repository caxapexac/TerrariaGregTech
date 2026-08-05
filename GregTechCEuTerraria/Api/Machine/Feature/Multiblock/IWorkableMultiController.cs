#nullable enable
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

namespace GregTechCEuTerraria.Api.Machine.Feature.Multiblock;


public interface IWorkableMultiController : IRecipeLogicMachine
{
	MultiblockControllerMachine Self();
}
