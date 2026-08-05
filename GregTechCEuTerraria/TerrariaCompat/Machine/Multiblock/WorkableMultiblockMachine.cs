#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Pattern;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Modifier;
using GregTechCEuTerraria.Common.Machine.Trait;
using GregTechCEuTerraria.TerrariaCompat.Net;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Microsoft.Xna.Framework;

namespace GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;

public abstract class WorkableMultiblockMachine : MultiblockControllerMachine, IWorkableMultiController, IVoidable
{
	private RecipeLogic? _recipeLogic;
	public RecipeLogic Recipe { get { EnsureRecipeLogic(); return _recipeLogic!; } }

	public int ActiveRecipeType { get; private set; }

	public void SetActiveRecipeType(int idx)
	{
		var types = GetRecipeTypes();
		if (types.Length == 0) return;
		idx = System.Math.Clamp(idx, 0, types.Length - 1);
		if (idx == ActiveRecipeType) return;
		ActiveRecipeType = idx;
		Recipe.MarkLastRecipeDirty();
		Recipe.UpdateTickSubscription();
		if (IsServer) MachineStateSyncPacket.Broadcast(this);
	}

	public Dictionary<IO, List<RecipeHandlerList>> CapabilitiesProxy { get; } = new();
	public Dictionary<IO, Dictionary<object, List<object>>> CapabilitiesFlat { get; } = new();

	public bool IsMuffled { get; private set; }

	private GTRecipeType[]? _recipeTypes;
	public GTRecipeType[] GetRecipeTypes()
	{
		if (_recipeTypes is not null) return _recipeTypes;
		var def = Definition;
		if (def?.RecipeTypes is { Length: > 0 } multi) return _recipeTypes = multi;
		if (def?.RecipeType is GTRecipeType single)    return _recipeTypes = new[] { single };
		return _recipeTypes = Array.Empty<GTRecipeType>();
	}

	private readonly List<ISubscription> _traitSubscriptions = new();

	public MultiblockVoidingMode VoidingMode { get; private set; } = MultiblockVoidingMode.VoidNone;
	public void SetVoidingMode(MultiblockVoidingMode mode)
	{
		VoidingMode = mode;
		_recipeLogic?.UpdateTickSubscription();
	}
	public MultiblockVoidingMode GetVoidingMode() => VoidingMode;

	public virtual bool CanVoidRecipeOutputs(object capability) =>
		VoidingMode.CanVoid(capability) ||
		(GetOutputLimits() is { } limits && limits.TryGetValue(capability, out var n) && n == 0);

	protected WorkableMultiblockMachine() : base() { }

	protected void EnsureRecipeLogic()
	{
		if (_recipeLogic is not null) return;
		BindDefinition();
		_recipeLogic = CreateRecipeLogic();
		Traits.Attach(_recipeLogic);
		Traits.RegisterPersistent("recipe", _recipeLogic);

		var cleanroomReceiver = new CleanroomReceiverTrait();
		Traits.Attach(cleanroomReceiver);
		Traits.RegisterPersistent("CleanroomReceiver", cleanroomReceiver);
	}

	protected virtual RecipeLogic CreateRecipeLogic() => new();

	public void SetMuffled(bool muffled) => IsMuffled = muffled;

	public MultiblockControllerMachine Self() => this;

