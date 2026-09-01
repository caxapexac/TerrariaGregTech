// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (appeng.me.storage.DelegatingMEInventory), Forge 1.20.1. Original is unheadered;
// AE2 is LGPL-3.0-only (older API files MIT). See AE2's LICENSE.

#nullable enable
using System;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;

namespace GregTechCEuTerraria.AppliedEnergistics.Me.Storage;

public class DelegatingMEInventory : MEStorage
{
	private MEStorage _delegate;

	public DelegatingMEInventory(MEStorage @delegate) =>
		_delegate = @delegate ?? throw new ArgumentNullException(nameof(@delegate));

	protected MEStorage GetDelegate() => _delegate;

	protected void SetDelegate(MEStorage @delegate) => _delegate = @delegate;

	public virtual bool IsPreferredStorageFor(AEKey what, IActionSource source) =>
		_delegate.IsPreferredStorageFor(what, source);

	public virtual long Insert(AEKey what, long amount, Actionable mode, IActionSource source) =>
		_delegate.Insert(what, amount, mode, source);

	public virtual long Extract(AEKey what, long amount, Actionable mode, IActionSource source) =>
		_delegate.Extract(what, amount, mode, source);

	public virtual void GetAvailableStacks(KeyCounter @out) => _delegate.GetAvailableStacks(@out);

	public virtual KeyCounter GetAvailableStacks() => _delegate.GetAvailableStacks();

	public virtual string GetDescription() => _delegate.GetDescription();
}
