using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    TradeRoutes,

    /// <summary>
    /// 多边形地块网格：按地块填充 + 地块边界描边。
    /// 配色与 <see cref="Biomes"/> 一致，便于与栅格版本做逐像素 A/B 对照。
    /// </summary>
    PolygonGrid
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

    // Palette lookups resolved once instead of re-parsing hex strings per pixel.
    private static readonly Color TopographicShelf = Hex("#2c8fd6");
    private static readonly Color TopographicCoast = Hex("#a7c872");
    private static readonly Color TopographicContour = Hex("#5e7b49");
    private static readonly Color TemperatureCold = Hex("#004cff");
    private static readonly Color TemperatureMild = Hex("#ffe45c");
    private static readonly Color TemperatureHot = Hex("#ff2a00");
    private static readonly Color MoistureDry = Hex("#f4f8fc");
    private static readonly Color MoistureLight = Hex("#a8d0f0");
    private static readonly Color MoistureMedium = Hex("#438ecf");
    private static readonly Color MoistureHeavy = Hex("#104b8f");
    private static readonly Color MoistureTorrential = Hex("#041c42");
    private static readonly Color RiverWater = Hex("#0e3f95");
    private static readonly Color EcologyBarren = Hex("#8a4f2b");
    private static readonly Color EcologyDry = Hex("#d69a45");
    private static readonly Color EcologyGrass = Hex("#74b152");
    private static readonly Color EcologyLush = Hex("#2bcf74");
    private static readonly Color CivilizationNeutralDark = Hex("#3a3f47");
    private static readonly Color CivilizationNeutralLight = Hex("#5e6672");
    private static readonly Color CivilizationTintBase = Hex("#1f232a");
    private static readonly Color TradeGroundDark = Hex("#2f3f34");
    private static readonly Color TradeGroundLight = Hex("#4a5f47");
    private static readonly Color TradeRouteDim = Hex("#d79a4a");
    private static readonly Color TradeRouteBright = Hex("#f4df8c");
    private static readonly Color CityMarker = Hex("#ff3bbf");
    private static readonly Color SatelliteDryHue = Hex("#8f7b56");
    private static readonly Color SatelliteWetHue = Hex("#2f7b43");
    private static readonly Color SatelliteRockLow = Hex("#7f6e57");
    private static readonly Color SatelliteRockHigh = Hex("#b9ab95");
    private static readonly Color SatelliteSeaIce = new(0.84f, 0.92f, 0.98f, 1f);
    private static readonly Color SatelliteCoastSand = new(222f / 255f, 232f / 255f, 187f / 255f, 1f);
    private static readonly Color SatelliteSnow = new(232f / 255f, 246f / 255f, 255f / 255f, 1f);
    private static readonly Color SatellitePolarSnow = new(236f / 255f, 248f / 255f, 255f / 255f, 1f);

    private static readonly Color[] BiomeColors =
    {
        Hex("#2f5f88"), // Ocean
        Hex("#4f7ea8"), // ShallowOcean
        Hex("#dfe4c9"), // Coastland
        Hex("#c2d3da"), // Ice
        Hex("#a1814a"), // Tundra
        Hex("#4f6e34"), // BorealForest
        Hex("#5f8640"), // Taiga
        Hex("#c7c5ac"), // Steppe
        Hex("#b8c98a"), // Grassland
        Hex("#a8a07f"), // Chaparral
        Hex("#d7c691"), // TemperateDesert
        Hex("#2fb95a"), // TemperateSeasonalForest
        Hex("#46a857"), // TemperateRainForest
        Hex("#cfd18a"), // Savanna
        Hex("#7c8f53"), // Shrubland
        Hex("#e9d79b"), // TropicalDesert
        Hex("#aed45a"), // TropicalSeasonalForest
        Hex("#7acb33"), // TropicalRainForest
        Hex("#8f8067"), // RockyMountain
        Hex("#e7edf0"), // SnowyMountain
        Hex("#2ea3d4")  // River
    };

    private static readonly Color[] RockColors =
    {
        Hex("#FFF307"), // Sedimentary
        Hex("#4da0ab"), // Igneous
        Hex("#EF6876")  // Metamorphic
    };

    private static readonly Color[] OreColors =
    {
        Colors.Black,   // None
        Hex("#808080"), // Coal
        Hex("#F7B946"), // Copper
        Hex("#298970"), // Tin
        Hex("#ea4545"), // Iron
        Hex("#F3F029"), // Gold
        Hex("#cb5bea"), // Diamond
        Hex("#5bcd5e"), // Platinum
        Hex("#34e5f5"), // Aluminum
        Hex("#E7E7EE"), // Silver
        Hex("#EAA19A")  // Lead
    };

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

        var buffer = new byte[width * height * 4];

        if (layer == MapLayer.Wind)
        {
            Parallel.For(0, height, y =>
            {
                for (var x = 0; x < width; x++)
                {
                    var color = DrawMoisture(moisture[x, y]);
                    var isLand = elevation[x, y] >= seaLevel;
                    if (isLand)
                    {
                        var left = x > 0 ? x - 1 : width - 1;
                        var right = x < width - 1 ? x + 1 : 0;
                        var top = y > 0 ? y - 1 : 0;
                        var bottom = y < height - 1 ? y + 1 : height - 1;
                        if (elevation[left, y] < seaLevel || elevation[right, y] < seaLevel ||
                            elevation[x, top] < seaLevel || elevation[x, bottom] < seaLevel)
                        {
                            color = Hex("#111111");
                        }
                    }

                    WritePixel(buffer, width, x, y, color);
                }
            });

            return CreateImageFromBuffer(buffer, width, height);
        }

        Parallel.For(0, height, y =>
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

                WritePixel(buffer, width, x, y, SanitizeColor(color));
            }
        });

        return CreateImageFromBuffer(buffer, width, height);
    }

    private static Image CreateImageFromBuffer(byte[] buffer, int width, int height)
    {
        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, buffer);
    }

    private static void WritePixel(byte[] buffer, int width, int x, int y, Color color)
    {
        var index = (y * width + x) * 4;
        buffer[index] = ToChannelByte(color.R);
        buffer[index + 1] = ToChannelByte(color.G);
        buffer[index + 2] = ToChannelByte(color.B);
        buffer[index + 3] = 255;
    }

    private static byte ToChannelByte(float channel)
    {
        var value = Mathf.Clamp(channel, 0f, 1f) * 255f + 0.5f;
        return value >= 255f ? (byte)255 : (byte)value;
    }

    /// <summary>按生态健康度取色，与 <see cref="MapLayer.Ecology"/> 图层完全一致。</summary>
    public Color GetEcologyColor(float ecology, float elevation, float seaLevel)
        => DrawEcologyColor(ecology, elevation, seaLevel);

    /// <summary>城市标记色，与 <see cref="MapLayer.Cities"/> 图层里的标记完全一致。</summary>
    public Color GetCityMarkerColor() => CityMarker;

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
            return EcologyBarren.Lerp(EcologyDry, local);
        }

        if (t < 0.67f)
        {
            var local = (t - 0.34f) / 0.33f;
            return EcologyDry.Lerp(EcologyGrass, local);
        }

        var lush = (t - 0.67f) / 0.33f;
        return EcologyGrass.Lerp(EcologyLush, lush);
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
            return CivilizationNeutralDark.Lerp(CivilizationNeutralLight, neutral * 0.75f);
        }

        var baseColor = ColorForPolity(polityId);
        var tinted = CivilizationTintBase.Lerp(baseColor, Mathf.Clamp(influence * 0.95f + 0.05f, 0f, 1f));
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

        var baseGround = TradeGroundDark.Lerp(TradeGroundLight, Mathf.Clamp(influence, 0f, 1f) * 0.55f);
        if (!hasRoute)
        {
            return baseGround;
        }

        var routeColor = TradeRouteDim.Lerp(TradeRouteBright, Mathf.Clamp(flow, 0f, 1f));
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
        return GetPlateColor(site.DebugColor, plate.BoundaryTypes[x, y]);
    }

    /// <summary>
    /// 按板块取色：边界类型优先，否则用板块自身的调色。
    /// 逐像素与逐地块两条路径共用这一份实现，配色不可能分叉。
    /// </summary>
    public Color GetPlateColor(Color debugColor, PlateBoundaryType boundary)
    {
        return boundary switch
        {
            PlateBoundaryType.Convergent => new Color(0.98f, 0.22f, 0.22f),
            PlateBoundaryType.Divergent => new Color(0.22f, 0.92f, 0.95f),
            PlateBoundaryType.Transform => new Color(0.98f, 0.80f, 0.28f),
            _ => debugColor
        };
    }

    private Color DrawTemperature(float value)
    {
        var t = Mathf.Clamp(value, 0f, 1f);

        if (t < 0.5f)
        {
            return Blend(TemperatureCold, TemperatureMild, t * 2f);
        }

        return Blend(TemperatureMild, TemperatureHot, (t - 0.5f) * 2f);
    }

	private Color DrawRivers(float elevation, float seaLevel, float river)
	{
		if (river > 0.12f)
		{
			var t = Mathf.Clamp((river - 0.12f) / 1.2f, 0f, 1f);
			return Blend(RiverWater, RiverBlue, t);
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
        if (t < 0.25f) return Blend(MoistureDry, MoistureLight, t * 4f);
        if (t < 0.50f) return Blend(MoistureLight, MoistureMedium, (t - 0.25f) * 4f);
        if (t < 0.75f) return Blend(MoistureMedium, MoistureHeavy, (t - 0.50f) * 4f);
        return Blend(MoistureHeavy, MoistureTorrential, (t - 0.75f) * 4f);
    }

    public void OverlayWindArrows(Image image, Vector2[,] wind, int sourceWidth, int sourceHeight, float density)
    {
        if (image.GetWidth() <= 0 || image.GetHeight() <= 0 || sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        var targetWidth = image.GetWidth();
        var targetHeight = image.GetHeight();

        var densityFactor = Mathf.Clamp(density, 0.4f, 2.5f);
        var baseSpacing = 70f / densityFactor;
        var desiredColumns = Mathf.Clamp(Mathf.RoundToInt(targetWidth / baseSpacing), 16, 48);
        var desiredRows = Mathf.Clamp(Mathf.RoundToInt(targetHeight / baseSpacing), 8, 24);
        var spacingX = targetWidth / (float)desiredColumns;
        var spacingY = targetHeight / (float)desiredRows;
        var refLength = Mathf.Min(spacingX, spacingY) * 0.50f;

        var windColor = Hex("#007a24");
        for (var r = 0; r < desiredRows; r++)
        {
            var y = (int)((r + 0.5f) * spacingY);
            for (var c = 0; c < desiredColumns; c++)
            {
                var x = (int)((c + 0.5f) * spacingX);
                var sampleX = Mathf.Clamp((int)((long)x * sourceWidth / targetWidth), 0, sourceWidth - 1);
                var sampleY = Mathf.Clamp((int)((long)y * sourceHeight / targetHeight), 0, sourceHeight - 1);
                var vector = wind[sampleX, sampleY];
                DrawWindArrow(image, x, y, vector, windColor, refLength);
            }
        }

        // 右上角标尺箭头
        var scaleLen = Mathf.Max(refLength, 32f);
        var padX = 36;
        var padY = 24;
        var refStartX = targetWidth - padX - (int)scaleLen - 60;
        var refEndX = refStartX + (int)scaleLen;
        if (refStartX > 0 && refEndX < targetWidth)
        {
            DrawWindLineThick(image, refStartX, padY, refEndX, padY, windColor, 1);
            var barbLeft = new Vector2(refEndX, padY) - (Vector2.Right.Rotated(0.50f) * scaleLen * 0.28f);
            var barbRight = new Vector2(refEndX, padY) - (Vector2.Right.Rotated(-0.50f) * scaleLen * 0.28f);
            DrawWindLineThick(image, refEndX, padY, barbLeft.X, barbLeft.Y, windColor, 1);
            DrawWindLineThick(image, refEndX, padY, barbRight.X, barbRight.Y, windColor, 1);
        }
    }

    private void DrawWindArrow(Image image, int x, int y, Vector2 vector, Color color, float refLength)
    {
        var magnitude = vector.Length();
        if (magnitude < 0.35f)
        {
            DrawWindPoint(image, x, y, 1, color);
            return;
        }

        var direction = vector / magnitude;
        var ratio = Mathf.Clamp(magnitude / 10.0f, 0.10f, 1.45f);
        var shaftLength = refLength * ratio;
        var endX = x + direction.X * shaftLength;
        var endY = y + direction.Y * shaftLength;

        DrawWindLineThick(image, x, y, endX, endY, color, 1);

        var back = -direction;
        const float headAngle = 0.50f;
        var headLength = Mathf.Clamp(shaftLength * 0.28f, 3.2f, 7.0f);
        var leftHead = RotateVector(back, headAngle) * headLength;
        var rightHead = RotateVector(back, -headAngle) * headLength;

        DrawWindLineThick(image, endX, endY, endX + leftHead.X, endY + leftHead.Y, color, 1);
        DrawWindLineThick(image, endX, endY, endX + rightHead.X, endY + rightHead.Y, color, 1);
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
                oceanColor = Blend(oceanColor, TopographicShelf, shelfHighlight * 0.22f);
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
            landColor = Blend(landColor, TopographicCoast, coastAlpha * 0.28f);
        }

        var contourPhase = Mathf.Abs(Mathf.PosMod(landT * 24f, 1f) - 0.5f) * 2f;
        var contourAlpha = Mathf.Clamp((0.18f - contourPhase) / 0.18f, 0f, 1f);
        if (contourAlpha > 0f)
        {
            landColor = Blend(landColor, TopographicContour, contourAlpha * 0.10f);
        }

        var snowPatchAlpha = Mathf.Clamp((landT - 0.88f) / 0.12f, 0f, 1f);
        if (snowPatchAlpha > 0f)
        {
            landColor = Blend(landColor, ElevationLandSnow, snowPatchAlpha * 0.30f);
        }

        return landColor;
    }

	private Color DrawSatellite(int y, int height, float elevation, float temperature, float avgMoisture, BiomeType biome, float river, float seaLevel)
		=> GetSatelliteColor(ComputePolarMask(y, height), elevation, temperature, avgMoisture, biome, river, seaLevel);

	/// <summary>
	/// 按地形总览取色。逐像素路径传"该行的极区掩膜"，地块路径传"该地块中心的极区掩膜"，
	/// 两者共用这一份实现，配色不可能分叉。
	/// </summary>
	public Color GetSatelliteColor(float polarMask, float elevation, float temperature, float avgMoisture, BiomeType biome, float river, float seaLevel)
	{
		Color color;
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
                    color = Blend(color, SatelliteSeaIce, Mathf.Clamp(seaIceAlpha, 0f, 0.78f));
                }
            }
		}
		else
		{
			var landT = Mathf.Clamp((elevation - safeSea) / Mathf.Max(1f - safeSea, 0.0001f), 0f, 1f);
			var elevationBase = DrawElevationRealistic(elevation, safeSea);

			var moisture = Mathf.Clamp(avgMoisture, 0f, 1f);
			var vegetationStrength = Mathf.Clamp((1f - Mathf.Pow(landT, 1.22f)) * (0.35f + 0.65f * moisture), 0.14f, 0.78f);
			var vegetationHue = Blend(SatelliteDryHue, SatelliteWetHue, moisture);

			color = Blend(elevationBase, vegetationHue, vegetationStrength);

			if (elevation <= safeSea + 0.01f)
			{
				color = Blend(color, SatelliteCoastSand, 0.2f);
			}

			var rockyAlpha = Mathf.Clamp((landT - 0.56f) / 0.30f, 0f, 1f);
			if (rockyAlpha > 0f)
			{
				var rockTint = Blend(SatelliteRockLow, SatelliteRockHigh, Mathf.Clamp((landT - 0.72f) / 0.22f, 0f, 1f));
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
                color = Blend(color, SatelliteSnow, snowAlpha);
            }

            if (polarMask > 0f)
            {
                var polarSnowAlpha = polarMask * Mathf.Clamp((0.30f - temperature) / 0.30f, 0f, 1f);
                if (polarSnowAlpha > 0f)
                {
                    color = Blend(color, SatellitePolarSnow, Mathf.Clamp(polarSnowAlpha, 0f, 0.82f));
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

    /// <summary>
    /// 按行号计算极区掩膜（0 = 赤道，1 = 极点附近）。
    /// 公开出来是为了让地块渲染能按"地块中心所在行"取同一个掩膜。
    /// </summary>
    public static float ComputePolarMask(int y, int height)
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

        var rockIndex = (int)rock;
        return rockIndex >= 0 && rockIndex < RockColors.Length
            ? RockColors[rockIndex]
            : Colors.Black;
    }

    private Color DrawOre(OreType ore, float elevation, float seaLevel)
    {
        if (elevation < seaLevel)
        {
            return DeepOcean;
        }

        var oreIndex = (int)ore;
        return oreIndex >= 0 && oreIndex < OreColors.Length
            ? OreColors[oreIndex]
            : Colors.Black;
    }

    private Color DrawBiomeColor(BiomeType biome)
    {
        var biomeIndex = (int)biome;
        return biomeIndex >= 0 && biomeIndex < BiomeColors.Length
            ? BiomeColors[biomeIndex]
            : Colors.Black;
    }

    private Color DrawCitiesOverlay(bool hasCity, int x, int y, int width, int height, float elevation, float temperature, float avgMoisture, BiomeType biome, float river, float seaLevel)
    {
        if (hasCity)
        {
            return CityMarker;
        }

        return Blend(DrawSatellite(y, height, elevation, temperature, avgMoisture, biome, river, seaLevel), Colors.Black, 0.45f);
    }

    private float[,] AverageLandArray(float[,] input, float[,] elevation, int width, int height, float seaLevel, int radius)
    {
        // Same land-only windowed average as before (including the +1 count
        // baseline), computed with two box-filter passes: O(width*height*r)
        // instead of O(width*height*r*r).
        var horizontalSum = new float[width, height];
        var horizontalCount = new int[width, height];

        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                var count = 0;

                for (var ox = -radius; ox <= radius; ox++)
                {
                    var nx = x + ox;
                    if (nx < 0)
                    {
                        nx += width;
                    }
                    else if (nx >= width)
                    {
                        nx -= width;
                    }

                    if (elevation[nx, y] > seaLevel)
                    {
                        sum += input[nx, y];
                        count++;
                    }
                }

                horizontalSum[x, y] = sum;
                horizontalCount[x, y] = count;
            }
        });

        var result = new float[width, height];

        Parallel.For(0, height, y =>
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0f;
                var count = 1; // Preserve the original denominator baseline.

                for (var oy = -radius; oy <= radius; oy++)
                {
                    var ny = y + oy;
                    if (ny < 0)
                    {
                        ny = 0;
                    }
                    else if (ny >= height)
                    {
                        ny = height - 1;
                    }

                    sum += horizontalSum[x, ny];
                    count += horizontalCount[x, ny];
                }

                result[x, y] = sum / Mathf.Max(count, 1);
            }
        });

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

    // ── 多边形渲染用的取色接口 ──────────────────────────────────────
    //
    // 多边形渲染是"逐地块一次"取色，而不是逐像素，所以需要把内部配色函数开放出来。
    // 刻意只开放取色，不开放整套 DrawXxx：这样栅格渲染与多边形渲染永远共用同一套配色，
    // 两边的 A/B 对照才有意义（差异只可能来自几何，不可能来自调色）。

    /// <summary>按生物群系取色，与 <see cref="MapLayer.Biomes"/> 图层完全一致。</summary>
    public Color GetBiomeColor(BiomeType biome) => DrawBiomeColor(biome);

    /// <summary>按高程取色，与 <see cref="MapLayer.Elevation"/> 图层完全一致。</summary>
    public Color GetElevationColor(float elevation, float seaLevel, ElevationStyle style)
        => DrawElevation(elevation, seaLevel, style);

    /// <summary>
    /// 按政体编号取色，与 <see cref="MapLayer.Civilization"/> 图层完全一致。
    /// </summary>
    public Color GetPolityColor(int polityId) => ColorForPolity(polityId);

    /// <summary>
    /// 文明图层的取色（逐地块调用）。
    /// 与逐像素路径共用同一份实现——<see cref="DrawCivilizationColor"/> 就是它，
    /// 这里只是把可见性放开，保证栅格与多边形两条路径的配色不可能分叉。
    /// </summary>
    public Color GetCivilizationColor(float influence, int polityId, bool isBorder, float elevation, float seaLevel)
        => DrawCivilizationColor(influence, polityId, isBorder, elevation, seaLevel);

    /// <summary>贸易图层的取色（逐地块调用）；与逐像素路径共用实现，理由同上。</summary>
    public Color GetTradeRouteColor(bool hasRoute, float flow, float influence, float elevation, float seaLevel)
        => DrawTradeRouteColor(hasRoute, flow, influence, elevation, seaLevel);

    /// <summary>按岩石类型取色，与 <see cref="MapLayer.RockTypes"/> 图层完全一致。</summary>
    public Color GetRockColor(RockType rock, float elevation, float seaLevel)
        => DrawRock(rock, elevation, seaLevel);

    /// <summary>按矿产类型取色，与 <see cref="MapLayer.Ores"/> 图层完全一致。</summary>
    public Color GetOreColor(OreType ore, float elevation, float seaLevel)
        => DrawOre(ore, elevation, seaLevel);

    /// <summary>按温度取色，与 <see cref="MapLayer.Temperature"/> 图层完全一致。</summary>
    public Color GetTemperatureColor(float value) => DrawTemperature(value);

    /// <summary>按湿度取色，与 <see cref="MapLayer.Moisture"/> 图层完全一致。</summary>
    public Color GetMoistureColor(float value) => DrawMoisture(value);

    /// <summary>按河流取色，与 <see cref="MapLayer.Rivers"/> 图层完全一致。</summary>
    public Color GetRiverColor(float elevation, float seaLevel, float river)
        => DrawRivers(elevation, seaLevel, river);
}
