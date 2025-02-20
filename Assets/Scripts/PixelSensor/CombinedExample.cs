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
	// eye
	private Texture2D[] eyeTextures;


	private MagicLeapPixelSensorFeature pixelSensorFeature;
	private PixelSensorId? rgbSensorID;
	private PixelSensorId? depthSensorID;
	private PixelSensorId? worldSensorID;
	private PixelSensorId? eyeSensorID;
	private readonly List<uint> configuredRGBStreams = new List<uint>();
	private readonly List<uint> configuredDepthStreams = new List<uint>();
	private readonly List<uint> configuredWorldStreams = new List<uint>();
	private readonly List<uint> configuredEyeStreams = new List<uint>();

	
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

			if (eyeSensorID == null && sensor.XrPathString.Contains("/pixelsensor/eye/nasal/left"))
				eyeSensorID = sensor;
		}

		Debug.Log($"CombinedExample InitSensors rgb:{rgbSensorID} depth:{depthSensorID} world:{worldSensorID} eye:{eyeSensorID}");

		// Subscribe to the Availability changed callback if the sensor becomes available.
		pixelSensorFeature.OnSensorAvailabilityChanged += (PixelSensorId id, bool available) => {
			if (rgbSensorID.HasValue && id == rgbSensorID && available)
				TryInitializeRGBSensor();
			if (depthSensorID.HasValue && id == depthSensorID && available)
				TryInitializeDepthSensor();
			if (worldSensorID.HasValue && id == worldSensorID && available)
				TryInitializeWorldSensor();
			if (eyeSensorID.HasValue && id == eyeSensorID && available)
				TryInitializeEyeSensor();
		};

		if (rgbSensorID.HasValue)
			TryInitializeRGBSensor();
		if (depthSensorID.HasValue)
			TryInitializeDepthSensor();
		if (worldSensorID.HasValue)
			TryInitializeWorldSensor();
		if (eyeSensorID.HasValue)
			TryInitializeEyeSensor();
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

		if (pixelSensorFeature.GetSensorStatus(rgbSensorID.Value) == PixelSensorStatus.NotConfigured)
		{
			pixelSensorFeature.GetPixelSensorCapabilities(rgbSensorID.Value, 1, out var capabilities);
			PixelSensorCapabilityType[] targetCapabilityTypes = new[]
			{
				PixelSensorCapabilityType.UpdateRate,
				PixelSensorCapabilityType.Resolution,
				PixelSensorCapabilityType.Format
			};

			foreach (var pixelSensorCapability in capabilities)
			{
				if (!targetCapabilityTypes.Contains(pixelSensorCapability.CapabilityType))
					continue;

				if (!pixelSensorFeature.QueryPixelSensorCapability(rgbSensorID.Value, pixelSensorCapability.CapabilityType, 1, out PixelSensorCapabilityRange range) || !range.IsValid)
					continue;

				if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate)
					pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.UpdateRate, 30, 1);
				else if (range.CapabilityType == PixelSensorCapabilityType.Format)
					pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.Format, (uint)PixelSensorFrameFormat.Rgba8888, 1);
				else if (range.CapabilityType == PixelSensorCapabilityType.Resolution)
					pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.Resolution, new Vector2Int(1280, 720), 1);
			}
		}

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
		Debug.Log("CombinedExample TryInitializeWorldSensor");

		// Make sure the sensor is Undefined before creating it
		if (pixelSensorFeature.GetSensorStatus(worldSensorID.Value) != PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(worldSensorID.Value))
		{
			Debug.LogWarning("Failed to create World sensor. Will retry when it becomes available.");
			return;
		}

		Debug.Log("World sensor created successfully.");

		uint streamCount = pixelSensorFeature.GetStreamCount(worldSensorID.Value);
		Debug.Log($"World sensor has {streamCount} stream(s).");
		worldTextures = new Texture2D[streamCount];

		configuredWorldStreams.Clear();
		if (streamCount > 0)
			configuredWorldStreams.Add(0);
		if (streamCount > 1)
			configuredWorldStreams.Add(1);

		Debug.Log("CombinedExample ConfigureWorldStreamsManually()");
		if (pixelSensorFeature.GetSensorStatus(worldSensorID.Value) == PixelSensorStatus.NotConfigured)
		{
			pixelSensorFeature.GetPixelSensorCapabilities(rgbSensorID.Value, 1, out var capabilities);
			PixelSensorCapabilityType[] targetCapabilityTypes = new[]
			{
				PixelSensorCapabilityType.UpdateRate,
				PixelSensorCapabilityType.Resolution,
				PixelSensorCapabilityType.Format,
				PixelSensorCapabilityType.AutoExposureMode
			};

			foreach (var pixelSensorCapability in capabilities)
			{
				if (!targetCapabilityTypes.Contains(pixelSensorCapability.CapabilityType))
					continue;

				if (!pixelSensorFeature.QueryPixelSensorCapability(worldSensorID.Value, pixelSensorCapability.CapabilityType, 0, out PixelSensorCapabilityRange range) || !range.IsValid)
					continue;
				if (!pixelSensorFeature.QueryPixelSensorCapability(worldSensorID.Value, pixelSensorCapability.CapabilityType, 1, out PixelSensorCapabilityRange range2) || !range.IsValid)
					continue;

				Debug.Log($"Updating world sensor {pixelSensorCapability.CapabilityType}");
				if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate)
				{
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.UpdateRate, 30, 0);
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.UpdateRate, 30, 1);
				}
				else if (range.CapabilityType == PixelSensorCapabilityType.Format)
				{
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Format, (uint)PixelSensorFrameFormat.Grayscale, 0);
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Format, (uint)PixelSensorFrameFormat.Grayscale, 1);
				}
				else if (range.CapabilityType == PixelSensorCapabilityType.Resolution)
				{
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Resolution, new Vector2Int(1016, 1016), 0);
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Resolution, new Vector2Int(1016, 1016), 1);
				}
				else if (range.CapabilityType == PixelSensorCapabilityType.AutoExposureMode)
				{
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, new PixelSensorConfigData(PixelSensorCapabilityType.AutoExposureMode, 0) { ExposureMode = PixelSensorAutoExposureMode.EnvironmentTracking });
					pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, new PixelSensorConfigData(PixelSensorCapabilityType.AutoExposureMode, 1) { ExposureMode = PixelSensorAutoExposureMode.ProximityIrTracking });
				}
			}
		}

		// Start the custom configuration
		StartCoroutine(StartWorldStream());
	}

	private IEnumerator StartWorldStream()
	{
		Debug.Log("CombinedExample Calling ConfigureSensor for World sensor...");
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensor(worldSensorID.Value, configuredWorldStreams.ToArray());
		yield return configureOperation;


		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure World sensor streams for near IR usage.");
			yield break;
		}
		Debug.Log("World sensor configured successfully with custom settings.");


		Debug.Log("Starting World sensor...");
		PixelSensorAsyncOperationResult startOperation = pixelSensorFeature.StartSensor(worldSensorID.Value, configuredWorldStreams);
		yield return startOperation;

		if (!startOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to start World sensor for near IR usage.");
			yield break;
		}

		Debug.Log("World sensor started successfully.");
		yield return MonitorWorldSensor();
	}

	private void TryInitializeEyeSensor()
	{
		Debug.Log($"CombinedExample TryInitializeEyeSensor");

		if (pixelSensorFeature.GetSensorStatus(eyeSensorID.Value) != PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(eyeSensorID.Value))
		{
			Debug.LogWarning("Failed to create Eye sensor. Will retry when it becomes available.");
			return;
		}

		Debug.Log("Eye sensor created successfully.");
		uint streamCount = pixelSensorFeature.GetStreamCount(eyeSensorID.Value);
		Debug.Log($"Eye sensor has {streamCount} streams");

		if (streamCount < 1)
		{
			Debug.LogError("Expected at least one Eye stream from the sensor.");
			return;
		}

		eyeTextures = new Texture2D[streamCount];

		configuredEyeStreams.Clear();
		configuredEyeStreams.Add(0);
		StartCoroutine(StartEyeStream());
	}

	private IEnumerator StartEyeStream()
	{
		Debug.Log($"CombinedExample StartEyeStream");
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensorWithDefaultCapabilities(
			eyeSensorID.Value,
			configuredEyeStreams.ToArray()
		);
		yield return configureOperation;

		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure Eye sensor.");
			yield break;
		}

		Debug.Log("Eye sensor configured successfully.");

		PixelSensorAsyncOperationResult startOperation = pixelSensorFeature.StartSensor(
			eyeSensorID.Value,
			configuredEyeStreams
		);
		yield return startOperation;

		if (!startOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to start Eye sensor.");
			yield break;
		}

		Debug.Log("Eye sensor started successfully. Monitoring data...");
		StartCoroutine(MonitorEyeSensor());
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
				if (pixelSensorFeature.GetSensorData(worldSensorID.Value, stream, out PixelSensorFrame frame, out PixelSensorMetaData[] currentFrameMetaData, Allocator.Temp, shouldFlipTexture: true))
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

	private IEnumerator MonitorEyeSensor()
	{
		Debug.Log($"CombinedExample MonitorEyeSensor");
		while (eyeSensorID.HasValue && pixelSensorFeature.GetSensorStatus(eyeSensorID.Value) == PixelSensorStatus.Started)
		{
			foreach (uint stream in configuredEyeStreams)
			{
				Debug.Log($"CombinedExample MonitorEyeSensor stream {stream}");
				if (pixelSensorFeature.GetSensorData(eyeSensorID.Value, stream, out PixelSensorFrame frame, out PixelSensorMetaData[] currentFrameMetaData, Allocator.Temp, shouldFlipTexture: true))
				{
					PixelSensorPlane plane = frame.Planes[0];
					Texture2D texture = eyeTextures[stream]; 
					if (!frame.IsValid || frame.Planes.Length == 0)
						continue;

					if (texture == null)
					{
						texture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.R8, false);
						eyeTextures[stream] = texture;
					}

					texture.LoadRawTextureData(plane.ByteData);
					texture.Apply();

					float unityTime = Time.realtimeSinceStartup;
					DateTimeOffset deviceTime = DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
					Pose sensorPose = pixelSensorFeature.GetSensorPose(eyeSensorID.Value);
					ImageSaver.InitNewFrame(Time.frameCount, unityTime, deviceTime);
					ImageSaver.SaveSensor(texture, $"eye{stream}", unityTime, deviceTime, sensorPose);
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

		if (eyeSensorID.HasValue)
		{
			PixelSensorAsyncOperationResult stopEyeAsyncResult = pixelSensorFeature.StopSensor(eyeSensorID.Value, configuredEyeStreams);
			yield return stopEyeAsyncResult;

			if (stopEyeAsyncResult.DidOperationSucceed)
			{
				pixelSensorFeature.DestroyPixelSensor(eyeSensorID.Value);
				Debug.Log("Eye sensor stopped and destroyed successfully.");
			}
			else
			{
				Debug.LogError("Failed to stop the Eye sensor.");
			}
		}

	}

	public enum LongRangeUpdateRate { OneFps = 1, FiveFps = 5 }
    public enum ShortRangeUpdateRate { FiveFps = 5, ThirtyFps = 30, SixtyFps = 60 }
}