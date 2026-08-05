#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net.Actions;

public static class MachineActions
{
	public static void Send(IMachineAction action, MetaMachine entity)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient)
		{
			lock (Api.SaveTickGate.Lock)
				action.Apply(entity, Main.myPlayer);
			if (Main.netMode == NetmodeID.Server)
				MachineStateSyncPacket.Broadcast(entity);
			return;
		}

		var p = NetRouter.NewPacket(action.Type);
		p.WritePoint16(entity.Position);
		action.Write(p);
		p.Send();
	}

	public static void HandleIncoming<T>(BinaryReader r, int whoAmI) where T : IMachineAction, new()
	{
		if (Main.netMode != NetmodeID.Server)
		{
			NetHelpers.LogBadPacket("action", $"{typeof(T).Name} received on non-server side");
			return;
		}
		var pos = r.ReadPoint16();
		var action = new T();
		action.Read(r);

		if (!TileEntity.ByPosition.TryGetValue(pos, out var te) || te is not MetaMachine machine)
		{
			NetHelpers.LogBadPacket("action", $"{typeof(T).Name}: no MetaMachine at {pos} from player {whoAmI}");
			return;
		}
		if (!machine.HasViewer(whoAmI))
		{
			NetHelpers.LogBadPacket("action", $"{typeof(T).Name}: player {whoAmI} not in viewer set for {pos}");
			return;
		}
		lock (Api.SaveTickGate.Lock)
			action.Apply(machine, whoAmI);
		MachineStateSyncPacket.Broadcast(machine);
	}
}
