using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MagicLeap.Android;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.MagicLeap;
using UnityEngine.XR.OpenXR;
using MagicLeap.OpenXR.Features.PixelSensors;
using System;
using System.IO.Compression;
using System.IO;
using static SensorNameUtility;
using Unity.XR.CoreUtils.Collections;
using UnityEngine.InputSystem;
using static UnityEngine.SpatialTracking.TrackedPoseDriver;
using static UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator;

public class CombinedExample : MonoBehaviour
{
    public DepthStreamVisualizer streamVisualizer;

    // depth
	public bool UseRawDepth = false;
	public bool LongRange = false;
    public ShortRangeUpdateRate SRUpdateRate;
    public LongRangeUpdateRate LRUpdateRate;
    private uint targetStream => LongRange ? (uint)0 : (uint)1;

    // world
	public bool useWorldStream0 = true;
	public bool useWorldStream1 = true;
	private Texture2D[] worldTextures = new Texture2D[2];


	private MagicLeapPixelSensorFeature pixelSensorFeature;
	private PixelSensorId? rgbSensorID;
	private PixelSensorId? worldSensorID;
	private PixelSensorId? depthSensorID;
	private readonly List<uint> configuredWorldStreams = new List<uint>();
    private readonly List<uint> configuredDepthStreams = new List<uint>();

    void Start()
    {
        // gets Pixel Sensor Feature, basic object to access all pixel sensors
        pixelSensorFeature = OpenXRSettings.Instance.GetFeature<MagicLeapPixelSensorFeature>();
        if (pixelSensorFeature == null || !pixelSensorFeature.enabled)
        {
            Debug.LogError("Pixel Sensor Feature not found or not enabled!");
            enabled = false;
            return;
        }


		string[] allRequiredPermissions = new string[]
        {
            UnityEngine.Android.Permission.Camera, // Needed for RGB and World camera
            Permissions.DepthCamera,
            Permissions.EyeCamera
        };

	    Permissions.RequestPermissions(allRequiredPermissions,
            (string permission) => { // granted
				InitSensors();
			},
            (string permission) =>{ // denied
                Debug.LogError($"Permission {permission} not granted. Example script will not work.");
                enabled = false;
            },
            (string permission) => { // denied
                Debug.LogError($"Permission {permission} not granted. Example script will not work.");
                enabled = false;
            }
        );
    }

    private void InitSensors()
    {
        foreach (PixelSensorId sensor in pixelSensorFeature.GetSupportedSensors())
        {
            Debug.Log("Sensor Name Found: " + sensor.XrPathString);
			if (rgbSensorID == null && sensor.SensorName.Contains("Picture"))
				rgbSensorID = sensor;

			if (depthSensorID == null && sensor.SensorName.Contains("Depth"))
				depthSensorID = sensor;

            if (worldSensorID == null && sensor.SensorName.Contains("World"))
                worldSensorID = sensor;
        }

        // Subscribe to the Availability changed callback if the sensor becomes available.
        pixelSensorFeature.OnSensorAvailabilityChanged += (PixelSensorId id, bool available) => {
			if (rgbSensorID.HasValue && id == rgbSensorID && available)
				TryInitializeRGBSensor();
			if (depthSensorID.HasValue && id == depthSensorID && available)
				TryInitializeDepthSensor();
			if (worldSensorID.HasValue && id == worldSensorID && available)
				TryInitializeWorldSensor();
		};

		TryInitializeRGBSensor();
		TryInitializeDepthSensor();
		TryInitializeWorldSensor();
	}

	private void TryInitializeRGBSensor()
    {

    }

