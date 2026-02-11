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
    Cities
}

public sealed class WorldRenderer
{
    private static readonly Color DeepOcean = Hex("#1a2482");
    private static readonly Color ShallowOcean = Hex("#0059b3");
    private static readonly Color CoastBrown = new(0.55f, 0.27f, 0.07f);
    private static readonly Color RiverBlue = Hex("#0000ff");
    private static readonly Color DarkOcean = Hex("#0a2044");

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
        float seaLevel)
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
                    MapLayer.Temperature => DrawTemperature(x, y, width, height, elevation, temperature[x, y], seaLevel),
                    MapLayer.Rivers => DrawRivers(elevation[x, y], seaLevel, river[x, y]),
                    MapLayer.Moisture => DrawMoisture(moisture[x, y]),
                    MapLayer.Wind => Colors.Black,
                    MapLayer.Elevation => DrawElevation(elevation[x, y], seaLevel),
                    MapLayer.RockTypes => DrawRock(rock[x, y], elevation[x, y], seaLevel),
                    MapLayer.Ores => DrawOre(ore[x, y], elevation[x, y], seaLevel),
                    MapLayer.Biomes => DrawBiomeColor(biome[x, y]),
                    MapLayer.Cities => DrawCitiesOverlay(
                        cityMask[x, y],
                        x,
                        y,
                        elevation[x, y],
                        temperature[x, y],
                        avgMoisture![x, y],
                        biome[x, y],
                        river[x, y],
                        seaLevel),
                    _ => DrawSatellite(
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

    private Color DrawTemperature(int x, int y, int width, int height, float[,] elevation, float value, float seaLevel)
    {
        var color = value switch
        {
            < 0.05f => Hex("#0066ff"),
            < 0.18f => Hex("#00e6b8"),
            < 0.3f => Hex("#66ff99"),
            < 0.47f => Hex("#ffff99"),
            < 0.7f => Hex("#ff6600"),
            _ => Hex("#cc0000")
        };

        if (elevation[x, y] >= seaLevel && IsCoastline(x, y, width, height, elevation, seaLevel))
        {
            color = CoastBrown;
        }

        return color;
    }

    private bool IsCoastline(int x, int y, int width, int height, float[,] elevation, float seaLevel)
    {
        for (var oy = -1; oy <= 1; oy++)
        {
            for (var ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0)
                {
                    continue;
                }

                var nx = x + ox;
                var ny = y + oy;

                if (nx < 0)
                {
                    nx = width - 1;
                }
                else if (nx >= width)
                {
                    nx = 0;
                }

                ny = Mathf.Clamp(ny, 0, height - 1);

                if (elevation[nx, ny] < seaLevel)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Color DrawRivers(float elevation, float seaLevel, float river)
    {
        if (river > 0f)
        {
            return RiverBlue;
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
        return ColorLuminance("#66ff99", value);
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
                var terrain = DrawSatellite(elevation[x, y], temperature[x, y], avgMoisture[x, y], biome[x, y], river[x, y], seaLevel);
                terrain = Blend(terrain, Colors.Black, 0.30f);
                image.SetPixel(x, y, SanitizeColor(terrain));
            }
        }

        for (var y = 0; y < height; y += 16)
        {
            for (var x = 0; x < width; x += 16)
            {
                var vector = wind[x, y];
                var magnitude = vector.Length();

                DrawWindPoint(image, x, y, 2, Colors.White);

                var endX = x + vector.X;
                var endY = y + vector.Y;

                if (magnitude > 20f)
                {
                    endX = x + 15f * vector.X / Mathf.Max(magnitude, 0.0001f);
                    endY = y + 15f * vector.Y / Mathf.Max(magnitude, 0.0001f);
                }

                DrawWindLine(image, x, y, endX, endY, Colors.White);
            }
        }

        return image;
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

    private Color DrawElevation(float value, float seaLevel)
    {
        if (value < 0.5714f * seaLevel)
        {
            return DeepOcean;
        }

        if (value < 0.8f * seaLevel)
        {
            return ShallowOcean;
        }

        if (value < seaLevel)
        {
            return ShallowOcean;
        }

        return ColorLuminance("#ffffff", value);
    }

    private Color DrawSatellite(float elevation, float temperature, float avgMoisture, BiomeType biome, float river, float seaLevel)
    {
        Color color;

        if (elevation < seaLevel)
        {
            color = DarkOcean;
            if (elevation >= 0.5714f * seaLevel)
            {
                var alpha = Mathf.Clamp(elevation / Mathf.Max(seaLevel, 0.0001f), 0f, 1f);
                color = Blend(color, Hex("#0d3f82"), alpha);
            }
        }
        else
        {
            var baseLand = Hex("#d8c28b");

            var moistureOpacity = avgMoisture / (avgMoisture * 1.1f + 1f);
            moistureOpacity = Mathf.Clamp(moistureOpacity, 0f, 1f);

            var colorFactor = moistureOpacity * 2f * avgMoisture * avgMoisture * 0.07f;
            colorFactor = Mathf.Clamp(colorFactor, 0f, 1f);

            var moistureTint = new Color(
                ((1f - colorFactor) * 8f) / 255f,
                (colorFactor * 48f + (1f - colorFactor) * 63f) / 255f,
                (colorFactor * 12f + (1f - colorFactor) * 3f) / 255f
            );

            color = Blend(baseLand, moistureTint, moistureOpacity);

            if (elevation <= seaLevel + 0.01f)
            {
                color = Blend(color, new Color(222f / 255f, 232f / 255f, 187f / 255f), 0.2f);
            }

            if (temperature < 0.2f)
            {
                var t = Mathf.Max(temperature, 0f);
                var snowAlpha = Mathf.Clamp(1f - 25f * t * t, 0f, 1f);
                color = Blend(color, new Color(232f / 255f, 246f / 255f, 255f / 255f), snowAlpha);
            }
        }

        if (biome == BiomeType.Ice && elevation >= seaLevel)
        {
            color = Hex("#ddf2ff");
        }

        if (river > 0f)
        {
            color = DarkOcean;
        }

        return color;
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
            BiomeType.Ocean => Hex("#1a2482"),
            BiomeType.ShallowOcean => Hex("#0059b3"),
            BiomeType.Coastland => Hex("#ffffcc"),
            BiomeType.TropicalRainForest => Hex("#004800"),
            BiomeType.TropicalSeasonalForest => Hex("#0c8d0c"),
            BiomeType.Shrubland => Hex("#607818"),
            BiomeType.Savanna => Hex("#f4f48b"),
            BiomeType.TropicalDesert => Hex("#a86048"),
            BiomeType.TemperateRainForest => Hex("#64b464"),
            BiomeType.TemperateSeasonalForest => Hex("#628f56"),
            BiomeType.Chaparral => Hex("#8f849a"),
            BiomeType.Grassland => Hex("#90d848"),
            BiomeType.Steppe => Hex("#bfbfbf"),
            BiomeType.TemperateDesert => Hex("#d8a878"),
            BiomeType.BorealForest => Hex("#006048"),
            BiomeType.Taiga => Hex("#489090"),
            BiomeType.Tundra => Hex("#8cccbd"),
            BiomeType.Ice => Hex("#b3ecff"),
            BiomeType.RockyMountain => Hex("#ad421f"),
            BiomeType.SnowyMountain => Hex("#e6f3ff"),
            BiomeType.River => Hex("#0059b3"),
            _ => Colors.Black
        };
    }

    private Color DrawCitiesOverlay(bool hasCity, int x, int y, float elevation, float temperature, float avgMoisture, BiomeType biome, float river, float seaLevel)
    {
        if (hasCity)
        {
            return Hex("#ff3bbf");
        }

        return Blend(DrawSatellite(elevation, temperature, avgMoisture, biome, river, seaLevel), Colors.Black, 0.45f);
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
