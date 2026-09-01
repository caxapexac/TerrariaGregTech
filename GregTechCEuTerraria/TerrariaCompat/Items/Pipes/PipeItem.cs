#nullable enable
using System.Collections.Generic;
using GregTechCEuTerraria.Api.Data.Chemical.Material;
using GregTechCEuTerraria.TerrariaCompat.Machine.Rendering;
using GregTechCEuTerraria.TerrariaCompat.Pipelike;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace GregTechCEuTerraria.TerrariaCompat.Items.Pipes;

public sealed class PipeItem : ModItem, ITextureWarmUp
{
	private readonly string? _id;
	private readonly string? _label;
	[CloneByReference] private readonly Material? _material;
	private readonly string _sizeWord = "normal"; // tiny/small/normal/large/huge/quadruple/nonuple
	private readonly PipeKind _kind = PipeKind.Item;
	private readonly bool _restrictive;

	public PipeKind Kind => _kind;

	internal string? MaterialId => _material?.Id;
	internal PipeSize Size => Pipelike.PipeSizes.FromWord(_sizeWord);
	internal bool Restrictive => _restrictive;

	public PipeItem() { }
	public PipeItem(string id, string label, Material material, string sizeWord, PipeKind kind, bool restrictive = false)
	{
		_id = id;
		_label = label;
		_material = material;
		_sizeWord = sizeWord;
		_kind = kind;
		_restrictive = restrictive;
	}

	public override string Name => _id ?? nameof(PipeItem);

	public override string Texture => $"GregTechCEuTerraria/Content/Textures/block/pipe/pipe_{_sizeWord}_in";
	protected override bool CloneNewInstances => true;
	public override bool IsLoadingEnabled(Mod mod) => _material != null;

	public override void SetStaticDefaults()
	{
		if (_label != null)
			Language.GetOrRegister($"Mods.GregTechCEuTerraria.Items.{Name}.DisplayName", () => _label);
	}

	private int _removeCooldown;

	public override void SetDefaults()
	{
		Item.maxStack = 9999;
		Item.width = 32;
		Item.height = 32;
		Item.useTime = 2;
		Item.useAnimation = 6;
		Item.useStyle = ItemUseStyleID.Swing;
		Item.autoReuse = true;
		Item.consumable = false;
		Item.rare = ItemRarityID.White;
		Item.UseSound = null;
	}

	public override void ModifyTooltips(List<TooltipLine> tooltips)
	{
		base.ModifyTooltips(tooltips);
		if (_material is null) return;
		tooltips.Add(new TooltipLine(Mod, "PipeKind",
			$"{Capitalize(_sizeWord)} {KindWord(_kind, _restrictive)}"));

		if (_kind == Pipelike.PipeKind.Fluid)
		{
			var c = BuildFluidCell();
			if (c is not null)
			{
				var f = c.Value;
				tooltips.Add(new TooltipLine(Mod, "PipeThroughput",
					$"[c/55FFFF:Transfer Rate:] {f.Throughput:N0} mB/t"));
				if (f.Channels > 1)
					tooltips.Add(new TooltipLine(Mod, "PipeChannels",
						$"[c/FFFF55:Channels:] {f.Channels}"));
			}
		}
		else
		{
			var i = BuildItemCell();
			float rate = i.TransferRate;
			string rateLine = $"[c/55FFFF:Transfer Rate:] {(int)((rate * 64) + 0.5f)} items/s";
			tooltips.Add(new TooltipLine(Mod, "PipeTransferRate", rateLine));
		}

		Tools.GregTechMultitool.AppendHint(Mod, tooltips);
	}

	private Api.Pipenet.IGridLayerHandle Layer => _kind == Pipelike.PipeKind.Fluid
		? Pipelike.Fluid.FluidPipeLayerHandle.Instance
		: (Api.Pipenet.IGridLayerHandle)Pipelike.ItemPipe.ItemPipeLayerHandle.Instance;

