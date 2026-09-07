#nullable enable
using System;
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.Api.Recipe.Ingredient;
using GregTechCEuTerraria.Api.Transfer;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.Api.Machine.Trait;

public class NotifiableFluidTank
	: NotifiableRecipeHandlerTrait<FluidIngredient>, Capability.IFluidHandlerModifiable
{
	public static readonly MachineTraitType<NotifiableFluidTank> TYPE = new(allowMultipleInstances: true);
	public override MachineTraitType TraitType => TYPE;

	public IO HandlerIO { get; }
	public IO CapabilityIO { get; }
	public CustomFluidTank[] Storages { get; }

	public bool AllowSameFluids { get; protected set; }

	private bool _shouldSearchContent = true;
	public bool ShouldSearchContent
	{
		get => _shouldSearchContent;
		set => _shouldSearchContent = value;
	}

	private bool? _isEmpty;

	public CustomFluidTank LockedFluid { get; } = new(1000);

	public Predicate<FluidStack> Filter { get; protected set; } = _ => true;

	public NotifiableFluidTank(int slots, int capacity, IO io, IO capabilityIO)
	{
		HandlerIO = io;
		CapabilityIO = capabilityIO;
		Storages = new CustomFluidTank[slots];
		for (int i = 0; i < Storages.Length; i++)
		{
			Storages[i] = new CustomFluidTank(capacity);
			Storages[i].OnContentsChangedAction = OnContentsChanged;
		}
	}

	public NotifiableFluidTank(IList<CustomFluidTank> storages, IO io, IO capabilityIO)
	{
		HandlerIO = io;
		CapabilityIO = capabilityIO;
		Storages = new CustomFluidTank[storages.Count];
		for (int i = 0; i < storages.Count; i++)
		{
			Storages[i] = storages[i];
			Storages[i].OnContentsChangedAction = OnContentsChanged;
		}
		if (io == IO.IN) AllowSameFluids = true;
	}

	public NotifiableFluidTank(int slots, int capacity, IO io)
		: this(slots, capacity, io, io) { }

	public NotifiableFluidTank(IList<CustomFluidTank> storages, IO io)
		: this(storages, io, io) { }

	public void OnContentsChanged()
	{
		_isEmpty = null;
		NotifyListeners();
	}

	public override List<FluidIngredient>? HandleRecipeInner(IO io, GTRecipe recipe,
	                                                         List<FluidIngredient> left, bool simulate)
	{
		if (io != HandlerIO) return left;
		if (io != IO.IN && io != IO.OUT) return left.Count == 0 ? null : left;

		var listeners = new Action[Storages.Length];
		for (int i = 0; i < Storages.Length; i++)
		{
			listeners[i] = Storages[i].OnContentsChangedAction;
			Storages[i].OnContentsChangedAction = () => { };
		}
		bool changed = false;

		var visited = new FluidStack[Storages.Length];
		bool[] visitedSet = new bool[Storages.Length];

		for (int idx = 0; idx < left.Count; )
		{
			var ingredient = left[idx];
			if (ingredient.IsEmpty) { left.RemoveAt(idx); continue; }

			FluidStack[] fluids;
			if (ingredient is IntProviderFluidIngredient provider)
			{
				if (simulate)
				{
					fluids = new[] { provider.GetMaxSizeFluid().Length > 0 ? provider.GetMaxSizeFluid()[0] : FluidStack.Empty };
				}
				else
				{
					fluids = provider.GetMaterialized();
				}
			}
			else
			{
				fluids = MaterializeFluids(ingredient);
			}
			if (fluids.Length == 0 || fluids[0].IsEmpty) { left.RemoveAt(idx); continue; }
			int amount = fluids[0].Amount;

			if (io == IO.OUT && !AllowSameFluids)
			{
				CustomFluidTank? existing = null;
				int existingTank = 0;
				for (int i = 0; i < Storages.Length; i++)
				{
					var s = Storages[i];
					if (!s.Fluid.IsEmpty && s.Fluid.SameTypeAs(fluids[0]))
					{
						existing = s; existingTank = i; break;
					}
				}
				if (existing is not null)
				{
					var output = new FluidStack(fluids[0].Type!, amount, fluids[0].Nbt);
					int filled = existing.Fill(output, simulate);
					if (filled > 0)
					{
						visited[existingTank] = new FluidStack(output.Type!, existing.FluidAmount, output.Nbt);
						visitedSet[existingTank] = true;
						changed = true;
					}
					amount -= filled;
					if (amount > 0) ingredient.Amount = amount;
					else left.RemoveAt(idx);
					if (amount > 0) idx++;
					continue;
				}
			}

			bool handled = false;
			for (int tank = 0; tank < Storages.Length; tank++)
			{
				FluidStack current = visitedSet[tank] ? visited[tank] : GetFluidInTank(tank);
				int count = current.IsEmpty ? 0 : current.Amount;

				if (io == IO.IN)
				{
					if (current.IsEmpty) continue;
					if (((Ingredient)ingredient).Test(new Terraria.Item()))
					{ }
					if (FluidMatchesIngredient(ingredient, current))
					{
						var drained = Storages[tank].Drain(Math.Min(count, amount), simulate);
						if (!drained.IsEmpty)
						{
							visited[tank] = new FluidStack(drained.Type!, count - drained.Amount, drained.Nbt);
							visitedSet[tank] = true;
							changed = true;
						}
						amount -= drained.IsEmpty ? 0 : drained.Amount;
					}
				}
				else
				{
					var output = new FluidStack(fluids[0].Type!, amount, fluids[0].Nbt);
					bool sameAsVisited = !visitedSet[tank] || visited[tank].SameTypeAs(output);
					if (sameAsVisited && count < Storages[tank].GetCapacity())
					{
						int filled = Storages[tank].Fill(output, simulate);
						if (filled > 0)
						{
							visited[tank] = new FluidStack(output.Type!, count + filled, output.Nbt);
							visitedSet[tank] = true;
							changed = true;
							amount -= filled;
							if (!AllowSameFluids)
							{
								if (amount <= 0) { left.RemoveAt(idx); handled = true; }
								break;
							}
						}
					}
				}

				if (amount <= 0)
				{
					left.RemoveAt(idx);
					handled = true;
					break;
				}
			}

			if (!handled)
			{
				if (amount > 0) { ingredient.Amount = amount; idx++; }
				else            { left.RemoveAt(idx); }
			}
		}

		for (int i = 0; i < Storages.Length; i++)
		{
			Storages[i].OnContentsChangedAction = listeners[i];
			if (changed && !simulate) listeners[i]();
		}

		return left.Count == 0 ? null : left;
	}

	private static bool FluidMatchesIngredient(FluidIngredient ing, FluidStack current)
	{
		if (current.IsEmpty) return false;
		if (ing.ExactType is not null) return current.Type!.Id == ing.ExactType.Id;
		foreach (var f in ing.GetFluids()) if (f.Id == current.Type!.Id) return true;
		if (ing.TagName is not null)
		{
			return current.Type!.Id == ing.TagName;
		}
		if (ing.Attribute is not null && current.Type!.Attributes is not null)
		{
			foreach (var a in current.Type.Attributes)
				if (a == ing.Attribute) return true;
			return false;
		}
		return false;
	}

	private static FluidStack[] MaterializeFluids(FluidIngredient ing)
	{
		if (ing.ExactType is not null)
			return new[] { new FluidStack(ing.ExactType, ing.Amount) };
		var resolved = ing.GetFluids();
		if (resolved.Count > 0)
			return new[] { new FluidStack(resolved[0], ing.Amount) };
		return System.Array.Empty<FluidStack>();
	}

	public override bool Test(FluidIngredient ingredient) =>
		!IsLocked || ingredient.TestFluid(LockedFluid.Fluid.Type);

	public override int GetPriority() =>
		!IsLocked || LockedFluid.Fluid.IsEmpty
			? base.GetPriority()
			: IFilteredHandler<FluidIngredient>.HIGH - GetTanks();

	public bool IsLocked => !LockedFluid.Fluid.IsEmpty;

	public void SetLocked(bool locked)
	{
		SetLocked(locked, Storages.Length > 0 ? Storages[0].Fluid : FluidStack.Empty);
	}

	public void SetLocked(bool locked, FluidStack fluidStack)
	{
		if (IsLocked == locked) return;
		if (locked && !fluidStack.IsEmpty)
		{
			LockedFluid.SetFluid(new FluidStack(fluidStack.Type!, 1, fluidStack.Nbt));
			SetFilter(stack => !stack.IsEmpty && stack.SameTypeAs(LockedFluid.Fluid));
		}
		else
		{
			LockedFluid.SetFluid(FluidStack.Empty);
			SetFilter(_ => true);
		}
		OnContentsChanged();
	}

	public NotifiableFluidTank SetFilter(Predicate<FluidStack> filter)
	{
		Filter = filter;
		foreach (var s in Storages) s.Validator = filter;
		return this;
	}

	public override RecipeCapability<FluidIngredient> GetCapability() => FluidRecipeCapability.CAP;

	public override IO GetHandlerIO() => HandlerIO;

	public int GetTanks() => Storages.Length;
	public int GetSize() => Storages.Length;

	public override IReadOnlyList<object> GetContents()
	{
		var contents = new List<object>();
		for (int i = 0; i < GetTanks(); i++)
		{
			var s = GetFluidInTank(i);
			if (!s.IsEmpty) contents.Add(s);
		}
		return contents;
	}

	public override double GetTotalContentAmount()
	{
		long amount = 0;
		for (int i = 0; i < GetTanks(); i++)
		{
			var s = GetFluidInTank(i);
			if (!s.IsEmpty) amount += s.Amount;
		}
		return amount;
	}

	public bool IsEmpty()
	{
		if (_isEmpty is null)
		{
			_isEmpty = true;
			foreach (var s in Storages) if (!s.Fluid.IsEmpty) { _isEmpty = false; break; }
		}
		return _isEmpty.Value;
	}

	public FluidStack GetFluidInTank(int tank) => Storages[tank].Fluid;
	public void SetFluidInTank(int tank, FluidStack stack) => Storages[tank].SetFluid(stack);
	public int GetTankCapacity(int tank) => Storages[tank].Capacity;
	public bool IsFluidValid(int tank, FluidStack stack) => Storages[tank].IsFluidValid(stack);

	public int        TankCount             => Storages.Length;
	public FluidStack GetTank(int tank)     => Storages[tank].Fluid;
	public int        GetCapacity(int tank) => Storages[tank].Capacity;

	public IFluidHandler GetTankAccess(int tank) => Storages[tank];

	public bool CanCapInput()  => CapabilityIO == IO.IN  || CapabilityIO == IO.BOTH;
	public bool CanCapOutput() => CapabilityIO == IO.OUT || CapabilityIO == IO.BOTH;

	public (bool AllowFill, bool AllowDrain) GetTankClickCaps(int tank) => (CanCapInput(), true);

	public int Fill(FluidStack resource, bool simulate)
	{
		if (!CanCapInput()) return 0;
		return FillInternal(resource, simulate);
	}

	public int FillInternal(FluidStack resource, bool simulate)
	{
		if (resource.IsEmpty) return 0;
		int remaining = resource.Amount;
		CustomFluidTank? existing = null;
		if (!AllowSameFluids)
		{
			foreach (var s in Storages)
				if (!s.Fluid.IsEmpty && s.Fluid.SameTypeAs(resource)) { existing = s; break; }
		}
		if (existing is null)
		{
			foreach (var s in Storages)
			{
				var candidate = new FluidStack(resource.Type!, remaining, resource.Nbt);
				int filled = s.Fill(candidate, simulate);
				if (filled > 0)
				{
					remaining -= filled;
					if (!AllowSameFluids) break;
				}
				if (remaining <= 0) break;
			}
		}
		else
		{
			var candidate = new FluidStack(resource.Type!, remaining, resource.Nbt);
			remaining -= existing.Fill(candidate, simulate);
		}
		return resource.Amount - remaining;
	}

	public FluidStack Drain(FluidStack resource, bool simulate)
	{
		if (!CanCapOutput()) return FluidStack.Empty;
		return DrainInternal(resource, simulate);
	}

	public FluidStack DrainInternal(FluidStack resource, bool simulate)
	{
		if (resource.IsEmpty) return FluidStack.Empty;
		int remaining = resource.Amount;
		foreach (var s in Storages)
		{
			var candidate = new FluidStack(resource.Type!, remaining, resource.Nbt);
			var d = s.Drain(candidate, simulate);
			if (!d.IsEmpty) remaining -= d.Amount;
			if (remaining <= 0) break;
		}
		int drained = resource.Amount - remaining;
		return drained > 0 ? new FluidStack(resource.Type!, drained, resource.Nbt) : FluidStack.Empty;
	}

	public FluidStack Drain(int maxDrain, bool simulate)
	{
		if (!CanCapOutput()) return FluidStack.Empty;
		return DrainInternal(maxDrain, simulate);
	}

	public FluidStack DrainInternal(int maxDrain, bool simulate)
	{
		if (maxDrain <= 0) return FluidStack.Empty;
		FluidStack total = FluidStack.Empty;
		foreach (var s in Storages)
		{
			if (total.IsEmpty)
			{
				total = s.Drain(maxDrain, simulate);
				if (total.IsEmpty) continue;
				maxDrain -= total.Amount;
			}
			else
			{
				var d = s.Drain(new FluidStack(total.Type!, maxDrain, total.Nbt), simulate);
				if (!d.IsEmpty)
				{
					total = new FluidStack(total.Type!, total.Amount + d.Amount, total.Nbt);
					maxDrain -= d.Amount;
				}
			}
			if (maxDrain <= 0) break;
		}
		return total;
	}

	public override void OnMachineLoad()
	{
		base.OnMachineLoad();
		if (IsLocked)
			SetFilter(stack => !stack.IsEmpty && stack.SameTypeAs(LockedFluid.Fluid));
	}

	public override void Save(TagCompound tag)
	{
		var tanks = new List<TagCompound>(Storages.Length);
		foreach (var s in Storages) tanks.Add(s.SerializeNBT());
		tag["storages"]    = tanks;
		tag["allowSame"]   = AllowSameFluids;
		tag["isDistinct"]  = IsDistinct;
		tag["lockedFluid"] = LockedFluid.SerializeNBT();
	}

	public override void Load(TagCompound tag)
	{
		if (tag.ContainsKey("storages"))
		{
			var tanks = tag.GetList<TagCompound>("storages");
			for (int i = 0; i < tanks.Count && i < Storages.Length; i++)
				Storages[i].DeserializeNBT(tanks[i]);
		}
		if (tag.ContainsKey("allowSame"))   AllowSameFluids = tag.GetBool("allowSame");
		if (tag.ContainsKey("isDistinct"))  SetDistinct(tag.GetBool("isDistinct"));
		if (tag.ContainsKey("lockedFluid")) LockedFluid.DeserializeNBT(tag.Get<TagCompound>("lockedFluid"));
	}
}
