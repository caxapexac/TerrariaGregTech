#nullable enable
using GregTechCEuTerraria.TerrariaCompat.Items.Registry;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace GregTechCEuTerraria.TerrariaCompat.Tiles.Casings;

// Placeable casing / decorative block - a plain 2x2 tile without logic
public sealed class CasingTile : ModTile, ITextureWarmUp
{
	private readonly string? _id;
	private readonly string? _texture;
	private readonly string? _activeTexture;
	private readonly string? _displayName;
	private Vector3 _glow;

	public CasingTile() { }
	public CasingTile(string id, string texture, string displayName, string? activeTexture = null)
	{
		_id = id;
		_texture = texture;
		_activeTexture = activeTexture;
		_displayName = displayName;
	}

	public override bool IsLoadingEnabled(Mod mod) => _id != null;
	public override string Name => _id ?? nameof(CasingTile);
	public override string Texture => "GregTechCEuTerraria/Content/TerrariaCompat/TooManyItemsTile";

	public bool IsActiveAware => _activeTexture != null;

	public string? BlockTexture => _texture;

	public override void SetStaticDefaults()
	{
		Main.tileFrameImportant[Type] = true;
		Main.tileSolid[Type]          = false;
		Main.tileSolidTop[Type]       = true;
		Main.tileNoAttach[Type]       = false;
		Main.tileLavaDeath[Type]      = false;
		Main.tileBlockLight[Type]     = false;
		_glow = _id is null ? Vector3.Zero : MultiActiveLight.For(_id);
		Main.tileLighted[Type]        = _glow != Vector3.Zero;
		TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
		TileObjectData.newTile.LavaDeath    = false;
		TileObjectData.newTile.Origin       = new Point16(1, 1);
		TileObjectData.newTile.AnchorBottom = default(AnchorData);
		TileObjectData.addTile(Type);
		TerrariaCompat.Players.CenteredPlacementPlayer.CenteredPlacementTiles.Add(Type);

		AddMapEntry(new Color(120, 122, 134),
			Language.GetOrRegister($"Mods.GregTechCEuTerraria.Tiles.{Name}.MapEntry",
				() => _displayName ?? RegistryDump.Humanize(Name)));

		DustType = DustID.Iron;
		HitSound = SoundID.Tink;
		MineResist = 1.5f;
		MinPick = 0;
	}

	public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
	{
		var col = _glow;
		if (col.X == 0f && col.Y == 0f && col.Z == 0f) return;

		Tile tile = Main.tile[i, j];
		int anchorX = i - (tile.TileFrameX >= 18 ? 1 : 0);
		int anchorY = j - (tile.TileFrameY >= 18 ? 1 : 0);
		if (!ActiveCasingState.IsActive(anchorX, anchorY)) return;

		r += col.X;
		g += col.Y;
		b += col.Z;
	}

	public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
	{
		WarmUpTexture();
		return true;
	}

	public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
	{
		if (_activeTexture is null) return;

		Tile tile = Main.tile[i, j];
		int anchorX = i - (tile.TileFrameX >= 18 ? 1 : 0);
		int anchorY = j - (tile.TileFrameY >= 18 ? 1 : 0);
		if (!ActiveCasingState.IsActive(anchorX, anchorY)) return;

		var sheet = CasingRenderer.GetActiveSheet(Type);
		if (sheet is null) return;

		Vector2 zero = Main.drawToScreen ? Vector2.Zero
		                                 : new Vector2(Main.offScreenRange, Main.offScreenRange);
		Vector2 pos  = new Vector2(i * 16 - (int)Main.screenPosition.X,
		                           j * 16 - (int)Main.screenPosition.Y) + zero;
		var src   = new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16);
		var light = Lighting.GetColor(i, j);
		sheet = MachineRenderer.GetPaintedSheet(sheet, tile.TileColor);
		spriteBatch.Draw(sheet, pos, src, light, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
	}

	public void WarmUpTexture()
	{
		if (_texture is not null)
			CasingRenderer.EnsureTileTexture(Type, _texture, _activeTexture);
	}
}