	public override void OnStructureFormed()
	{
		base.OnStructureFormed();
		EnsureRecipeLogic();

		foreach (var sub in _traitSubscriptions) sub.Unsubscribe();
		_traitSubscriptions.Clear();
		CapabilitiesProxy.Clear();
		CapabilitiesFlat.Clear();

		var ioMap = GetMultiblockState().MatchContext
			.GetOrDefault("ioMap", (Dictionary<long, IO>?)null) ?? new Dictionary<long, IO>();

		foreach (var part in GetParts())
		{
			var pos = part.Self().Position;
			long packed = Api.Pattern.MultiblockState.PackPos(pos.X, pos.Y);
			IO io = ioMap.TryGetValue(packed, out var partIo) ? partIo : IO.BOTH;
			if (io == IO.NONE) continue;

			foreach (var handlerList in part.GetRecipeHandlers())
			{
				if (!handlerList.IsValid(io)) continue;
				AddHandlerList(handlerList);
				_traitSubscriptions.Add(handlerList.Subscribe(_recipeLogic!.UpdateTickSubscription));
			}
		}

		var ioTraits = new Dictionary<IO, List<object>>();
		foreach (var trait in Traits.AllTraits)
		{
			if (trait is IRecipeHandlerTrait rht)
			{
				if (!ioTraits.TryGetValue(rht.GetHandlerIO(), out var list))
				{
					list = new List<object>();
					ioTraits[rht.GetHandlerIO()] = list;
				}
				list.Add(trait);
			}
		}
		foreach (var entry in ioTraits)
		{
			var handlerList = RecipeHandlerList.Of(entry.Key, entry.Value);
			AddHandlerList(handlerList);
			_traitSubscriptions.Add(handlerList.Subscribe(_recipeLogic!.UpdateTickSubscription));
		}

		_recipeLogic!.UpdateTickSubscription();
	}

	public override void OnStructureInvalid()
	{
		base.OnStructureInvalid();
		CapabilitiesProxy.Clear();
		CapabilitiesFlat.Clear();
		foreach (var sub in _traitSubscriptions) sub.Unsubscribe();
		_traitSubscriptions.Clear();
		_recipeLogic?.ResetRecipeLogic();
	}

	public override void OnPartUnload()
	{
		base.OnPartUnload();
		CapabilitiesProxy.Clear();
		CapabilitiesFlat.Clear();
		foreach (var sub in _traitSubscriptions) sub.Unsubscribe();
		_traitSubscriptions.Clear();
		_recipeLogic?.UpdateTickSubscription();
	}

	public void AddHandlerList(RecipeHandlerList handlerList)
	{
		if (handlerList == RecipeHandlerList.NO_DATA) return;
		IO io = handlerList.HandlerIO;

		if (!CapabilitiesProxy.TryGetValue(io, out var proxyList))
		{
			proxyList = new List<RecipeHandlerList>();
			CapabilitiesProxy[io] = proxyList;
		}
		proxyList.Add(handlerList);

		if (!CapabilitiesFlat.TryGetValue(io, out var inner))
		{
			inner = new Dictionary<object, List<object>>(handlerList.HandlerMap.Count);
			CapabilitiesFlat[io] = inner;
		}
		foreach (var entry in handlerList.HandlerMap)
		{
			if (!inner.TryGetValue(entry.Key, out var capList))
			{
				capList = new List<object>(entry.Value.Count);
				inner[entry.Key] = capList;
			}
			capList.AddRange(entry.Value);
		}
	}

	public GTRecipe? DoModifyRecipe(GTRecipe recipe)
	{
		foreach (var part in GetParts())
		{
			var modified = part.ModifyRecipe(recipe);
			if (modified is null) return null;
			recipe = modified;
		}
		return GetRealRecipe(recipe);
	}

	public virtual GTRecipe? FullModifyRecipe(GTRecipe recipe)
		=> DoModifyRecipe(Api.Recipe.RecipeHelper.TrimRecipeOutputs(recipe, GetOutputLimits()));

	public virtual bool PreventPowerFail() => HasPowerFailPreventingCover();

	private string? _lastModifierFailReason;
	public string? GetLastModifierFailReason() => _lastModifierFailReason;

	protected virtual GTRecipe? GetRealRecipe(GTRecipe recipe)
	{
		var modifier = GetRecipeModifier();
		var fn = modifier.GetModifier(this, recipe);
		var result = fn.Apply(recipe);
		_lastModifierFailReason = result == null ? fn.FailReason : null;
		return result;
	}

