// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (the crafting-simulation half of appeng.hooks.ticking.TickHandler), Forge 1.20.1.
// LGPL-3.0-only. See AE2 LICENSE.

#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Config;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;

public sealed class MeCraftingTickSystem : ModSystem
{
	private static readonly List<CraftingCalculation> _craftingJobs = new();

	internal static void RegisterCraftingSimulation(CraftingCalculation craftingCalculation)
	{
		lock (_craftingJobs)
			_craftingJobs.Add(craftingCalculation);
	}

	public override void PostUpdateWorld()
	{
		SimulateCraftingJobs();
		Net.MeCraftPackets.PollPendingPlans();
	}

	public override void ClearWorld()
	{
		Net.MeCraftPackets.ClearPlans();
		lock (_craftingJobs)
		{
			foreach (var cj in _craftingJobs)
				cj.Interrupt();
			_craftingJobs.Clear();
		}
	}

	private static void SimulateCraftingJobs()
	{
		lock (_craftingJobs)
		{
			if (_craftingJobs.Count == 0)
				return;

			int jobSize = _craftingJobs.Count;
			int microSecondsPerTick = GTConfig.Instance.CraftingCalculationTimePerTick * 1000;
			int simTime = Math.Max(1, microSecondsPerTick / jobSize);

			for (int i = _craftingJobs.Count - 1; i >= 0; i--)
				if (!_craftingJobs[i].SimulateFor(simTime))
					_craftingJobs.RemoveAt(i);
		}
	}
}
