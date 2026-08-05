#nullable enable
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.Config;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

namespace GregTechCEuTerraria.Common.Recipe.Condition;

public sealed class CleanroomCondition : RecipeCondition
{
	public string CleanroomType { get; }

	public CleanroomCondition() : this("cleanroom") { }
	public CleanroomCondition(string cleanroomType) { CleanroomType = cleanroomType; }

	public override bool Test(RecipeLogic logic)
	{
		var machine = logic.Machine;

		if (!GTConfig.Instance.EnableCleanroom) return true;
		if (!GTConfig.Instance.MultiblocksNeedCleanroom
		    && machine is MultiblockControllerMachine) return true;

		var receiver = machine.Traits.GetTrait(CleanroomReceiverTrait.TYPE);
		var type = Api.Machine.Multiblock.CleanroomType.GetByName(CleanroomType);

		if (receiver != null && type != null) return receiver.HasActiveCleanroom(type);
		return true;
	}

	public override string GetTooltips() => $"Requires cleanroom: {CleanroomType}";
	public override string GetTypeName() => "gtceu:cleanroom";

	public override string GetFailureMessage(RecipeLogic logic)
	{
		var machine = logic.Machine;
		var receiver = machine.Traits.GetTrait(CleanroomReceiverTrait.TYPE);
		if (receiver == null) return "Machine has no cleanroom receiver (internal)";
		if (receiver.CleanroomProvider == null) return "Not inside a formed cleanroom";

		var type = Api.Machine.Multiblock.CleanroomType.GetByName(CleanroomType);
		if (type == null) return $"Unknown cleanroom type: {CleanroomType}";

		var provider = receiver.CleanroomProvider;
		if (!provider.ProvidedTypes.Contains(type))
			return $"Cleanroom provides wrong type (need {type.Name})";
		if (!provider.IsActive)
			return "Cleanroom not clean yet (95% required)";
		return $"Requires cleanroom: {type.Name}";
	}
}
