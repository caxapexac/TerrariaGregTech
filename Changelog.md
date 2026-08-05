## 0.0.14

60 days after mod's first release, Greg's completeness was proven by Zub. Mod is playable now!
Still getting minor stability and multiplayer fixes, but from now on there will be more actual content updates

Important:
- Changed shapes for RHF and BBC (thanks TheHatShallDie)
- Fixed rare crash on autosave (thanks 404岛主)
- Fixed machine covers inert on rejoin (thanks 404岛主)
- Fixed parallel hathes not respecting max energy (thanks Zub)
- Fixed ultimate battery recipe (thanks Zub)
- Fixed multiplayer packet overflow in multiplayer on large bases (thanks perch)
- Fixed craft missing card lag (thanks 404岛主)
- Fixed cleanroom recipes in multiblocks (thanks Bup Supreme)

Qol:
- Added Pipe Intersection support for ME, LD, optical, laser pipes (thanks holeow)
- Added more options to fisher (thanks Zub)
- Added object holder slot locking
- Changed neon sign recipe to accept any lv circuit (thanks 404岛主)
- Changed ME autocrafting to async for smoother performance
- Changed RHF and BBC recipe to be craftable earlier (thanks Zub)
- Fixed piping nbt items into vanilla chests
- Fixed electric tools max energy inconsistency
- Fixed fisher and boiler accepting any item through piping (thanks Zub)
- Fixed machines not detecting cleanroom in some cases (thanks holeow)
- Fixed tag filter ui sync (thanks Bup Supreme)
- Fixed subnet storage bus merging with another net interface (thanks 404岛主)
- Fixed block breaker tree farm vertical conflict (thanks PersonNo.752)

Visual:
- Added sounds to fusion reactor and plasma generator
- Removed duplicated generic tooltips in multis
- Fixed recipe progress tooltip sync on rejoin in research station and HPCA (thanks Zub)
- Fixed laser hatch tooltip (thanks Zub)
- Fixed ore spawn height tooltip (thanks holeow)
- Fixed round robin button visible with 1 filtered item (thanks bobifhd_bg)


## 0.0.13

Minor bugfix

Important:
- Changed most slots to have a limit of 64 items, works better for gt balance (thanks Creater~)
- Fixed assembly line shape to allow overclock (thanks Isenki)
- Fixed manual crafting stations autocraft patterns (thanks Bup Supreme)

Qol:
- Optimized terminal a bit, should open faster now
- Fixed tag pinning for vanilla tags

Visual:
- Fixed favorites tags persistance
- Fixed crafting result chat message fluid amount

slots 64 overflow won't void so its okay to update

## 0.0.12

Minor bugfix

Important:
- Fixed data stick metadata (thanks Isenki)
- Fixed manual crafting of the recipes that are done in both gt and vanilla stations (thanks TheHatShallDie)
- Fixed assembly line 2+ fluid input hatches (thanks Isenki)

Visuals:
- Fixed magic storage warning unsafe items spam if piping from the heart (thanks Bup Supreme)

## 0.0.11

Minor bugfix for better mod compatibility

Important:
- Fixed calamity compatibility with autocrafting, and other mods that add global serializable data (thanks holeow)
- Fixed internal crafting pattern station encoding method so changing modpack content doesn't break patterns

## 0.0.10

Fat steam multis update, other multis will be eventually updated too, to fit the small scale of terraria zoom which makes most multis look too weak

Important:
- Fixed lag on unloaded tml items in terminal and terminal overflow (thanks holeow)
- Fixed subnet merging with main net in some configurations (thanks 404岛主)
- Fixed blocking mode ignoring subnet contents (thanks holeow)

Qol:
- Added back button to recipe browser
- Added persistence to sort buttons in terminal (thanks 404岛主)
- Changed mv bosses to drop sapphire instead of aluminium (thanks holeow)
- Changed shapes for steam oven and grinder (thanks Tama)
- Fixed cleanroom paused state (thanks Bup Supreme)
- Fixed fusion reactor max energy tier
- Fixed fishing junk roll (thanks Bup Supreme)
- Fixed terminal open click leaking into terminal (thanks holeow)

Visuals:
- Added texture to history of logistics (thanks Flareguy)
- Added coil temp into recipe browser (thanks TheHatShallDie)
- Changed pipe filtering options to hide irrelevant fields (thanks Bup Supreme)
- Fixed fluid pipe rendering performance (thanks Bup Supreme)
- Fixed distillation tower insufficient output tooltip (thanks TheBobster)
- Fixed duplicated smelting recipes in recipe browser (thanks holeow)
- Fixed chest name render in storage bus (thanks Tama)
- Fixed widgets positioning when opening through the world space modal
- Fixed multitool preview render for ME cables (thanks holeow)

