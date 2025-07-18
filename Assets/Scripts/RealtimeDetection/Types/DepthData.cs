using UnityEngine;
using UnityEngine.Profiling;

public class DepthData
{
	public readonly int width;
	public readonly int height;
	public ushort[] pixels;
	private byte[] asA8;
	private Texture2D asTexture;
	public ushort this[int i, int j] => pixels[i * width + j];

	public DepthData(int width, int height)
	{
		this.width = width;
		this.height = height;
		this.pixels = new ushort[width * height];
		this.asA8 = new byte[width * height];
		this.asTexture = new Texture2D(width, height, TextureFormat.Alpha8, false);
	}

	public void UpdateFromDevice(ushort[] rawData) => this.pixels = rawData;

	public Texture2D AsTexture(bool depth)
	{
		Profiler.BeginSample("PGM_AsTexture");
		//float maxVal = 0;
		//for (int i = 0; i < width * height; i++)
		//	if (pixels[i] > maxVal)
		//		maxVal = pixels[i];

		if (depth)
		{
			for (int i = 0; i < width * height; i++)
			{
				float val01 = pixels[i] >= 4090 ? 0f : pixels[i] / 1024;
				val01 = val01 > 1 ? 1 : val01;
				asA8[i] = (byte)(255 * val01);
			}
		}
		else
		{
			for (int i = 0; i < width * height; i++)
			{
				if (pixels[i] > Settings.max_ir)
					asA8[i] = 128;
				if (pixels[i] < Settings.min_ir)
					asA8[i] = 0;
				else
					asA8[i] = 255;
			}
		}
		

		asTexture.LoadRawTextureData(asA8);
		asTexture.Apply();
		Profiler.EndSample();

		return asTexture;
	}
}
