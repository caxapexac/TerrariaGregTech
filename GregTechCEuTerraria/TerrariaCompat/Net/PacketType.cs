#nullable enable
namespace GregTechCEuTerraria.TerrariaCompat.Net;

public enum PacketType : byte
{
	// Machine GUI lifecycle
	MachineViewBegin,      // C->S  opened a machine's GUI
	MachineViewEnd,        // C->S  closed a machine's GUI

	// Machine GUI mutations
	SlotAction,            // C->S  slot click / shift-click
	FluidSlotAction,       // C->S  bucket fill / drain
	CircuitSet,            // C->S  circuit selector value
	IOConfigSet,           // C->S  output side / input-from-output flags
	PowerToggle,           // C->S  power on/off
	TankConfigSet,         // C->S  tank locked / voiding / auto-output
	ChestAction,           // C->S  chest locked / voiding / auto-output / dump
	PartIoDirection,       // C->S  part IoDirection (auto-IO side)
	ParallelSet,           // C->S  parallel hatch CurrentParallel
	ActiveRecipeTypeSet,   // C->S  cycle a multi's active recipe type
	BoilerThrottleSet,     // C->S  Large Boiler throttle +/-5%
	DistinctSet,           // C->S  IDistinctPart bus distinctness
	FisherFilterSet,       // C->S  fisher loot-exclusion flag (junk / fish / crate)
	BlockBreakerModeSet,   // C->S  block breaker mine-down / cut-trees mode
	BlockBreakerReplantSet,// C->S  block breaker cut-trees replant flag
	MachineFilter,         // C->S  machine filter edit
	CreativeChestSet,      // C->S  creative chest config
	CreativeTankSet,       // C->S  creative tank config
	CreativeEnergySet,     // C->S  creative energy container config

	// World right-click interactions
	TransformerToggle,     // C->S  flip transformer direction
	LdEndpointToggle,      // C->S  flip long-distance endpoint IO role
	DrumScrewdriver,       // C->S  toggle drum auto-output
	NeonSignEdit,          // C->S  set a Neon Sign's text / color / size

	// Covers
	CoverAction,           // C->S  place / remove a cover
	CoverConfig,           // C->S  change a cover setting
	CoverFilter,           // C->S  cover filter edit

	// Machine placement, state sync
	MachinePlaced,         // C->S  placed a machine (create entity)
	MachineStateSync,      // S->C  full machine state snapshot
	MachineStateRequest,   // C->S  chunk-load state request (batched positions)
	MachineEnergySync,     // S->C  compact energy-stored sync
	CursorUpdate,          // S->C  authoritative cursor after a SlotAction
	EnderChannelSync,      // S->C  virtual ender channel contents
	MultiblockFormed,      // S->C  formed/unformed edge

	// Cables
	CableSet,              // C<->S place a cable / confirm
	CableRemove,           // C<->S remove a cable / confirm or burnout
	CableLayerRequest,     // C->S  late-join cable layer request
	CableLayerFull,        // S->C  full cable layer dump

	// Pipes
	PipePlaced,            // C<->S place a pipe / confirm
	PipeRemove,            // C<->S remove a pipe / confirm
	PipeLayerRequest,      // C->S  late-join pipe layer request
	PipeLayerFull,         // S->C  full pipe layer dump
	PipeCoverSync,         // S->C  per-cell pipe-cover state
	PipeSideModeSet,       // C->S  pipe per-side mode
	SimplePipeSideSet,     // C->S  simple pipe side (Off/Insert/Extract)
	PipeStats,             // S->C  per-pipe transferred-item counts
	FluidPipeStats,        // S->C  per-fluid-pipe tank contents
	CrossoverChange,       // C<->S Pipe Intersection changed; re-eval nets

	// Energy net, world effects
	EnergyNetStats,        // S->C  per-network throughput
	BlockExplosionEffect,  // S->C  bomb sound + smoke dust at (x,y)
	ItemCollectEffect,     // S->C  item-collected sparkle dust

	// Profiler
	ProfilerSync,          // S->C  server profiler counter snapshot

	// Questbook
	QuestbookStateRequest, // C->S  late-join questbook progress request
	QuestbookSync,         // S->C  full questbook snapshot
	QuestbookComplete,     // C->S  manual quest completion request
	QuestbookTaskComplete, // C->S  manual single-task (fluid) completion request

	// Applied Energistics
	MeStationCraftEffect,  // S->C  craft-complete particle burst at a vanilla station
	MeStorageAction,       // C->S  ME Storage deposit / dump
	MePatternProviderBlocking, // C->S  toggle provider blocking mode
	MePatternProviderRename,   // C->S  set provider custom name
	MePatternProviderLockMode, // C->S  set provider crafting-lock mode
	MePatternProviderShowInTerm, // C->S  toggle provider visibility in the pattern access terminal
	MePatternProviderPushDir, // C->S  set provider directional push side
	MeTerminalContent,     // S->C  incremental ME Terminal network-inventory sync
	MeTerminalAction,      // C->S  ME Terminal grid / shift-insert interaction
	MeCraftAtTerminal,     // C->S  ME Crafting Terminal station-craft to hand
	MeStationCraftRequest, // C->S  station-craft request (recipe index + amount) -> craft or shortfall
	MeStationCraftResult,  // S->C  station-craft result: success (close) or shortfall (plan window)
	MeCraftPlanBegin,      // C->S  compute an autocraft plan for (key, amount)
	MeCraftPlanResult,     // S->C  plan summary + CPU list -> opens confirm window
	MeCraftSubmit,         // C->S  submit the confirmed plan to a Quantum Computer
	MeCraftStatusRequest,  // C->S  request the selected CPU's job status
	MeCraftStatusResult,   // S->C  CPU list + selected job breakdown
	MeCraftCancel,         // C->S  cancel a CPU's job
	MeCraftJobStatus,      // S->C  job started/finished/cancelled (pin + PendingCraftingJobs)
	MePatternEncoding,     // C->S  ME Pattern Encoding Terminal mutations
	MePatternAccess,       // C->S  ME Pattern Access Terminal remote pattern-slot swap
	MeInterfaceAction,     // C->S  ME Interface config / priority / craft / take mutations
	MeBusSet,              // C<->S set/clear one cable-side bus attachment
	MeBusLayerRequest,     // C->S  late-join bus-layer dump request
	MeBusLayerFull,        // S->C  full bus-layer dump
	MeCableSet,            // C<->S place one ME cable cell
	MeCableRemove,         // C<->S remove one ME cable cell
	MeCableLayerRequest,   // C->S  late-join ME-cable-layer dump request
	MeCableLayerFull,      // S->C  full ME-cable-layer dump

	// Gregtech Toilet aura
	ToiletAura,            // C->S  placed / removed a Gregtech Toilet

	// Transport
	Fragment,              // C<->S one chunk of LargePacket

	MeCraftPlanAbandon,    // C->S  confirm window closed -> cancel the in-flight plan calculation
}