	private void TryInitializeDepthSensor()
    {
		if (!depthSensorID.HasValue || pixelSensorFeature.GetSensorStatus(depthSensorID.Value) !=
			PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(depthSensorID.Value))
		{
			Debug.LogWarning("Failed to create sensor. Will retry when it becomes available.");
            return;
		}

		Debug.Log("Sensor created successfully.");
        uint streamCount = pixelSensorFeature.GetStreamCount(depthSensorID.Value);
        if (streamCount < 1)
        {
            Debug.LogError("Expected at least one stream from the sensor.");
            return;
        }

        configuredDepthStreams.Add(targetStream);
        pixelSensorFeature.GetPixelSensorCapabilities(depthSensorID.Value, targetStream, out PixelSensorCapability[] capabilities);
        foreach (PixelSensorCapability pixelSensorCapability in capabilities)
        {
            if (pixelSensorFeature.QueryPixelSensorCapability(depthSensorID.Value, pixelSensorCapability.CapabilityType, targetStream, out PixelSensorCapabilityRange range) && range.IsValid)
            {
                if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.IntValue = LongRange ? (uint)LRUpdateRate : (uint)SRUpdateRate;
                    pixelSensorFeature.ApplySensorConfig(depthSensorID.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Format)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.IntValue = (uint)range.FrameFormats[UseRawDepth ? 1 : 0];
                    pixelSensorFeature.ApplySensorConfig(depthSensorID.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Resolution)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.VectorValue = range.ExtentValues[0];
                    pixelSensorFeature.ApplySensorConfig(depthSensorID.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Depth)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.FloatValue = LongRange ? 5f : 0.99f;
                    pixelSensorFeature.ApplySensorConfig(depthSensorID.Value, configData);
                }
            }
        }

