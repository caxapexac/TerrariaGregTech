// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (appeng.me.storage.MEInventoryHandler), Forge 1.20.1. Original LGPL header
// preserved verbatim below per AE2's license terms.
//
// This file is part of Applied Energistics 2.
// Copyright (c) 2013 - 2014, AlgorithmX2, All rights reserved.
//
// Applied Energistics 2 is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Applied Energistics 2 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with Applied Energistics 2.  If not, see <http://www.gnu.org/licenses/lgpl>.

#nullable enable
using System;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;

namespace GregTechCEuTerraria.AppliedEnergistics.Me.Storage;

public class MEInventoryHandler : DelegatingMEInventory
{
	private Func<AEKey, bool>? _matchesFilter;
	private Func<AEKey, bool>? _isListed;
	private bool _filterOnExtraction;
	private bool _filterAvailableContents;
	private bool _allowExtraction = true;
	private bool _allowInsertion = true;

	private bool _gettingAvailableContent;

	public MEInventoryHandler(MEStorage inventory) : base(inventory) { }

	public void SetAllowExtraction(bool allowExtraction) => _allowExtraction = allowExtraction;

	public void SetAllowInsertion(bool allowInsertion) => _allowInsertion = allowInsertion;

	public void SetAccessRestriction(AccessRestriction setting)
	{
		SetAllowExtraction(setting.IsAllowExtraction());
		SetAllowInsertion(setting.IsAllowInsertion());
	}

	public void SetPartitionList(Func<AEKey, bool>? matchesFilter, Func<AEKey, bool>? isListed)
	{
		_matchesFilter = matchesFilter;
		_isListed = isListed;
	}

	public void SetExtractFiltering(bool filterOnExtraction, bool filterAvailableContents)
	{
		_filterOnExtraction = filterOnExtraction;
		_filterAvailableContents = filterAvailableContents;
	}

	public override long Insert(AEKey what, long amount, Actionable mode, IActionSource source)
	{
		if (!_allowInsertion || !PassesBlackOrWhitelist(what)) return 0;

		return base.Insert(what, amount, mode, source);
	}

	public override long Extract(AEKey what, long amount, Actionable mode, IActionSource source)
	{
		if (_filterOnExtraction && !CanExtract(what)) return 0;

		return base.Extract(what, amount, mode, source);
	}

	public override void GetAvailableStacks(KeyCounter @out)
	{
		if (_gettingAvailableContent) return;

		_gettingAvailableContent = true;
		try
		{
			if (!_filterAvailableContents)
			{
				base.GetAvailableStacks(@out);
			}
			else
			{
				if (!_allowExtraction) return;

				foreach (var entry in GetDelegate().GetAvailableStacks())
					if (CanExtract(entry.Key))
						@out.Add(entry.Key, entry.Value);
			}
		}
		finally
		{
			_gettingAvailableContent = false;
		}
	}

	public override bool IsPreferredStorageFor(AEKey input, IActionSource source)
	{
		if (_isListed != null && _isListed(input)) return true;

		if (base.Extract(input, 1, Actionable.SIMULATE, source) > 0) return true;

		return base.IsPreferredStorageFor(input, source);
	}

	protected bool CanExtract(AEKey request) => _allowExtraction && PassesBlackOrWhitelist(request);

	private bool PassesBlackOrWhitelist(AEKey input) => _matchesFilter == null || _matchesFilter(input);
}
