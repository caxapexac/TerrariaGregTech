#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Recipe.Modifier;
using GregTechCEuTerraria.Common.Energy;
using GregTechCEuTerraria.Common.Recipe;
using GregTechCEuTerraria.Api.Transfer;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Items;
using GregTechCEuTerraria.TerrariaCompat.Recipes;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Machine;

public class WorkableTieredMachine : TieredEnergyMachine, IItemHandler, IFluidHandler, IRecipeLogicMachine, IOverclockMachine, Api.Machine.Feature.IHasCircuitSlot
{
	public WorkableTieredMachine() { }
	public WorkableTieredMachine(VoltageTier tier) : base(tier) { }

	public virtual GTRecipeType GetRecipeType() => Definition?.RecipeType!;

	protected virtual int InputSlotCount  => Definition?.InputSlotCount  ?? 0;
	protected virtual int OutputSlotCount => Definition?.OutputSlotCount ?? 0;

	protected virtual int InputFluidTankCount  => Definition?.InputFluidTankCount  ?? 0;
	protected virtual int OutputFluidTankCount => Definition?.OutputFluidTankCount ?? 0;

	protected override string Label => Definition?.Label ?? "Machine";

	public int InputSlots       => InputSlotCount;
	public int OutputSlots      => OutputSlotCount;
	public int InputFluidTanks  => InputFluidTankCount;
	public int OutputFluidTanks => OutputFluidTankCount;

	public virtual bool UsesCircuit => Definition?.UsesCircuit ?? false;

	protected virtual int FluidTankCapacity => Definition?.FluidTankCapacity ?? 16_000;

	public override long EnergyCapacity => VoltageTiers.Voltage(Tier) * 64;
	public override bool CanAccept => true;

	private NotifiableItemStackHandler? _importItems;
	private NotifiableItemStackHandler? _exportItems;
	private NotifiableItemStackHandler? _circuitInventory;
	private NotifiableFluidTank? _importFluids;
	private NotifiableFluidTank? _exportFluids;

	public NotifiableItemStackHandler ImportItems { get { EnsureTraits(); return _importItems!; } }
	public NotifiableItemStackHandler ExportItems { get { EnsureTraits(); return _exportItems!; } }
	public NotifiableItemStackHandler? CircuitInventory { get { EnsureTraits(); return _circuitInventory; } }
	public NotifiableFluidTank? ImportFluids      { get { EnsureTraits(); return _importFluids; } }
	public NotifiableFluidTank? ExportFluids      { get { EnsureTraits(); return _exportFluids; } }

	private static bool IsProgrammedCircuit(Item stack) =>
		stack != null && stack.ModItem is IntCircuitItem;

	public Item[] Slots
	{
		get
		{
			EnsureTraits();
			var input  = _importItems!.Storage.Stacks;
			var output = _exportItems!.Storage.Stacks;
			var combined = new Item[input.Length + output.Length];
			Array.Copy(input, 0, combined, 0, input.Length);
			Array.Copy(output, 0, combined, input.Length, output.Length);
			return combined;
		}
	}

	public FluidStack[] FluidTanks
	{
		get
		{
			EnsureTraits();
			int inputCount  = _importFluids?.Storages.Length ?? 0;
			int outputCount = _exportFluids?.Storages.Length ?? 0;
			var combined = new FluidStack[inputCount + outputCount];
			for (int i = 0; i < inputCount;  i++) combined[i]               = _importFluids!.Storages[i].Fluid;
			for (int i = 0; i < outputCount; i++) combined[inputCount + i] = _exportFluids!.Storages[i].Fluid;
			return combined;
		}
	}

	public override Item[]? GetSlotGroup(SlotGroup group) => group switch
	{
		SlotGroup.Inventory       => Slots,                              // R/O concat
		SlotGroup.InventoryInput  => ImportItems.Storage.Stacks,
		SlotGroup.InventoryOutput => ExportItems.Storage.Stacks,
		_                         => base.GetSlotGroup(group),
	};

