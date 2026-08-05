#nullable enable
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Machine;
using Terraria;
using Terraria.DataStructures;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public static class MachineViewPacket
{
	public static void SendBegin(Point16 pos)
	{
		var p = NetRouter.NewPacket(PacketType.MachineViewBegin);
		p.WritePoint16(pos);
		p.Send();
	}

	public static void HandleBegin(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server)
		{
			NetHelpers.LogBadPacket("ViewBegin", "received on non-server side");
			return;
		}
		var pos = r.ReadPoint16();
		if (!TileEntity.ByPosition.TryGetValue(pos, out var te) || te is not MetaMachine machine)
		{
			NetHelpers.LogBadPacket("ViewBegin", $"no MetaMachine at {pos} from player {whoAmI}");
			return;
		}
		machine.AddViewer(whoAmI);
		if (machine is AppliedEnergistics.MeTerminalMachine term) term.ResetViewerSync(whoAmI);
		MachineStateSyncPacket.SendFullStateTo(machine, whoAmI);
		EnderChannelSyncPacket.SendChannelsTo(machine, whoAmI);
	}

	public static void SendEnd(Point16 pos)
	{
		var p = NetRouter.NewPacket(PacketType.MachineViewEnd);
		p.WritePoint16(pos);
		p.Send();
	}

	public static void HandleEnd(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != Terraria.ID.NetmodeID.Server) return;
		var pos = r.ReadPoint16();
		if (TileEntity.ByPosition.TryGetValue(pos, out var te) && te is MetaMachine machine)
			machine.RemoveViewer(whoAmI);
	}
}