	public virtual RecipeModifier GetRecipeModifier() => RecipeModifier.NO_MODIFIER;

	public bool IsMultiblockController() => true;
	public bool KeepSubscribing() => false;
	public virtual bool IsRecipeLogicAvailable() => IsFormed && !GetMultiblockState().HasError();

	public void AfterWorking()
	{
		foreach (var part in GetParts()) part.AfterWorking(this);
	}

	public bool BeforeWorking(GTRecipe? recipe)
	{
		foreach (var part in GetParts())
		{
			if (!part.BeforeWorking(this)) return false;
		}
		return true;
	}

	public virtual bool OnWorking()
	{
		foreach (var part in GetParts())
		{
			if (!part.OnWorking(this)) return false;
		}
		return true;
	}

	public virtual bool RegressWhenWaiting() => true;

	public void OnWaiting()
	{
		foreach (var part in GetParts()) part.OnWaiting(this);
	}

	public virtual void SetWorkingEnabled(bool isWorkingAllowed)
	{
		if (!isWorkingAllowed)
		{
			foreach (var part in GetParts()) part.OnPaused(this);
		}
		_recipeLogic?.SetWorkingEnabled(isWorkingAllowed);
	}

	public GTRecipeType GetRecipeType()
	{
		var types = GetRecipeTypes();
		if (types.Length == 0)
			throw new InvalidOperationException(
				$"WorkableMultiblockMachine {GetType().Name} has no RecipeType(s) on its MachineDefinition.");
		int idx = ActiveRecipeType;
		if (idx < 0 || idx >= types.Length) idx = 0;
		return types[idx];
	}

	protected override void SaveMachineData(Terraria.ModLoader.IO.TagCompound tag)
	{
		base.SaveMachineData(tag);
		tag["activeRecipeType"] = ActiveRecipeType;
		if (LastRecipeId is not null) tag["lastRecipeId"] = LastRecipeId;
	}

	public override void LoadData(Terraria.ModLoader.IO.TagCompound tag)
	{
		EnsureRecipeLogic();
		base.LoadData(tag);
		if (tag.ContainsKey("activeRecipeType"))
			ActiveRecipeType = tag.GetInt("activeRecipeType");
		LastRecipeId = tag.ContainsKey("lastRecipeId") ? tag.GetString("lastRecipeId") : null;
	}

	public long OffsetTimer => Math.Abs(HashCode.Combine(Position.X, Position.Y));

	protected virtual bool SupportsRecipeLookupCore => true;
	bool IRecipeLogicMachine.SupportsRecipeLookup => SupportsRecipeLookupCore;

	IReadOnlyList<Terraria.Item> IRecipeLogicMachine.LookupInputItems
	{
		get
		{
			var result = new List<Terraria.Item>();
			foreach (var handler in InputHandlersFor(ItemRecipeCapability.CAP))
			{
				if (handler is not IItemHandler ih) continue;
				for (int i = 0; i < ih.SlotCount; i++)
				{
					var stack = ih.GetSlot(i);
					if (!stack.IsAir) result.Add(stack);
				}
			}
			return result;
		}
	}

	IReadOnlyList<FluidStack> IRecipeLogicMachine.LookupInputFluids
	{
		get
		{
			var result = new List<FluidStack>();
			foreach (var handler in InputHandlersFor(FluidRecipeCapability.CAP))
			{
				if (handler is not IFluidHandler fh) continue;
				for (int i = 0; i < fh.TankCount; i++)
				{
					var stack = fh.GetTank(i);
					if (!stack.IsEmpty) result.Add(stack);
				}
			}
			return result;
		}
	}

	private IEnumerable<object> InputHandlersFor(object cap)
	{
		if (CapabilitiesFlat.TryGetValue(IO.IN, out var inMap) &&
			inMap.TryGetValue(cap, out var inList))
			foreach (var h in inList) yield return h;
		if (CapabilitiesFlat.TryGetValue(IO.BOTH, out var bothMap) &&
			bothMap.TryGetValue(cap, out var bothList))
			foreach (var h in bothList) yield return h;
	}