	public override void NotifySlotGroupChanged(SlotGroup group)
	{
		EnsureTraits();
		if (group == SlotGroup.InventoryInput)       _importItems!.OnContentsChanged();
		else if (group == SlotGroup.InventoryOutput) _exportItems!.OnContentsChanged();
	}

	private RecipeLogic? _recipeLogic;
	public RecipeLogic Recipe
	{
		get { EnsureTraits(); return _recipeLogic!; }
	}

	private AutoOutputTrait? _autoOutput;
	public override AutoOutputTrait? AutoOutput { get { EnsureTraits(); return _autoOutput; } }

	protected void EnsureTraits()
	{
		if (_recipeLogic is not null) return;
		BindDefinition();

		_recipeLogic = new RecipeLogic();
		Traits.Attach(_recipeLogic);
		Traits.RegisterPersistent("RecipeLogic", _recipeLogic);

		_importItems = new NotifiableItemStackHandler(InputSlotCount,  Api.Capability.Recipe.IO.IN);
		_exportItems = new NotifiableItemStackHandler(OutputSlotCount, Api.Capability.Recipe.IO.OUT);
		Traits.Attach(_importItems);
		Traits.Attach(_exportItems);
		Traits.RegisterPersistent("ImportItems", _importItems);
		Traits.RegisterPersistent("ExportItems", _exportItems);

		if (InputFluidTankCount > 0)
		{
			_importFluids = new NotifiableFluidTank(InputFluidTankCount, FluidTankCapacity, Api.Capability.Recipe.IO.IN, Api.Capability.Recipe.IO.BOTH);
			Traits.Attach(_importFluids);
			Traits.RegisterPersistent("ImportFluids", _importFluids);
		}
		if (OutputFluidTankCount > 0)
		{
			_exportFluids = new NotifiableFluidTank(OutputFluidTankCount, FluidTankCapacity, Api.Capability.Recipe.IO.OUT);
			Traits.Attach(_exportFluids);
			Traits.RegisterPersistent("ExportFluids", _exportFluids);
		}

		_autoOutput = new AutoOutputTrait(InputSlotCount, OutputSlotCount,
			InputFluidTankCount, OutputFluidTankCount);
		Traits.Attach(_autoOutput);
		Traits.RegisterPersistent("AutoOutput", _autoOutput);

		var cleanroomReceiver = new CleanroomReceiverTrait();
		Traits.Attach(cleanroomReceiver);
		Traits.RegisterPersistent("CleanroomReceiver", cleanroomReceiver);

		if (UsesCircuit)
		{
			_circuitInventory = new NotifiableItemStackHandler(1, Api.Capability.Recipe.IO.IN, Api.Capability.Recipe.IO.NONE)
				.SetFilter(IsProgrammedCircuit);
			_circuitInventory.ShouldDropInventoryInWorld = false;
			Traits.Attach(_circuitInventory);
			Traits.RegisterPersistent("CircuitInventory", _circuitInventory);
		}

		EnsureEnergyContainer();
		foreach (var t in Traits.AllTraits)
		{
			if (t is INotifiableRecipeHandler nrh)
				nrh.AddChangedListener(_recipeLogic.UpdateTickSubscription);
		}
	}

	protected void EnsureRecipeLogic() => EnsureTraits();

	protected override bool HasChargerSlot => true;

	// Display-only cache (post-overclock EU/t)
	private long _activeEut;
	long IRecipeLogicMachine.ActiveEut
	{
		get => _activeEut;
		set => _activeEut = value;
	}

	// GTRecipe isn't NBT-friendly; rebind via lookup on first tick.
	private string? _lastRecipeId;
	string? IRecipeLogicMachine.LastRecipeId
	{
		get => _lastRecipeId;
		set => _lastRecipeId = value;
	}

	public bool IsCircuitSlotEnabled() => UsesCircuit;

	bool IRecipeLogicMachine.SupportsRecipeLookup => true;

