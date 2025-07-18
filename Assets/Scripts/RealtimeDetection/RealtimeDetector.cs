using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;
using System.IO;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.InputSystem.EnhancedTouch;
using Unity.XR.CoreUtils.Collections;


public partial class RealtimeDetector : MonoBehaviour
{
	public static bool UseWorldSpace = true;
	public static bool EnableAlgorithm = true;
	public static bool EnablePointCloudGeneration = true;
	public static bool EnablePointCloudRendering = true;
	public static bool InstancedRendering = false;
	public static int PointCloudSpacing = 3;
   	public static int DLLPointCloudSpacing = 3;
    public static int POINT_CLOUD_MIN_DEPTH = 100;
    public static int POINT_CLOUD_MAX_DEPTH = 4000;

	public TextAsset DepthMetaJson;
	public TextAsset RGBMetaJson;

	public Transform[] Spheres; // used to mark the disks
	public Transform RigCenter; // used to create a reference frame inbetween the disks
	public Transform RotationAndScale;
	public Transform PointCloudRoot;
	public Camera Cam;
	public Mesh MicrosphereMesh;
	public Material MicrosphereMaterial;
	public Material DLLPointCloudMaterial;

	protected Vector3 MicrosphereScale = Vector3.one * 0.002f;

	protected DepthMetaData depthMeta;
	protected RGBMetaData rgbMeta;
	///  Lookup Table for 2D depth camera image to 3D rig space 
	protected Vector3[] lut;
	protected Matrix4x4 cam2rig;

	protected DepthData depthData;
	protected DepthData irData;
	protected RGBData rgbData;
	protected ConnectedComponentsOutput ccOutput;
	protected List<IRDisk> irDisks = new(4);
	protected Detection detection;
	protected readonly Vector3 outOfBounds = new(-9999, -9999, -9999);
	

	protected Matrix4x4 _cam2world;
	private const int IMAGE_HEIGHT = 512;
	private const int IMAGE_WIDTH = 512;
	private const int IMAGE_OFFSET = 16;
	private readonly int POINT_CLOUD_POINT_COUNT = ((IMAGE_HEIGHT - 2 * IMAGE_OFFSET) / PointCloudSpacing) * ((IMAGE_WIDTH - 2 * IMAGE_OFFSET) / PointCloudSpacing);

	private readonly Matrix4x4[] microspheresTRSs = new Matrix4x4[1024];


	public void Start()
    {
		depthMeta = JsonConvert.DeserializeObject<DepthMetaData>(DepthMetaJson.text);
		rgbMeta =  JsonConvert.DeserializeObject<RGBMetaData>(RGBMetaJson.text);

		depthData = new DepthData(512, 512);
		irData = new DepthData(512, 512);
		rgbData = new RGBData(760, 428);
		ccOutput = new ConnectedComponentsOutput();
		lut = Algorithms.LUTFromFloats(depthMeta.depth_mesh);
		cam2rig = Algorithms.MatrixFromFloats(depthMeta.cam2rig);
		if (Cam == null)
			Cam = Camera.main;

		if (UseWorldSpace)
		{
			RotationAndScale.eulerAngles = new Vector3(0f, 0f, 0f);
			RotationAndScale.localScale = new Vector3(1f, 1f, 1f);
		} 
		else
		{
			RotationAndScale.eulerAngles = new Vector3(0f, 0f, 0f);
			RotationAndScale.localScale = new Vector3(1f, 1f, 1f);
		}
    }

