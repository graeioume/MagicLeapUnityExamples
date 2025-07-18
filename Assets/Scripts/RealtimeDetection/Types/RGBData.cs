using UnityEngine;
using UnityEngine.Profiling;

public class RGBData
{
	public readonly int width;
	public readonly int height;
	public byte[] components;
	public int[] pixels;
	public Texture2D asTexture;

	public byte this[int i, int j, int c] => components[i * width + j * 4 + c];
	public int this[int i, int j] => pixels[i * width + j];

	public RGBData(int width, int height)
	{
		this.width = width;
		this.height = height;
		this.components = new byte[width * height * 4];
		this.pixels = new int[width * height];
		this.asTexture = new Texture2D(width, height, TextureFormat.BGRA32, false);
	}

	public Texture2D AsTexture()
	{
		Profiler.BeginSample("RGB_AsTexture");
		this.asTexture.LoadRawTextureData(components);
		this.asTexture.Apply();
		Profiler.EndSample();

		return this.asTexture;
	}
}
