// Ported to C#/Terraria for GregTechCEuTerraria from Applied Energistics 2
// (appeng.me.storage.NetworkStorage), Forge 1.20.1. Original LGPL header preserved
// verbatim below per AE2's license terms.
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
using System.Collections.Generic;
using System.Linq;
using Terraria.Localization;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Core;

namespace GregTechCEuTerraria.AppliedEnergistics.Me.Storage;

public class NetworkStorage : Api.Storage.MEStorage
{
	private bool _mountsInUse;

	private readonly SortedDictionary<int, List<Api.Storage.MEStorage>> _priorityInventory =
		new(Comparer<int>.Create((a, b) => b.CompareTo(a)));

	private readonly List<Api.Storage.MEStorage> _secondPassInventories = new();

	private List<QueuedOperation>? _queuedOperations;

	public void Mount(int priority, Api.Storage.MEStorage inventory)
	{
		if (_mountsInUse)
		{
			_queuedOperations ??= new List<QueuedOperation>();
			_queuedOperations.Add(new MountOperation(priority, inventory));
		}
		else
		{
			if (!_priorityInventory.TryGetValue(priority, out var list))
				_priorityInventory[priority] = list = new List<Api.Storage.MEStorage>();
			list.Add(inventory);
		}
	}

	public void Unmount(Api.Storage.MEStorage inventory)
	{
		if (_mountsInUse)
		{
			_queuedOperations ??= new List<QueuedOperation>();
			_queuedOperations.Add(new UnmountOperation(inventory));
		}
		else
		{
			foreach (var priority in _priorityInventory.Keys.ToList())
			{
				var inventories = _priorityInventory[priority];
				if (inventories.Remove(inventory) && inventories.Count == 0)
					_priorityInventory.Remove(priority);
			}
		}
	}

	public long Insert(AEKey what, long amount, Actionable type, IActionSource src)
	{
		if (_mountsInUse)
			return 0;

		var remaining = amount;

		_mountsInUse = true;
		try
		{
			foreach (var invList in _priorityInventory.Values)
			{
				_secondPassInventories.Clear();

				foreach (var inv in invList)
				{
					if (remaining <= 0) break;
					if (IsQueuedForRemoval(inv)) continue;

					if (inv.IsPreferredStorageFor(what, src))
						remaining -= inv.Insert(what, remaining, type, src);
					else
						_secondPassInventories.Add(inv);
				}

				foreach (var inv in _secondPassInventories)
				{
					if (remaining <= 0) break;
					if (IsQueuedForRemoval(inv)) continue;
					remaining -= inv.Insert(what, remaining, type, src);
				}
			}
		}
		finally
		{
			_mountsInUse = false;
		}

		FlushQueuedOperations();
		return amount - remaining;
	}

	private void FlushQueuedOperations()
	{
		if (_mountsInUse)
			throw new System.InvalidOperationException("mounts in use");
		var queued = _queuedOperations;
		if (queued != null)
		{
			_queuedOperations = null;
			foreach (var op in queued)
			{
				switch (op)
				{
					case MountOperation m: Mount(m.Priority, m.Storage); break;
					case UnmountOperation u: Unmount(u.Storage); break;
				}
			}
		}
	}

	private bool IsQueuedForRemoval(Api.Storage.MEStorage inv)
	{
		if (_queuedOperations != null)
			foreach (var op in _queuedOperations)
				if (op is UnmountOperation u && ReferenceEquals(u.Storage, inv))
					return true;
		return false;
	}

	public long Extract(AEKey what, long amount, Actionable mode, IActionSource source)
	{
		if (_mountsInUse)
			return 0;

		var extracted = 0L;

		_mountsInUse = true;
		try
		{
			foreach (var invList in _priorityInventory.Values.Reverse())
			{
				foreach (var inv in invList)
				{
					if (extracted >= amount) break;
					if (IsQueuedForRemoval(inv)) continue;
					extracted += inv.Extract(what, amount - extracted, mode, source);
				}
			}
		}
		finally
		{
			_mountsInUse = false;
		}

		FlushQueuedOperations();
		return extracted;
	}

	public void GetAvailableStacks(KeyCounter @out)
	{
		if (_mountsInUse)
			return;

		_mountsInUse = true;
		try
		{
			foreach (var i in _priorityInventory.Values)
				foreach (var j in i)
					j.GetAvailableStacks(@out);
		}
		finally
		{
			_mountsInUse = false;
		}
	}

	public string GetDescription() => Language.GetTextValue(AELocale.StorageMENetwork);

	private abstract record QueuedOperation;

	private sealed record MountOperation(int Priority, Api.Storage.MEStorage Storage) : QueuedOperation;

	private sealed record UnmountOperation(Api.Storage.MEStorage Storage) : QueuedOperation;
}