## 0.0.9

Important:
- Fixed tag resolution on processing patterns substitutions (thanks CloneCreatesClassics)
- Fixed sand dupe on aoe mining (thanks rapter2561)
- Fixed item collector picking up non-item pickups (thanks Bup Supreme)

Qol:
- Added hellforge and hellstone ore recipes (thanks Bup Supreme)

## 0.0.8

Emergent hotfix update, thanks to you all for early playtesting, we found potentially very critical compat bug, now calamity should behave with ae

Important:
- Added chinese, japanese and all other IME languages typing support to input fields (thanks Aeolian)
- Fixed other mods compatibility when they broke autocraft with their tags (thanks holeow)
- Fixed drums could lose fluid when mined (thanks Bup Supreme)

Qol:
- Fixed composite crafting stations compatibility with terminal (thanks Bup Supreme)
- Fixed ae components recipes to require any circuit (thanks Bup Supreme)

Visuals:
- Fixed ambiguity on some gt recipes getting into vanilla category in TMI (thanks Bup Supreme)
- Fixed demon altar, water and other conditions not shown in recipe browser

## 0.0.7

Logistics update! Meet barebones AE2 port for proper gregtech automation. Barebones means you can't use it as a storage solution by itself, Magic Storage heart, crates, super tanks can be connected with storage bus to use them in ME network. Let me know if there are other mods that you think should be supported by storage buses.

Its very minimalistic (no channels, no energy consumption, no drives, no spatial, no quantum, no p2p). Nothing else expect maybe wireless terminals and level emitters isn't planned to be ported. Perchance, when we get to proper endgame playtesting we will encounter some logistical issues that will force us to port more of AE2 but for now it seems okay. But keep in mind that current AE features are already extremely powerful and may break the game balance completely (which they in fact do to 99% of existing Minecraft modpacks) so this is most likely some eternal problem that is yet to be solved by humankind

Important:
- Added ae2 cables, terminals, pattern provider, interface, import/export/storage bus integrated into cable
- Added missing molten metals recipes (thanks Bup Supreme)
- Added tree farming mode to block breaker (thanks all who asked for infinite wood)
- Added bootleg XP crafting with terraria money

QOL:
- Added simple pipes support to gregtech multitool (thanks CloneCreatesClassics)
- Added debuff to hot ingots (thanks Bup Supreme)
- Added terraria superconductors conversion recipes (thanks Bup Supreme)
- Added gregtech toilet, one of the final machines
- Added item picker to empty filters
- Added clicking to questbook requirement icons (thanks holeow)
- Added paint to dye conversion recipes for easier lenses (thanks hhhhhhhhhhhhhhh)
- Added simple trinket effects to exquisite gems (thanks holeow)
- Added toggle for output settings widget, changed its buttons layout
- Added grid-aligned placement for all 2x2 tiles when holding mouse
- Added fluid bucket requirements skip button into the questbook (thanks Bup Supreme)
- Added usecase for paracetamol and radaway
- Added dropdown on clicking onto colliding interactable things
- Added damascus steel recipe (thanks Bup Supreme)
- Added output to any side to drums (thanks hhhhhhhhhhhhhhh)
- Added Neon Sign
- Added warning when trying to put cells, drums with fluid into Magic Storage (thanks Bup Supreme)
- Changed long distance pipes to require less distance (thanks Bup Supreme)
- Changed some wood-related recipes so it accepts more terraria wood types
- Changed undervoltage machines manual toggle behavior (thanks TheBobster)
- Changed how integrated recipe browser is shown
- Changed extractinator to be slower
- Changed pipes filter slots UI
- Changed Queen Slime to have HV loot table (thanks Bup Supreme)
- Changed creative tank/chest slots to support item picker
- Changed recipe browser search logic for more relevant results
- Fixed treated wooden pipe multitool interaction (thanks Sanya268)
- Fixed input hatches fluid containers UI interaction (thanks Bup Supreme)
- Fixed mining and damage tiers of tools (thanks Bup Supreme)
- Fixed bug where platforms under machines got machines UI hover
- Removed cleanroom diodes (thanks Bup Supreme)
- Removed tape and some other dummy items
- Removed cover support from some steam machines to remove early game visual clutter
- Removed some of boss drops, this is the end of early playtesting so it doesn't fit the balance anymore
- Removed netherite tools (thanks Bup Supreme)

