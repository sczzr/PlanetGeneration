namespace PlanetGeneration.WorldGen;

public static class Array2D
{
	public static T[,] Create<T>(int width, int height, T defaultValue)
	{
		var result = new T[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				result[x, y] = defaultValue;
			}
		}

		return result;
	}

	public static T[,] Clone<T>(T[,] source)
	{
		var width = source.GetLength(0);
		var height = source.GetLength(1);
		var result = new T[width, height];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				result[x, y] = source[x, y];
			}
		}

		return result;
	}
}
