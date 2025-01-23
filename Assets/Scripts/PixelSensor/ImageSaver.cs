using MagicLeap.OpenXR.Features.PixelSensors;
using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class ImageSaver
{
	private static FileStream currentFile;
	private static ZipArchive currentZip;
	private static int currentFrame = -1;

    public static void InitNewFrame(int frameCount, float frameTime, DateTimeOffset deviceTime)
    {
		if (currentZip != null && currentFile != null && frameCount != currentFrame)
			CloseLastFile();
			
		try
		{
			var dataPath = Path.Combine(Application.persistentDataPath, "img", $"{Time.frameCount}_{frameTime}_{deviceTime.UtcTicks}.data");
			currentFile = new FileStream(dataPath, FileMode.Create);
			currentZip = new ZipArchive(currentFile, ZipArchiveMode.Create, leaveOpen: true);
			currentFrame = frameCount;
			Console.WriteLine($"{dataPath} created");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Failed to create frame {frameCount} {frameTime} {deviceTime.UtcTicks}: {ex.Message}");
		}
	}
	public static void CloseLastFile()
	{
		currentZip?.Dispose();
		currentZip = null;
		currentFile?.Close();
		currentFile?.Dispose();
		currentFile = null;
	}

	public static void SaveSensor(Texture2D texture, string prefix, float time, DateTimeOffset offset, Pose sensorPose)
	{
		if (currentFile == null || currentZip == null)
		{
			Debug.LogError($"Can't save {prefix} sensor with no file open");
			return;
		}

		try
		{
            byte[] data = texture.GetRawTextureData();

			ZipArchiveEntry entry = currentZip.CreateEntry($"{prefix}.bytes", System.IO.Compression.CompressionLevel.NoCompression);
			using (Stream es = entry.Open())
				es.Write(data);

			entry = currentZip.CreateEntry($"{prefix}_pose.txt", System.IO.Compression.CompressionLevel.NoCompression);
			using (Stream es = entry.Open())
			using (StreamWriter sw = new StreamWriter(es))
				sw.WriteLine($"{prefix}: {sensorPose.position} | {sensorPose.rotation} | {texture.width} | {texture.height} | {texture.format}");

			Console.WriteLine($" -{prefix} appended");
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"Error writing {prefix} sensor: {ex}");
		}
	}


 //   public static void SaveImage(byte[] fileData, string filename)
 //   {
 //       try
 //       {
 //           // Application.persistentDataPath path is --> /storage/emulated/0/Android/data/com.magicleap.unity.examples/files/
 //           var strDataPath = Path.Combine(Application.persistentDataPath, "img", filename);
 //           // ImageConversion.EncodeArrayToPNG(data, GraphicsFormat.R8G8B8A8_UInt8, w, h);
 //           File.WriteAllBytes(strDataPath, fileData);
 //       }
 //       catch (Exception ex)
 //       {
 //           Debug.LogWarning("File writing error ::" + ex);
 //       }
 //   }
 //   public static void SaveTestFile()
 //   {
 //       try
 //       {
 //           // Application.persistentDataPath path is --> /storage/emulated/0/Android/data/com.magicleap.unity.examples/files/
 //           var strDataPath = Path.Combine(Application.persistentDataPath, "SaveFile.txt");
 //           File.WriteAllText(strDataPath, "Hello world");
 //       }
 //       catch (Exception ex)
 //       {
 //           Debug.LogWarning("File writing error ::" + ex);
 //       }
 //   }
}