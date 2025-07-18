//#define USE_MOCK
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System.IO;
using UnityEngine.Profiling;
using System;


public partial class RealtimeDetector
{
#if USE_MOCK
	public static bool UseNewDataFormat = true;

	public RawImage DepthFeed;
	public RawImage IRFeed;
	public RawImage RGBFeed;
	public Transform Hand;
	private List<long> depthTimestamps;
	private List<long> rgbTimestamps;
	private string[] footageFiles;
	private float[] dllPointCloud = new float[32768 * 3];
	private int dllPointCloudCount;
	private Dictionary<long, long> depth2rgb;
	private int currentFrame = 0;
	private float timer = 0f;
	private readonly Matrix4x4 InvertZMatrix = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));


	private string SA => Application.streamingAssetsPath;
	
	public void Awake()
	{
		// Only needed on non hololens
		if (UseNewDataFormat == false)
		{
			depthTimestamps = JsonConvert.DeserializeObject<List<long>>(File.ReadAllText(SA + "/depth_timestamps.json"));
			rgbTimestamps = JsonConvert.DeserializeObject<List<long>>(File.ReadAllText(SA + "/rgb_timestamps.json"));
			depth2rgb = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(SA + "/depth2rgb.json"))
				.ToDictionary(x => long.Parse(x.Key), x => long.Parse(x.Value));
		}
		else
		{
			string[] files = Directory.GetFiles($"{SA}/Device/", "*.bytes");
			files = files.OrderBy(x =>
			{
				string[] tokens = x.Split('_');
				string ending = tokens[tokens.Length - 1];
				int n = int.Parse(ending.Split('.')[0]);
				return n;
			}).ToArray();

			footageFiles = files;
			currentFrame = 0;
		}
		
		Hand.gameObject.SetActive(true);
		MicrosphereScale = Vector3.one * 0.008f;
	}

	private void Update()
	{
		Profiler.BeginSample("Mock.Update");
		Matrix4x4 cam2world = UpdateSensors();
		if (UseNewDataFormat)
			cam2world = InvertZMatrix * cam2world;
		ProcessFrame(depthData.pixels, irData.pixels, cam2world);
		UpdateFeed();
		//UpdatePointCloud(cam2world);
		//RenderDLLPointCloud(dllPointCloud, dllPointCloudCount);
		UpdateCamera(cam2world);
		Profiler.EndSample();
	}


	private Matrix4x4 UpdateSensors()
	{
		Profiler.BeginSample("Mock.UpdateSensors");
		Matrix4x4 cam2world = Matrix4x4.identity;
		if (UseNewDataFormat == false)
		{
			long timestamp = depthTimestamps[currentFrame];
			long rgbTimestamp = depth2rgb[timestamp];
			DataIO.LoadPGM($"{SA}/Depth AHat/{timestamp}.pgm", depthData);
			DataIO.LoadPGM($"{SA}/Depth AHat/{timestamp}_ab.pgm", irData);
			DataIO.LoadRGB($"{SA}/PV/{rgbTimestamp}.bytes", rgbData);
			Matrix4x4 rig2world = Algorithms.MatrixFromFloats(depthMeta.rig2world[timestamp]);
			cam2world = cam2rig * rig2world;
			dllPointCloudCount = 0;

			currentFrame++;
			if (currentFrame == depthTimestamps.Count)
				currentFrame = 0;
		}
		else
		{
			DataIO.LoadHololensDump(footageFiles[currentFrame], depthData, irData, out cam2world, 
				out Vector3 handPos, out Quaternion handRot,
				out Vector3 camPos, out Quaternion camRot,
				dllPointCloud, out dllPointCloudCount);
			Hand.position = handPos;
			Hand.rotation = handRot;
			Cam.transform.position = camPos;
			Cam.transform.rotation = camRot;
			cam2world = cam2world.transpose;
			if (Time.time >= timer)
			{
				currentFrame++;
				if (currentFrame >= footageFiles.Length)
					currentFrame = 0;
				timer = Time.time + 0.1f;
			}
		}
		
		Profiler.EndSample();
		return cam2world;
	}

	private void UpdateFeed()
	{
		if (DepthFeed == null)
			return;

		Profiler.BeginSample("Mock.UpdateFeed");
		DepthFeed.texture = depthData.AsTexture(depth: true);
		IRFeed.texture = irData.AsTexture(depth: false);
		RGBFeed.texture = rgbData.AsTexture();
		Profiler.EndSample();
	}
	
	private void UpdateCamera(Matrix4x4 cam2world)
	{
		if (!RealtimeDetector.UseWorldSpace || UseNewDataFormat)
			return;
		
		Vector3 pos = Algorithms.ConvertVectorToCoordinateSpace(Vector3.zero, cam2world);
		//Vector3 forward = Algorithms.ConvertVectorToCoordinateSpace(Vector3.forward, cam2world);
		Vector3 up = Algorithms.ConvertVectorToCoordinateSpace(Vector3.up * 10, cam2world).normalized;
		//Vector3 forward = cam2world * new Vector4(0, 0, 1, 1).normalized;
		//Vector3 up = cam2world * new Vector4(0, 1, 0, 1).normalized;

		Vector3 p = lut[256 * 512 + 256] * 2000f / 1000f;
		if (UseWorldSpace)
			p = Algorithms.ConvertVectorToCoordinateSpace(p, cam2world);
		Vector3 forward = (p - pos).normalized;

		Cam.transform.localPosition = pos;
		Cam.transform.localRotation = Quaternion.LookRotation(forward, up);
		Cam.transform.localEulerAngles += new Vector3(0, 0, 180);

		Cam.ResetProjectionMatrix();
		Cam.projectionMatrix = Cam.projectionMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));
		GL.invertCulling = true;
		Profiler.EndSample();
	}
#endif
}