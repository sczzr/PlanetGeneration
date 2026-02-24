using Godot;
using System.Collections.Generic;

namespace PlanetGeneration.WorldGen;

public enum MapLayer
{
    Satellite,
    Plates,
    Temperature,
    Rivers,
    Moisture,
    Wind,
    Elevation,
    RockTypes,
    Ores,
    Biomes,
    Cities,
    Landform,
    Ecology,
    Civilization,
    TradeRoutes
}

public enum ElevationStyle
{
    Realistic,
    Topographic
}

public sealed class WorldRenderer
{
    private static readonly Color DeepOcean = Hex("#1a2482");
    private static readonly Color ShallowOcean = Hex("#0059b3");
    private static readonly Color RiverBlue = Hex("#0000ff");
    private static readonly Color DarkOcean = Hex("#0a2044");
    private static readonly Color ElevationOceanDeep = Hex("#03123a");
    private static readonly Color ElevationOceanMid = Hex("#0a316f");
    private static readonly Color ElevationOceanShallow = Hex("#1674c4");
    private static readonly Color ElevationLandLow = Hex("#2e9143");
    private static readonly Color ElevationLandMidLow = Hex("#58ab51");
    private static readonly Color ElevationLandMid = Hex("#8db462");
    private static readonly Color ElevationLandHigh = Hex("#c5bc92");
    private static readonly Color ElevationLandVeryHigh = Hex("#ddd3b6");
    private static readonly Color ElevationLandPeak = Hex("#f0e8d7");
    private static readonly Color ElevationLandSnow = Hex("#f9fafb");

    public Image Render(
        int width,
        int height,
        MapLayer layer,
        PlateResult plate,
        float[,] elevation,
        float[,] temperature,
        float[,] moisture,
        Vector2[,] wind,
        float[,] river,
        BiomeType[,] biome,
        RockType[,] rock,
        OreType[,] ore,
        List<CityInfo> cities,
        float seaLevel,
        ElevationStyle elevationStyle,
        float[,]? ecology = null,
        float[,]? civilizationInfluence = null,
        int[,]? civilizationPolityId = null,
        bool[,]? civilizationBorders = null,
        bool[,]? tradeRouteMask = null,
        float[,]? tradeFlow = null)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        var cityMask = new bool[width, height];
        if (layer == MapLayer.Cities)
        {
            MarkCities(cityMask, cities, width, height);
        }

        float[,]? avgMoisture = null;
        if (layer == MapLayer.Satellite || layer == MapLayer.Cities || layer == MapLayer.Wind)
        {
            avgMoisture = AverageLandArray(moisture, elevation, width, height, seaLevel, 13);
        }