	IReadOnlyList<Item> IRecipeLogicMachine.LookupInputItems
	{
		get
		{
			var input = ImportItems.Storage.Stacks;
			if (_circuitInventory == null || _circuitInventory.SlotCount == 0) return input;
			var combined = new Item[input.Length + _circuitInventory.SlotCount];
			System.Array.Copy(input, 0, combined, 0, input.Length);
			System.Array.Copy(_circuitInventory.Storage.Stacks, 0, combined, input.Length, _circuitInventory.SlotCount);
			return combined;
		}
	}

	IReadOnlyList<FluidStack> IRecipeLogicMachine.LookupInputFluids
	{
		get
		{
			var f = ImportFluids;
			if (f is null) return System.Array.Empty<FluidStack>();
			var list = new FluidStack[f.Storages.Length];
			for (int i = 0; i < list.Length; i++) list[i] = f.Storages[i].Fluid;
			return list;
		}
	}

	public RecipeLogic GetRecipeLogic() { EnsureRecipeLogic(); return _recipeLogic!; }

	long IRecipeLogicMachine.RecipeVoltageCap => VoltageTiers.Voltage(Tier);

	long IRecipeLogicMachine.OffsetTimer =>
		Main.GameUpdateCount + (uint)(Position.X * 7 + Position.Y * 13);

	long IRecipeLogicMachine.EnergyStored
	{
		get => EnergyStored;
		set => EnergyStored = value;
	}

	public virtual bool BeforeWorking(GTRecipe recipe) => true;
	public virtual bool OnWorking()                    => true;
	public virtual void AfterWorking()                 { }
	public virtual void OnWaiting()                    { }
	public virtual bool KeepSubscribing()              => false;
	public virtual bool RegressWhenWaiting()    => true;
	public virtual bool AlwaysTryModifyRecipe() => true;
	public virtual bool IsMultiblockController()       => false;
	public virtual bool PreventPowerFail()             => HasPowerFailPreventingCover();

	public virtual GTRecipe? FullModifyRecipe(GTRecipe recipe) => DoModifyRecipe(RecipeHelper.TrimRecipeOutputs(recipe, GetOutputLimits()));

	public virtual GTRecipe? DoModifyRecipe(GTRecipe recipe)
	{
		var fn = GetRecipeModifier().GetModifier(this, recipe);
		var result = fn.Apply(recipe);
		_lastModifierFailReason = result == null ? fn.FailReason : null;
		return result;
	}

	public virtual bool CanVoidRecipeOutputs(object capability)
	{
		var limits = GetOutputLimits();
		return limits != null && limits.TryGetValue(capability, out var n) && n == 0;
	}

	private string? _lastModifierFailReason;
	public string? GetLastModifierFailReason() => _lastModifierFailReason;

	public virtual RecipeModifier GetRecipeModifier() => GTRecipeModifiers.OC_NON_PERFECT;

	public override bool IsActive => _recipeLogic?.IsActive() ?? false;

	public virtual void NotifyStatusChanged(RecipeLogicStatus oldStatus, RecipeLogicStatus newStatus)
	{
		if (newStatus == RecipeLogicStatus.WORKING)
		{
			((IRecipeLogicMachine)this).EnsureLoopSound(((IRecipeLogicMachine)this).GetWorldPos());
		}
		else
		{
			((IRecipeLogicMachine)this).StopLoopSound();
		}
	}

	public bool IsRunning        => _recipeLogic?.IsWorking() ?? false;
	public int  ProgressTicks    => _recipeLogic?.GetProgress() ?? 0;
	public int  DurationTicks    => _recipeLogic?.GetMaxProgress() ?? 0;
	public float Progress01      => DurationTicks > 0 ? (float)ProgressTicks / DurationTicks : 0f;
	public long ActiveEuPerTick  => _recipeLogic?.GetLastRecipe() is null ? 0 : _activeEut;
	public int ActiveOverclock   => _recipeLogic?.GetLastRecipe()?.OcLevel ?? 0;

	internal void MarkLastRecipeDirty() { EnsureRecipeLogic(); _recipeLogic!.MarkLastRecipeDirty(); }