Visuals:
- Added foreground pipe render to gregtech multitool (thanks Flungus)
- Added custom multitool and terminal upgrade card textures (thanks Flareguy)
- Added creosote overflow tooltip to coke oven
- Added output side rendering
- Changed fluid and tag items hover in recipe browser
- Changed energy bar renderer as a first step of UI overhaul
- Changed UI scale of some machines
- Changed multiblock ghost tips so its more brief
- Fixed long distance pipes tooltip on rejoin (thanks Bup Supreme)
- Fixed solar panel covers tooltip
- Fixed block breaker tooltip
- Fixed iv multis mode selection locale
- Fixed boiler steam output tooltip (thanks holeow)
- Fixed recipe browser edge case with wrong ingredients amount render (thanks holeow)
- Fixed rock breaker recipe browser icon
- Fixed fluid buckets equipped rendering
- Fixed fluid slots amount abbreviation for big numbers

## 0.0.6

QoL update! Basically a small intermediate update with lots of random stuff that will hopefully make the gameplay a bit smoother. AE2 will be next update

Important:
- Added Magic Storage gregtech piping support, both in/out from heart and access points
- Added Manual Crafting Stations for better Magic Storage compatibility and reducing recipe amount when hand crafting
- Added Gregtech Mutlitool for better wires/pipes setup UX (thanks Flungus)
- Added functionality to Prospector device (thanks Flungus, TrRzeczy)
- Added terraria metals superconductors (thanks TrRzeczy)
- Fixed plascrete recipe (thanks CreepersX17)
- Fixed distillery first output (thanks CreepersX17)
- Fixed gray pressure plate recipe (thanks hhhhhhhhhhhhhhh)
- Fixed removed calcite recipes
- Changed compat recipes internal format for easier contributions
- Changed questbook data storage so its multiplayer compatible and all the players share the same progress
- Removed duplicated ingots, with legacy conversion recipes (thanks Flungus)
- Removed shimmer recipes for all gt stuff because people are unable to self-restrain from duping stuff
- Removed shimmer for substituded vanilla ingots, can enable back in mod config

QOL:
- Added ultimate battery as a requirement of the final Gregith (thanks Flungus)
- Added nether star recipe from mana stars (thanks Royal Tek)
- Added questbook editor
- Added shimmer and honey fluids for later compatibility
- Added bottomless bucket extractor recipes (thanks TheLarvi)
- Added recipes between Chum Bucket and fermented biomass
- Added hotkeys for TMI and questbook
- Added more rubber into starter bags
- Changed raw rubber dust yield so its more rewarding to automate it (thanks Bup Supreme)
- Changed recipe browser favorites list to be persistent on the player
- Changed questbook to track tasks separately (thanks Leha44581)

Visuals:
- Added move/resize to recipe browser, improved visuals (thanks Flungus)
- Added pin button to recipe browser
- Added gt armor sprites (thanks Flareguy)
- Added multiblocks, saw actions, ore veins in recipe browser (thanks hamood)
- Added tooltip to wooden form so its obvious its an intentional recipe change
- Changed nether star texture
- Changed the rest of wood textures
- Fixed dimension condition tooltip in recipe browser (thanks TheLarvi)
- Removed all the unresolved recipes from the recipe browser

Existing worlds should be fine to update
Make sure to convert your tin, platinum etc. ingots into terraria variants

## 0.0.5

Bugfix update! I want to get core gregtech logic to a full stability until introducing new mechanics and overhauls

Important:
- Added sulfur and redstone trades to EBF chan until we figure out the better early game source
- Added extractinator item piping support (thanks Three)
- Changed Large Miner outputs (thanks HiKoNe)
- Changed primitive water pump to give a bit more water to avoid extreme pump spam (thanks Three)
- Changed battery buffer to support directional in/out (thanks TrRzeczy, TheLarvi)
- Fixed non-normal simple pipes drop item (thanks HiKoNe)
- Fixed fisher, item collector, miner pipes interactions (thanks PotatoSagall)
- Fixed hammer having pickaxe abilities (thanks Leha44581)
- Fixed Shift+click logic in storages (thanks Three)
- Fixed wires overamperage logic and battery buffers energy dupe (thanks TheLarvi, TrRzeczy)
- Fixed super tank capacity (thanks HiKoNe)
- Fixed parallel recipes output for byproducts
- Removed gt default harderRods because Lathe should be better (thanks Flungus)

QOL:
- Added fluid drum RMB onto fluid slots (thanks Three)
- Added terraria coins packing recipe
- Added more safe ore substitutions
- Added gt ores to vanilla ores recipe
- Added medium oil source (thanks HiKoNe)
- Added shovel chance to get bait from grass (thanks Flareguy)
- Added FAQ to EBF Chan until proper questbook chapter
- Fixed auto-output into bus lag (thanks TheLarvi)
- Fixed recipe browser space tokenization logic
- Removed whitelist by default
- Removed 999 stack size limit, was inconvenient (thanks Three)