        if (layer == MapLayer.Wind)
        {
            return RenderWindVectors(width, height, wind, elevation, temperature, avgMoisture!, biome, river, seaLevel);
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = layer switch
                {
                    MapLayer.Plates => DrawPlates(plate, x, y),
                    MapLayer.Temperature => DrawTemperature(temperature[x, y]),
                    MapLayer.Rivers => DrawRivers(elevation[x, y], seaLevel, river[x, y]),
                    MapLayer.Moisture => DrawMoisture(moisture[x, y]),
                    MapLayer.Wind => Colors.Black,
                    MapLayer.Elevation => DrawElevation(elevation[x, y], seaLevel, elevationStyle),
                    MapLayer.RockTypes => DrawRock(rock[x, y], elevation[x, y], seaLevel),
                    MapLayer.Ores => DrawOre(ore[x, y], elevation[x, y], seaLevel),
                    MapLayer.Biomes => DrawBiomeColor(biome[x, y]),
                    MapLayer.Ecology when ecology != null => DrawEcologyColor(ecology[x, y], elevation[x, y], seaLevel),
                    MapLayer.Civilization when civilizationInfluence != null => DrawCivilizationColor(
                        civilizationInfluence[x, y],
                        civilizationPolityId != null ? civilizationPolityId[x, y] : -1,
                        civilizationBorders != null && civilizationBorders[x, y],
                        elevation[x, y],
                        seaLevel),
                    MapLayer.TradeRoutes when tradeRouteMask != null => DrawTradeRouteColor(
                        tradeRouteMask[x, y],
                        tradeFlow != null ? tradeFlow[x, y] : 0f,
                        civilizationInfluence != null ? civilizationInfluence[x, y] : 0f,
                        elevation[x, y],
                        seaLevel),
                    MapLayer.Cities => DrawCitiesOverlay(
                        cityMask[x, y],
                        x,
                        y,
                        width,
                        height,
                        elevation[x, y],
                        temperature[x, y],
                        avgMoisture![x, y],
                        biome[x, y],
                        river[x, y],
                        seaLevel),
                    _ => DrawSatellite(
                        y,
                        height,
                        elevation[x, y],
                        temperature[x, y],
                        avgMoisture![x, y],
                        biome[x, y],
                        river[x, y],
                        seaLevel)
                };

                image.SetPixel(x, y, SanitizeColor(color));
            }
        }

        return image;
    }

    private Color DrawEcologyColor(float ecology, float elevation, float seaLevel)
    {
        if (elevation <= seaLevel)
        {
            var deep = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            return DarkOcean.Lerp(ShallowOcean, 1f - deep * 0.65f);
        }

        var t = Mathf.Clamp(ecology, 0f, 1f);
        if (t < 0.34f)
        {
            var local = t / 0.34f;
            return Hex("#8a4f2b").Lerp(Hex("#d69a45"), local);
        }

        if (t < 0.67f)
        {
            var local = (t - 0.34f) / 0.33f;
            return Hex("#d69a45").Lerp(Hex("#74b152"), local);
        }

        var lush = (t - 0.67f) / 0.33f;
        return Hex("#74b152").Lerp(Hex("#2bcf74"), lush);
    }

    private Color DrawCivilizationColor(float influence, int polityId, bool isBorder, float elevation, float seaLevel)
    {
        if (elevation <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            return DarkOcean.Lerp(ShallowOcean, 1f - depth * 0.55f);
        }

        if (polityId < 0 || influence < 0.16f)
        {
            var neutral = Mathf.Clamp(influence, 0f, 1f);
            return Hex("#3a3f47").Lerp(Hex("#5e6672"), neutral * 0.75f);
        }

        var baseColor = ColorForPolity(polityId);
        var tinted = Hex("#1f232a").Lerp(baseColor, Mathf.Clamp(influence * 0.95f + 0.05f, 0f, 1f));
        if (isBorder)
        {
            return tinted.Lerp(Colors.White, 0.26f);
        }

        return tinted;
    }

    private static Color ColorForPolity(int polityId)
    {
        uint hash = unchecked((uint)(polityId * 2654435761));
        var red = 0.28f + ((hash & 0xFFu) / 255f) * 0.62f;
        var green = 0.28f + (((hash >> 8) & 0xFFu) / 255f) * 0.62f;
        var blue = 0.28f + (((hash >> 16) & 0xFFu) / 255f) * 0.62f;
        return new Color(red, green, blue, 1f);
    }

    private Color DrawTradeRouteColor(bool hasRoute, float flow, float influence, float elevation, float seaLevel)
    {
        if (elevation <= seaLevel)
        {
            var depth = Mathf.Clamp((seaLevel - elevation) / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
            return DarkOcean.Lerp(ShallowOcean, 1f - depth * 0.55f);
        }

        var baseGround = Hex("#2f3f34").Lerp(Hex("#4a5f47"), Mathf.Clamp(influence, 0f, 1f) * 0.55f);
        if (!hasRoute)
        {
            return baseGround;
        }

        var routeColor = Hex("#d79a4a").Lerp(Hex("#f4df8c"), Mathf.Clamp(flow, 0f, 1f));
        return baseGround.Lerp(routeColor, 0.72f);
    }

    private void MarkCities(bool[,] cityMask, List<CityInfo> cities, int width, int height)
    {
        foreach (var city in cities)
        {
            var cx = city.Position.X;
            var cy = city.Position.Y;

            var radius = city.Population switch
            {
                CityPopulation.Small => 1,
                CityPopulation.Medium => 2,
                _ => 3
            };

            var radiusSquared = radius * radius;

            for (var oy = -radius; oy <= radius; oy++)
            {
                for (var ox = -radius; ox <= radius; ox++)
                {
                    var x = cx + ox;
                    var y = cy + oy;

                    if (x < 0)
                    {
                        x += width;
                    }
                    else if (x >= width)
                    {
                        x -= width;
                    }

                    if (y < 0 || y >= height)
                    {
                        continue;
                    }

                    if (ox * ox + oy * oy <= radiusSquared)
                    {
                        cityMask[x, y] = true;
                    }
                }
            }
        }
    }

    private Color DrawPlates(PlateResult plate, int x, int y)
    {
        var site = plate.Sites[plate.PlateIds[x, y]];

        return plate.BoundaryTypes[x, y] switch
        {
            PlateBoundaryType.Convergent => new Color(0.98f, 0.22f, 0.22f),
            PlateBoundaryType.Divergent => new Color(0.22f, 0.92f, 0.95f),
            PlateBoundaryType.Transform => new Color(0.98f, 0.80f, 0.28f),
            _ => site.DebugColor
        };
    }

    private Color DrawTemperature(float value)
    {
        var t = Mathf.Clamp(value, 0f, 1f);
        var cold = Hex("#004cff");
        var mild = Hex("#ffe45c");
        var hot = Hex("#ff2a00");

        if (t < 0.5f)
        {
            return Blend(cold, mild, t * 2f);
        }

        return Blend(mild, hot, (t - 0.5f) * 2f);
    }

	private Color DrawRivers(float elevation, float seaLevel, float river)
	{
		if (river > 0.12f)
		{
			var t = Mathf.Clamp((river - 0.12f) / 1.2f, 0f, 1f);
			return Blend(Hex("#0e3f95"), RiverBlue, t);
		}

        if (elevation < 0.5714f * seaLevel)
        {
            return DeepOcean;
        }

        if (elevation < seaLevel)
        {
            return ShallowOcean;
        }

        return Colors.Black;
    }

    private Color DrawMoisture(float value)
    {
        var t = Mathf.Clamp(value, 0f, 1f);
        var lightRain = Hex("#d9ecff");
        var mediumRain = Hex("#5aa9ff");
        var heavyRain = Hex("#0d3f95");

        if (t < 0.5f)
        {
            return Blend(lightRain, mediumRain, t * 2f);
        }

        return Blend(mediumRain, heavyRain, (t - 0.5f) * 2f);
    }

    private Image RenderWindVectors(
        int width,
        int height,
        Vector2[,] wind,
        float[,] elevation,
        float[,] temperature,
        float[,] avgMoisture,
        BiomeType[,] biome,
        float[,] river,
        float seaLevel)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var terrain = DrawSatellite(y, height, elevation[x, y], temperature[x, y], avgMoisture[x, y], biome[x, y], river[x, y], seaLevel);
                terrain = Blend(terrain, Colors.Black, 0.30f);
                image.SetPixel(x, y, SanitizeColor(terrain));
            }
        }

        return image;
    }

    public void OverlayWindArrows(Image image, Vector2[,] wind, int sourceWidth, int sourceHeight, float density)
    {
        if (image.GetWidth() <= 0 || image.GetHeight() <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        var targetWidth = image.GetWidth();
        var targetHeight = image.GetHeight();

        var densityFactor = Mathf.Clamp(density, 0.5f, 2.5f);
        var desiredColumns = Mathf.Clamp(Mathf.RoundToInt(targetWidth / 92f * densityFactor), 20, 140);
        var desiredRows = Mathf.Clamp(Mathf.RoundToInt(targetHeight / 92f * densityFactor), 10, 72);
        var spacingX = Mathf.Max(36, targetWidth / Mathf.Max(desiredColumns, 1));
        var spacingY = Mathf.Max(36, targetHeight / Mathf.Max(desiredRows, 1));

        var windColor = new Color(0.92f, 0.97f, 1f, 1f);
        for (var y = spacingY / 2; y < targetHeight; y += spacingY)
        {
            for (var x = spacingX / 2; x < targetWidth; x += spacingX)
            {
                var sampleX = Mathf.Clamp((int)((long)x * sourceWidth / targetWidth), 0, sourceWidth - 1);
                var sampleY = Mathf.Clamp((int)((long)y * sourceHeight / targetHeight), 0, sourceHeight - 1);
                var vector = wind[sampleX, sampleY];
                DrawWindArrow(image, x, y, vector, windColor);
            }
        }
    }

    private void DrawWindArrow(Image image, int x, int y, Vector2 vector, Color color)
    {
        var magnitude = vector.Length();
        if (magnitude <= 0.0001f)
        {
            return;
        }

        var direction = vector / magnitude;
        var shaftLength = Mathf.Lerp(12f, 30f, Mathf.Clamp(magnitude / 35f, 0f, 1f));
        var endX = x + direction.X * shaftLength;
        var endY = y + direction.Y * shaftLength;

        DrawWindLineThick(image, x, y, endX, endY, color, 2);

        var back = -direction;
        const float headAngle = 0.55f;
        var headLength = Mathf.Clamp(shaftLength * 0.38f, 5f, 11f);
        var leftHead = RotateVector(back, headAngle) * headLength;
        var rightHead = RotateVector(back, -headAngle) * headLength;

        DrawWindLineThick(image, endX, endY, endX + leftHead.X, endY + leftHead.Y, color, 2);
        DrawWindLineThick(image, endX, endY, endX + rightHead.X, endY + rightHead.Y, color, 2);
        DrawWindPoint(image, x, y, 1, color);
    }

    private void DrawWindLineThick(Image image, float x0, float y0, float x1, float y1, Color color, int thickness)
    {
        if (thickness <= 1)
        {
            DrawWindLine(image, x0, y0, x1, y1, color);
            return;
        }

        var direction = new Vector2(x1 - x0, y1 - y0);
        var length = direction.Length();
        if (length <= 0.0001f)
        {
            DrawWindPoint(image, Mathf.RoundToInt(x0), Mathf.RoundToInt(y0), thickness / 2, color);
            return;
        }

        var normal = new Vector2(-direction.Y / length, direction.X / length);
        var half = 0.5f * (thickness - 1);

        for (var i = 0; i < thickness; i++)
        {
            var offset = i - half;
            var ox = normal.X * offset;
            var oy = normal.Y * offset;
            DrawWindLine(image, x0 + ox, y0 + oy, x1 + ox, y1 + oy, color);
        }
    }

    private static Vector2 RotateVector(Vector2 input, float angle)
    {
        var cos = Mathf.Cos(angle);
        var sin = Mathf.Sin(angle);
        return new Vector2(input.X * cos - input.Y * sin, input.X * sin + input.Y * cos);
    }

    private void DrawWindPoint(Image image, int cx, int cy, int radius, Color color)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();
        var radiusSq = radius * radius;

        for (var oy = -radius; oy <= radius; oy++)
        {
            for (var ox = -radius; ox <= radius; ox++)
            {
                if (ox * ox + oy * oy > radiusSq)
                {
                    continue;
                }

                var x = cx + ox;
                var y = cy + oy;

                if (x < 0)
                {
                    x += width;
                }
                else if (x >= width)
                {
                    x -= width;
                }

                if (y < 0 || y >= height)
                {
                    continue;
                }

                image.SetPixel(x, y, color);
            }
        }
    }

    private void DrawWindLine(Image image, float x0, float y0, float x1, float y1, Color color)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();

        var dx = x1 - x0;
        var dy = y1 - y0;
        var steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));

        if (steps <= 0.0001f)
        {
            DrawWindPoint(image, Mathf.RoundToInt(x0), Mathf.RoundToInt(y0), 0, color);
            return;
        }

        var count = Mathf.CeilToInt(steps);
        for (var i = 0; i <= count; i++)
        {
            var t = count == 0 ? 0f : (float)i / count;
            var x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            var y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));

            if (x < 0)
            {
                x = (x % width + width) % width;
            }
            else if (x >= width)
            {
                x %= width;
            }

            if (y < 0 || y >= height)
            {
                continue;
            }

            image.SetPixel(x, y, color);
        }
    }
    private Color DrawElevation(float value, float seaLevel, ElevationStyle style)
    {
        return style == ElevationStyle.Realistic
            ? DrawElevationRealistic(value, seaLevel)
            : DrawElevationTopographic(value, seaLevel);
    }

    private Color DrawElevationRealistic(float value, float seaLevel)
    {
        var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);

        if (value < safeSea)
        {
            var oceanT = Mathf.Clamp(value / safeSea, 0f, 1f);

            if (oceanT < 0.38f)
            {
                return Blend(ElevationOceanDeep, ElevationOceanMid, oceanT / 0.38f);
            }

            return Blend(ElevationOceanMid, ElevationOceanShallow, (oceanT - 0.38f) / 0.62f);
        }

        var landT = Mathf.Clamp((value - safeSea) / Mathf.Max(1f - safeSea, 0.0001f), 0f, 1f);

        if (landT < 0.16f)
        {
            return Blend(ElevationLandLow, ElevationLandMidLow, landT / 0.16f);
        }

        if (landT < 0.36f)
        {
            return Blend(ElevationLandMidLow, ElevationLandMid, (landT - 0.16f) / 0.20f);
        }

        if (landT < 0.58f)
        {
            return Blend(ElevationLandMid, ElevationLandHigh, (landT - 0.36f) / 0.22f);
        }

        if (landT < 0.80f)
        {
            return Blend(ElevationLandHigh, ElevationLandVeryHigh, (landT - 0.58f) / 0.22f);
        }

        return Blend(ElevationLandVeryHigh, ElevationLandSnow, (landT - 0.80f) / 0.20f);
    }

    private Color DrawElevationTopographic(float value, float seaLevel)
    {
        var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);

        if (value < safeSea)
        {
            var oceanT = Mathf.Clamp(value / safeSea, 0f, 1f);
            Color oceanColor;

            if (oceanT < 0.30f)
            {
                oceanColor = Blend(ElevationOceanDeep, ElevationOceanMid, oceanT / 0.30f);
            }
            else
            {
                oceanColor = Blend(ElevationOceanMid, ElevationOceanShallow, (oceanT - 0.30f) / 0.70f);
            }

            var shelfHighlight = Mathf.Clamp((oceanT - 0.82f) / 0.18f, 0f, 1f);
            if (shelfHighlight > 0f)
            {
                oceanColor = Blend(oceanColor, Hex("#2c8fd6"), shelfHighlight * 0.22f);
            }

            return oceanColor;
        }

        var landT = Mathf.Clamp((value - safeSea) / Mathf.Max(1f - safeSea, 0.0001f), 0f, 1f);
        Color landColor;

        if (landT < 0.10f)
        {
            landColor = Blend(ElevationLandLow, ElevationLandMidLow, landT / 0.10f);
        }
        else if (landT < 0.24f)
        {
            landColor = Blend(ElevationLandMidLow, ElevationLandMid, (landT - 0.10f) / 0.14f);
        }
        else if (landT < 0.42f)
        {
            landColor = Blend(ElevationLandMid, ElevationLandHigh, (landT - 0.24f) / 0.18f);
        }
        else if (landT < 0.62f)
        {
            landColor = Blend(ElevationLandHigh, ElevationLandVeryHigh, (landT - 0.42f) / 0.20f);
        }
        else if (landT < 0.82f)
        {
            landColor = Blend(ElevationLandVeryHigh, ElevationLandPeak, (landT - 0.62f) / 0.20f);
        }
        else
        {
            landColor = Blend(ElevationLandPeak, ElevationLandSnow, (landT - 0.82f) / 0.18f);
        }

        var coastAlpha = Mathf.Clamp((0.035f - landT) / 0.035f, 0f, 1f);
        if (coastAlpha > 0f)
        {
            landColor = Blend(landColor, Hex("#a7c872"), coastAlpha * 0.28f);
        }

        var contourPhase = Mathf.Abs(Mathf.PosMod(landT * 24f, 1f) - 0.5f) * 2f;
        var contourAlpha = Mathf.Clamp((0.18f - contourPhase) / 0.18f, 0f, 1f);
        if (contourAlpha > 0f)
        {
            landColor = Blend(landColor, Hex("#5e7b49"), contourAlpha * 0.10f);
        }

        var snowPatchAlpha = Mathf.Clamp((landT - 0.88f) / 0.12f, 0f, 1f);
        if (snowPatchAlpha > 0f)
        {
            landColor = Blend(landColor, ElevationLandSnow, snowPatchAlpha * 0.30f);
        }

        return landColor;
    }

	private Color DrawSatellite(int y, int height, float elevation, float temperature, float avgMoisture, BiomeType biome, float river, float seaLevel)
	{
		Color color;
		var polarMask = ComputePolarMask(y, height);
		var safeSea = Mathf.Clamp(seaLevel, 0.0001f, 0.9999f);

		if (elevation < safeSea)
		{
			color = DrawElevationRealistic(elevation, safeSea);
			color = Blend(color, DarkOcean, 0.18f);

			if (polarMask > 0f)
			{
				var seaIceAlpha = polarMask * Mathf.Clamp((0.34f - temperature) / 0.34f, 0f, 1f);
                if (seaIceAlpha > 0f)
                {
                    color = Blend(color, new Color(0.84f, 0.92f, 0.98f), Mathf.Clamp(seaIceAlpha, 0f, 0.78f));
                }
            }
		}
		else
		{
			var landT = Mathf.Clamp((elevation - safeSea) / Mathf.Max(1f - safeSea, 0.0001f), 0f, 1f);
			var elevationBase = DrawElevationRealistic(elevation, safeSea);

			var moisture = Mathf.Clamp(avgMoisture, 0f, 1f);
			var vegetationStrength = Mathf.Clamp((1f - Mathf.Pow(landT, 1.22f)) * (0.35f + 0.65f * moisture), 0.14f, 0.78f);
			var dryHue = Hex("#8f7b56");
			var wetHue = Hex("#2f7b43");
			var vegetationHue = Blend(dryHue, wetHue, moisture);

			color = Blend(elevationBase, vegetationHue, vegetationStrength);

			if (elevation <= safeSea + 0.01f)
			{
				color = Blend(color, new Color(222f / 255f, 232f / 255f, 187f / 255f), 0.2f);
			}

			var rockyAlpha = Mathf.Clamp((landT - 0.56f) / 0.30f, 0f, 1f);
			if (rockyAlpha > 0f)
			{
				var rockTint = Blend(Hex("#7f6e57"), Hex("#b9ab95"), Mathf.Clamp((landT - 0.72f) / 0.22f, 0f, 1f));
				color = Blend(color, rockTint, rockyAlpha * 0.54f);
			}

			var snowAlpha = ComputeSatelliteSnowAlpha(elevation, temperature, seaLevel);
			var peakSnowFloor = Mathf.Clamp((landT - 0.82f) / 0.18f, 0f, 1f) * 0.64f;
			snowAlpha = Mathf.Max(snowAlpha, peakSnowFloor);
			if (biome == BiomeType.Ice)
			{
				snowAlpha = Mathf.Max(snowAlpha, 0.55f);
            }

            if (snowAlpha > 0f)
            {
                color = Blend(color, new Color(232f / 255f, 246f / 255f, 255f / 255f), snowAlpha);
            }

            if (polarMask > 0f)
            {
                var polarSnowAlpha = polarMask * Mathf.Clamp((0.30f - temperature) / 0.30f, 0f, 1f);
                if (polarSnowAlpha > 0f)
                {
                    color = Blend(color, new Color(236f / 255f, 248f / 255f, 255f / 255f), Mathf.Clamp(polarSnowAlpha, 0f, 0.82f));
                }
            }
        }

		if (river > 0.12f)
		{
			var riverAlpha = Mathf.Clamp((river - 0.12f) / 1.2f, 0.18f, 0.62f);
			color = Blend(color, DarkOcean, riverAlpha);
		}

		return color;
	}

	private static float ComputePolarMask(int y, int height)
	{
		if (height <= 1)
		{
			return 0f;
		}

		var latitude = Mathf.Abs((2f * y / (height - 1f)) - 1f);
		var t = Mathf.Clamp((latitude - 0.74f) / 0.26f, 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	private static float ComputeSatelliteSnowAlpha(float elevation, float temperature, float seaLevel)
	{
		if (elevation <= seaLevel)
		{
			return 0f;
		}

		var normalizedElevation = Mathf.Clamp((elevation - seaLevel) / Mathf.Max(1f - seaLevel, 0.0001f), 0f, 1f);
		var coldness = Mathf.Clamp((0.24f - temperature) / 0.24f, 0f, 1f);
		var altitudeBoost = Mathf.Lerp(0.28f, 1f, normalizedElevation);
		var alpha = Mathf.Pow(coldness, 1.55f) * altitudeBoost;
		return Mathf.Clamp(alpha, 0f, 0.95f);
	}

    private Color DrawRock(RockType rock, float elevation, float seaLevel)
    {
        if (elevation < seaLevel)
        {
            return DeepOcean;
        }

        return rock switch
        {
            RockType.Sedimentary => Hex("#FFF307"),
            RockType.Igneous => Hex("#4da0ab"),
            RockType.Metamorphic => Hex("#EF6876"),
            _ => Colors.Black
        };
    }

    private Color DrawOre(OreType ore, float elevation, float seaLevel)
    {
        if (elevation < seaLevel)
        {
            return DeepOcean;
        }

        return ore switch
        {
            OreType.Aluminum => Hex("#34e5f5"),
            OreType.Tin => Hex("#298970"),
            OreType.Copper => Hex("#F7B946"),
            OreType.Silver => Hex("#E7E7EE"),
            OreType.Lead => Hex("#EAA19A"),
            OreType.Gold => Hex("#F3F029"),
            OreType.Iron => Hex("#ea4545"),
            OreType.Platinum => Hex("#5bcd5e"),
            OreType.Coal => Hex("#808080"),
            OreType.Diamond => Hex("#cb5bea"),
            _ => Colors.Black
        };
    }

    private Color DrawBiomeColor(BiomeType biome)
    {
        return biome switch
        {
            BiomeType.Ocean => Hex("#2f5f88"),
            BiomeType.ShallowOcean => Hex("#4f7ea8"),
            BiomeType.Coastland => Hex("#dfe4c9"),
            BiomeType.TropicalRainForest => Hex("#7acb33"),
            BiomeType.TropicalSeasonalForest => Hex("#aed45a"),
            BiomeType.Shrubland => Hex("#7c8f53"),
            BiomeType.Savanna => Hex("#cfd18a"),
            BiomeType.TropicalDesert => Hex("#e9d79b"),
            BiomeType.TemperateRainForest => Hex("#46a857"),
            BiomeType.TemperateSeasonalForest => Hex("#2fb95a"),
            BiomeType.Chaparral => Hex("#a8a07f"),
            BiomeType.Grassland => Hex("#b8c98a"),
            BiomeType.Steppe => Hex("#c7c5ac"),
            BiomeType.TemperateDesert => Hex("#d7c691"),
            BiomeType.BorealForest => Hex("#4f6e34"),
            BiomeType.Taiga => Hex("#5f8640"),
            BiomeType.Tundra => Hex("#a1814a"),
            BiomeType.Ice => Hex("#c2d3da"),
            BiomeType.RockyMountain => Hex("#8f8067"),
            BiomeType.SnowyMountain => Hex("#e7edf0"),
            BiomeType.River => Hex("#4f7ea8"),
            _ => Colors.Black
        };
    }

    private Color DrawCitiesOverlay(bool hasCity, int x, int y, int width, int height, float elevation, float temperature, float avgMoisture, BiomeType biome, float river, float seaLevel)
    {
        if (hasCity)
        {
            return Hex("#ff3bbf");
        }

        return Blend(DrawSatellite(y, height, elevation, temperature, avgMoisture, biome, river, seaLevel), Colors.Black, 0.45f);
    }

    private float[,] AverageLandArray(float[,] input, float[,] elevation, int width, int height, float seaLevel, int radius)
    {
        var result = new float[width, height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                var count = 1;

                for (var oy = -radius; oy <= radius; oy++)
                {
                    for (var ox = -radius; ox <= radius; ox++)
                    {
                        var nx = x + ox;
                        var ny = y + oy;

                        if (nx < 0)
                        {
                            nx = width + nx;
                        }
                        else if (nx >= width)
                        {
                            nx %= width;
                        }

                        if (ny < 0)
                        {
                            ny = 0;
                        }
                        else if (ny >= height)
                        {
                            ny = height - 1;
                        }

                        if (elevation[nx, ny] > seaLevel)
                        {
                            sum += input[nx, ny];
                            count++;
                        }
                    }
                }

                result[x, y] = sum / Mathf.Max(count, 1);
            }
        }

        return result;
    }

    private static Color ColorLuminance(string hex, float lumValue)
    {
        var baseColor = Hex(hex);
        var lum = (1f - lumValue) * (-1f);

        var r = Mathf.Clamp(baseColor.R * 255f + baseColor.R * 255f * lum, 0f, 255f);
        var g = Mathf.Clamp(baseColor.G * 255f + baseColor.G * 255f * lum, 0f, 255f);
        var b = Mathf.Clamp(baseColor.B * 255f + baseColor.B * 255f * lum, 0f, 255f);

        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    private static Color Blend(Color baseColor, Color overlay, float alpha)
    {
        var a = Mathf.Clamp(alpha, 0f, 1f);
        return new Color(
            baseColor.R * (1f - a) + overlay.R * a,
            baseColor.G * (1f - a) + overlay.G * a,
            baseColor.B * (1f - a) + overlay.B * a,
            1f);
    }

    private static Color SanitizeColor(Color color)
    {
        if (float.IsNaN(color.R) || float.IsNaN(color.G) || float.IsNaN(color.B) ||
            float.IsInfinity(color.R) || float.IsInfinity(color.G) || float.IsInfinity(color.B))
        {
            return Colors.Black;
        }

        return new Color(
            Mathf.Clamp(color.R, 0f, 1f),
            Mathf.Clamp(color.G, 0f, 1f),
            Mathf.Clamp(color.B, 0f, 1f),
            1f);
    }

    private static Color Hex(string hex)
    {
        var value = hex.StartsWith("#") ? hex.Substring(1) : hex;

        if (value.Length != 6)
        {
            return Colors.Magenta;
        }

        var r = byte.Parse(value.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        var g = byte.Parse(value.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        var b = byte.Parse(value.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
