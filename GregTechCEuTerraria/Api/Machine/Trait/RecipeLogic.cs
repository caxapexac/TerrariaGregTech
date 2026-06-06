#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature;
using Status = GregTechCEuTerraria.Api.Machine.Feature.RecipeLogicStatus;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Chance.Logic;
using GregTechCEuTerraria.Api.Recipe.Modifier;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using Terraria;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.Api.Machine.Trait;

// port of com.gregtechceu.gtceu.api.machine.trait.RecipeLogic.
//
// adaptations:
//   - Component -> string
//   - ChanceCacheMap -> flat Dictionary<string, int> keyed by ChanceKey(Ingredient)
//   - @SaveField, @SyncToClient, ClientFieldChangeListener dropped - MachineStateSyncPacket carries the Save() blob
//   - RecipeHelper.matchContents / handleRecipeIO / matchTickRecipe collapsed
//     into IRecipeLogicMachine.TryMatchInputContents / TryConsumeInputContents
//     / HasOutputRoomContents / DepositOutputContents. Items and fluids are
//     passed as separate args. ActionResult preserved so EU brownout
//     detection works precisely (gates on io == IN && capability == EU).
//   - MultiblockControllerCover detection collapsed to IRecipeLogicMachine.PreventPowerFail
//   - IFancyTooltip dropped
public class RecipeLogic : MachineTrait, IWorkable
{
	public static readonly MachineTraitType<RecipeLogic> TYPE = new(allowMultipleInstances: false);
	public override MachineTraitType TraitType => TYPE;

	// === Mutable state ==================

	public List<GTRecipe>? lastFailedMatches;

	private Status _status = Status.IDLE;
	public Status GetStatus() => _status;

	protected bool _isActive;

	protected string? _waitingReason;
	public string? GetWaitingReason() => _waitingReason;

	protected readonly List<string> _failureReasons = new();
	public IReadOnlyList<string> GetFailureReasons() => _failureReasons;

	protected readonly Dictionary<GTRecipe, string> _failureReasonMap = new();
	public IReadOnlyDictionary<GTRecipe, string> GetFailureReasonMap() => _failureReasonMap;

	protected GTRecipe? _lastRecipe;
	public GTRecipe? GetLastRecipe() => _lastRecipe;

	protected int _consecutiveRecipes = 0;
	public int GetConsecutiveRecipes() => _consecutiveRecipes;

	protected GTRecipe? _lastOriginRecipe;
	public GTRecipe? GetLastOriginRecipe() => _lastOriginRecipe;

	protected int _progress;
	public int GetProgress() => _progress;

	protected int _duration;

	protected bool _recipeDirty;
	public bool IsRecipeDirty() => _recipeDirty;

	protected long _totalContinuousRunningTime;
	public long GetTotalContinuousRunningTime() => _totalContinuousRunningTime;

	protected int _runAttempt = 0;
	protected int _runDelay   = 0;

	protected bool _suspendAfterFinish = false;
	public bool IsSuspendAfterFinish() => _suspendAfterFinish;
	public void SetSuspendAfterFinish(bool v) => _suspendAfterFinish = v;

	// Chance accumulator - flat Dictionary<string, int> keyed by ChanceKey(Ingredient)
	protected readonly Dictionary<string, int> _chanceCaches = new();
	public IReadOnlyDictionary<string, int> GetChanceCaches() => _chanceCaches;
	private static readonly Random Rng = new();

	protected TickableSubscription? _subscription;

	protected object? _workingSound;

	// === Construction / attachment ==========================================

	public RecipeLogic() : base() { }

	public IRecipeLogicMachine GetRLMachine() => (IRecipeLogicMachine)Machine;

	protected override IReadOnlyList<Type> ValidMachineClasses() =>
		new[] { typeof(IRecipeLogicMachine) };