	public long RecipeVoltageCap => long.MaxValue;

	public virtual long EnergyStored { get; set; }

	public long ActiveEut   { get; set; }
	public string? LastRecipeId { get; set; }

	private ReLogic.Utilities.SlotId _loopSlot;
	private MachineAudioTracker? _loopTracker;

	public void EnsureLoopSound(Vector2 worldPos)
	{
		if (Terraria.Audio.SoundEngine.TryGetActiveSound(_loopSlot, out var existing) && existing is not null)
			return;

		var style = StationSounds.TryGetLoop(GetRecipeType().RegistryName);
		if (style is null) return;

		_loopTracker = new MachineAudioTracker(this);
		var tracker = _loopTracker;
		_loopSlot = Terraria.Audio.SoundEngine.PlaySound(style.Value, worldPos, tracker.Tick);
	}

	public void StopLoopSound()
	{
		_loopTracker?.MarkStopped();
		_loopTracker = null;
		if (Terraria.Audio.SoundEngine.TryGetActiveSound(_loopSlot, out var sound) && sound is not null)
			sound.Stop();
		_loopSlot = ReLogic.Utilities.SlotId.Invalid;
	}

	public void PlayFinishSound(Vector2 worldPos)
	{
		Terraria.Audio.SoundEngine.PlaySound(StationSounds.DefaultFinish, worldPos);
	}

	public Vector2 GetWorldPos() =>
		new(Position.X * 16f + Size.Width * 8f, Position.Y * 16f + Size.Height * 8f);

	public virtual void NotifyStatusChanged(RecipeLogicStatus oldStatus, RecipeLogicStatus newStatus)
	{
		if (newStatus == RecipeLogicStatus.WORKING) MachineLoopVoiceArbiter.SetWant(this);
		else                                        MachineLoopVoiceArbiter.ClearWant(this);
	}

	internal override void OnClientSync()
	{
		base.OnClientSync();
		if (Recipe.IsWorking()) MachineLoopVoiceArbiter.SetWant(this);
		else                    MachineLoopVoiceArbiter.ClearWant(this);
	}

	public RecipeLogic GetRecipeLogic() => Recipe;

