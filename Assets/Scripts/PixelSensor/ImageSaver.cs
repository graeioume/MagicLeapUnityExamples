using System;
using System.IO;
using UnityEngine;

public static class ImageSaver
{
	private static int currentFrame = -1;
	private static string currentFolderPath;

	public static void InitNewFrame(int frameCount, float frameTime, DateTimeOffset deviceTime)
	{
		try
		{
			// Create a folder named after the current frame count
			currentFrame = frameCount;
			currentFolderPath = Path.Combine(Application.persistentDataPath, "img", frameCount.ToString());
			if (!Directory.Exists(currentFolderPath))
				Directory.CreateDirectory(currentFolderPath);

			// Write the other two values (frameTime and deviceTime) into a txt file inside the folder
			string metadataPath = Path.Combine(currentFolderPath, "metadata.txt");
			if (!File.Exists(metadataPath))
				File.WriteAllText(metadataPath, $"FrameTime: {frameTime}\nDeviceTime (UTC Ticks): {deviceTime.UtcTicks}");

			string logsPath = Path.Combine(currentFolderPath, "logs.txt");
			if (!File.Exists(logsPath))
				File.WriteAllText(logsPath, LogsSaver.Logs.ToString());

			Debug.Log($"{currentFolderPath} created");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Failed to create folder for frame {frameCount}: {ex.Message}");
		}
	}

	public static void SaveSensor(Texture2D texture, string prefix, float time, DateTimeOffset offset, Pose sensorPose)
	{
		if (string.IsNullOrEmpty(currentFolderPath))
		{
			Debug.LogError($"Can't save {prefix} sensor: no folder open.");
			return;
		}

		try
		{
			// Save the raw texture data
			byte[] data = texture.GetRawTextureData();
			string bytesPath = Path.Combine(currentFolderPath, $"{prefix}.bytes");
			File.WriteAllBytes(bytesPath, data);

			// Save pose and texture metadata
			string posePath = Path.Combine(currentFolderPath, $"{prefix}_pose.txt");
			File.WriteAllText(posePath, $"{prefix}: {sensorPose.position} | {sensorPose.rotation} | {texture.width} | {texture.height} | {texture.format}");
			Debug.Log($" -{prefix} saved in {currentFolderPath}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"Error writing {prefix} sensor: {ex}");
		}
	}

	public static void ClearImgFolder()
	{
		try
		{
			// Build the full path to the img folder
			string imgFolderPath = Path.Combine(Application.persistentDataPath, "img");

			// Check if the folder exists
			if (Directory.Exists(imgFolderPath))
			{
				// Delete it recursively
				Directory.Delete(imgFolderPath, recursive: true);
				Debug.Log($"Deleted folder and all contents at: {imgFolderPath}");
			}
			else
			{
				Debug.Log($"Folder does not exist at: {imgFolderPath}");
			}

			Directory.CreateDirectory(imgFolderPath);
			Debug.Log("Creating img folder");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Error while deleting folder: {ex.Message}");
		}
	}
}
