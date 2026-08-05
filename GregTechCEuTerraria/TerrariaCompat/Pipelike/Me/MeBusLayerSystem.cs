#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.AppliedEnergistics.Api.Config;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.TerrariaCompat.Net;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Pipelike.Me;

public sealed class MeBusLayerSystem : ModSystem
{
	public static MeBusLayer Buses { get; } = new();

	public override void ClearWorld() => Buses.Clear();

	public override void OnWorldLoad() => MeBusPackets.SendLayerRequest();

	public override void SaveWorldData(TagCompound tag)
	{
		lock (Api.SaveTickGate.Lock)
			WriteWorldData(tag);
	}

	private void WriteWorldData(TagCompound tag)
	{
		if (Buses.All.Count == 0) return;
		var list = new List<TagCompound>();
		foreach (var kv in Buses.All)
		{
			for (int i = 0; i < 4; i++)
			{
				var a = kv.Value[i];
				if (a is null || a.Kind == MeBusKind.None) continue;
				var t = new TagCompound
				{
					["x"] = kv.Key.x,
					["y"] = kv.Key.y,
					["side"] = (byte)i,
					["kind"] = (byte)a.Kind,
					["access"] = (byte)a.Access,
					["prio"] = a.Priority,
					["speed"] = (byte)a.Speed,
					["craftMissing"] = a.CraftMissing,
					["craftOnly"] = a.CraftOnly,
					["sched"] = (byte)a.Scheduling,
					["nextSlot"] = a.NextSlot,
					["filterExt"] = a.FilterOnExtract,
					["extractOnly"] = a.ExtractableOnly,
				};
				var filter = new List<TagCompound>();
				for (int s = 0; s < MeBusAttachment.FilterSize; s++)
					if (a.Filter[s] is { } key)
						filter.Add(new TagCompound { ["slot"] = (byte)s, ["key"] = key.ToTagGeneric() });
				if (filter.Count > 0) t["filter"] = filter;
				list.Add(t);
			}
		}
		tag["me_bus.cells"] = list;
	}

	public override void LoadWorldData(TagCompound tag)
	{
		Buses.Clear();
		if (!tag.ContainsKey("me_bus.cells")) return;
		foreach (var t in tag.GetList<TagCompound>("me_bus.cells"))
		{
			var kind = (MeBusKind)t.GetByte("kind");
			if (kind == MeBusKind.None) continue;
			var att = new MeBusAttachment(kind, (AccessRestriction)t.GetByte("access"), t.GetInt("prio"),
				t.ContainsKey("speed") ? t.GetByte("speed") : 0)
			{
				CraftMissing = t.ContainsKey("craftMissing") && t.GetBool("craftMissing"),
				CraftOnly = t.ContainsKey("craftOnly") && t.GetBool("craftOnly"),
				Scheduling = t.ContainsKey("sched") ? (MeBusSchedulingMode)t.GetByte("sched") : MeBusSchedulingMode.Default,
				NextSlot = t.ContainsKey("nextSlot") ? t.GetInt("nextSlot") : 0,
				FilterOnExtract = !t.ContainsKey("filterExt") || t.GetBool("filterExt"),
				ExtractableOnly = t.ContainsKey("extractOnly") && t.GetBool("extractOnly"),
			};
			if (t.ContainsKey("filter"))
				foreach (var f in t.GetList<TagCompound>("filter"))
				{
					int slot = f.GetByte("slot");
					if (slot < 0 || slot >= MeBusAttachment.FilterSize) continue;
					att.Filter[slot] = AEKey.FromTagGeneric(f.GetCompound("key"));
				}
			Buses.Set(t.GetInt("x"), t.GetInt("y"), MeBusLayer.SideFromIndex(t.GetByte("side")), att);
		}
		Buses.ClearDirty();
	}
}