Visuals:
- Added drums fluid render in inventory
- Changed ore block render to fit terraria style
- Changed cables render in inventory (thanks PotatoSagall)
- Fixed fluid drilling rig recipe browser tooltip (thanks TrRzeczy)
- Fixed solar boiler tooltips (thanks Leha44581)
- Fixed multiblock ghosts render near formed multiblocks
- Fixed warning tooltips color render (thanks TheLarvi)
- Fixed steam machines energy consumption tooltip (thanks TheLarvi)
- Fixed terraria paint onto hatches (thanks TrRzeczy)
- Fixed EBF coils active state of world rejoin
- Fixed parallel hatches texture
- Fixed sound collision when lots of machines are running
- Removed stack size tooltip conversion (thanks TheLarvi)

## 0.0.4

Pipe update! Wires/pipes now have a proper neat render, animated in world and can intersect (check Pipe Intersection item)

- Added native gt tooltips for crafting ingredients (thanks Konomi)
- Added nether star to WoF drop pool (thanks Flungus, hamood)
- Added redstone relation to amber (thanks Neko CR-21)
- Added bigger variations of simple pipes (thanks Flungus)
- Added warning to empty whitelist on filters for better UX (thanks TrRzeczy)
- Added questbook wip disclaimer (thanks megacraftbuilder)
- Added extra resin source
- Added ability to use nether stars in a star cannon (thanks Royal Tek)
- Changed oil drilling rig to give you more oil per cycle (thanks TrRzeczy)
- Changed solar boilers and panels sky check so its more convenient to use (thanks TrRzeczy, TheLarvi)
- Changed ore rarity so lava won't destroy them (thanks hamood)
- Fixed running recipe loading inconsistency on changing mods (thanks TrRzeczy)
- Fixed duplicated mining speed tooltip (thanks Konomi)
- Fixed EBF-chan name render and census support (thanks Konomi for implementation)
- Fixed recipe browser chance render for very low chances (thanks TrRzeczy)
- Fixed trees dropping rubber wood instead of rubber logs, added acorns (thanks HiKoNe, Natsuki)
- Fixed EoW and BoC loot droprates (thanks HiKoNe)
- Fixed wrenches pipe highlight (thanks hamood)
- Fixed edge case with 2 touching fluid pipes of different type interacting with the same tile (thanks PotatoSagall)
- Fixed several obsolete quests from stone and steam age (thanks Flungus)

## 0.0.3

- Added recipe browser station query clicks (thanks PotatoSagall)
- Added ender pearls to LV bosses drop pool for earlier fluid voiding cover (thanks Neko CR-21)
- Added missing Fluid Detector and Item Detector Cover recipe as crafting ingredient
- Added clay recipe from sand and water (thanks PotatoSagall)
- Added terraprisma gt recipe (thanks abxx5)
- Added so you can use gregtech rounds in guns (thanks PotatoSagall)
- Added vanilla gems compat conversions (thanks hamood, thanks pruberu for implementation)
- Changed EBF-chan to spawn on world creation, updated visuals (thanks QiuQiu)
- Changed pipes logic so they can interact from behind of the machines (thanks Dedust, ADtsd)
- Changed recipe browser search logic to ignore crafting tools when those aren't relevant
- Changed fluid slot render (thanks TrRzeczy)
- Fixed compatibility with Quality of Terraria mod (internal name: ImproveGame) (thanks quickgk)
- Fixed multiblock macerator picked up unresolved recipes as valid without input (thanks TrRzeczy)
- Fixed ghost circuits dropping in world (thanks TrRzeczy)
- Fixed wood planks recipes inconsistency, removed stripped wood (thanks Flungus)
- Fixed hoe couldn't harvest from planter boxes (thanks TrRzeczy)
- Fixed steam machines speed debuff (thanks TrRzeczy)
- Fixed parallel hatch active recipe state on world reload (thanks TrRzeczy)
- Fixed mining hammer aoe mining power and machine removal (thanks TrRzeczy, Char)
- Fixed crafting station icons in recipe browser for hand recipes (thanks Char, PotatoSagall)
- Fixed axe power (thanks MrKo_no)
- Fixed steam multiblocks casings (thanks TrRzeczy)
- Fixed multiplayer multiblock recipe display sync output amounts (thanks TrRzeczy)
- Fixed pre-hv macerator byproduct (thanks TrRzeczy)
- Fixed right click to open machine UI when holding item (thanks TrRzeczy)
- Fixed slow Magic Storage loading time (thanks Flungus)
- Fixed multiblock preview render overhead
- Fixed tools render rotation (thanks pruberu)
- Disabled crate+tape interaction until its fixed (thanks hamood)

Existing worlds should be fine to update

## 0.0.2

- Added gregtech-ported localization
- Fixed search bar non-english typing
- Fixed Steam Boiler ticking
- Fixed fluid crafting in hand
- Fixed several minor bugs
