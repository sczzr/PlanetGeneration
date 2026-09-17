using Godot;
using PlanetGeneration.WorldGen;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using IOPath = System.IO.Path;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOFileInfo = System.IO.FileInfo;
using CryptoSha256 = System.Security.Cryptography.SHA256;

namespace PlanetGeneration;

public partial class Main : Control
{
	/// <summary>Global position of map texel (0,0), global pixels per texel, and the source texture size.</summary>
	private readonly record struct MapPaintedTransform(Vector2 Origin, Vector2 Scale, Vector2 TexSize);

	private static readonly Vector2I MinimapTextureSize = new(512, 256);

	private void UpdateMinimapTexture(Texture sourceTexture, Image sourceImage)
	{
		if (_minimapPanel == null || _minimapTexture == null)
		{
			return;
		}

		if (sourceTexture == null || sourceImage == null)
		{
			_minimapSourceTexture = null;
			_minimapTexture.Texture = null;
			_minimapPanel.Visible = false;
			return;
		}

		if (!ReferenceEquals(_minimapSourceTexture, sourceTexture))
		{
			_minimapSourceTexture = sourceTexture;
			var downscaled = (Image)sourceImage.Duplicate();
			downscaled.Resize(MinimapTextureSize.X, MinimapTextureSize.Y, Image.Interpolation.Bilinear);
			_minimapTexture.Texture = ImageTexture.CreateFromImage(downscaled);
		}

		_minimapPanel.Visible = true;
	}

	/// <summary>Mirrors the map's zoom/pan onto the minimap viewport rectangle. Runs every frame with a cheap change check.</summary>
	private void UpdateMinimapViewportRect()
	{
		var miniTexture = _minimapTexture?.Texture;
		var mapTexture = _mapTexture?.Texture;
		if (_minimapViewRect == null || miniTexture == null || mapTexture == null || _mapCenter == null)
		{
			return;
		}

		if (!ComputeMapPaintedTransform(out var origin, out var scale, out var texSize))
		{
			return;
		}

		var viewportRect = _mapCenter.GetGlobalRect();
		var uvMin = Clamp01(((viewportRect.Position - origin) / scale) / texSize);
		var uvMax = Clamp01(((viewportRect.End - origin) / scale) / texSize);
		if (uvMax.X < uvMin.X) uvMin.X = uvMax.X;
		if (uvMax.Y < uvMin.Y) uvMin.Y = uvMax.Y;

		var minimapRect = _minimapTexture;
		if (minimapRect == null)
		{
			return;
		}

		var display = FitRectInside(miniTexture.GetSize(), minimapRect.Size);
		var rect = new Rect2(
			display.Position + new Vector2(uvMin.X * display.Size.X, uvMin.Y * display.Size.Y),
			new Vector2((uvMax.X - uvMin.X) * display.Size.X, (uvMax.Y - uvMin.Y) * display.Size.Y));

		if (IsCloseToLastMinimapRect(rect))
		{
			return;
		}

		_lastMinimapViewRect = rect;
		_minimapViewRect.OffsetLeft = rect.Position.X;
		_minimapViewRect.OffsetTop = rect.Position.Y;
		_minimapViewRect.OffsetRight = rect.End.X;
		_minimapViewRect.OffsetBottom = rect.End.Y;
	}

	private void OnMinimapGuiInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton click when click.ButtonIndex == MouseButton.Left:
				_minimapDragging = click.Pressed;
				if (click.Pressed)
				{
					PanMapToMinimapPoint(click.Position);
				}
				break;
			case InputEventMouseMotion motion when _minimapDragging:
				PanMapToMinimapPoint(motion.Position);
				break;
		}
	}

	/// <summary>Drags the main view so the clicked minimap texel sits at the center of the map viewport.</summary>
	private void PanMapToMinimapPoint(Vector2 minimapLocal)
	{
		var miniTexture = _minimapTexture?.Texture;
		var mapTexture = _mapTexture?.Texture;
		if (miniTexture == null || mapTexture == null || _mapCenter == null)
		{
			return;
		}

		var minimapRect = _minimapTexture;
		if (minimapRect == null)
		{
			return;
		}

		var display = FitRectInside(miniTexture.GetSize(), minimapRect.Size);
		if (display.Size.X <= 0f || display.Size.Y <= 0f)
		{
			return;
		}

		var uv = Clamp01((minimapLocal - display.Position) / display.Size);
		if (!ComputeMapPaintedTransform(out var origin, out var scale, out var texSize))
		{
			return;
		}

		var currentGlobal = origin + uv * texSize * scale;
		var targetGlobal = _mapCenter.GetGlobalRect().GetCenter();
		// MapAspect is the zoomed ancestor: a global shift g requires a local Position shift of g / zoom.
		_mapAspect.Position += (targetGlobal - currentGlobal) / Mathf.Max(_mapZoom, 0.0001f);
	}

	/// <summary>MapTexture letterboxes its texture (keep-aspect-centered); compute where texel (0,0) lands globally and the texel scale.</summary>
	private bool ComputeMapPaintedTransform(out Vector2 origin, out Vector2 scale, out Vector2 texSize)
	{
		origin = Vector2.Zero;
		scale = Vector2.One;
		texSize = Vector2.Zero;

		var texture = _mapTexture?.Texture;
		if (texture == null || _mapTexture == null)
		{
			return false;
		}

		texSize = texture.GetSize();
		if (texSize.X < 1f || texSize.Y < 1f)
		{
			return false;
		}

		var controlXf = _mapTexture.GetGlobalTransform();
		var scaleX = controlXf.X.Length();
		var scaleY = controlXf.Y.Length();
		if (scaleX < 0.0001f || scaleY < 0.0001f)
		{
			return false;
		}

		var painted = FitRectInside(texSize, _mapTexture.Size);
		if (painted.Size.X <= 0f || painted.Size.Y <= 0f)
		{
			return false;
		}

		origin = controlXf * painted.Position;
		scale = new Vector2(
			scaleX * painted.Size.X / texSize.X,
			scaleY * painted.Size.Y / texSize.Y);
		return true;
	}

	private bool IsCloseToLastMinimapRect(Rect2 rect)
	{
		return Mathf.Abs(rect.Position.X - _lastMinimapViewRect.Position.X) < 0.25f
			&& Mathf.Abs(rect.Position.Y - _lastMinimapViewRect.Position.Y) < 0.25f
			&& Mathf.Abs(rect.Size.X - _lastMinimapViewRect.Size.X) < 0.25f
			&& Mathf.Abs(rect.Size.Y - _lastMinimapViewRect.Size.Y) < 0.25f;
	}

	private static Vector2 Clamp01(Vector2 value)
	{
		return new Vector2(Mathf.Clamp(value.X, 0f, 1f), Mathf.Clamp(value.Y, 0f, 1f));
	}

	private static Rect2 FitRectInside(Vector2 contentSize, Vector2 containerSize)
	{
		if (contentSize.X <= 0f || contentSize.Y <= 0f || containerSize.X <= 0f || containerSize.Y <= 0f)
		{
			return new Rect2();
		}

		var fit = Mathf.Min(containerSize.X / contentSize.X, containerSize.Y / contentSize.Y);
		var size = contentSize * fit;
		return new Rect2((containerSize - size) * 0.5f, size);
	}
}
