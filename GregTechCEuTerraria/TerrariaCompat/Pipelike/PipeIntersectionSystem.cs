#nullable enable
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike;

public sealed class PipeIntersectionSystem : ModSystem
{
	public override void PostUpdateEverything() => PipeIntersection.TickRecheck();

	public override void ClearWorld() => PipeIntersection.ClearPending();
}
