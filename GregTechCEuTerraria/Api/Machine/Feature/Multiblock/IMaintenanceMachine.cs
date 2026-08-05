#nullable enable
using System;
using GregTechCEuTerraria.Api.Recipe;

namespace GregTechCEuTerraria.Api.Machine.Feature.Multiblock;

public interface IMaintenanceMachine : IMultiPart
{
	const byte ALL_PROBLEMS = 0;
	const byte NO_PROBLEMS  = 0b111111;

	private static readonly Random Rng = new();

	bool IsFullAuto();

	bool IsTaped();
	void SetTaped(bool isTaped);

	byte StartProblems();

	byte GetMaintenanceProblems();
	void SetMaintenanceProblems(byte problems);

	int GetTimeActive();
	void SetTimeActive(int time);

	float GetDurationMultiplier() => 1f;

	float GetTimeMultiplier() => 1f;

	new bool CanShared() => false;

	void CalculateMaintenance(IMaintenanceMachine maintenanceMachine, int duration)
	{
		if (!MaintenanceConfig.Enabled || maintenanceMachine.IsFullAuto())
			return;

		SetTimeActive(GetTimeActive() + duration);
		float rate = MaintenanceConfig.CheckRate / maintenanceMachine.GetTimeMultiplier();
		if (GetTimeActive() >= rate)
		{
			SetTimeActive(0);
			if (Rng.Next(6000) == 0)
			{
				CauseRandomMaintenanceProblems();
				maintenanceMachine.SetTaped(false);
			}
		}
	}

	void CalculateMaintenance(IMaintenanceMachine maintenanceMachine) =>
		CalculateMaintenance(maintenanceMachine, 1);

	int GetNumMaintenanceProblems() =>
		MaintenanceConfig.Enabled ? 6 - PopCount(GetMaintenanceProblems()) : 0;

	bool HasMaintenanceProblems() =>
		MaintenanceConfig.Enabled && GetMaintenanceProblems() < 63;

	void SetMaintenanceFixed(int index) =>
		SetMaintenanceProblems((byte)(GetMaintenanceProblems() | (byte)(1 << index)));

	void CauseRandomMaintenanceProblems() =>
		SetMaintenanceProblems((byte)(GetMaintenanceProblems() & (byte)~(1 << Rng.Next(6))));

	new bool OnWorking(IWorkableMultiController controller)
	{
		CalculateMaintenance(this);
		if (HasMaintenanceProblems())
			controller.GetRecipeLogic().MarkLastRecipeDirty();
		return true;
	}

	new GTRecipe? ModifyRecipe(GTRecipe recipe)
	{
		if (MaintenanceConfig.Enabled)
		{
			if (HasMaintenanceProblems())
				return null;
			var durationMultiplier = GetDurationMultiplier();
			if (durationMultiplier != 1f)
			{
				recipe = recipe.Copy();
				recipe.Duration = (int)(recipe.Duration * durationMultiplier);
			}
		}
		return recipe;
	}

	private static int PopCount(byte b)
	{
		int count = 0;
		while (b != 0) { count++; b &= (byte)(b - 1); }
		return count;
	}
}

public static class MaintenanceConfig
{
	public static readonly bool Enabled = false;

	public const int CheckRate = 100;
}
