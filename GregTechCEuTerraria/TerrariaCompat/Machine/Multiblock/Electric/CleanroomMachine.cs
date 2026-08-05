#nullable enable
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Misc;
using GregTechCEuTerraria.Api.Pattern;
using GregTechCEuTerraria.Common.Block;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Part;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock.Electric;

public sealed class CleanroomMachine : WorkableElectricMultiblockMachine, IControllable
{
	public const int CLEAN_AMOUNT_THRESHOLD = 95;
	public const int MIN_CLEAN_AMOUNT       = 0;

	protected override string Label => "Cleanroom";

	private int  _cleanAmount;
	public  int  CleanAmount => _cleanAmount;

	private CleanroomType? _cleanroomType;
	public  CleanroomType? CleanroomTypeResolved => _cleanroomType;

	private EnergyContainerList? _inputEnergyContainers;
	public  EnergyContainerList? InputEnergyContainers => _inputEnergyContainers;

	private List<CleanroomReceiverTrait>? _cleanroomReceivers;

	private readonly CleanroomProviderTrait _cleanroomProvider;

	public Terraria.DataStructures.Point16 FormedTopLeft { get; private set; }
	public int FormedTileWidth  { get; private set; }
	public int FormedTileHeight { get; private set; }

	public bool CleanroomActive => _cleanroomProvider.IsActive;

	public CleanroomMachine() : base()
	{
		_cleanroomProvider = new CleanroomProviderTrait();
		Traits.Attach(_cleanroomProvider);
	}

	protected override RecipeLogic CreateRecipeLogic() => new CleanroomLogic();

	public new CleanroomLogic Recipe => (CleanroomLogic)base.Recipe;

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		InitializeAbilities();

		var filterType = GetMultiblockState().MatchContext.Get<CleanroomFilterType>("FilterType");
		_cleanroomType = filterType?.CleanroomType ?? CleanroomType.CLEANROOM;
		_cleanroomProvider.ProvidedTypes = new HashSet<CleanroomType> { _cleanroomType };

		if (_cleanroomReceivers != null)
		{
			foreach (var r in _cleanroomReceivers) r.RemoveCleanroom();
			_cleanroomReceivers = null;
		}
		var receivers = GetMultiblockState().MatchContext
			.GetOrCreate("cleanroomReceiver", () => new HashSet<CleanroomReceiverTrait>());
		_cleanroomReceivers = receivers.ToList();
		foreach (var r in _cleanroomReceivers) r.CleanroomProvider = _cleanroomProvider;

		var ctx = GetMultiblockState().MatchContext;
		int horizontal = ctx.GetOrDefault("horizontalRepeats", 3);
		int vertical   = ctx.GetOrDefault("verticalRepeats",   3);
		int width      = horizontal + 2;
		int height     = vertical   + 2;
		int duration   = System.Math.Max(100, (int)(System.Math.Pow(width, 0.8) * height));
		Recipe.SetDuration(duration);