	// === IWorkable + IControllable (forward to trait) ======================
	// read getters are field-only with null->default because ModifyLight reads them
	// on FastParallel worker threads
	int IWorkable.GetProgress()    => _recipeLogic?.GetProgress() ?? 0;
	int IWorkable.GetMaxProgress() => _recipeLogic?.GetMaxProgress() ?? 0;
	bool IWorkable.IsActive()      => _recipeLogic?.IsActive() ?? false;
	bool IControllable.IsWorkingEnabled() => _recipeLogic?.IsWorkingEnabled() ?? true;
	void IControllable.SetWorkingEnabled(bool v) { EnsureRecipeLogic(); _recipeLogic!.SetWorkingEnabled(v); }

	// === IItemHandler (routes to trait by slot index) =======================
	// Slot index 0..InputSlotCount-1 maps to ImportItems[i] + ExportItems[i - InputSlotCount] for IItemHandler
	public int SlotCount       { get { EnsureTraits(); return _importItems!.SlotCount + _exportItems!.SlotCount; } }
	public Item GetSlot(int s) { EnsureTraits(); return s < InputSlotCount ? _importItems!.GetSlot(s) : _exportItems!.GetSlot(s - InputSlotCount); }
	public Item Insert(int s, Item item, bool simulate)
	{
		EnsureTraits();
		return s < InputSlotCount
			? _importItems!.Insert(s, item, simulate)
			: _exportItems!.Insert(s - InputSlotCount, item, simulate);
	}
	public Item Extract(int s, int maxAmount, bool simulate)
	{
		EnsureTraits();
		return s < InputSlotCount
			? _importItems!.Extract(s, maxAmount, simulate)
			: _exportItems!.Extract(s - InputSlotCount, maxAmount, simulate);
	}

	public override bool SupportsAutoOutputItems  => OutputSlotCount > 0;
	public override bool SupportsAutoOutputFluids => OutputFluidTankCount > 0;

	public bool IsItemValid(int slot, Item item) => slot < InputSlotCount;

	public override int ResolveFluidTank(Api.Capability.Recipe.IO direction, int localIndex) =>
		direction == Api.Capability.Recipe.IO.OUT ? InputFluidTankCount + localIndex : localIndex;

	public int TankCount       { get { EnsureTraits(); return (_importFluids?.GetTanks() ?? 0) + (_exportFluids?.GetTanks() ?? 0); } }
	public FluidStack GetTank(int tank)
	{
		EnsureTraits();
		if (tank < InputFluidTankCount)
			return _importFluids?.GetFluidInTank(tank) ?? FluidStack.Empty;
		return _exportFluids?.GetFluidInTank(tank - InputFluidTankCount) ?? FluidStack.Empty;
	}
	public int GetCapacity(int tank) => FluidTankCapacity;
	public bool IsFluidValid(int tank, FluidStack fluid) => tank < InputFluidTankCount;

	public IFluidHandler GetTankAccess(int tank)
	{
		EnsureTraits();
		if (tank < InputFluidTankCount && _importFluids is not null)
			return _importFluids.Storages[tank];
		if (_exportFluids is not null)
			return _exportFluids.Storages[tank - InputFluidTankCount];
		return this;
	}

	public (bool AllowFill, bool AllowDrain) GetTankClickCaps(int tank) =>
		tank < InputFluidTankCount ? (true, true) : (false, true);

	public int Fill(FluidStack fluid, bool simulate)
	{
		if (fluid.IsEmpty) return 0;
		EnsureTraits();
		return _importFluids?.Fill(fluid, simulate) ?? 0;
	}

	public FluidStack Drain(int maxAmount, bool simulate)
	{
		if (maxAmount <= 0) return FluidStack.Empty;
		EnsureTraits();
		return _exportFluids?.Drain(maxAmount, simulate) ?? FluidStack.Empty;
	}

	public FluidStack Drain(FluidStack fluidStack, bool simulate)
	{
		if (fluidStack.IsEmpty) return FluidStack.Empty;
		EnsureTraits();
		return _exportFluids?.Drain(fluidStack, simulate) ?? FluidStack.Empty;
	}

