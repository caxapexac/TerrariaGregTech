#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Pipes;

// throughput base = Potin (40 scaled), heat tolerance + containment = Naquadah + acid-proof
public sealed class SimpleFluidPipeItem : ModItem, ITextureWarmUp
{
	private const int PotinBaseThroughput = 40;
	private const int  NaquadahMaxTemp     = 3776;
	private const bool NaquadahGasProof    = true;
	private const bool NaquadahAcidProof   = true;
	private const bool NaquadahCryoProof   = true;
	private const bool NaquadahPlasmaProof = true;

	private readonly PipeSize? _size;

	public SimpleFluidPipeItem() { }
	public SimpleFluidPipeItem(PipeSize size) { _size = size; }

	public override bool IsLoadingEnabled(Mod mod) => _size != null;

	private PipeSize Size     => _size ?? PipeSize.Normal;
	private string   SizeWord => PipeSizes.Word(Size);

	public override string Name => Size == PipeSize.Normal
		? "simple_fluid_pipe"
		: $"simple_fluid_pipe_{SizeWord}";

	public override string Texture => $"GregTechCEuTerraria/Content/Textures/block/pipe/pipe_{SizeWord}_in";
	protected override bool CloneNewInstances => true;

	public override void SetStaticDefaults()
	{
		string label = Size == PipeSize.Normal
			? "Simple Fluid Pipe"
			: $"{Capitalize(SizeWord)} Simple Fluid Pipe";
		Language.GetOrRegister(
			$"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName",
			() => label);
	}

	public override void SetDefaults()
	{
		Item.maxStack = 9999;
		Item.width = 32; Item.height = 32;
		Item.useTime = 2; Item.useAnimation = 6;
		Item.useStyle = ItemUseStyleID.Swing;
		Item.autoReuse = true;
		Item.consumable = false;
		Item.rare = ItemRarityID.White;
		Item.UseSound = null;
	}

	private int Throughput => PotinBaseThroughput * PipeSizes.FluidPipeCapacityMultiplier(Size);

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		tooltips.Add(new TooltipLine(Mod, "PipeKind", $"{Capitalize(SizeWord)} Simple Fluid Pipe"));
		tooltips.Add(new TooltipLine(Mod, "PipeRate", $"[c/55FFFF:Transfer Rate:] {Throughput:N0} mB/t"));
		tooltips.Add(new TooltipLine(Mod, "PipeSimple", "[c/AAFFAA:Auto-connects to adjacent storage on placement.]"));
		tooltips.Add(new TooltipLine(Mod, "PipeSimpleUI", "[c/AAFFAA:Right-click to toggle per-side mode (Off / Insert / Extract).]"));
		Tools.GregTechMultitool.AppendHint(Mod, tooltips);
	}

	public override bool? UseItem(Player player)
	{
		if (Main.myPlayer != player.whoAmI) return null;
		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1) return false;
		if (Item.stack <= 0) return false;

		var cell = BuildCell(Size);

		if (!Pipelike.Fluid.FluidPipeLayerHandle.Instance.TryPlace(cell, x, y, player))
			return false;

		Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50, new Microsoft.Xna.Framework.Vector2(x * 16, y * 16));
		SimpleItemPipeItem.AutoInsertOnAdjacentStorage(Pipelike.PipeKind.Fluid, x, y);

		Item.stack--;
		return true;
	}

	internal static readonly PipeSize[] Sizes =
		{ PipeSize.Tiny, PipeSize.Small, PipeSize.Normal, PipeSize.Large, PipeSize.Huge };

	public static Pipelike.Fluid.FluidPipeCell BuildCell(PipeSize size)
	{
		int throughput = PotinBaseThroughput * PipeSizes.FluidPipeCapacityMultiplier(size);
		return new Pipelike.Fluid.FluidPipeCell(
			MaterialId:          "simple_fluid",
			Size:                size,
			Throughput:          throughput,
			Channels:            PipeSizes.FluidPipeChannels(size),
			MaxFluidTemperature: NaquadahMaxTemp,
			GasProof:            NaquadahGasProof,
			CryoProof:           NaquadahCryoProof,
			PlasmaProof:         NaquadahPlasmaProof,
			AcidProof:           NaquadahAcidProof,
			IsSimple:            true);
	}

	public static int TypeFor(PipeSize size)
	{
		string id = size == PipeSize.Normal
			? "simple_fluid_pipe"
			: "simple_fluid_pipe_" + PipeSizes.Word(size);
		return ModContent.GetInstance<GregTechCEuTerraria>().TryFind<ModItem>(id, out var mi) ? mi.Type : 0;
	}

	private static Dictionary<int, PipeSize>? _byType;
	public static bool TryGetSize(int itemType, out PipeSize size)
	{
		if (_byType is null)
		{
			var d = new Dictionary<int, PipeSize>();
			foreach (var s in Sizes) { int t = TypeFor(s); if (t > 0) d[t] = s; }
			_byType = d;
		}
		return _byType.TryGetValue(itemType, out size);
	}

	void ITextureWarmUp.WarmUpTexture() => ItemIconBaker.Install(Item.type, Texture);

	private int _removeCooldown;

	public override void HoldItem(Player player)
	{
		((ITextureWarmUp)this).WarmUpTexture();
		PipeHeldItemBehavior.Tick(player, Pipelike.PipeKind.Fluid, "Simple Fluid Pipe",
			Pipelike.Fluid.FluidPipeLayerHandle.Instance,
			ref _removeCooldown, Item.useTime);
	}

	private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