	public override bool? UseItem(Player player)
	{
		if (_material is null) return null;
		if (Main.myPlayer != player.whoAmI) return null;

		int x = (int)(Main.MouseWorld.X / 16);
		int y = (int)(Main.MouseWorld.Y / 16);
		if (x <= 0 || x >= Main.maxTilesX - 1 || y <= 0 || y >= Main.maxTilesY - 1)
			return false;
		if (Item.stack <= 0) return false;

		bool placed;
		if (_kind == Pipelike.PipeKind.Fluid)
		{
			var cell = BuildFluidCell();
			if (cell is null) return false;
			placed = Pipelike.Fluid.FluidPipeLayerHandle.Instance.TryPlace(cell.Value, x, y, player);
		}
		else
		{
			placed = Pipelike.ItemPipe.ItemPipeLayerHandle.Instance.TryPlace(BuildItemCell(), x, y, player);
		}
		if (!placed) return false;
		Terraria.Audio.SoundEngine.PlaySound(SoundID.Item50, new Vector2(x * 16, y * 16));
		Item.stack--;
		return true;
	}

	private Pipelike.ItemPipe.ItemPipeCell BuildItemCell()
		=> BuildItemCellForSize(Pipelike.PipeSizes.FromWord(_sizeWord));

	internal Pipelike.ItemPipe.ItemPipeCell BuildItemCellForSize(PipeSize size)
	{
		var basePriority = _material!.ItemPipe?.Priority     ?? 1;
		var baseRate     = _material .ItemPipe?.TransferRate ?? 0.25f;
		var mod = Pipelike.ItemPipe.ItemPipeSizeModifier.For(size, _restrictive);
		int  priority = (int)((basePriority * mod.ResistanceMultiplier) + 0.5f);
		float rate    = baseRate * mod.RateMultiplier;
		return new Pipelike.ItemPipe.ItemPipeCell(
			MaterialId:   _material.Id,
			Size:         size,
			Restrictive:  _restrictive,
			Priority:     priority,
			TransferRate: rate);
	}

	private Pipelike.Fluid.FluidPipeCell? BuildFluidCell()
		=> BuildFluidCellForSize(Pipelike.PipeSizes.FromWord(_sizeWord));

	internal Pipelike.Fluid.FluidPipeCell? BuildFluidCellForSize(PipeSize size)
	{
		var props = _material!.FluidPipe;
		if (props is null) return null;
		int throughput = props.Throughput * Pipelike.PipeSizes.FluidPipeCapacityMultiplier(size);
		int channels   = Pipelike.PipeSizes.FluidPipeChannels(size);
		return new Pipelike.Fluid.FluidPipeCell(
			MaterialId:          _material.Id,
			Size:                size,
			Throughput:          throughput,
			Channels:            channels,
			MaxFluidTemperature: props.MaxFluidTemperature,
			GasProof:    props.GasProof,
			CryoProof:   props.CryoProof,
			PlasmaProof: props.PlasmaProof,
			AcidProof:   props.AcidProof);
	}

	public override void HoldItem(Player player)
	{
		EnsureTextureBaked();
		if (Main.myPlayer != player.whoAmI) return;
		if (_material is null) return;
		string heldKindLabel = _kind == Pipelike.PipeKind.Fluid
			? "Fluid Pipe"
			: (_restrictive ? "Restrictive Item Pipe" : "Item Pipe");
		PipeHeldItemBehavior.Tick(player, _kind, heldKindLabel, Layer,
			ref _removeCooldown, Item.useTime);
	}

	void ITextureWarmUp.WarmUpTexture() => EnsureTextureBaked();

	private void EnsureTextureBaked() =>
		ItemIconBaker.Install(Item.type,
			$"GregTechCEuTerraria/Content/Textures/block/pipe/pipe_{_sizeWord}_in",
			Tint());

	private static string KindWord(PipeKind kind, bool restrictive) => kind switch
	{
		PipeKind.Fluid => "Fluid Pipe",
		PipeKind.Item  => restrictive ? "Restrictive Item Pipe" : "Item Pipe",
		_              => "Pipe",
	};

	private Color Tint()
	{
		uint c = _material?.Color ?? 0xFFFFFFu;
		return new Color((byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF));
	}

	private static string Capitalize(string s) =>
		string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
