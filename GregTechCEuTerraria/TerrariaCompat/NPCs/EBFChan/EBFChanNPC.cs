#nullable enable
using System.Collections.Generic;
using System.Linq;
using GregTechCEuTerraria.Api.Fluids;
using GregTechCEuTerraria.Common.Materials;
using GregTechCEuTerraria.TerrariaCompat.Items.Fluids;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace GregTechCEuTerraria.TerrariaCompat.NPCs.EBFChan;

// Npc that guides the player through the gregtech eventually
[AutoloadHead]
public class EBFChanNPC : ModNPC
{
	public const string ShopName = "Shop";

	private const string AssetDir = "GregTechCEuTerraria/Content/TerrariaCompat/NPCs/EBFChan/";

	public override string Texture => AssetDir + "EBFChan";

	public override void SetStaticDefaults()
	{
		Main.npcFrameCount[Type] = 22;

		NPCID.Sets.HatOffsetY[Type] = 4;

		NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new() { Velocity = 1f, Direction = 1 };
		NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

		Language.GetOrRegister("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.DisplayName", () => "EBF-chan");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Bestiary",
			() => "The spirit of a Electric Blast Furnace, settled and friendly now that her Fallen shell is dealt with. She runs a little stall of heat-treated ingredients - and is always hot to the touch.");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.DeathMessage", () => "{0} let her coils go cold...");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Chat1", () => "Hold on, I'm still cooking your kanthal ingot...");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Chat2", () => "Don't touch the casing. It's hot. Everything's hot.");
		Language.GetOrRegister("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Chat3", () => "Overclock responsibly. I've seen what happens when you don't.");

		Language.GetOrRegister("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Census.SpawnCondition", () => "{$Census.SpawnConditions.Guide}");
	}

	public override void SetDefaults()
	{
		NPC.townNPC = true;
		NPC.friendly = true;
		NPC.width = 18;
		NPC.height = 40;
		NPC.aiStyle = NPCAIStyleID.Passive;
		NPC.damage = 10;
		NPC.defense = 15;
		NPC.lifeMax = 250;
		NPC.HitSound = SoundID.NPCHit4;     // metallic clang
		NPC.DeathSound = SoundID.NPCDeath14;
		NPC.knockBackResist = 0.5f;

		AnimationType = NPCID.Guide;
	}

	public override LocalizedText DeathMessage => Language.GetText("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.DeathMessage");

	public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
	{
		bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
		{
			BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
			new FlavorTextBestiaryInfoElement("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Bestiary"),
		});
	}

	// Available from world start
	public override bool CanTownNPCSpawn(int numTownNPCs) => true;

	public override ITownNPCProfile TownNPCProfile() =>
		new Profiles.DefaultNPCProfile(Texture, ModContent.GetModHeadSlot(HeadTexture));

	public override string GetChat()
	{
		WeightedRandom<string> chat = new();
		chat.Add(Language.GetTextValue("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Chat1"));
		chat.Add(Language.GetTextValue("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Chat2"));
		chat.Add(Language.GetTextValue("Mods.GregTechCEuTerraria.NPCs.EBFChanNPC.Chat3"));
		return chat;
	}

	public override void SetChatButtons(ref string button, ref string button2)
	{
		button = Language.GetTextValue("LegacyInterface.28"); // "Shop"
	}

	public override void OnChatButtonClicked(bool firstButton, ref string shop)
	{
		if (firstButton)
			shop = ShopName;
	}

	public override void AddShops()
	{
		var shop = new NPCShop(Type, ShopName);

		AddHydrogenCell(shop, Item.buyPrice(gold: 2));

		AddIfPresent(shop, "aluminium_dust", Item.buyPrice(silver: 20));
		AddIfPresent(shop, "iron_dust", Item.buyPrice(silver: 8));
		AddIfPresent(shop, "copper_dust", Item.buyPrice(silver: 8));
		AddIfPresent(shop, "tin_dust", Item.buyPrice(silver: 8));
		AddIfPresent(shop, "nickel_dust", Item.buyPrice(silver: 12));
		AddIfPresent(shop, "lead_dust", Item.buyPrice(silver: 10));

		AddIfPresent(shop, "steel_ingot", Item.buyPrice(gold: 25));
		AddIfPresent(shop, "aluminium_ingot", Item.buyPrice(gold: 35));
		AddIfPresent(shop, "kanthal_ingot", Item.buyPrice(gold: 50));

		shop.Register();
	}

	private void AddHydrogenCell(NPCShop shop, int priceCopper)
	{
		if (!Mod.TryFind<ModItem>("fluid_cell", out var cellMi)) return;
		var it = new Item(cellMi.Type);
		FluidType? hydrogen = MaterialRegistry.All.TryGetValue("hydrogen", out var mat)
			? mat.FluidProperty?.Get() ?? mat.FluidProperty?.Fluids.FirstOrDefault()
			: null;
		if (it.ModItem is FluidCellItem cell && hydrogen != null)
			cell.Fill(new FluidStack(hydrogen, cell.Capacity), simulate: false);
		it.shopCustomPrice = priceCopper;
		shop.Add(it);
	}

	private void AddIfPresent(NPCShop shop, string itemId, int priceCopper)
	{
		if (Mod.TryFind<ModItem>(itemId, out var mi))
			shop.Add(new Item(mi.Type) { shopCustomPrice = priceCopper });
	}
}
