#nullable enable
using System.Diagnostics;
using System.IO;
using GregTechCEuTerraria.TerrariaCompat.Net.Actions;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public static class NetRouter
{
	private static Mod? _mod;
	public static Mod Mod => _mod ??= ModLoader.GetMod("GregTechCEuTerraria");

	public static void Handle(BinaryReader reader, int whoAmI)
	{
		long entryPos = reader.BaseStream.Position;
		var type = (PacketType)reader.ReadByte();
		bool prof = Profiler.Profiler.Enabled;
		string typeName = "";
		long handleT0 = 0;
		if (prof)
		{
			typeName = type.ToString();
			Profiler.Profiler.Count("net.in.count", typeName);
			handleT0 = Stopwatch.GetTimestamp();
		}
		switch (type)
		{
			case PacketType.MachineViewBegin: MachineViewPacket.HandleBegin(reader, whoAmI); break;
			case PacketType.MachineViewEnd:   MachineViewPacket.HandleEnd  (reader, whoAmI); break;

			case PacketType.SlotAction:       MachineActions.HandleIncoming<SlotAction>(reader, whoAmI); break;
			case PacketType.FluidSlotAction:  MachineActions.HandleIncoming<FluidSlotAction>(reader, whoAmI); break;
			case PacketType.CircuitSet:       MachineActions.HandleIncoming<CircuitSetAction>(reader, whoAmI); break;
			case PacketType.IOConfigSet:      MachineActions.HandleIncoming<IOConfigSetAction>(reader, whoAmI); break;
			case PacketType.PowerToggle:      MachineActions.HandleIncoming<PowerToggleAction>(reader, whoAmI); break;
			case PacketType.TankConfigSet:    MachineActions.HandleIncoming<TankConfigSetAction>(reader, whoAmI); break;
			case PacketType.ChestAction:      MachineActions.HandleIncoming<ChestAction>(reader, whoAmI); break;
			case PacketType.PartIoDirection:  MachineActions.HandleIncoming<PartIoDirectionSetAction>(reader, whoAmI); break;
			case PacketType.ParallelSet:      MachineActions.HandleIncoming<ParallelSetAction>(reader, whoAmI); break;
			case PacketType.ActiveRecipeTypeSet: MachineActions.HandleIncoming<ActiveRecipeTypeSetAction>(reader, whoAmI); break;
			case PacketType.BoilerThrottleSet: MachineActions.HandleIncoming<BoilerThrottleSetAction>(reader, whoAmI); break;
			case PacketType.DistinctSet:      MachineActions.HandleIncoming<DistinctSetAction>(reader, whoAmI); break;
			case PacketType.FisherFilterSet:  MachineActions.HandleIncoming<FisherFilterAction>(reader, whoAmI); break;
			case PacketType.BlockBreakerModeSet: MachineActions.HandleIncoming<BlockBreakerModeSetAction>(reader, whoAmI); break;
			case PacketType.BlockBreakerReplantSet: MachineActions.HandleIncoming<BlockBreakerReplantSetAction>(reader, whoAmI); break;
			case PacketType.MachineFilter:    MachineActions.HandleIncoming<MachineFilterAction>(reader, whoAmI); break;
			case PacketType.CreativeChestSet: MachineActions.HandleIncoming<CreativeChestSetAction>(reader, whoAmI); break;
			case PacketType.CreativeTankSet:  MachineActions.HandleIncoming<CreativeTankSetAction>(reader, whoAmI); break;
			case PacketType.CreativeEnergySet:MachineActions.HandleIncoming<CreativeEnergySetAction>(reader, whoAmI); break;

			case PacketType.TransformerToggle:TransformerTogglePacket.Handle(reader, whoAmI); break;
			case PacketType.LdEndpointToggle: LdEndpointTogglePacket.Handle(reader, whoAmI); break;
			case PacketType.DrumScrewdriver:  DrumScrewdriverPacket.Handle(reader, whoAmI); break;
			case PacketType.NeonSignEdit:     NeonSignEditPacket.Handle(reader, whoAmI); break;

			case PacketType.CoverAction:      CoverActions.HandleIncoming<CoverAction>(reader, whoAmI); break;
			case PacketType.CoverConfig:      CoverActions.HandleIncoming<CoverConfigAction>(reader, whoAmI); break;
			case PacketType.CoverFilter:      CoverActions.HandleIncoming<CoverFilterAction>(reader, whoAmI); break;

			case PacketType.MachinePlaced:    MachinePlacedPacket.Handle(reader, whoAmI); break;
			case PacketType.MachineStateSync: MachineStateSyncPacket.HandleOnClient(reader); break;
			case PacketType.MachineStateRequest: MachineStateRequestPacket.Handle(reader, whoAmI); break;
			case PacketType.MachineEnergySync: MachineEnergySyncPacket.HandleOnClient(reader); break;
			case PacketType.CursorUpdate:     CursorUpdatePacket.HandleOnClient(reader); break;
			case PacketType.EnderChannelSync: EnderChannelSyncPacket.HandleOnClient(reader); break;
			case PacketType.MultiblockFormed: MultiblockFormedPacket.HandleSet(reader); break;

			case PacketType.CableSet:         CablePackets.HandleSet(reader, whoAmI); break;
			case PacketType.CableRemove:      CablePackets.HandleRemove(reader, whoAmI); break;
			case PacketType.CableLayerRequest:CablePackets.HandleLayerRequest(reader, whoAmI); break;
			case PacketType.CableLayerFull:   CablePackets.HandleLayerFull(reader); break;

			case PacketType.PipePlaced:       PipePackets.HandlePlaced(reader, whoAmI); break;
			case PacketType.PipeRemove:       PipePackets.HandleRemove(reader, whoAmI); break;
			case PacketType.PipeLayerRequest: PipePackets.HandleLayerRequest(reader, whoAmI); break;
			case PacketType.PipeLayerFull:    PipePackets.HandleLayerFull(reader); break;
			case PacketType.PipeCoverSync:    PipeCoverSyncPacket.HandleOnClient(reader); break;
			case PacketType.PipeSideModeSet:  PipeSideModePacket.HandleSet(reader, whoAmI); break;
			case PacketType.SimplePipeSideSet: SimplePipeSideSetPacket.HandleSet(reader, whoAmI); break;
			case PacketType.PipeStats:        PipeStatsPacket.HandleOnClient(reader); break;
			case PacketType.FluidPipeStats:   FluidPipeStatsPacket.HandleOnClient(reader); break;
			case PacketType.CrossoverChange:  Pipelike.PipeIntersection.HandleChange(reader, whoAmI); break;

			case PacketType.EnergyNetStats:   EnergyNetStatsPacket.HandleOnClient(reader); break;
			case PacketType.BlockExplosionEffect: BlockExplosionEffectPacket.HandleOnClient(reader); break;
			case PacketType.ItemCollectEffect: ItemCollectEffectPacket.HandleOnClient(reader); break;
			case PacketType.MeStationCraftEffect: MeStationCraftEffectPacket.HandleOnClient(reader); break;
			case PacketType.MeStorageAction: MachineActions.HandleIncoming<MeStorageAction>(reader, whoAmI); break;
			case PacketType.MePatternProviderBlocking: MachineActions.HandleIncoming<MePatternProviderBlockingAction>(reader, whoAmI); break;
			case PacketType.MePatternProviderRename: MachineActions.HandleIncoming<MePatternProviderRenameAction>(reader, whoAmI); break;
			case PacketType.MePatternProviderLockMode: MachineActions.HandleIncoming<MePatternProviderLockModeAction>(reader, whoAmI); break;
			case PacketType.MePatternProviderShowInTerm: MachineActions.HandleIncoming<MePatternProviderShowInTermAction>(reader, whoAmI); break;
			case PacketType.MePatternProviderPushDir: MachineActions.HandleIncoming<MePatternProviderPushDirAction>(reader, whoAmI); break;
			case PacketType.MeTerminalContent: MeTerminalContentPacket.HandleOnClient(reader); break;
			case PacketType.MeTerminalAction: MachineActions.HandleIncoming<MeTerminalAction>(reader, whoAmI); break;
			case PacketType.MeCraftAtTerminal: MachineActions.HandleIncoming<MeCraftAtTerminalAction>(reader, whoAmI); break;
			case PacketType.MeStationCraftRequest: MeStationCraftPackets.HandleRequest(reader, whoAmI); break;
			case PacketType.MeStationCraftResult: MeStationCraftPackets.HandleResult(reader); break;
			case PacketType.MeCraftPlanBegin: MeCraftPackets.HandleBegin(reader, whoAmI); break;
			case PacketType.MeCraftPlanResult: MeCraftPackets.HandleResult(reader); break;
			case PacketType.MeCraftSubmit: MeCraftPackets.HandleSubmit(reader, whoAmI); break;
			case PacketType.MeCraftPlanAbandon: MeCraftPackets.HandleAbandon(whoAmI); break;
			case PacketType.MeCraftStatusRequest: MeCraftPackets.HandleStatusRequest(reader, whoAmI); break;
			case PacketType.MeCraftStatusResult: MeCraftPackets.HandleStatusResult(reader); break;
			case PacketType.MeCraftCancel: MeCraftPackets.HandleCancel(reader, whoAmI); break;
			case PacketType.MeCraftJobStatus: MeCraftJobStatusPacket.HandleOnClient(reader); break;
			case PacketType.MePatternEncoding: MachineActions.HandleIncoming<MePatternEncodingAction>(reader, whoAmI); break;
			case PacketType.MePatternAccess: MachineActions.HandleIncoming<MePatternAccessAction>(reader, whoAmI); break;
			case PacketType.MeInterfaceAction: MachineActions.HandleIncoming<MeInterfaceAction>(reader, whoAmI); break;
			case PacketType.MeBusSet: MeBusPackets.HandleSet(reader, whoAmI); break;
			case PacketType.MeBusLayerRequest: MeBusPackets.HandleLayerRequest(reader, whoAmI); break;
			case PacketType.MeBusLayerFull: MeBusPackets.HandleLayerFull(reader); break;
			case PacketType.MeCableSet: MeCablePackets.HandleSet(reader, whoAmI); break;
			case PacketType.MeCableRemove: MeCablePackets.HandleRemove(reader, whoAmI); break;
			case PacketType.MeCableLayerRequest: MeCablePackets.HandleLayerRequest(reader, whoAmI); break;
			case PacketType.MeCableLayerFull: MeCablePackets.HandleLayerFull(reader); break;

			case PacketType.ProfilerSync:     ProfilerSyncPacket.HandleOnClient(reader); break;

			case PacketType.QuestbookStateRequest: QuestbookPackets.HandleStateRequest(reader, whoAmI); break;
			case PacketType.QuestbookSync:    QuestbookPackets.HandleSync(reader); break;
			case PacketType.QuestbookComplete: QuestbookPackets.HandleCompleteRequest(reader, whoAmI); break;
			case PacketType.QuestbookTaskComplete: QuestbookPackets.HandleTaskCompleteRequest(reader, whoAmI); break;

			case PacketType.ToiletAura:       ToiletAuraPacket.Handle(reader, whoAmI); break;

			case PacketType.Fragment:         LargePacket.HandleFragment(reader, whoAmI); break;

			default:
				NetHelpers.LogBadPacket("dispatch", $"unknown PacketType={(byte)type} from whoAmI={whoAmI}");
				break;
		}
		if (prof)
		{
			Profiler.Profiler.AccumulateTimer("net.handle", typeName, Stopwatch.GetTimestamp() - handleT0);
			Profiler.Profiler.Count("net.in.bytes", typeName, reader.BaseStream.Position - entryPos);
		}
	}

	public static ModPacket NewPacket(PacketType type)
	{
		Profiler.Profiler.Count("net.out.count", type.ToString());
		var p = Mod.GetPacket();
		p.Write((byte)type);
		return p;
	}
}
