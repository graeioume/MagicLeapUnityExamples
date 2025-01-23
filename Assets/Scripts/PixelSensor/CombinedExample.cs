using MagicLeap.Android;
using MagicLeap.OpenXR.Features.PixelSensors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.OpenXR;



public class CombinedExample : MonoBehaviour
{
    public DepthStreamVisualizer streamVisualizer;

	// rgb
	private Texture2D[] rgbTextures;

	// depth
	public bool UseRawDepth = false;
	public bool LongRange = false;
    public ShortRangeUpdateRate SRUpdateRate;
    public LongRangeUpdateRate LRUpdateRate;
    private uint targetStream => LongRange ? (uint)0 : (uint)1;

    // world
	private Texture2D[] worldTextures;


	private MagicLeapPixelSensorFeature pixelSensorFeature;
	private PixelSensorId? rgbSensorID;
	private PixelSensorId? depthSensorID;
	private PixelSensorId? worldSensorID;
	private readonly List<uint> configuredRGBStreams = new List<uint>();
	private readonly List<uint> configuredDepthStreams = new List<uint>();
	private readonly List<uint> configuredWorldStreams = new List<uint>();

	
    void Start()
    {
		ImageSaver.ClearImgFolder();
		LogsSaver.Initialize();

		Debug.Log($"CombinedExample Start frame: {Time.frameCount} go: {gameObject}");

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
				Debug.Log($"CombinedExample Permission Granted: {permission}");
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
		Debug.Log($"CombinedExample InitSensors");
		foreach (PixelSensorId sensor in pixelSensorFeature.GetSupportedSensors())
        {
            Debug.Log("Sensor Name Found: " + sensor.SensorName);
			if (rgbSensorID == null && sensor.XrPathString.Contains("/pixelsensor/picture/center"))
				rgbSensorID = sensor;

			if (depthSensorID == null && sensor.XrPathString.Contains("/pixelsensor/depth/center"))
				depthSensorID = sensor;

            if (worldSensorID == null && sensor.XrPathString.Contains("/pixelsensor/world/center"))
                worldSensorID = sensor;
        }

		Debug.Log($"CombinedExample InitSensors {rgbSensorID} {depthSensorID} {worldSensorID}");

		// Subscribe to the Availability changed callback if the sensor becomes available.
		pixelSensorFeature.OnSensorAvailabilityChanged += (PixelSensorId id, bool available) => {
			if (rgbSensorID.HasValue && id == rgbSensorID && available)
				TryInitializeRGBSensor();
			if (depthSensorID.HasValue && id == depthSensorID && available)
				TryInitializeDepthSensor();
			if (worldSensorID.HasValue && id == worldSensorID && available)
				TryInitializeWorldSensor();
		};

		if (rgbSensorID.HasValue)
			TryInitializeRGBSensor();
		if (depthSensorID.HasValue)
			TryInitializeDepthSensor();
		if (worldSensorID.HasValue)
			TryInitializeWorldSensor();
	}