	private bool fauxFrame = false;
	private int counter = 0;
	/// <summary>
	/// 
	/// </summary>
	/// <param name="depthFrameData"> depth image 2d 512x512 frame data</param>
	/// <param name="abFrameData">absolute brightness 2d 512x512 frame data </param>
	/// <param name="cam2world">3D depth camera pose to world coordinates pose</param>
	public void ProcessFrame(ushort[] depthFrameData, ushort[] abFrameData, Matrix4x4 cam2world)
	{
		depthData.UpdateFromDevice(depthFrameData);
		irData.UpdateFromDevice(abFrameData);
		if (EnableAlgorithm)
		{
			if (irDisks == null || irDisks.Count != 4)
			{
				// run fully, without hints
				Algorithms.ConnectedComponentAnalysis_8(depthData, irData, ccOutput);
			}
			else
			{
				(int rowStart, int rowEnd, int colStart, int colEnd) = Algorithms.GenerateLabelHints(irDisks);
				Algorithms.ConnectedComponentAnalysis_8(depthData, irData, ccOutput, rowStart, rowEnd, colStart, colEnd);
			}

			Algorithms.FindIRDisks(irDisks, ccOutput.components, ccOutput.labelIndices, lut, depthData, cam2world, Cam);
		}
		else
		{
			irDisks.Clear();
			detection = default;
		}
		
		if (irDisks != null && ((irDisks.Count == 3 && detection.isValid) || irDisks.Count == 4))
		{
			if (detection.isValid == false)
			{
				detection = Detection.FromDetections(irDisks, Cam.transform);
				//counter = 0;
			}
			else
			{
				//if (counter >= 4 && irDisks.Count == 4)
				//{
				//	counter = 0;
				//	int idx = UnityEngine.Random.Range(0, 4);
				//	List<IRDisk> original = irDisks.ToList();
				//	List<IRDisk> copy = original.ToList();
				//	Detection gt = Detection.FromDetections(copy, Cam.transform);

				//	copy = original.ToList();
				//	copy.RemoveAt(idx);
				//	(Detection pd, int idx_g) = Detection.UpdateFrom3(detection, copy, Cam.transform);
				//	float s = (gt.south.worldCenter - pd.south.worldCenter).magnitude;
				//	float w = (gt.west.worldCenter - pd.west.worldCenter).magnitude;
				//	float n = (gt.north.worldCenter - pd.north.worldCenter).magnitude;
				//	float e = (gt.east.worldCenter - pd.east.worldCenter).magnitude;
				//	bool g = idx == idx_g;

				//	if (s + w + n + e < 0.002f)
				//		Debug.Log("Match");
				//	else
				//		Debug.Log("Fail");

				//	copy = original.ToList();
				//	copy.RemoveAt(idx);
				//	(Detection pdd, int idx_gg) = Detection.UpdateFrom3(detection, copy, Cam.transform);
				//	Debug.Assert(idx_g == idx_gg);

				//	irDisks.RemoveAt(idx);
				//}


				if (irDisks.Count == 4)
					detection = Detection.Update(detection, irDisks, Cam.transform);
				else
					(detection, _) = Detection.UpdateFrom3(detection, irDisks, Cam.transform);
				//counter++;
			}

			// Move square "alignment guide" to position and rotation of each of the 4 tracked reflective disks
			Spheres[0].localPosition = detection.south.worldCenter;
			Spheres[0].localRotation = detection.orientation;
			Spheres[1].localPosition = detection.west.worldCenter;
			Spheres[1].localRotation = detection.orientation;
			Spheres[2].localPosition = detection.north.worldCenter;
			Spheres[2].localRotation = detection.orientation;
			Spheres[3].localPosition = detection.east.worldCenter;
			Spheres[3].localRotation = detection.orientation;

			// Move square to follow position and rotation of 4 tracked reflective disks
			RigCenter.localPosition = detection.center;
			RigCenter.localRotation = detection.orientation;

			fauxFrame = false;
		}
		else
		{
			if (irDisks == null)
				Debug.Log($"IRDisks is null");
			else
				Debug.Log($"IRDisks: {irDisks.Count}");


			if (fauxFrame == false)
			{
				Debug.Log("Faux Frame");
				fauxFrame = true; // keep previous detection for a single frame to mask lost 1 frame detections
			}
			else
			{
				for (int f = 0; f < 4; f++)
					Spheres[f].position = outOfBounds;
				RigCenter.localPosition = outOfBounds;
			}
		}
		// if(RealtimeDetector.EnablePointCloudGeneration && RealtimeDetector.EnablePointCloudRendering)
			// UpdatePointCloud(cam2world);
	}
/*
	public void UpdatePointCloud(Matrix4x4 cam2world)
	{
		Profiler.BeginSample("Mock.UpdatePointCloud");
		Quaternion towardsCamera = Quaternion.identity;

		// Microsphere counter
		int k = 0; 
		// For every few pixels in the depth image
		for (int r = IMAGE_OFFSET; r < IMAGE_HEIGHT-IMAGE_OFFSET; r += PointCloudSpacing)
		{
			for (int c = IMAGE_OFFSET; c < IMAGE_WIDTH - IMAGE_OFFSET; c += PointCloudSpacing)
			{
				float depth = depthData.pixels[r * IMAGE_WIDTH + c];

				// don't render zero or negative depth
				if (depth <= POINT_CLOUD_MIN_DEPTH || depth >= POINT_CLOUD_MAX_DEPTH)
					continue;

				// Multiply thousandth of depth value by the base point in camera space (stored in the LUT table)
				Vector3 p = lut[r * IMAGE_WIDTH + c] * depth / 1000f;
				if (UseWorldSpace)
					p = Algorithms.ConvertVectorToCoordinateSpace(p, cam2world);

				if (InstancedRendering)
				{
					microspheresTRSs[k % 1024] = Matrix4x4.TRS(p, towardsCamera, MicrosphereScale);
					k++;
					if (k % 1024 == 0) // batching works in groups of 1023 entries
						Graphics.DrawMeshInstanced(MicrosphereMesh, 0, MicrosphereMaterial, microspheresTRSs, 1023, null, ShadowCastingMode.Off, false);
				}
				else
				{
					Graphics.DrawMesh(MicrosphereMesh, Matrix4x4.TRS(p, towardsCamera, MicrosphereScale), MicrosphereMaterial, 0, null, 0, null, ShadowCastingMode.Off, false, null, LightProbeUsage.Off, null);
					k++;
					if (k > POINT_CLOUD_POINT_COUNT)
					{
						Profiler.EndSample();
						return;
					}
				}
			}
		}

		if (InstancedRendering && k % 1024 > 0) // renders all remaining entries
			Graphics.DrawMeshInstanced(MicrosphereMesh, 0, MicrosphereMaterial, microspheresTRSs, k % 1024, null, ShadowCastingMode.Off, false);
		// Profiler.EndSample();
	}

	public void RenderDLLPointCloud(float[] pointsXYZ, int xyzCount)
	{
		// Profiler.BeginSample("Mock.UpdatePointCloud");
		Quaternion towardsCamera = Camera.main.transform.rotation;

		// Microsphere counter
		int k = 0;
		int nPoints = xyzCount / 3;
		int spacing = nPoints / POINT_CLOUD_POINT_COUNT;
		if (DLLPointCloudSpacing > spacing)
			spacing = DLLPointCloudSpacing;

		// For every few pixels in the depth image
		for (int i = 0; i < xyzCount; i += 3 * spacing) // read every fourth point
		{
			Vector3 p = new(pointsXYZ[i], pointsXYZ[i + 1], pointsXYZ[i + 2]);
			if (InstancedRendering)
			{
				microspheresTRSs[k % 1024] = Matrix4x4.TRS(p, towardsCamera, MicrosphereScale);
				k++;
				if (k % 1024 == 0) // batching works in groups of 1023 entries
					Graphics.DrawMeshInstanced(MicrosphereMesh, 0, DLLPointCloudMaterial, microspheresTRSs, 1023, null, ShadowCastingMode.Off, false);
			}
			else
			{
				Graphics.DrawMesh(MicrosphereMesh, Matrix4x4.TRS(p, towardsCamera, MicrosphereScale), DLLPointCloudMaterial, 0, null, 0, null, ShadowCastingMode.Off, false, null, LightProbeUsage.Off, null);
				k++;
				if (k > POINT_CLOUD_POINT_COUNT)
				{
					Profiler.EndSample();
					return;
				}
			}

			
		}

		if (InstancedRendering && k % 1024 > 0) // renders all remaining entries
			Graphics.DrawMeshInstanced(MicrosphereMesh, 0, DLLPointCloudMaterial, microspheresTRSs, k % 1024, null, ShadowCastingMode.Off, false);

		// Profiler.EndSample();
	}
*/
}