        StartCoroutine(StartDepthStream());
    }

	private IEnumerator StartDepthStream()
	{
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensor(depthSensorID.Value, configuredDepthStreams.ToArray());

		yield return configureOperation;
		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure sensor.");
			yield break;
		}

		Debug.Log("Sensor configured with defaults successfully.");
		Dictionary<uint, PixelSensorMetaDataType[]> supportedMetadataTypes = new Dictionary<uint, PixelSensorMetaDataType[]>();
		foreach (uint stream in configuredDepthStreams)
			if (pixelSensorFeature.EnumeratePixelSensorMetaDataTypes(depthSensorID.Value, stream, out PixelSensorMetaDataType[] metaDataTypes))
				supportedMetadataTypes[stream] = metaDataTypes;

		// Assuming that `configuredStreams` is correctly populated with the intended stream indices
		PixelSensorAsyncOperationResult startOperation = pixelSensorFeature.StartSensor(depthSensorID.Value, configuredDepthStreams, supportedMetadataTypes);
		yield return startOperation;

		if (!startOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to start sensor.");
            yield break;
		}

		Debug.Log("Sensor started successfully. Monitoring data...");
		StartCoroutine(MonitorDepthSensor());
	}

	private void TryInitializeWorldSensor()
	{
		uint streamCount = pixelSensorFeature.GetStreamCount(worldSensorID.Value);
		if (useWorldStream1 && streamCount < 2 || useWorldStream0 && streamCount < 1)
		{
			Debug.LogError("target Streams are not available from the sensor.");
			return;
		}

		for (uint i = 0; i < streamCount; i++)
			if ((useWorldStream0 && i == 0) || (useWorldStream1 && i == 1))
				configuredWorldStreams.Add(i);

		StartCoroutine(StartWorldStream());
	}

	// Coroutine to configure stream and start sensor streams
	private IEnumerator StartWorldStream()
	{
		// Configure the sensor with default configuration
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensorWithDefaultCapabilities(worldSensorID.Value, configuredWorldStreams.ToArray());
		yield return configureOperation;

		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure sensor.");
			yield break;
		}

		Debug.Log("Sensor configured with defaults successfully.");

		// Start the sensor with the default configuration and specify that all of the meta data should be requested.
		PixelSensorAsyncOperationResult sensorStartAsyncResult = pixelSensorFeature.StartSensor(worldSensorID.Value, configuredWorldStreams);
		yield return sensorStartAsyncResult;

		if (!sensorStartAsyncResult.DidOperationSucceed)
		{
			Debug.LogError("Stream could not be started.");
			yield break;
		}

		Debug.Log("Stream started successfully.");
		yield return ProcessSensorData();
	}

	

    private IEnumerator MonitorDepthSensor()
    {
        Quaternion frameRotation = pixelSensorFeature.GetSensorFrameRotation(depthSensorID.Value);

        streamVisualizer.Initialize(targetStream, frameRotation, pixelSensorFeature, depthSensorID.Value);
        while (pixelSensorFeature.GetSensorStatus(depthSensorID.Value) == PixelSensorStatus.Started)
        {
            foreach (uint stream in configuredDepthStreams)
            {
                if (pixelSensorFeature.GetSensorData(depthSensorID.Value, stream, out PixelSensorFrame frame, out PixelSensorMetaData[] metaData, Allocator.Temp, shouldFlipTexture: true))
                {
					Pose sensorPose = pixelSensorFeature.GetSensorPose(depthSensorID.Value);
					DateTimeOffset deviceTime = DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
                    float unityTime = Time.realtimeSinceStartup;
                    ImageSaver.InitNewFrame(Time.frameCount, unityTime, deviceTime);

					Texture2D depth = streamVisualizer.ProcessFrame(frame);
                    ImageSaver.SaveSensor(depth, "depth", unityTime, deviceTime, sensorPose);

					PixelSensorDepthConfidenceBuffer confidenceMetadata = metaData.OfType<PixelSensorDepthConfidenceBuffer>().FirstOrDefault();
                    if (confidenceMetadata != null)
                    {
						Texture2D confidence = streamVisualizer.ProcessDepthConfidenceData(in confidenceMetadata);
						ImageSaver.SaveSensor(confidence, "confidence", unityTime, deviceTime, sensorPose);
					}

					PixelSensorDepthFlagBuffer flagMetadata = metaData.OfType<PixelSensorDepthFlagBuffer>().FirstOrDefault();
					if (flagMetadata != null)
					{
						Texture2D flags = streamVisualizer.ProcessDepthFlagData(in flagMetadata);
						ImageSaver.SaveSensor(flags, "flags", unityTime, deviceTime, sensorPose);
					}
				}

				yield return null;
            }
        }
    }

	private IEnumerator ProcessSensorData()
	{
		while (worldSensorID.HasValue && pixelSensorFeature.GetSensorStatus(worldSensorID.Value) == PixelSensorStatus.Started)
		{
			foreach (uint stream in configuredWorldStreams)
			{
				// In this example, the meta data is not used.
				if (pixelSensorFeature.GetSensorData(
						worldSensorID.Value, stream, out PixelSensorFrame frame,
						out PixelSensorMetaData[] currentFrameMetaData,
						Allocator.Temp, shouldFlipTexture: true))
				{
					PixelSensorPlane plane = frame.Planes[0];
					Texture2D texture = worldTextures[stream];
					if (!frame.IsValid || frame.Planes.Length == 0)
						continue;

					if (texture == null)
						texture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.R8, false);
					texture.LoadRawTextureData(plane.ByteData);
					texture.Apply();

					float unityTime = Time.realtimeSinceStartup;
					DateTimeOffset deviceTime = DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
					Pose sensorPose = pixelSensorFeature.GetSensorPose(worldSensorID.Value);
					ImageSaver.InitNewFrame(Time.frameCount, unityTime, deviceTime);
					ImageSaver.SaveSensor(texture, $"world{stream}", unityTime, deviceTime, sensorPose);
				}
			}

			yield return null;
		}
	}

	public void OnDisable()
    {
        //We start the Coroutine on another MonoBehaviour since it can only run while the object is enabled.
        MonoBehaviour camMono = Camera.main.GetComponent<MonoBehaviour>();
        camMono.StartCoroutine(StopSensorCoroutine());
    }

    private IEnumerator StopSensorCoroutine()
    {
        if (depthSensorID.HasValue)
        {
            PixelSensorAsyncOperationResult stopSensorAsyncResult = pixelSensorFeature.StopSensor(depthSensorID.Value, configuredDepthStreams);
            yield return stopSensorAsyncResult;

            if (stopSensorAsyncResult.DidOperationSucceed)
            {
                Debug.Log("Sensor stopped successfully.");
                pixelSensorFeature.ClearAllAppliedConfigs(depthSensorID.Value);
                // Free the sensor so it can be marked available and used in other scripts.
                pixelSensorFeature.DestroyPixelSensor(depthSensorID.Value);
            }
            else
				Debug.LogError("Failed to stop the sensor.");
		}

		if (worldSensorID.HasValue)
		{
			PixelSensorAsyncOperationResult stopSensorAsyncResult = pixelSensorFeature.StopSensor(worldSensorID.Value, configuredWorldStreams);
			yield return stopSensorAsyncResult;

			if (stopSensorAsyncResult.DidOperationSucceed)
			{
				pixelSensorFeature.DestroyPixelSensor(worldSensorID.Value);
				Debug.Log("Sensor stopped and destroyed successfully.");
			}
			else
				Debug.LogError("Failed to stop the sensor.");
		}
	}

	private void OnApplicationQuit()
	{
		ImageSaver.CloseLastFile();
	}

	public enum LongRangeUpdateRate { OneFps = 1, FiveFps = 5 }
    public enum ShortRangeUpdateRate { FiveFps = 5, ThirtyFps = 30, SixtyFps = 60 }
}