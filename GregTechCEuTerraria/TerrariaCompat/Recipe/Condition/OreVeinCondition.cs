#nullable enable
using GregTechCEuTerraria.Api.Machine.Trait;
using GregTechCEuTerraria.Api.Recipe;
using GregTechCEuTerraria.TerrariaCompat.Worldgen;
using Terraria;

namespace GregTechCEuTerraria.TerrariaCompat.Recipe.Condition;

public sealed class OreVeinCondition : RecipeCondition
{
	public string Layer { get; }
	public int HeightMin { get; }
	public int HeightMax { get; }

	public OreVeinCondition() : this("STONE", 0, 0) { }
	public OreVeinCondition(string layer, int heightMin, int heightMax)
	{
		Layer = layer;
		HeightMin = heightMin;
		HeightMax = heightMax;
	}

	public override bool Test(RecipeLogic logic) => true;

	public override string GetTooltips()
	{
		if (Main.maxTilesY <= 0) return "";

		var dims = VeinWorldDimensions.Current();

		var (yMin, yMax) = LayerDepthMapping.ForVein(Layer, HeightMin, HeightMax, dims);
		int feetShallow = Feet(yMin), feetDeep = Feet(yMax);
		return $"{feetShallow}-{feetDeep} ft ({LayerName(yMax)})";
	}

	private static int Feet(int tileY) => 2 * (tileY - (int)Main.worldSurface);

	private static string LayerName(int tileY)
	{
		if (tileY > Main.maxTilesY - 204) return "Underworld";
		if (tileY > (int)Main.rockLayer) return "Caverns";
		if (tileY > (int)Main.worldSurface) return "Underground";
		return "Surface";
	}

	public override string GetTypeName() => "terraria:ore_vein";
}