	public virtual ActionResult TryMatchInputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids)
		=> HandleContentsThroughCapProxy(recipe, items, fluids, IO.IN, simulate: true);

	public virtual ActionResult HasOutputRoomContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids)
		=> HandleContentsThroughCapProxy(recipe, items, fluids, IO.OUT, simulate: true);

	public virtual ActionResult TryConsumeInputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids)
		=> HandleContentsThroughCapProxy(recipe, items, fluids, IO.IN, simulate: false);

	public virtual ActionResult DepositOutputContents(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids,
		RecipeLogic logic)
		=> HandleContentsThroughCapProxy(recipe, items, fluids, IO.OUT, simulate: false);

	public virtual ActionResult TryDrainEU(Api.Recipe.GTRecipe recipe, long voltage)
		=> HandleEUThroughCapProxy(recipe, voltage, IO.IN);

	public ActionResult DepositOutputEU(Api.Recipe.GTRecipe recipe, long voltage)
		=> HandleEUThroughCapProxy(recipe, voltage, IO.OUT);

	public virtual ActionResult TryHandleTickCwu(Api.Recipe.GTRecipe recipe, IO io, bool simulate)
	{
		var cwu = io == IO.IN
			? recipe.GetTickInputContents(CWURecipeCapability.CAP)
			: recipe.GetTickOutputContents(CWURecipeCapability.CAP);
		if (cwu.Count == 0) return ActionResult.SUCCESS;

		var list = new List<object>(cwu.Count);
		foreach (var c in cwu) list.Add(c.Payload);
		var contents = new Dictionary<object, List<object>> { [CWURecipeCapability.CAP] = list };
		return DispatchContents(recipe, io, contents, simulate);
	}

	private ActionResult HandleContentsThroughCapProxy(
		Api.Recipe.GTRecipe recipe,
		IReadOnlyList<Api.Recipe.Content.Content> items,
		IReadOnlyList<Api.Recipe.Content.Content> fluids,
		IO io, bool simulate)
	{
		var contents = new Dictionary<object, List<object>>();
		if (items.Count > 0)
		{
			var (match, consume) = RecipeContentSplit.Split(
				ItemRecipeCapability.CAP, items, io, isTick: false, recipe,
				chanceCache: null, totalRuns: recipe.GetTotalRuns());
			var picked = simulate ? match : consume;
			if (picked.Count > 0) contents[ItemRecipeCapability.CAP] = picked;
		}
		if (fluids.Count > 0)
		{
			var (match, consume) = RecipeContentSplit.Split(
				FluidRecipeCapability.CAP, fluids, io, isTick: false, recipe,
				chanceCache: null, totalRuns: recipe.GetTotalRuns());
			var picked = simulate ? match : consume;
			if (picked.Count > 0) contents[FluidRecipeCapability.CAP] = picked;
		}

		return DispatchContents(recipe, io, contents, simulate);
	}

	private ActionResult HandleEUThroughCapProxy(
		Api.Recipe.GTRecipe recipe, long voltage, IO io)
	{
		var contents = new Dictionary<object, List<object>>
		{
			[EURecipeCapability.CAP] = new List<object> { new Api.Recipe.Ingredient.EnergyStack(voltage, 1) },
		};
		return DispatchContents(recipe, io, contents, simulate: false);
	}

	private ActionResult DispatchContents(
		Api.Recipe.GTRecipe recipe, IO io,
		Dictionary<object, List<object>> contents, bool simulate)
	{
		if (contents.Count == 0) return ActionResult.PASS_NO_CONTENTS;

		if (!CapabilitiesProxy.TryGetValue(io, out var handlerLists) || handlerLists.Count == 0)
		{
			object? firstCap = null;
			foreach (var c in contents.Keys) { firstCap = c; break; }
			string capSuffix =
				  ReferenceEquals(firstCap, ItemRecipeCapability.CAP)  ? "item"
				: ReferenceEquals(firstCap, FluidRecipeCapability.CAP) ? "fluid"
				: ReferenceEquals(firstCap, EURecipeCapability.CAP)    ? "eu"
				: "any";
			string ioSuffix = io == IO.IN ? "in" : "out";
			return ActionResult.Fail($"gtceu.recipe.no_capabilities_{capSuffix}_{ioSuffix}", firstCap, io);
		}

		List<RecipeHandlerList> handlers = handlerLists;
		if (io == IO.OUT)
		{
			handlers = new List<RecipeHandlerList>(handlerLists);
			handlers.Sort((a, b) => RecipeHandlerList.COMPARATOR.Compare(b, a));
		}

		var handlerGroups = new Dictionary<RecipeHandlerGroup, List<RecipeHandlerList>>();
		foreach (var h in handlers)
			AddToRecipeHandlerMap(h.Group, h, handlerGroups);

		int groupColor = recipe.GroupColor;
		bool lockIn = io == IO.IN && simulate;

		if (handlerGroups.TryGetValue(RecipeHandlerGroupDistinctness.BUS_DISTINCT, out var distinctList))
		{
			foreach (var handler in distinctList)
			{
				var res = handler.HandleRecipe(io, recipe, contents, true);
				if (res.Count > 0 && handlerGroups.TryGetValue(
						RecipeHandlerGroupDistinctness.BYPASS_DISTINCT, out var bypass1))
				{
					foreach (var bypassHandler in bypass1)
					{
						res = bypassHandler.HandleRecipe(io, recipe, res, true);
						if (res.Count == 0) break;
					}
				}

				if (io == IO.OUT)
				{
					if (HasAnyNonVoidingContents(res)) continue;
				}
				else if (io == IO.IN)
				{
					if (res.Count > 0) continue;
				}

				if (!simulate)
				{
					contents = handler.HandleRecipe(io, recipe, contents, false);
					if (contents.Count > 0 && handlerGroups.TryGetValue(
							RecipeHandlerGroupDistinctness.BYPASS_DISTINCT, out var bypass2))
					{
						foreach (var bypassHandler in bypass2)
						{
							contents = bypassHandler.HandleRecipe(io, recipe, contents, false);
							if (contents.Count == 0) break;
						}
					}
				}
				contents.Clear();
				recipe.GroupColor = groupColor;
				return ActionResult.SUCCESS;
			}
		}

		foreach (var entry in handlerGroups)
		{
			if (entry.Key.Equals(RecipeHandlerGroupDistinctness.BUS_DISTINCT)) continue;

			if (entry.Key is RecipeHandlerGroupColor coloredGroup)
			{
				if (lockIn)
				{
					groupColor = coloredGroup.Color;
				}
				else if (coloredGroup.Color != -1 && coloredGroup.Color != groupColor)
				{
					continue;
				}
			}

			var copied = contents;
			foreach (var handler in entry.Value)
			{
				copied = handler.HandleRecipe(io, recipe, copied, true);
				if (copied.Count == 0) break;
			}
			if (!entry.Key.Equals(RecipeHandlerGroupDistinctness.BYPASS_DISTINCT) &&
				handlerGroups.TryGetValue(RecipeHandlerGroupDistinctness.BYPASS_DISTINCT, out var bypass3))
			{
				foreach (var bypassHandler in bypass3)
				{
					copied = bypassHandler.HandleRecipe(io, recipe, copied, true);
					if (copied.Count == 0) break;
				}
			}

			if (io == IO.OUT)
			{
				if (HasAnyNonVoidingContents(copied)) continue;
			}
			else if (io == IO.IN)
			{
				if (copied.Count > 0) continue;
			}

			if (simulate)
			{
				recipe.GroupColor = groupColor;
				return ActionResult.SUCCESS;
			}

			foreach (var handler in entry.Value)
			{
				contents = handler.HandleRecipe(io, recipe, contents, false);
				if (contents.Count == 0)
				{
					recipe.GroupColor = groupColor;
					return ActionResult.SUCCESS;
				}
			}
			if (!entry.Key.Equals(RecipeHandlerGroupDistinctness.BYPASS_DISTINCT) &&
				handlerGroups.TryGetValue(RecipeHandlerGroupDistinctness.BYPASS_DISTINCT, out var bypass4))
			{
				foreach (var bypassHandler in bypass4)
				{
					contents = bypassHandler.HandleRecipe(io, recipe, contents, false);
					if (contents.Count == 0)
					{
						recipe.GroupColor = groupColor;
						return ActionResult.SUCCESS;
					}
				}
			}
		}

		foreach (var kv in contents)
		{
			if (!simulate && io == IO.OUT && CanVoidRecipeOutputs(kv.Key))
				kv.Value?.Clear();
			if (kv.Value is not null && kv.Value.Count > 0)
			{
				recipe.GroupColor = groupColor;
				string reasonKey = io == IO.IN
					? "gtceu.recipe_logic.insufficient_in"
					: "gtceu.recipe_logic.insufficient_out";
				return ActionResult.Fail(reasonKey, kv.Key, io);
			}
		}

		recipe.GroupColor = groupColor;
		bool containsStuff = false;
		foreach (var kv in contents)
			if (kv.Value is not null && kv.Value.Count > 0) { containsStuff = true; break; }
		return containsStuff ? ActionResult.FAIL_NO_REASON : ActionResult.PASS_NO_CONTENTS;
	}

	private static void AddToRecipeHandlerMap(
		RecipeHandlerGroup key, RecipeHandlerList handler,
		Dictionary<RecipeHandlerGroup, List<RecipeHandlerList>> map)
	{
		if (handler.DoesCapabilityBypassDistinct())
		{
			if (!map.TryGetValue(RecipeHandlerGroupDistinctness.BYPASS_DISTINCT, out var bypass))
			{
				bypass = new List<RecipeHandlerList>();
				map[RecipeHandlerGroupDistinctness.BYPASS_DISTINCT] = bypass;
			}
			bypass.Add(handler);
			return;
		}

		if (key.Equals(RecipeHandlerGroupColor.UNDYED))
		{
			foreach (var entry in map)
			{
				if (entry.Key.Equals(RecipeHandlerGroupDistinctness.BUS_DISTINCT) ||
					entry.Key.Equals(RecipeHandlerGroupDistinctness.BYPASS_DISTINCT) ||
					entry.Key.Equals(RecipeHandlerGroupColor.UNDYED)) continue;
				entry.Value.Add(handler);
			}
		}

		map.TryGetValue(RecipeHandlerGroupColor.UNDYED, out var undyed);
		if (!map.TryGetValue(key, out var bucket))
		{
			bucket = undyed is null ? new List<RecipeHandlerList>() : new List<RecipeHandlerList>(undyed);
			map[key] = bucket;
		}
		bucket.Add(handler);
	}

	private bool HasAnyNonVoidingContents(Dictionary<object, List<object>> contents)
	{
		foreach (var entry in contents)
		{
			if (CanVoidRecipeOutputs(entry.Key)) continue;
			if (entry.Value is not null && entry.Value.Count > 0) return true;
		}
		return false;
	}

	public override bool IsActive => _recipeLogic?.IsActive() ?? false;

	int  IWorkable.GetProgress()    => _recipeLogic?.GetProgress() ?? 0;
	int  IWorkable.GetMaxProgress() => _recipeLogic?.GetMaxProgress() ?? 0;
	bool IWorkable.IsActive()       => _recipeLogic?.IsActive() ?? false;

	bool IControllable.IsWorkingEnabled()      => _recipeLogic?.IsWorkingEnabled() ?? true;
	void IControllable.SetWorkingEnabled(bool isWorkingAllowed) => SetWorkingEnabled(isWorkingAllowed);

	public override void AppendTooltip(System.Collections.Generic.List<string> lines)
	{
		base.AppendTooltip(lines);
		if (!IsFormed) return;
		lines.Add(RecipeStatusText.StatusLineForMulti(this, _recipeLogic));
		AppendEnergyLine(lines);
		if (_recipeLogic is { } rl && rl.IsWorking())
		{
			string? primaryOutput = ResolveActiveOutputName(rl.GetLastRecipe());
			if (primaryOutput != null)
				lines.Add($"-> {primaryOutput}");
			AppendDrawingLine(lines);
		}
		RecipeStatusText.AppendFailureDetail(_recipeLogic, lines);
	}

	protected virtual void AppendDrawingLine(List<string> lines)
	{
		if (ActiveEut > 0)
			lines.Add($"Drawing: {ActiveEut:N0} EU/t");
	}

	protected virtual void AppendEnergyLine(List<string> lines) { }

	private static string? ResolveActiveOutputName(Api.Recipe.GTRecipe? recipe)
	{
		if (recipe == null) return null;
		if (!recipe.Outputs.TryGetValue(Api.Capability.Recipe.ItemRecipeCapability.CAP, out var contents)) return null;
		foreach (var content in contents)
		{
			if (content.Payload is not Api.Recipe.Ingredient.Ingredient ing) continue;
			int type = ResolveItemType(ing);
			if (type <= 0) continue;
			return Terraria.Lang.GetItemName(type).Value;
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
