#nullable enable
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.NeonSign;

public sealed class NeonSignEntity : ModTileEntity
{
	private const float StepScale = 0.2f;
	public const sbyte MinStep = 1;
	public const sbyte MaxStep = 20;
	public const sbyte DefaultStep = 5;

	public const int MaxTextLength = 200;

	private static string ClampText(string? text) =>
		text is null ? "" : text.Length <= MaxTextLength ? text : text[..MaxTextLength];

	public string Text = "";
	public byte ColorIndex;
	public sbyte SizeStep = DefaultStep;

	public float Scale => Math.Clamp(SizeStep, MinStep, MaxStep) * StepScale;

	public override bool IsTileValidForEntity(int x, int y)
	{
		Tile tile = Main.tile[x, y];
		return tile.HasTile && tile.TileType == ModContent.TileType<NeonSignTile>();
	}

	public static NeonSignEntity? At(int i, int j) =>
		TileEntity.ByPosition.TryGetValue(new Point16(i, j), out var te) && te is NeonSignEntity e ? e : null;

	public void ApplyEdit(string text, byte colorIndex, sbyte sizeStep)
	{
		Text = ClampText(text);
		ColorIndex = (byte)Math.Clamp(colorIndex, 0, NeonSignPalette.Count - 1);
		SizeStep = (sbyte)Math.Clamp(sizeStep, MinStep, MaxStep);
	}

	public override void SaveData(TagCompound tag)
	{
		tag["text"] = Text;
		tag["color"] = (int)ColorIndex;
		tag["size"] = (int)SizeStep;
	}

	public override void LoadData(TagCompound tag)
	{
		Text = ClampText(tag.GetString("text"));
		ColorIndex = (byte)Math.Clamp(tag.GetInt("color"), 0, NeonSignPalette.Count - 1);
		SizeStep = (sbyte)Math.Clamp(tag.GetInt("size"), MinStep, MaxStep);
	}

	public override void NetSend(BinaryWriter writer)
	{
		writer.Write(Text);
		writer.Write(ColorIndex);
		writer.Write(SizeStep);
	}

	public override void NetReceive(BinaryReader reader)
	{
		Text = reader.ReadString();
		ColorIndex = reader.ReadByte();
		SizeStep = reader.ReadSByte();
	}
}
