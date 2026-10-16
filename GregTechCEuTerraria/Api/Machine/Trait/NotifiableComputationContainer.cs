#nullable enable
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Capability;
using GregTechCEuTerraria.Api.Capability.Recipe;
using GregTechCEuTerraria.Api.Machine.Feature;
using GregTechCEuTerraria.Api.Machine.Feature.Multiblock;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Capabilities;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using GregTechCEuTerraria.TerrariaCompat.Machine.Multiblock;
using GregTechCEuTerraria.TerrariaCompat.Pipelike.Optical;

namespace GregTechCEuTerraria.Api.Machine.Trait;

public class NotifiableComputationContainer
	: NotifiableRecipeHandlerTrait<int>, IOpticalComputationHatch, IOpticalComputationReceiver
{
	public static readonly MachineTraitType<NotifiableComputationContainer> TYPE = new(allowMultipleInstances: true);
	public override MachineTraitType TraitType => TYPE;

	public IO   HandlerIO   { get; }
	public bool Transmitter { get; }

	protected long _lastTimeStamp = long.MinValue;
	private   int  _currentOutputCwu;
	private   int  _lastOutputCwu;

	public NotifiableComputationContainer(IO handlerIO, bool transmitter) : base()
	{
		HandlerIO   = handlerIO;
		Transmitter = transmitter;
	}

	public override IO GetHandlerIO() => HandlerIO;

	public bool IsTransmitter() => Transmitter;

	public virtual int RequestCWUt(int cwut, bool simulate, ICollection<IOpticalComputationProvider> seen)
	{
		long latestTimeStamp = Machine.GetOffsetTimer();
		if (_lastTimeStamp < latestTimeStamp)
		{
			_lastOutputCwu    = _currentOutputCwu;
			_currentOutputCwu = 0;
			_lastTimeStamp    = latestTimeStamp;
		}

		seen.Add(this);
		if (HandlerIO == IO.IN)
		{
			if (Transmitter)
			{
				var provider = FindControllerProvider();
				return provider?.RequestCWUt(cwut, simulate, seen) ?? 0;
			}
			else
			{
				var provider = GetOpticalNetProvider();
				return provider?.RequestCWUt(cwut, simulate, seen) ?? 0;
			}
		}
		else
		{
			_lastOutputCwu = _lastOutputCwu - cwut;
			return System.Math.Min(_lastOutputCwu, cwut);
		}
	}

	public int RequestCWUt(int cwut, bool simulate) =>
		RequestCWUt(cwut, simulate, NewSeen());

	public virtual int GetMaxCWUt(ICollection<IOpticalComputationProvider> seen)
	{
		seen.Add(this);
		if (HandlerIO == IO.IN)
		{
			if (Transmitter)
			{
				var provider = FindControllerProvider();
				return provider?.GetMaxCWUt(seen) ?? 0;
			}
			else
			{
				var provider = GetOpticalNetProvider();
				return provider?.GetMaxCWUt(seen) ?? 0;
			}
		}
		else
		{
			return _lastOutputCwu;
		}
	}

	public int GetMaxCWUt() => GetMaxCWUt(NewSeen());

	public virtual bool CanBridge(ICollection<IOpticalComputationProvider> seen)
	{
		seen.Add(this);
		if (HandlerIO == IO.IN)
		{
			if (Transmitter)
			{
				var provider = FindControllerProvider();
				return provider?.CanBridge(seen) ?? false;
			}
			else
			{
				var provider = GetOpticalNetProvider();
				return provider == null || provider.CanBridge(seen);
			}
		}
		return false;
	}

	public bool CanBridge() => CanBridge(NewSeen());

	private IOpticalComputationProvider? FindControllerProvider()
	{
		var machine = Machine;
		if (machine is IMultiPart part)
		{
			if (!part.IsFormed()) return null;
			foreach (var controller in part.GetControllers())
			{
				if (controller is IOpticalComputationProvider cp) return cp;
				foreach (var trait in controller.Traits.AllTraits)
				{
					if (trait is IOpticalComputationProvider tp) return tp;
				}
			}
			return null;
		}
		if (machine is IOpticalComputationProvider direct) return direct;
		return null;
	}

	public override List<int>? HandleRecipeInner(IO io, GTRecipe recipe, List<int> left, bool simulate)
	{
		var provider = GetOpticalNetProvider();
		if (provider == null) return left;

		int sum = left.Sum();
		if (io == IO.IN)
		{
			int availableCWUt = RequestCWUt(int.MaxValue, simulate: true);
			if (availableCWUt >= sum)
			{
				bool totalCwuMode = Recipe.RecipeDataUtil.GetBool(recipe.Data, "duration_is_total_cwu");
				if (totalCwuMode)
				{
					int drawn = provider.RequestCWUt(availableCWUt, simulate);
					if (!simulate)
					{
						var rlm = Machine as IRecipeLogicMachine;
						if (rlm == null && Machine is IMultiPart mp)
						{
							foreach (var c in mp.GetControllers())
							{
								if (c is IRecipeLogicMachine controllerRlm) { rlm = controllerRlm; break; }
							}
						}
						if (rlm != null)
						{
							var rl = rlm.GetRecipeLogic();
							rl.SetProgress(rl.GetProgress() - 1 + drawn);
						}
					}
					sum -= drawn;
				}
				else
				{
					sum -= provider.RequestCWUt(sum, simulate);
				}
			}
		}
		else if (io == IO.OUT)
		{
			int canInput = GetMaxCWUt() - _lastOutputCwu;
			if (!simulate)
				_currentOutputCwu = System.Math.Min(canInput, sum);
			sum -= canInput;
		}
		return sum <= 0 ? null : new List<int> { sum };
	}

	public override IReadOnlyList<object> GetContents() => new object[] { _lastOutputCwu };
	public override double GetTotalContentAmount() => _lastOutputCwu;
	public override RecipeCapability<int> GetCapability() => CWURecipeCapability.CAP;

	public virtual IOpticalComputationProvider? GetComputationProvider()
	{
		if (HandlerIO.Supports(IO.OUT)) return this;
		var machine = Machine;
		if (machine is IOpticalComputationReceiver receiver) return receiver.GetComputationProvider();
		if (machine is IOpticalComputationProvider provider) return provider;
		if (machine is IRecipeCapabilityHolder holder)
		{
			var cwuList = holder.GetCapabilitiesFlat(IO.IN, CWURecipeCapability.CAP);
			if (cwuList.Count > 0 && cwuList[0] is IOpticalComputationProvider firstProvider && firstProvider != this)
				return firstProvider;
		}
		return ScanAdjacentForProvider();
	}

	private IOpticalComputationProvider? GetOpticalNetProvider() => ScanAdjacentForProvider();

	private IOpticalComputationProvider? ScanAdjacentForProvider()
	{
		if (Machine is not MetaMachine self) return null;
		foreach (var (side, x, y) in WorldCapability.Perimeter(self))
		{
			if (OpticalPipeLayerSystem.Pipes.Has(x, y))
			{
				var net = OpticalPipeNetSystem.Level.GetNetFromPos((x, y));
				if (net != null)
				{
					var handler = new OpticalNetHandler(net, (x, y), side.Opposite());
					if (handler.ResolveRemoteComputationProvider() != null)
						return handler;
				}
				continue;
			}
			var provider = WorldCapability.Get<IOpticalComputationProvider>(x, y);
			if (provider != null && provider != this)
			{
				if (provider is IOpticalComputationHatch hatch && !hatch.IsTransmitter())
					continue;
				return provider;
			}
		}
		return null;
	}

	private ICollection<IOpticalComputationProvider> NewSeen()
	{
		var s = new HashSet<IOpticalComputationProvider>();
		s.Add(this);
		return s;
	}
}