	private void TryInitializeRGBSensor()
    {
		Debug.Log($"CombinedExample TryInitializeRGBSensor");
		if (pixelSensorFeature.GetSensorStatus(rgbSensorID.Value) != PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(rgbSensorID.Value))
		{
			Debug.LogWarning("Failed to create RGB sensor. Will retry when it becomes available.");
			rgbSensorID = null;
			return;
		}

		Debug.Log("RGB sensor created successfully.");
		uint streamCount = pixelSensorFeature.GetStreamCount(rgbSensorID.Value);
		Debug.Log($"RGB sensor has {streamCount} streams");
		if (streamCount < 1)
		{
			Debug.LogError("Expected at least one RGB stream from the sensor.");
			return;
		}

		rgbTextures = new Texture2D[streamCount];
		configuredRGBStreams.Clear();
		configuredRGBStreams.Add(1); // CV stream

		pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.Format, (uint)PixelSensorFrameFormat.Rgba8888, 1);
		pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.Resolution, new Vector2Int(1280, 720), 1);
		pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.UpdateRate, 30, 1);

		StartCoroutine(StartRGBStream());
	}

	private IEnumerator StartRGBStream()
	{
		Debug.Log($"CombinedExample StartRGBStream");
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensor(rgbSensorID.Value, configuredRGBStreams.ToArray());

		yield return configureOperation;
		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure RGB sensor.");
			rgbSensorID = null;
			yield break;
		}

		Debug.Log("RGB sensor configured with defaults successfully.");
		PixelSensorAsyncOperationResult startOperation = pixelSensorFeature.StartSensor(rgbSensorID.Value, configuredRGBStreams);
		yield return startOperation;

		if (!startOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to start RGB sensor.");
			yield break;
		}

		Debug.Log("RGB sensor started successfully. Monitoring data...");
		StartCoroutine(MonitorRGBSensor());
	}

	private void TryInitializeDepthSensor()
    {
		Debug.Log($"CombinedExample TryInitializeDepthSensor");
		if (!depthSensorID.HasValue || pixelSensorFeature.GetSensorStatus(depthSensorID.Value) !=
			PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(depthSensorID.Value))
		{
			Debug.LogWarning("Failed to create Depth sensor. Will retry when it becomes available.");
            return;
		}

		Debug.Log("Depth sensor created successfully.");
        uint streamCount = pixelSensorFeature.GetStreamCount(depthSensorID.Value);
        if (streamCount < 1)
        {
            Debug.LogError("Expected at least one Depth stream from the sensor.");
            return;
        }

		Debug.Log($"CombinedExample TryInitializeDepthSensor GetPixelSensorCapabilities");
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
		Debug.Log($"CombinedExample StartDepthStream");
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensor(depthSensorID.Value, configuredDepthStreams);

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
		Debug.Log($"CombinedExample TryInitializeWorldSensor");

		// Make sure the sensor is Undefined before creating it
		if (pixelSensorFeature.GetSensorStatus(worldSensorID.Value) != PixelSensorStatus.Undefined ||
			!pixelSensorFeature.CreatePixelSensor(worldSensorID.Value))
		{
			Debug.LogWarning("Failed to create World sensor. Will retry when it becomes available.");
			return;
		}

		Debug.Log($"CombinedExample TryInitializeWorldSensor GetStreamCount");
		uint streamCount = pixelSensorFeature.GetStreamCount(worldSensorID.Value);
		if (streamCount < 1)
		{
			Debug.LogError("Expected at least 1 World streams to be available at the sensor.");
			return;
		}

		Debug.Log($"CombinedExample TryInitializeWorldSensor Creating Textures");
		worldTextures = new Texture2D[streamCount];
		for (uint i = 0; i < streamCount; i++)
			configuredWorldStreams.Add(i);

		StartCoroutine(StartWorldStream());
	}

	private IEnumerator StartWorldStream()
	{
		Debug.Log($"CombinedExample StartWorldStream");

		// Configure the sensor with default configuration
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensorWithDefaultCapabilities(worldSensorID.Value, configuredWorldStreams.ToArray());
		yield return configureOperation;

		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure World sensor.");
			yield break;
		}

		Debug.Log("World sensor configured with defaults successfully.");

		// Start the sensor with the default configuration and specify that all of the meta data should be requested.
		PixelSensorAsyncOperationResult sensorStartAsyncResult = pixelSensorFeature.StartSensor(worldSensorID.Value, configuredWorldStreams);
		yield return sensorStartAsyncResult;

		if (!sensorStartAsyncResult.DidOperationSucceed)
		{
			Debug.LogError("World stream could not be started.");
			yield break;
		}

		Debug.Log("World stream started successfully.");
		yield return MonitorWorldSensor();
	}


	private IEnumerator MonitorRGBSensor()
	{
		Debug.Log($"CombinedExample MonitorRGBSensor");
		while (rgbSensorID.HasValue && pixelSensorFeature.GetSensorStatus(rgbSensorID.Value) == PixelSensorStatus.Started)
		{
			foreach (uint stream in configuredRGBStreams)
			{
				Debug.Log($"CombinedExample MonitorRGBSensor {stream}");

				// In this example, the meta data is not used.
				if (pixelSensorFeature.GetSensorData(
						rgbSensorID.Value, stream, out PixelSensorFrame frame,
						out PixelSensorMetaData[] currentFrameMetaData,
						Allocator.Temp, shouldFlipTexture: true))
				{
					PixelSensorPlane plane = frame.Planes[0];
					Texture2D texture = rgbTextures[stream];
					if (!frame.IsValid || frame.Planes.Length == 0)
						continue;

					if (texture == null)
						texture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.ARGB32, false);
					texture.LoadRawTextureData(plane.ByteData);
					texture.Apply();

					float unityTime = Time.realtimeSinceStartup;
					DateTimeOffset deviceTime = DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
					Pose sensorPose = pixelSensorFeature.GetSensorPose(rgbSensorID.Value);
					ImageSaver.InitNewFrame(Time.frameCount, unityTime, deviceTime);
					ImageSaver.SaveSensor(texture, $"rgb{stream}", unityTime, deviceTime, sensorPose);
				}
			}

			yield return null;
		}
	}

	private IEnumerator MonitorDepthSensor()
    {
		Debug.Log($"CombinedExample MonitorDepthSensor");
		Quaternion frameRotation = pixelSensorFeature.GetSensorFrameRotation(depthSensorID.Value);

        streamVisualizer.Initialize(targetStream, frameRotation, pixelSensorFeature, depthSensorID.Value);
        while (pixelSensorFeature.GetSensorStatus(depthSensorID.Value) == PixelSensorStatus.Started)
        {
            foreach (uint stream in configuredDepthStreams)
            {
				Debug.Log($"CombinedExample MonitorDepthSensor {stream}");
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

	private IEnumerator MonitorWorldSensor()
	{
		Debug.Log($"CombinedExample MonitorWorldSensor");
		while (worldSensorID.HasValue && pixelSensorFeature.GetSensorStatus(worldSensorID.Value) == PixelSensorStatus.Started)
		{
			foreach (uint stream in configuredWorldStreams)
			{
				Debug.Log($"CombinedExample MonitorWorldSensor {stream}");
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
		Debug.Log($"CombinedExample OnDisable");

		//We start the Coroutine on another MonoBehaviour since it can only run while the object is enabled.
		MonoBehaviour camMono = Camera.main.GetComponent<MonoBehaviour>();
        camMono.StartCoroutine(StopSensorCoroutine());
    }

    private IEnumerator StopSensorCoroutine()
    {
		Debug.Log($"CombinedExample StopSensorCoroutine");
		if (rgbSensorID.HasValue)
		{
			PixelSensorAsyncOperationResult stopRGBAsyncResult = pixelSensorFeature.StopSensor(rgbSensorID.Value, configuredRGBStreams);
			yield return stopRGBAsyncResult;

			if (stopRGBAsyncResult.DidOperationSucceed)
			{
				pixelSensorFeature.DestroyPixelSensor(rgbSensorID.Value);
				Debug.Log("RGB sensor stopped and destroyed successfully.");
			}
			else
				Debug.LogError("Failed to stop the RGB sensor.");
		}

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

	public enum LongRangeUpdateRate { OneFps = 1, FiveFps = 5 }
    public enum ShortRangeUpdateRate { FiveFps = 5, ThirtyFps = 30, SixtyFps = 60 }
}