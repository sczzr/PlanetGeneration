using Godot;

namespace PlanetGeneration.WorldGen;

public sealed class ErosionSimulator
{
    public void Run(
        int width,
        int height,
        int iterations,
        float[,] elevation,
        float[,] waterLayer,
        float[,] riverLayer)
    {
        for (var i = 0; i < iterations; i++)
        {
            Step(width, height, elevation, waterLayer, isRiverMode: false);
        }

        for (var i = 0; i < Mathf.Max(1, iterations / 2); i++)
        {
            Step(width, height, elevation, riverLayer, isRiverMode: true);
        }
    }

    private void Step(int width, int height, float[,] elevation, float[,] fluidLayer, bool isRiverMode)
    {
        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                if (fluidLayer[x, y] <= 0f)
                {
                    continue;
                }

                var minEl = float.PositiveInfinity;
                var minX = x;
                var minY = y;

                for (var oy = -1; oy <= 1; oy++)
                {
                    for (var ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0)
                        {
                            continue;
                        }

                        var nx = x + ox;
                        if (nx < 0)
                        {
                            nx = width - 1;
                        }
                        else if (nx >= width)
                        {
                            nx = 0;
                        }

                        var ny = y + oy;
                        ny = Mathf.Clamp(ny, 0, height - 1);

                        var candidate = elevation[nx, ny] + fluidLayer[nx, ny];
                        if (candidate < minEl)
                        {
                            minEl = candidate;
                            minX = nx;
                            minY = ny;
                        }
                    }
                }

                var diff = 0.5f * (elevation[x, y] + fluidLayer[x, y] - minEl);
                if (diff <= 0f)
                {
                    continue;
                }

                var erosion = diff;
                if (!isRiverMode)
                {
                    elevation[x, y] = Mathf.Max(0f, elevation[x, y] - erosion);
                }

                diff = 0.5f * (elevation[x, y] + fluidLayer[x, y] - (elevation[minX, minY] + fluidLayer[minX, minY]));
                fluidLayer[x, y] = Mathf.Max(0f, fluidLayer[x, y] - diff);
                fluidLayer[minX, minY] += Mathf.Max(0f, diff);
            }
        }
    }
}