	public void ResetRecipeLogic()
	{
		_recipeDirty = false;
		_lastRecipe = null;
		_lastOriginRecipe = null;
		_consecutiveRecipes = 0;
		_progress = 0;
		_duration = 0;
		_isActive = false;
		lastFailedMatches = null;
		_waitingReason = null;
		_failureReasons.Clear();
		if (_status != Status.SUSPEND)
			SetStatus(Status.IDLE);
		UpdateTickSubscription();
	}

	public override void OnMachineLoad()
	{
		base.OnMachineLoad();
		TryRestoreLastRecipe();
		UpdateTickSubscription();
	}

	public void TryRestoreLastRecipe()
	{
		if (_lastRecipe != null) return;
		if (!(IsWorking() || IsWaiting())) return;
		var m = GetRLMachine();
		var rid = m.LastRecipeId;
		if (!string.IsNullOrEmpty(rid))
			_lastRecipe = m.GetRecipeType()?.GetRecipeById(rid!);
	}

	public void UpdateTickSubscription()
	{
		if (IsSuspend() || !GetRLMachine().IsRecipeLogicAvailable())
		{
			if (_subscription is not null)
			{
				_subscription.Unsubscribe();
				_subscription = null;
			}
		}
		else
		{
			_subscription = SubscribeServerTick(_subscription, ServerTick);
		}
	}

	public void SetProgress(int progress) { _progress = progress; }

	public double GetProgressPercent() => _duration == 0 ? 0.0 : _progress / (_duration * 1.0);