	protected override void OnTick() => EnsureTraits();

	protected override void OnMachineLoaded()
	{
		base.OnMachineLoaded();
		if (Recipe.IsWorking())
			((IRecipeLogicMachine)this).EnsureLoopSound(((IRecipeLogicMachine)this).GetWorldPos());
	}

	// we dont have OC tier selector button ported yet
	public int OverclockTier    => (int)Tier;
	public int MaxOverclockTier => (int)Tier;
	public int MinOverclockTier => 0;
	public long OverclockVoltage => VoltageTiers.Voltage(Tier);
	public void SetOverclockTier(int tier) { }

	ActionResult IRecipeLogicMachine.TryMatchInputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids)
	{
		EnsureTraits();
		var (itemMatch, _) = Api.Recipe.RecipeContentSplit.Split(
			ItemRecipeCapability.CAP, items, Api.Capability.Recipe.IO.IN, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());
		var itemRemainder = HandleItemPayloads(itemMatch, Api.Capability.Recipe.IO.IN, simulate: true);
		if (itemRemainder is not null && itemRemainder.Count > 0)
			return ActionResult.Fail("gtceu.recipe.no_input", ItemRecipeCapability.CAP, Api.Capability.Recipe.IO.IN);
		var (fluidMatch, _) = Api.Recipe.RecipeContentSplit.Split(
			FluidRecipeCapability.CAP, fluids, Api.Capability.Recipe.IO.IN, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());
		var fluidRemainder = HandleFluidPayloads(fluidMatch, Api.Capability.Recipe.IO.IN, simulate: true);
		if (fluidRemainder is not null && fluidRemainder.Count > 0)
			return ActionResult.Fail("gtceu.recipe.no_fluid", FluidRecipeCapability.CAP, Api.Capability.Recipe.IO.IN);
		return ActionResult.SUCCESS;
	}

	ActionResult IRecipeLogicMachine.HasOutputRoomContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids)
	{
		EnsureTraits();
		var (_, itemConsume) = Api.Recipe.RecipeContentSplit.Split(
			ItemRecipeCapability.CAP, items, Api.Capability.Recipe.IO.OUT, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());
		var itemRemainder = HandleItemPayloads(itemConsume, Api.Capability.Recipe.IO.OUT, simulate: true);
		if (itemRemainder is not null && itemRemainder.Count > 0)
			return ActionResult.Fail("gtceu.recipe.output_full", ItemRecipeCapability.CAP, Api.Capability.Recipe.IO.OUT);
		var (_, fluidConsume) = Api.Recipe.RecipeContentSplit.Split(
			FluidRecipeCapability.CAP, fluids, Api.Capability.Recipe.IO.OUT, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());
		var fluidRemainder = HandleFluidPayloads(fluidConsume, Api.Capability.Recipe.IO.OUT, simulate: true);
		if (fluidRemainder is not null && fluidRemainder.Count > 0)
			return ActionResult.Fail("gtceu.recipe.fluid_output_full", FluidRecipeCapability.CAP, Api.Capability.Recipe.IO.OUT);
		return ActionResult.SUCCESS;
	}

	ActionResult IRecipeLogicMachine.TryConsumeInputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids)
	{
		EnsureTraits();
		var (itemMatch, itemConsume) = Api.Recipe.RecipeContentSplit.Split(
			ItemRecipeCapability.CAP, items, Api.Capability.Recipe.IO.IN, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());
		var (fluidMatch, fluidConsume) = Api.Recipe.RecipeContentSplit.Split(
			FluidRecipeCapability.CAP, fluids, Api.Capability.Recipe.IO.IN, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());

		var simItems = HandleItemPayloads(itemMatch, Api.Capability.Recipe.IO.IN, simulate: true);
		if (simItems is not null && simItems.Count > 0)
			return ActionResult.Fail("gtceu.recipe.no_input", ItemRecipeCapability.CAP, Api.Capability.Recipe.IO.IN);
		var simFluids = HandleFluidPayloads(fluidMatch, Api.Capability.Recipe.IO.IN, simulate: true);
		if (simFluids is not null && simFluids.Count > 0)
			return ActionResult.Fail("gtceu.recipe.no_fluid", FluidRecipeCapability.CAP, Api.Capability.Recipe.IO.IN);
		HandleItemPayloads(itemConsume, Api.Capability.Recipe.IO.IN, simulate: false);
		HandleFluidPayloads(fluidConsume, Api.Capability.Recipe.IO.IN, simulate: false);
		return ActionResult.SUCCESS;
	}

	ActionResult IRecipeLogicMachine.DepositOutputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids,
		RecipeLogic logic)
	{
		EnsureTraits();
		var (_, itemConsume) = Api.Recipe.RecipeContentSplit.Split(
			ItemRecipeCapability.CAP, items, Api.Capability.Recipe.IO.OUT, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());
		var (_, fluidConsume) = Api.Recipe.RecipeContentSplit.Split(
			FluidRecipeCapability.CAP, fluids, Api.Capability.Recipe.IO.OUT, isTick: false, recipe,
			chanceCache: null, totalRuns: recipe.GetTotalRuns());
		HandleItemPayloads(itemConsume, Api.Capability.Recipe.IO.OUT, simulate: false);
		HandleFluidPayloads(fluidConsume, Api.Capability.Recipe.IO.OUT, simulate: false);
		return ActionResult.SUCCESS;
	}

	// HandleRecipeInner mutates SizedIngredient.Amount on the remainder, so
	// ingredients are copied before passing.
	private List<Ingredient>? HandleItemPayloads(
		IReadOnlyList<object> payloads, Api.Capability.Recipe.IO io, bool simulate)
	{
		var primary = io == Api.Capability.Recipe.IO.IN ? _importItems : _exportItems;
		if (primary is null) return null;

		var list = new List<Ingredient>(payloads.Count);
		foreach (var p in payloads)
		{
			var ing = (Ingredient)p;
			list.Add(CopyIngredient(ing, CountOf(ing)));
		}

		var remaining = primary.HandleRecipeInner(io, _sentinelRecipe, list, simulate);
		if (io == Api.Capability.Recipe.IO.IN && _circuitInventory is not null
		    && remaining is { Count: > 0 })
			remaining = _circuitInventory.HandleRecipeInner(io, _sentinelRecipe, remaining, simulate);
		return remaining;
	}

	private List<FluidIngredient>? HandleFluidPayloads(
		IReadOnlyList<object> payloads, Api.Capability.Recipe.IO io, bool simulate)
	{
		var handler = io == Api.Capability.Recipe.IO.IN ? _importFluids : _exportFluids;
		if (handler is null)
		{
			if (payloads.Count == 0) return null;
			var unhandled = new List<FluidIngredient>(payloads.Count);
			foreach (var p in payloads) unhandled.Add(CopyFluidIngredient((FluidIngredient)p));
			return unhandled;
		}
		var list = new List<FluidIngredient>(payloads.Count);
		foreach (var p in payloads) list.Add(CopyFluidIngredient((FluidIngredient)p));
		return handler.HandleRecipeInner(io, _sentinelRecipe, list, simulate);
	}

	private static readonly GTRecipe _sentinelRecipe = new(
		GTRecipeType.GetOrCreate("__sentinel__"),
		new Dictionary<object, List<Api.Recipe.Content.Content>>(),
		new Dictionary<object, List<Api.Recipe.Content.Content>>(),
		new Dictionary<object, List<Api.Recipe.Content.Content>>(),
		new Dictionary<object, List<Api.Recipe.Content.Content>>(),
		new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(),
		new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(),
		new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(),
		new Dictionary<object, Api.Recipe.Chance.Logic.ChanceLogic>(),
		new List<RecipeCondition>(),
		Array.Empty<object>(),
		new TagCompound(),
		0,
		Api.Recipe.Category.GTRecipeCategory.DEFAULT,
		-1);

	private static Ingredient CopyIngredient(Ingredient ing, int count) => ing switch
	{
		SizedIngredient sized      => SizedIngredient.Create(sized.Inner, count),
		IntProviderIngredient ipi  => ipi,
		_                          => count > 1 ? SizedIngredient.Create(ing, count) : ing,
	};

	private static FluidIngredient CopyFluidIngredient(FluidIngredient ing)
	{
		if (ing.ExactType is not null)
			return new FluidIngredient(ing.ExactType, ing.Amount);
		if (ing.TagName is not null)
			return new FluidIngredient(ing.TagName, ing.GetFluids(), ing.Amount);
		if (ing.Attribute is not null)
			return new FluidIngredient(ing.Attribute, ing.GetFluids(), ing.Amount);
		return ing;
	}


	ActionResult IRecipeLogicMachine.TryDrainEU(Api.Recipe.GTRecipe recipe, long voltage)
	{
		if (EnergyStored < voltage)
			return ActionResult.Fail("gtceu.recipe.insufficient_eu", EURecipeCapability.CAP, Api.Capability.Recipe.IO.IN);
		EnergyStored -= voltage;
		return ActionResult.SUCCESS;
	}

	ActionResult IRecipeLogicMachine.DepositOutputEU(Api.Recipe.GTRecipe recipe, long voltage)
	{
		if (voltage <= 0) return ActionResult.SUCCESS;
		long room = EnergyCapacity - EnergyStored;
		if (room < voltage)
			return ActionResult.Fail("gtceu.recipe.eu_buffer_full", EURecipeCapability.CAP, Api.Capability.Recipe.IO.OUT);
		EnergyStored += voltage;
		return ActionResult.SUCCESS;
	}

	private ReLogic.Utilities.SlotId _loopSlot;
	private MachineAudioTracker? _loopTracker;

	Vector2 IRecipeLogicMachine.GetWorldPos() =>
		new Vector2(Position.X * 16, Position.Y * 16);

	void IRecipeLogicMachine.EnsureLoopSound(Vector2 worldPos)
	{
		if (Terraria.Audio.SoundEngine.TryGetActiveSound(_loopSlot, out var existing) && existing is not null)
			return;

		var style = StationSounds.TryGetLoop(GetRecipeType().RegistryName);
		if (style is null) return;

		// Canonical tML pattern: SoundUpdateCallback owns the shutdown decision
		_loopTracker = new MachineAudioTracker(this);
		var tracker = _loopTracker;
		_loopSlot = Terraria.Audio.SoundEngine.PlaySound(style.Value, worldPos, tracker.Tick);
	}

	void IRecipeLogicMachine.StopLoopSound()
	{
		_loopTracker?.MarkStopped();
		_loopTracker = null;
		if (Terraria.Audio.SoundEngine.TryGetActiveSound(_loopSlot, out var sound) && sound is not null)
			sound.Stop();
		_loopSlot = ReLogic.Utilities.SlotId.Invalid;
	}

	void IRecipeLogicMachine.PlayFinishSound(Vector2 worldPos)
	{
		Terraria.Audio.SoundEngine.PlaySound(StationSounds.DefaultFinish, worldPos);
	}

	internal override void OnClientSync()
	{
		base.OnClientSync();
		var rl = (IRecipeLogicMachine)this;
		var worldPos = rl.GetWorldPos();
		if (Recipe.IsWorking()) rl.EnsureLoopSound(worldPos);
		else                    rl.StopLoopSound();
	}

	private static Ingredient PeelToInner(Ingredient ing) => ing switch
	{
		SizedIngredient sized          => PeelToInner(sized.Inner),
		IntProviderIngredient ipi      => PeelToInner(ipi.Inner),
		IntProviderFluidIngredient ipf => ipf.Inner,
		_                              => ing,
	};

	private static int CountOf(Ingredient ing) => ing switch
	{
		SizedIngredient sized      => sized.Amount,
		IntProviderIngredient ipi  => ipi.RollSampledCount(),
		_                          => 1,
	};

	private static int PrimaryItemType(Ingredient ing) => PeelToInner(ing) switch
	{
		ItemStackIngredient isi    => isi.ItemType,
		NBTPredicateIngredient nbt => nbt.ItemType,
		TagIngredient tag          => tag.GetItems().Count > 0 ? tag.GetItems()[0].type : 0,
		_                          => 0,
	};

	private static bool TryExtractFluid(Ingredient ing, out FluidType ft, out int amount)
	{
		ft = null!; amount = 0;
		switch (ing)
		{
			case IntProviderFluidIngredient ipfi:
				if (!TryExtractFluidType(ipfi.Inner, out ft)) return false;
				amount = ipfi.RollSampledCount();
				return true;
			case FluidIngredient fi:
				if (!TryExtractFluidType(fi, out ft)) return false;
				amount = fi.Amount;
				return true;
		}
		return false;
	}

	private static bool TryExtractFluidType(FluidIngredient fi, out FluidType ft)
	{
		if (fi.ExactType is not null) { ft = fi.ExactType; return true; }
		var fluids = fi.GetFluids();
		if (fluids.Count > 0) { ft = fluids[0]; return true; }
		ft = null!;
		return false;
	}


	public override void OnKill()
	{
		base.OnKill();
		((IRecipeLogicMachine)this).StopLoopSound();
	}

	public override void SaveData(TagCompound tag)
	{
		EnsureTraits();
		base.SaveData(tag);
		tag["activeEut"] = _activeEut;
		if (_lastRecipeId is not null) tag["recipe"] = _lastRecipeId;
	}

	public override void LoadData(TagCompound tag)
	{
		EnsureTraits();
		base.LoadData(tag);
		_activeEut       = tag.GetLong("activeEut");
		_lastRecipeId    = tag.ContainsKey("recipe") ? tag.GetString("recipe") : null;
	}

	public override void AppendTooltip(List<string> lines)
	{
		base.AppendTooltip(lines);
		lines.Add(RecipeStatusText.StatusLine(_recipeLogic));
		if (_recipeLogic is { } rl && rl.IsWorking())
		{
			string? primaryOutput = ResolvePrimaryOutputName(rl.GetLastRecipe());
			if (primaryOutput != null)
				lines.Add($"-> {primaryOutput}");
			if (_activeEut > 0)
				lines.Add($"Drawing: {_activeEut:N0} EU/t");
		}
		RecipeStatusText.AppendFailureDetail(_recipeLogic, lines);
	}

	// Display-only first recipe output for tooltip
	private static string? ResolvePrimaryOutputName(Api.Recipe.GTRecipe? recipe)
	{
		if (recipe == null) return null;
		if (recipe.Outputs.TryGetValue(ItemRecipeCapability.CAP, out var contents))
		{
			foreach (var content in contents)
			{
				if (content.Payload is not Api.Recipe.Ingredient.Ingredient ing) continue;
				int type = ResolveItemType(ing);
				if (type <= 0) continue;
				return Terraria.Lang.GetItemName(type).Value;
			}
		}
		if (recipe.Outputs.TryGetValue(FluidRecipeCapability.CAP, out var fluidContents))
		{
			foreach (var content in fluidContents)
				if (content.Payload is Api.Recipe.Ingredient.FluidIngredient fi && fi.GetFluids().Count > 0)
					return fi.GetFluids()[0].DisplayName;
		}
		return null;
	}

	private static int ResolveItemType(Api.Recipe.Ingredient.Ingredient ing) => ing switch
	{
		Api.Recipe.Ingredient.SizedIngredient sized      => ResolveItemType(sized.Inner),
		Api.Recipe.Ingredient.ItemStackIngredient isi    => isi.ItemType,
		Api.Recipe.Ingredient.NBTPredicateIngredient nbt => nbt.ItemType,
		Api.Recipe.Ingredient.TagIngredient tag          => tag.GetItems().Count > 0 ? tag.GetItems()[0].type : 0,
		_                                                => 0,
	};
}