		int controllerCol = 1 + horizontal / 2;
		FormedTopLeft     = new Terraria.DataStructures.Point16(
			Position.X - controllerCol * 2,
			Position.Y /* controllerRow == 0 */);
		FormedTileWidth   = width  * 2;
		FormedTileHeight  = height * 2;
	}

	public override void OnStructureInvalid()
	{
		base.OnStructureInvalid();
		_inputEnergyContainers = null;
		_cleanAmount = MIN_CLEAN_AMOUNT;
		_cleanroomProvider.IsActive = false;
		if (_cleanroomReceivers != null)
		{
			foreach (var r in _cleanroomReceivers) r.RemoveCleanroom();
			_cleanroomReceivers = null;
		}
		FormedTileWidth = 0;
		FormedTileHeight = 0;
	}

	public override bool ShouldAddPartToController(IMultiPart part)
	{
		var state = GetMultiblockState();
		var cache = state.Cache;
		if (cache == null) return true;
		var pos = part.Self().Position;
		for (int dx = -1; dx <= 1; dx++)
		for (int dy = -1; dy <= 1; dy++)
		{
			if ((dx == 0) == (dy == 0)) continue;
			long key = ((long)(pos.X + dx) << 32) | (uint)(pos.Y + dy);
			if (!cache.Contains(key)) return true;
		}
		return false;
	}

	private void InitializeAbilities()
	{
		var energyContainers = new List<IEnergyContainer>();
		foreach (var part in GetParts())
		{
			if (IsPartIgnored(part)) continue;
			if (part is TieredIOPartMachine tiop && tiop.Io == IO.OUT) continue;
			foreach (var handlerList in part.GetRecipeHandlers())
			{
				if (!handlerList.IsValid(IO.IN)) continue;
				foreach (var handler in handlerList.GetCapability(EURecipeCapability.CAP))
					if (handler is IEnergyContainer ec) energyContainers.Add(ec);
			}
			if (part is IMaintenanceMachine mm) Recipe.MaintenanceMachine = mm;
		}
		_inputEnergyContainers = new EnergyContainerList(energyContainers);
		Recipe.EnergyContainer = _inputEnergyContainers;
		MultiTier = VoltageTiers.FloorTierByVoltage(GetMaxVoltage());
	}

	private static bool IsPartIgnored(IMultiPart part) => false;

	internal static bool InnerPredicateMatch(MultiblockState state)
	{
		var machine = state.GetMachine();
		if (machine != null && IsMachineBanned(machine)) return false;
		if (machine != null)
		{
			var receiver = machine.Traits.GetTrait(CleanroomReceiverTrait.TYPE);
			if (receiver != null)
			{
				var set = state.MatchContext.GetOrCreate(
					"cleanroomReceiver", () => new HashSet<CleanroomReceiverTrait>());
				set.Add(receiver);
			}
		}
		return true;
	}

	private static bool IsMachineBanned(MetaMachine machine)
	{
		if (machine.Traits.GetTrait(CleanroomProviderTrait.TYPE) != null) return true;
		if (machine is MufflerPartMachine) return true;
		if (machine is Primitive.CokeOvenMachine) return true;
		return false;
	}

	public void AdjustCleanAmount(int delta)
	{
		_cleanAmount = System.Math.Clamp(_cleanAmount + delta, 0, 100);
		_cleanroomProvider.IsActive = _cleanAmount >= CLEAN_AMOUNT_THRESHOLD;
	}

	public override long GetMaxVoltage() =>
		_inputEnergyContainers?.InputVoltage ?? VoltageTiers.V((int)VoltageTier.LV);

	bool IControllable.IsWorkingEnabled() => true;
	void IControllable.SetWorkingEnabled(bool ignored) { }

	protected override void SaveMachineData(TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["cleanAmount"] = _cleanAmount;
		if (_cleanroomType != null) tag["cleanroomType"] = _cleanroomType.Name;
		if (FormedTileWidth > 0 && FormedTileHeight > 0)
		{
			tag["formedX"] = (int)FormedTopLeft.X;
			tag["formedY"] = (int)FormedTopLeft.Y;
			tag["formedW"] = FormedTileWidth;
			tag["formedH"] = FormedTileHeight;
		}
	}

	public override void LoadData(TagCompound tag)
	{
		base.LoadData(tag);
		_cleanAmount = tag.GetInt("cleanAmount");
		if (tag.ContainsKey("cleanroomType"))
			_cleanroomType = CleanroomType.GetByName(tag.GetString("cleanroomType"));
		_cleanroomProvider.IsActive = _cleanAmount >= CLEAN_AMOUNT_THRESHOLD;
		if (_cleanroomType != null)
			_cleanroomProvider.ProvidedTypes = new HashSet<CleanroomType> { _cleanroomType };
		if (tag.ContainsKey("formedW"))
		{
			FormedTopLeft = new Terraria.DataStructures.Point16(tag.GetInt("formedX"), tag.GetInt("formedY"));
			FormedTileWidth  = tag.GetInt("formedW");
			FormedTileHeight = tag.GetInt("formedH");
		}
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		if (_cleanroomType != null) lines.Add($"Type: {_cleanroomType.Name}");
		string state = _cleanroomProvider.IsActive ? "[c/55FF55:Clean]" : "[c/FFAA00:Dirty]";
		lines.Add($"{state}  ({_cleanAmount}%)");
	}
}