	public virtual void ServerTick()
	{
		if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) != 0) return;

		var machine = GetRLMachine();

		if (!IsSuspend())
		{
			if (!IsIdle() && _lastRecipe != null)
			{
				if (_progress < _duration)
				{
					if (_runDelay > 0)
					{
						_runDelay--;
					}
					else
					{
						HandleRecipeWorking();
					}
				}
				if (_progress >= _duration)
				{
					OnRecipeFinish();
				}
			}
			else if (_lastRecipe != null)
			{
				FindAndHandleRecipe();
			}
			else if (!machine.KeepSubscribing() || machine.OffsetTimer % global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(5) == 0)
			{
				FindAndHandleRecipe();
				if (lastFailedMatches != null)
				{
					foreach (var match in lastFailedMatches)
					{
						if (CheckMatchedRecipeAvailable(match)) break;
					}
				}
			}
		}
		bool unsubscribe = false;
		if (IsSuspend())
		{
			// Machine is paused and can unsubscribe.
			unsubscribe = true;
		}
		else if (_lastRecipe == null && IsIdle() && !machine.KeepSubscribing() && !_recipeDirty &&
		         lastFailedMatches == null)
		{
			// No recipes available and the machine wants to unsubscribe until notified.
			unsubscribe = true;
		}
		if (IsIdle())
		{
			_failureReasons.Clear();
			_failureReasons.AddRange(_failureReasonMap.Values);
		}
		if (unsubscribe && _subscription != null)
		{
			_subscription.Unsubscribe();
			_subscription = null;
		}
	}

	protected virtual ActionResult MatchRecipe(GTRecipe recipe)
		=> RecipeHelper.MatchContents(GetRLMachine(), recipe);

	protected ActionResult CheckRecipe(GTRecipe recipe)
	{
		var conditionResult = CheckConditions(recipe);
		if (!conditionResult.IsSuccess) return conditionResult;

		long voltageCap = GetRLMachine().RecipeVoltageCap;
		if (recipe.InputEUt.Voltage > voltageCap)
			return ActionResult.Fail("gtceu.recipe.eu_too_high", EURecipeCapability.CAP, IO.IN);

		return MatchRecipe(recipe);
	}

	protected ActionResult CheckConditions(GTRecipe recipe)
	{
		foreach (var condition in recipe.Conditions)
		{
			if (!condition.Test(this))
			{
				string baseKey = $"gtceu.recipe.condition.{condition.GetTypeName()}";
				string msg = condition.GetFailureMessage(this);
				string reason = string.IsNullOrEmpty(msg) ? baseKey : $"{baseKey}|{msg}";
				return new ActionResult(false, reason, null, null);
			}
		}
		return ActionResult.SUCCESS;
	}

	private static Ingredient PeelToInner(Ingredient ing) => ing switch
	{
		SizedIngredient sized          => PeelToInner(sized.Inner),
		IntProviderIngredient ipi      => PeelToInner(ipi.Inner),
		IntProviderFluidIngredient ipf => ipf.Inner,
		_                              => ing,
	};

	public virtual bool CheckMatchedRecipeAvailable(GTRecipe match)
	{
		// Deviation from upstream: pre-screen the raw recipe against the
		// machine's inputs BEFORE running FullModifyRecipe. Otherwise modifier-
		// side cancellations (insufficient_voltage / coil_temperature_too_low
		// / wrong_machine_type - see GTRecipeModifiers) record their reason
		// for every too-high-V candidate while the input bus is empty, and an
		// idle multi surfaces "Voltage Tier Too Low" on hover even though the
		// player hasn't put anything in
		var rawMatch = MatchRecipe(match);
		if (!rawMatch.IsSuccess)
		{
			PutFailureReason(this, match, rawMatch.ReasonText());
			return false;
		}

		var modified = GetRLMachine().FullModifyRecipe(match);
		if (modified != null)
		{
			var recipeMatch = CheckRecipe(modified);
			if (recipeMatch.IsSuccess)
			{
				SetupRecipe(modified);
			}
			else
			{
				PutFailureReason(this, match, recipeMatch.ReasonText());
			}
			if (_lastRecipe != null && GetStatus() == Status.WORKING)
			{
				_lastOriginRecipe = match;
				lastFailedMatches = null;
				return true;
			}
		}
		else
		{
			var reason = GetRLMachine().GetLastModifierFailReason() ?? ModifierFunction.DEFAULT_FAILURE;
			PutFailureReason(this, match, reason);
		}
		return false;
	}

	public void HandleRecipeWorking()
	{
		var conditionResult = CheckConditions(_lastRecipe!);
		if (conditionResult.IsSuccess)
		{
			var handleTick = HandleTickRecipe(_lastRecipe!);
			if (handleTick.IsSuccess)
			{
				SetStatus(Status.WORKING);
				if (!GetRLMachine().OnWorking())
				{
					InterruptRecipe();
					return;
				}
				_progress++;
				_totalContinuousRunningTime++;
			}
			else
			{
				SetWaiting(handleTick.ReasonText());

				if (handleTick.Io == IO.IN && ReferenceEquals(handleTick.Capability, EURecipeCapability.CAP))
				{
					_runAttempt++;
					_runAttempt = Math.Clamp(_runAttempt, 0, 5);
					if (_runAttempt == 5)
					{
						bool preventPowerFail = GetRLMachine().PreventPowerFail();
						if (GetRLMachine().IsMultiblockController() && !preventPowerFail)
						{
							_runAttempt = 0;
							SetStatus(Status.SUSPEND);
						}
					}
					_runDelay = _runAttempt * 60;
				}
			}
		}
		else
		{
			SetWaiting(conditionResult.ReasonText());
		}
		if (IsWaiting() || IsSuspend())
		{
			RegressRecipe();
		}
	}

	protected void RegressRecipe()
	{
		if (_progress > 0 && GetRLMachine().RegressWhenWaiting())
		{
			_progress = 1;
		}
	}

	public IEnumerator<GTRecipe> SearchRecipe()
	{
		return GetRLMachine().GetRecipeType().SearchRecipe(GetRLMachine(), _ => true).GetEnumerator();
	}

	public void FindAndHandleRecipe()
	{
		lastFailedMatches = null;

		if (!_recipeDirty && _lastRecipe != null && CheckRecipe(_lastRecipe).IsSuccess)
		{
			GTRecipe recipe = _lastRecipe;
			_lastRecipe = null;
			_lastOriginRecipe = null;
			SetupRecipe(recipe);
		}
		else
		{
			_failureReasonMap.Clear();
			_lastRecipe = null;
			_lastOriginRecipe = null;
			HandleSearchingRecipes(SearchRecipe());
		}
		_recipeDirty = false;
	}

	protected void HandleSearchingRecipes(IEnumerator<GTRecipe> matches)
	{
		while (matches.MoveNext())
		{
			GTRecipe match = matches.Current;

			if (CheckMatchedRecipeAvailable(match))
				return;

			if (!MatchRecipe(match).IsSuccess)
			{
				continue;
			}

			lastFailedMatches ??= new List<GTRecipe>();
			lastFailedMatches.Add(match);
		}
	}

	public ActionResult HandleTickRecipe(GTRecipe recipe)
	{
		if (!recipe.HasTick()) return ActionResult.SUCCESS;

		var result = MatchTickRecipe(recipe);
		if (!result.IsSuccess) return result;

		result = HandleTickRecipeIO(recipe, IO.IN);
		if (!result.IsSuccess) return result;

		result = HandleTickRecipeIO(recipe, IO.OUT);
		return result;
	}

	protected ActionResult MatchTickRecipe(GTRecipe recipe)
		=> RecipeHelper.HandleRecipe(GetRLMachine(), recipe, IO.IN, isTick: true, simulate: true);

	protected virtual ActionResult HandleTickRecipeIO(GTRecipe recipe, IO io)
		=> RecipeHelper.HandleRecipe(GetRLMachine(), recipe, io, isTick: true, simulate: false);

	public virtual void SetupRecipe(GTRecipe recipe)
	{
		if (!GetRLMachine().BeforeWorking(recipe))
		{
			SetStatus(Status.IDLE);
			_consecutiveRecipes = 0;
			_progress = 0;
			_duration = 0;
			_isActive = false;
			return;
		}
		var handledIO = HandleRecipeIO(recipe, IO.IN);
		if (handledIO.IsSuccess)
		{
			if (_lastRecipe != null && !recipe.Equals(_lastRecipe))
			{
				_chanceCaches.Clear();
			}
			_failureReasonMap.Clear();
			_recipeDirty = false;
			_lastRecipe = recipe;
			SetStatus(Status.WORKING);
			_progress = 0;
			_duration = recipe.Duration;
			// Adaptation: ActiveEut is a machine-side display value (the UI
			// EU/t label). Upstream has no equivalent - per-tick EU is read
			// from the recipe's tickInputs each tick. We cache the post-modifier
			// real EU/t so the UI matches actual consumption.
			GetRLMachine().ActiveEut = RecipeHelper.GetRealEUt(recipe).GetTotalEU();
			_isActive = true;
			GetRLMachine().LastRecipeId = recipe.Id;
		}
	}

	protected virtual ActionResult HandleRecipeIO(GTRecipe recipe, IO io)
		=> RecipeHelper.HandleRecipe(GetRLMachine(), recipe, io, isTick: false, simulate: false);

	public void SetStatus(Status status)
	{
		if (_status != status)
		{
			if (_status == Status.WORKING)
			{
				_totalContinuousRunningTime = 0;
			}
			if ((status == Status.WAITING || status == Status.SUSPEND) && _suspendAfterFinish)
			{
				status = Status.SUSPEND;
				_suspendAfterFinish = false;
			}
			GetRLMachine().NotifyStatusChanged(
				_status, status);
			_status = status;
			UpdateTickSubscription();
			if (_status != Status.WAITING)
			{
				_waitingReason = null;
			}
		}
	}

	public void SetWaiting(string? reason)
	{
		SetStatus(Status.WAITING);
		_waitingReason = reason;
		GetRLMachine().OnWaiting();
	}

	public void MarkLastRecipeDirty() => _recipeDirty = true;

	public bool IsWorking() => _status == Status.WORKING;
	public bool IsIdle()    => _status == Status.IDLE;
	public bool IsWaiting() => _status == Status.WAITING;
	public bool IsSuspend() => _status == Status.SUSPEND;

	public bool IsWorkingEnabled() => !IsSuspend() && !IsSuspendAfterFinish();

	public void SetWorkingEnabled(bool isWorkingAllowed)
	{
		if (!isWorkingAllowed && GetStatus() == Status.IDLE)
		{
			SetStatus(Status.SUSPEND);
		}
		else
		{
			SetSuspendAfterFinish(!isWorkingAllowed);
			if (isWorkingAllowed)
			{
				if (_lastRecipe != null && _duration > 0)
				{
					SetStatus(Status.WORKING);
				}
				else
				{
					SetStatus(Status.IDLE);
				}
			}
		}
	}

	public int GetMaxProgress() => _duration;

	public bool IsActive() => IsWorking() || IsWaiting() || (IsSuspend() && _isActive);

	public virtual bool HasCustomProgressLine() => false;
	public virtual string? GetCustomProgressLine() => null;

	public void OnRecipeFinish()
	{
		GetRLMachine().AfterWorking();
		if (_lastRecipe != null)
		{
			_runAttempt = 0;
			_runDelay = 0;
			_consecutiveRecipes++;
			HandleRecipeIO(_lastRecipe, IO.OUT);
			if (_suspendAfterFinish)
			{
				SetStatus(Status.SUSPEND);
				_consecutiveRecipes = 0;
				_progress = 0;
				_duration = 0;
				_isActive = false;
				_lastRecipe = null;
				GetRLMachine().LastRecipeId = null;
				return;
			}
			if (GetRLMachine().AlwaysTryModifyRecipe())
			{
				if (_lastOriginRecipe != null)
				{
					var modified = GetRLMachine().FullModifyRecipe(_lastOriginRecipe.Copy());
					if (modified == null)
					{
						MarkLastRecipeDirty();
					}
					else
					{
						_lastRecipe = modified;
					}
				}
				else
				{
					MarkLastRecipeDirty();
				}
			}
			// Try it again
			var recipeCheck = CheckRecipe(_lastRecipe!);
			if (!_recipeDirty && recipeCheck.IsSuccess)
			{
				SetupRecipe(_lastRecipe!);
			}
			else
			{
				SetStatus(Status.IDLE);
				_consecutiveRecipes = 0;
				_progress = 0;
				_duration = 0;
				_isActive = false;
			}
		}
	}

	public void InterruptRecipe()
	{
		GetRLMachine().AfterWorking();
		if (_lastRecipe != null)
		{
			SetStatus(Status.IDLE);
			_progress = 0;
			_duration = 0;
		}
	}

	// === Chance roll - mirror of upstream ChanceLogic.OR ====================
	//
	// Upstream ChanceLogic.OR (ChanceLogic.java:42-69):
	//     cached  = previously stored "leftover chance" (random initial)
	//     chance  = newChance + cached
	//     while (chance >= maxChance) { produce one; chance -= maxChance;
	//                                   newChance -= maxChance; }
	//     cache[key] = newChance/2 + cached
	//
	// Makes probabilistic outputs deterministic over time. The flat key
	// space (vs upstream's per-capability IdentityHashMap) is documented
	// at the class level.
	public bool RollChance(Api.Recipe.Content.Content content)
	{
		int max = content.MaxChance;
		if (max <= 0 || content.Chance >= max) return true;
		int newChance = content.Chance;
		string key = ChanceKey((Ingredient)content.Payload);
		if (!_chanceCaches.TryGetValue(key, out int cached))
			cached = Rng.Next(max);
		int chance = newChance + cached;
		bool produced = false;
		if (chance >= max)
		{
			produced = true;
			newChance -= max;
		}
		_chanceCaches[key] = newChance / 2 + cached;
		return produced;
	}

	public static string ChanceKey(Ingredient ing) => PeelToInner(ing) switch
	{
		ItemStackIngredient isi      => $"item:{(string.IsNullOrEmpty(isi.UpstreamId) ? isi.ItemType.ToString() : isi.UpstreamId)}",
		TagIngredient tag            => $"tag:{tag.TagName}",
		NBTPredicateIngredient nbt   => $"nbt:{nbt.UpstreamId}:{nbt.ItemType}",
		IntCircuitIngredient ic      => $"circuit:{ic.Configuration}",
		FluidIngredient fi           => fi.ExactType is not null ? $"fluid:{fi.ExactType.Id}"
		                              : fi.TagName  is not null ? $"fluidtag:{fi.TagName}"
		                              : fi.Attribute is not null ? $"fluidattr:{fi.Attribute.Id}"
		                              : "fluid:?",
		_                            => $"?:{ing.GetTypeName()}",
	};

	public static void PutFailureReason(object machine, GTRecipe recipe, string reason)
	{
		if (machine is IRecipeLogicMachine rlm)
			PutFailureReason(rlm.GetRecipeLogic(), recipe, reason);
	}

	// Priority ranking for failure reasons - higher = more informative for
	// the player. Used by `Save()` to pick which reason to ship when many
	// recipes fail simultaneously. Ordering:
	//   - `insufficient_out`: inputs DID match; output blocked.        Most actionable.
	//   - `insufficient_eu` / `eu_too_high`: inputs+outputs OK; power. Actionable.
	//   - `recipe_modifier.*`: specific modifier rejection (no rotor, ...). Actionable.
	//   - `recipe.condition.*`: an environmental gate failed.          Actionable.
	//   - `recipe_logic.no_capabilities` / `no_contents`: structural.  Less actionable.
	//   - `recipe_logic.insufficient_in`: most-common noise.           Least actionable.
	//   - everything else: mid-tier default.
	private static int RankFailureReason(string r) => r switch
	{
		"gtceu.recipe_logic.insufficient_out" => 100,
		"gtceu.recipe.insufficient_eu"        => 90,
		"gtceu.recipe.eu_too_high"            => 85,
		_ when r.StartsWith("gtceu.recipe_modifier.", System.StringComparison.Ordinal) => 80,
		_ when r.StartsWith("gtceu.recipe.condition.", System.StringComparison.Ordinal) => 70,
		"gtceu.recipe_logic.no_capabilities"  => 50,
		"gtceu.recipe_logic.no_contents"      => 45,
		"gtceu.recipe_logic.insufficient_in"  => 10,
		_                                     => 30,
	};

	public static void PutFailureReason(RecipeLogic logic, GTRecipe recipe, string reason)
	{
		var map = logic._failureReasonMap;
		if (map.ContainsKey(recipe))
		{
			if (!string.IsNullOrEmpty(reason)) map[recipe] = reason;
		}
		else
		{
			map[recipe] = reason;
		}
	}


	public override void Save(TagCompound tag) => WriteCore(tag, includeTransient: true);

	public override void SaveForSync(TagCompound tag)
	{
		WriteCore(tag, includeTransient: false);
		if (Machine.ViewerCount > 0 && _lastRecipe != null)
			tag["lastRecipeBlob"] = GTRecipeNbt.Save(_lastRecipe);
	}

	private void WriteCore(TagCompound tag, bool includeTransient)
	{
		tag["status"]                      = (byte)_status;
		tag["isActive"]                    = _isActive;
		tag["waitingReason"]               = _waitingReason ?? string.Empty;
		if (_failureReasons.Count > 0)
		{
			string? best = null;
			int bestRank = int.MinValue;
			foreach (var r in _failureReasons)
			{
				int rank = RankFailureReason(r);
				if (rank > bestRank) { bestRank = rank; best = r; }
			}
			if (best is not null) tag["failureReasons"] = new List<string> { best };
		}
		tag["consecutiveRecipes"]          = _consecutiveRecipes;
		tag["duration"]                    = _duration;
		tag["suspendAfterFinish"]          = _suspendAfterFinish;
		tag["recipeDirty"]                 = _recipeDirty;

		if (includeTransient)
		{
			tag["progress"]                    = _progress;
			tag["totalContinuousRunningTime"]  = _totalContinuousRunningTime;
			tag["runAttempt"]                  = _runAttempt;
			tag["runDelay"]                    = _runDelay;
			if (_chanceCaches.Count > 0)
			{
				var cc = new TagCompound();
				foreach (var (k, v) in _chanceCaches) cc[k] = v;
				tag["chanceCaches"] = cc;
			}
			if (_lastRecipe != null)       tag["lastRecipeBlob"]       = GTRecipeNbt.Save(_lastRecipe);
			if (_lastOriginRecipe != null) tag["lastOriginRecipeBlob"] = GTRecipeNbt.Save(_lastOriginRecipe);
		}
	}

	public override void Load(TagCompound tag)
	{
		var prevStatus = _status;
		var prevConsecutive = _consecutiveRecipes;

		if (tag.ContainsKey("status"))                    _status                     = (Status)tag.GetByte("status");
		if (tag.ContainsKey("isActive"))                  _isActive                   = tag.GetBool("isActive");
		if (tag.ContainsKey("waitingReason"))             _waitingReason              = tag.GetString("waitingReason") is var s && s.Length == 0 ? null : s;
		_failureReasons.Clear();
		if (tag.ContainsKey("failureReasons"))            _failureReasons.AddRange(tag.GetList<string>("failureReasons"));
		if (tag.ContainsKey("consecutiveRecipes"))        _consecutiveRecipes         = tag.GetInt("consecutiveRecipes");
		if (tag.ContainsKey("duration"))                  _duration                   = tag.GetInt("duration");
		if (tag.ContainsKey("totalContinuousRunningTime")) _totalContinuousRunningTime = tag.GetLong("totalContinuousRunningTime");
		if (tag.ContainsKey("suspendAfterFinish"))        _suspendAfterFinish         = tag.GetBool("suspendAfterFinish");
		if (tag.ContainsKey("runAttempt"))                _runAttempt                 = tag.GetInt("runAttempt");
		if (tag.ContainsKey("runDelay"))                  _runDelay                   = tag.GetInt("runDelay");
		if (tag.ContainsKey("recipeDirty"))               _recipeDirty                = tag.GetBool("recipeDirty");

		if (tag.ContainsKey("progress"))
		{
			_progress = tag.GetInt("progress");
		}
		else if (prevStatus != _status || _consecutiveRecipes != prevConsecutive)
		{
			_progress = 0;
		}

		_chanceCaches.Clear();
		if (tag.ContainsKey("chanceCaches"))
		{
			var cc = tag.Get<TagCompound>("chanceCaches");
			foreach (var kv in cc)
				if (kv.Value is int i) _chanceCaches[kv.Key] = i;
		}

		if (tag.ContainsKey("lastRecipeBlob"))
			_lastRecipe = GTRecipeNbt.Load(tag.Get<TagCompound>("lastRecipeBlob")) ?? _lastRecipe;
		if (tag.ContainsKey("lastOriginRecipeBlob"))
			_lastOriginRecipe = GTRecipeNbt.Load(tag.Get<TagCompound>("lastOriginRecipeBlob")) ?? _lastOriginRecipe;
	}

	public override void OnClientTick()
	{
		if (Main.GameUpdateCount % (uint)global::GregTechCEuTerraria.Api.TickScale.FromMcTicks(1) != 0) return;
		if (_status == Status.WORKING && _progress < _duration)
			_progress++;
	}
}
