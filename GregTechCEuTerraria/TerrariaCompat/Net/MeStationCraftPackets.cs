#nullable enable
using System.Collections.Generic;
using System.IO;
using GregTechCEuTerraria.AppliedEnergistics.Api.Networking.Security;
using GregTechCEuTerraria.AppliedEnergistics.Api.Stacks;
using GregTechCEuTerraria.AppliedEnergistics.Api.Storage;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics;
using GregTechCEuTerraria.TerrariaCompat.AppliedEnergistics.Crafting;
using GregTechCEuTerraria.TerrariaCompat.Net.Actions;
using GregTechCEuTerraria.TerrariaCompat.UI.MeCraft;
using GregTechCEuTerraria.TerrariaCompat.Utils;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;

namespace GregTechCEuTerraria.TerrariaCompat.Net;

public static class MeStationCraftPackets
{
	private static IMeCraftingHost? HostAt(Point16 pos) =>
		TileEntity.ByPosition.TryGetValue(pos, out var te) && te is IMeCraftingHost h ? h : null;

	public static void Request(Point16 termPos, int recipeIndex, int amount, int outputType)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) { DoRequest(termPos, recipeIndex, amount, outputType, Main.myPlayer); return; }
		var p = NetRouter.NewPacket(PacketType.MeStationCraftRequest);
		p.Write(termPos.X); p.Write(termPos.Y);
		p.Write(recipeIndex);
		p.Write(amount);
		p.Write(outputType);
		p.Send();
	}

	public static void HandleRequest(BinaryReader r, int whoAmI)
	{
		if (Main.netMode != NetmodeID.Server) return;
		var pos = new Point16(r.ReadInt16(), r.ReadInt16());
		int idx = r.ReadInt32();
		int amount = r.ReadInt32();
		int outType = r.ReadInt32();
		DoRequest(pos, idx, amount, outType, whoAmI);
	}

	private static void DoRequest(Point16 termPos, int recipeIndex, int amount, int expectedOutType, int whoAmI)
	{
		var host = HostAt(termPos);
		if (host?.Network is not { } net) { SendResult(termPos, whoAmI, false, expectedOutType, 0, null); return; }
		if (recipeIndex < 0 || recipeIndex >= Terraria.Recipe.numRecipes) { SendResult(termPos, whoAmI, false, expectedOutType, 0, null); return; }

		var recipe = Main.recipe[recipeIndex];
		if (recipe.createItem.IsAir || recipe.createItem.type != expectedOutType) { SendResult(termPos, whoAmI, false, expectedOutType, 0, null); return; }

		var stations = host.Crafting.StationTiles();
		if (!RecipeNetworkCrafting.StationsSatisfy(recipe, stations)) { SendResult(termPos, whoAmI, false, expectedOutType, 0, null); return; }

		int yield = System.Math.Max(1, recipe.createItem.stack);
		int crafts = System.Math.Clamp((amount + yield - 1) / yield, 1, 100000);

		var storage = net.GetStorage();

		RecipeNetworkCrafting.SimulateBatch(storage, recipe, crafts, out bool anyMissing);
		if (anyMissing)
		{
			SendResult(termPos, whoAmI, false, expectedOutType, 0, BuildSummary(storage, recipe, crafts));
			return;
		}

		if (!RecipeNetworkCrafting.ExtractIngredients(recipe, storage, stations, IActionSource.Empty(), crafts))
		{
			SendResult(termPos, whoAmI, false, expectedOutType, 0, BuildSummary(storage, recipe, crafts));
			return;
		}

		SendResult(termPos, whoAmI, true, expectedOutType, (long)crafts * yield, null);
	}

	private static CraftingPlanSummary BuildSummary(MEStorage storage,
		Terraria.Recipe recipe, int crafts)
	{
		var entries = RecipeNetworkCrafting.SimulateBatch(storage, recipe, crafts, out _);
		return new CraftingPlanSummary(0, true, entries, System.Array.Empty<PlanPattern>());
	}

	private static void DeliverLocal(int itemType, long count)
	{
		if (count <= 0 || itemType <= 0) return;
		var player = Main.LocalPlayer;
		var proto = new Item();
		proto.SetDefaults(itemType);
		int maxStack = System.Math.Max(1, proto.maxStack);

		if (Main.mouseItem.IsAir)
		{
			var cur = proto.Clone();
			cur.stack = (int)System.Math.Min(count, maxStack);
			Main.mouseItem = cur;
			count -= cur.stack;
		}
		else if (Main.mouseItem.type == itemType
			&& Terraria.ModLoader.ItemLoader.CanStack(Main.mouseItem, proto)
			&& Main.mouseItem.stack < Main.mouseItem.maxStack)
		{
			int add = (int)System.Math.Min(Main.mouseItem.maxStack - Main.mouseItem.stack, count);
			Main.mouseItem.stack += add;
			count -= add;
		}

		var src = player.GetSource_OpenItem(itemType);
		while (count > 0)
		{
			var give = proto.Clone();
			give.stack = (int)System.Math.Min(count, maxStack);
			count -= give.stack;
			PlayerGive.Give(player, src, give);
		}
	}

	private static void SendResult(Point16 termPos, int whoAmI, bool success, int outputType, long count, CraftingPlanSummary? summary)
	{
		if (Main.netMode == NetmodeID.SinglePlayer) { ApplyResult(termPos, success, outputType, count, summary); return; }
		var p = NetRouter.NewPacket(PacketType.MeStationCraftResult);
		p.Write(termPos.X); p.Write(termPos.Y);
		p.Write(success);
		p.Write(outputType);
		p.Write7BitEncodedInt64(count);
		p.Write(summary != null);
		summary?.Write(p);
		p.Send(toClient: whoAmI);
	}

	public static void HandleResult(BinaryReader r)
	{
		if (Main.netMode != NetmodeID.MultiplayerClient) return;
		var termPos = new Point16(r.ReadInt16(), r.ReadInt16());
		bool success = r.ReadBoolean();
		int outputType = r.ReadInt32();
		long count = r.Read7BitEncodedInt64();
		bool hasSummary = r.ReadBoolean();
		var summary = hasSummary ? CraftingPlanSummary.Read(r) : null;
		ApplyResult(termPos, success, outputType, count, summary);
	}

	private static void ApplyResult(Point16 termPos, bool success, int outputType, long count, CraftingPlanSummary? summary)
	{
		if (success)
		{
			DeliverLocal(outputType, count);
			MeStationCraftSystem.Close();
			SoundEngine.PlaySound(SoundID.Grab);
			return;
		}
		if (summary != null)
		{
			var proto = new Item();
			proto.SetDefaults(outputType);
			var key = AEItemKey.Of(proto);
			if (key != null) MeCraftConfirmSystem.OpenForStation(termPos, key, summary);
			return;
		}
		Main.NewText("Cannot craft", 255, 120, 120);
	}
}
