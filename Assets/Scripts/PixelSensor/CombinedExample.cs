using MagicLeap.Android;
using MagicLeap.OpenXR.Features.EyeTracker;
using MagicLeap.OpenXR.Features.PixelSensors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
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
    private uint depthRangeToUse => LongRange ? (uint)0 : (uint)1;

    // world
	private Texture2D[] worldTextures;
	// eye
	private MagicLeapEyeTrackerFeature eyeTrackerFeature;
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
	private List<string> grantedPermissions = new();

	
    void Start()
    {
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
            Permissions.EyeCamera,
			Permissions.EyeTracking,
			Permissions.PupilSize,
        };

	    Permissions.RequestPermissions(allRequiredPermissions,
            (string permission) => { // granted
				Debug.Log($"CombinedExample Permission Granted: {permission}");
				grantedPermissions.Add(permission);

                InitSensors();
			},
            (string permission) =>{ // denied
                Debug.LogError($"Permission {permission} not granted. Script will not work.");
                enabled = false;
            },
            (string permission) => { // denied
                Debug.LogError($"Permission {permission} not granted. Script will not work.");
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

			if (eyeSensorID == null && sensor.XrPathString.Contains("/pixelsensor/eye/"))
				eyeSensorID = sensor;

			if (grantedPermissions.Contains(Permissions.EyeTracking) && grantedPermissions.Contains(Permissions.PupilSize))
			{
                eyeTrackerFeature = OpenXRSettings.Instance.GetFeature<MagicLeapEyeTrackerFeature>();
                eyeTrackerFeature.CreateEyeTracker();
            }
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
		PixelSensorStatus statuc = pixelSensorFeature.GetSensorStatus(rgbSensorID.Value);
		if (statuc != PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(rgbSensorID.Value))
		{
			Debug.LogWarning($"Failed to Init RGB sensor. Current Status: {statuc}");
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

 //       if (pixelSensorFeature.GetSensorStatus(rgbSensorID.Value) == PixelSensorStatus.NotConfigured)
	//	{
	//		pixelSensorFeature.GetPixelSensorCapabilities(rgbSensorID.Value, 1, out PixelSensorCapability[] capabilities);
	//		PixelSensorCapabilityType[] targetCapabilityTypes = new[]
	//		{
	//			PixelSensorCapabilityType.UpdateRate,
	//			PixelSensorCapabilityType.Resolution,
	//			PixelSensorCapabilityType.Format
	//		};

	//		foreach (PixelSensorCapability pixelSensorCapability in capabilities)
	//		{
	//			if (!targetCapabilityTypes.Contains(pixelSensorCapability.CapabilityType))
	//				continue;

	//			if (!pixelSensorFeature.QueryPixelSensorCapability(rgbSensorID.Value, pixelSensorCapability.CapabilityType, 1, out PixelSensorCapabilityRange range) || !range.IsValid)
	//				continue;

	//			if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate)
	//				pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.UpdateRate, 30, 1);
	//			else if (range.CapabilityType == PixelSensorCapabilityType.Format)
	//				pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.Format, (uint)PixelSensorFrameFormat.Rgba8888, 1);
	//			else if (range.CapabilityType == PixelSensorCapabilityType.Resolution)
	//				pixelSensorFeature.ApplySensorConfig(rgbSensorID.Value, PixelSensorCapabilityType.Resolution, new Vector2Int(1280, 720), 1);
	//		}
	//	}

		StartCoroutine(StartRGBStream());
	}

	private IEnumerator StartRGBStream()
	{
		Debug.Log($"CombinedExample StartRGBStream");
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensorWithDefaultCapabilities(rgbSensorID.Value, configuredRGBStreams.ToArray());

		yield return configureOperation;
		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure RGB sensor.");
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
		PixelSensorStatus statuc = pixelSensorFeature.GetSensorStatus(depthSensorID.Value);
		if (statuc != PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(depthSensorID.Value))
		{
			Debug.LogWarning($"Failed to Init Depth sensor. Current Status: {statuc}");
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
		configuredDepthStreams.Add(depthRangeToUse);
        pixelSensorFeature.GetPixelSensorCapabilities(depthSensorID.Value, depthRangeToUse, out PixelSensorCapability[] capabilities);
        foreach (PixelSensorCapability pixelSensorCapability in capabilities)
        {
            if (pixelSensorFeature.QueryPixelSensorCapability(depthSensorID.Value, pixelSensorCapability.CapabilityType, depthRangeToUse, out PixelSensorCapabilityRange range) && range.IsValid)
            {
                if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, depthRangeToUse);
                    configData.IntValue = LongRange ? (uint)LRUpdateRate : (uint)SRUpdateRate;
                    pixelSensorFeature.ApplySensorConfig(depthSensorID.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Format)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, depthRangeToUse);
                    configData.IntValue = (uint)range.FrameFormats[UseRawDepth ? 1 : 0];
                    pixelSensorFeature.ApplySensorConfig(depthSensorID.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Resolution)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, depthRangeToUse);
                    configData.VectorValue = range.ExtentValues[0];
                    pixelSensorFeature.ApplySensorConfig(depthSensorID.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Depth)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, depthRangeToUse);
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
		PixelSensorStatus statuc = pixelSensorFeature.GetSensorStatus(worldSensorID.Value);
		if (statuc != PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(worldSensorID.Value))
		{
			Debug.LogWarning($"Failed to Init World sensor. Current Status: {statuc}");
			return;
		}

		Debug.Log("World sensor created successfully.");

		uint streamCount = pixelSensorFeature.GetStreamCount(worldSensorID.Value);
		Debug.Log($"World sensor has {streamCount} stream(s).");
		worldTextures = new Texture2D[streamCount];

		configuredWorldStreams.Clear();
		if (streamCount > 0)
			configuredWorldStreams.Add(0);
		//if (streamCount > 1)
		//	configuredWorldStreams.Add(1);

		//Debug.Log("CombinedExample ConfigureWorldStreamsManually()");
		//if (pixelSensorFeature.GetSensorStatus(worldSensorID.Value) == PixelSensorStatus.NotConfigured)
		//{
		//	pixelSensorFeature.GetPixelSensorCapabilities(rgbSensorID.Value, 1, out PixelSensorCapability[] capabilities);
		//	PixelSensorCapabilityType[] targetCapabilityTypes = new[]
		//	{
		//		PixelSensorCapabilityType.UpdateRate,
		//		PixelSensorCapabilityType.Resolution,
		//		PixelSensorCapabilityType.Format,
		//		PixelSensorCapabilityType.AutoExposureMode
		//	};

		//	foreach (PixelSensorCapability pixelSensorCapability in capabilities)
		//	{
		//		if (!targetCapabilityTypes.Contains(pixelSensorCapability.CapabilityType))
		//			continue;

		//		if (!pixelSensorFeature.QueryPixelSensorCapability(worldSensorID.Value, pixelSensorCapability.CapabilityType, 0, out PixelSensorCapabilityRange range) || !range.IsValid)
		//			continue;
		//		if (!pixelSensorFeature.QueryPixelSensorCapability(worldSensorID.Value, pixelSensorCapability.CapabilityType, 1, out PixelSensorCapabilityRange range2) || !range.IsValid)
		//			continue;

		//		Debug.Log($"Updating world sensor {pixelSensorCapability.CapabilityType}");
		//		if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate)
		//		{
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.UpdateRate, 30, 0);
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.UpdateRate, 30, 1);
		//		}
		//		else if (range.CapabilityType == PixelSensorCapabilityType.Format)
		//		{
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Format, (uint)PixelSensorFrameFormat.Grayscale, 0);
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Format, (uint)PixelSensorFrameFormat.Grayscale, 1);
		//		}
		//		else if (range.CapabilityType == PixelSensorCapabilityType.Resolution)
		//		{
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Resolution, new Vector2Int(1016, 1016), 0);
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, PixelSensorCapabilityType.Resolution, new Vector2Int(1016, 1016), 1);
		//		}
		//		else if (range.CapabilityType == PixelSensorCapabilityType.AutoExposureMode)
		//		{
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, new PixelSensorConfigData(PixelSensorCapabilityType.AutoExposureMode, 0) { ExposureMode = PixelSensorAutoExposureMode.EnvironmentTracking });
		//			pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, new PixelSensorConfigData(PixelSensorCapabilityType.AutoExposureMode, 1) { ExposureMode = PixelSensorAutoExposureMode.ProximityIrTracking });
		//		}
		//	}
		//}

		// Start the custom configuration
		StartCoroutine(StartWorldStream());
	}

	private IEnumerator StartWorldStream()
	{
        Debug.Log("CombinedExample ConfigureWorldStreamsManually()");
        var targetCapabilityTypes = new PixelSensorCapabilityType[]
        {
            PixelSensorCapabilityType.AutoExposureMode,
            //PixelSensorCapabilityType.AutoExposureTargetBrightness,
        };

        foreach (uint streamIndex in configuredWorldStreams)
        {
            // Iterate through each of the target capabilities and try to find it in the sensors availible capabilities, then set it's value.
            // When configuring a sensor capabilities have to be configured iteratively since each applied configuration can impact other capabilities.
            // Step 1 : Itereate through each of the target capabilities
            for (var index = 0; index < targetCapabilityTypes.Length; index++)
            {
                PixelSensorCapabilityType pixelSensorCapability = targetCapabilityTypes[index];
				Debug.Log($"World Cam configuring {pixelSensorCapability} for stream {streamIndex}");

                // Get the sensors capabilities based on the previous applied settings.
                pixelSensorFeature.GetPixelSensorCapabilities(worldSensorID.Value, streamIndex, out PixelSensorCapability[] capabilities);
				Debug.Log($"World Stream {streamIndex} available capabilities: {string.Join(", ", capabilities.Select(x => x.CapabilityType))}");

                // Step2 Try to find a capability of the same type in the sensor
                PixelSensorCapability targetAbility = capabilities.FirstOrDefault(x => x.CapabilityType == pixelSensorCapability);
                // Verify that it was found - A null check would not work because it is a struct.
                if (targetAbility.CapabilityType == pixelSensorCapability)
                {
                    // Once found, we query the valid range of the capability
                    if (pixelSensorFeature.QueryPixelSensorCapability(worldSensorID.Value, targetAbility.CapabilityType, streamIndex, out PixelSensorCapabilityRange range) && range.IsValid)
                    {
                        // If the capability is required, we use the default value.
                        // This is because these values cannot be configure on the world cameras.
                        // UpdateRate = 30, Format = Grayscale, Resolution = 1016x1016
                        if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate
                            || range.CapabilityType == PixelSensorCapabilityType.Format
                            || range.CapabilityType == PixelSensorCapabilityType.Resolution)
                        {
                            pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, range.GetDefaultConfig(streamIndex));
                            yield return null;
                        }

                        const uint AutoExposure = 1; // nearIR
                        const float AutoExposureTargetBrightness = 1f;
                        const int ManualExposureTime = 50;
                        const int AnalogGain = 100;
                        // Custom Capability settings based on the settings of the Script (AutoExposure vs Manual Exposure)
                        // Auto Exposure: Auto Exposure Mode (Controller / Enviornment) ,  AutoExposure Target (-5.0 to 5.0)
                        if (range.CapabilityType == PixelSensorCapabilityType.AutoExposureMode)
                        {
                            var configData = new PixelSensorConfigData(range.CapabilityType, streamIndex);
                            Debug.Log($"World Camera AutoExposureMode range: {string.Join(", ", range.IntValues)}");
                            if (range.IntValues.Contains(AutoExposure))
                            {
                                configData.IntValue = AutoExposure;
                                pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, configData);
                                yield return null;
                            }
                        } 
						else if (range.CapabilityType == PixelSensorCapabilityType.AutoExposureTargetBrightness)
                        {
                            var configData = new PixelSensorConfigData(range.CapabilityType, streamIndex);
                            configData.FloatValue = Mathf.Clamp(AutoExposureTargetBrightness, range.FloatRange.Value.Min, range.FloatRange.Value.Max);
                            pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, configData);
                            yield return null;
                        } 
						else if (range.CapabilityType == PixelSensorCapabilityType.ManualExposureTime)
                        {
                            var configData = new PixelSensorConfigData(range.CapabilityType, streamIndex);
                            configData.IntValue = (uint)Mathf.Clamp(ManualExposureTime, range.IntRange.Value.Min, range.IntRange.Value.Max);
                            pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, configData);
                            yield return null;
                        }
						else if (range.CapabilityType == PixelSensorCapabilityType.AnalogGain)
                        {
                            var configData = new PixelSensorConfigData(range.CapabilityType, streamIndex);
                            configData.IntValue = (uint)Mathf.Clamp((uint)AnalogGain, range.IntRange.Value.Min, range.IntRange.Value.Max);
                            pixelSensorFeature.ApplySensorConfig(worldSensorID.Value, configData);
                            yield return null;
                        }
                    }
                }
            }
        }

        Debug.Log("CombinedExample Calling ConfigureSensor for World sensor...");
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensor(worldSensorID.Value, configuredWorldStreams.ToArray());
		yield return configureOperation;


		if (!configureOperation.DidOperationSucceed)
		{
			Debug.LogError("Failed to configure World sensor streams for near IR usage.");
			yield break;
		}
		Debug.Log("World sensor configured successfully with custom settings.");

		Dictionary<uint, PixelSensorMetaDataType[]> supportedMetadataTypes = new();
		foreach (uint stream in configuredWorldStreams)
		{
            if (pixelSensorFeature.EnumeratePixelSensorMetaDataTypes(worldSensorID.Value, stream, out var metaDataTypes))
            {
                Debug.Log($"World Cam: Obtaining supported metadata types: {string.Join(", ", metaDataTypes)}");
                supportedMetadataTypes.Add(stream, metaDataTypes);
            }
        }

        Debug.Log("Starting World sensor...");
        PixelSensorAsyncOperationResult startOperation =
            pixelSensorFeature.StartSensor(worldSensorID.Value, configuredWorldStreams, supportedMetadataTypes);
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
		PixelSensorStatus statuc = pixelSensorFeature.GetSensorStatus(eyeSensorID.Value);
		if (statuc != PixelSensorStatus.Undefined || !pixelSensorFeature.CreatePixelSensor(eyeSensorID.Value))
		{
			Debug.LogWarning($"Failed to Init Eye sensor. Current Status: {statuc}");
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
		foreach (uint streamIndex in configuredEyeStreams)
		{	
			pixelSensorFeature.GetPixelSensorCapabilities(eyeSensorID.Value, streamIndex, out PixelSensorCapability[] capabilities);
			Debug.Log($"Eye Stream {streamIndex} available capabilities: {string.Join(", ", capabilities.Select(x => x.CapabilityType))}");
		}

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

					Debug.Log($"RGB Plane: w{plane.Width} h{plane.Height} d{plane.BytesPerPixel}");
					if (!texture)
						texture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.RGB24, false);
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

        streamVisualizer.Initialize(depthRangeToUse, frameRotation, pixelSensorFeature, depthSensorID.Value);
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

					if (!texture)
						texture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.R8, false);
					texture.LoadRawTextureData(plane.ByteData);
					texture.Apply();

					StringBuilder meta = new StringBuilder(1024);
					meta.AppendLine();
                    foreach (PixelSensorMetaData metaData in currentFrameMetaData)
                    {
                        switch (metaData)
                        {
                            case PixelSensorAnalogGain analogGain:
                                meta.AppendLine($"AnalogGain: {analogGain.AnalogGain}");
                                break;
                            case PixelSensorDigitalGain digitalGain:
                                meta.AppendLine($"DigitalGain: {digitalGain.DigitalGain}");
                                break;
                            case PixelSensorExposureTime exposureTime:
                                meta.AppendLine($"ExposureTime: {exposureTime.ExposureTime:F1}");
                                break;
                            case PixelSensorFisheyeIntrinsics fisheyeIntrinsics:
                                {
                                    meta.AppendLine($"FOV: {fisheyeIntrinsics.FOV}");
                                    meta.AppendLine($"Focal Length: {fisheyeIntrinsics.FocalLength}");
                                    meta.AppendLine($"Principal Point: {fisheyeIntrinsics.PrincipalPoint}");
                                    meta.AppendLine(
                                        $"Radial Distortion: [{string.Join(',', fisheyeIntrinsics.RadialDistortion.Select(val => val.ToString("F1")))}]");
                                    meta.AppendLine(
                                        $"Tangential Distortion: [{string.Join(',', fisheyeIntrinsics.TangentialDistortion.Select(val => val.ToString("F1")))}]");
                                    break;
                                }
                        }
                    }


                    float unityTime = Time.realtimeSinceStartup;
					DateTimeOffset deviceTime = DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
					Pose sensorPose = pixelSensorFeature.GetSensorPose(worldSensorID.Value);
					ImageSaver.InitNewFrame(Time.frameCount, unityTime, deviceTime);
					ImageSaver.SaveSensor(texture, $"world{stream}", unityTime, deviceTime, sensorPose, meta.ToString());
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

					if (!texture)
					{
						texture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.R8, false);
						eyeTextures[stream] = texture;
					}

					texture.LoadRawTextureData(plane.ByteData);
					texture.Apply();

					float unityTime = Time.realtimeSinceStartup;
					DateTimeOffset deviceTime = DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
					Pose sensorPose = default; // pixelSensorFeature.GetSensorPose(eyeSensorID.Value);
                    EyeTrackerData eyeTrackerData = eyeTrackerFeature.GetEyeTrackerData();
					StringBuilder data = new(1024);
					for (int i = 0; i < eyeTrackerData.PupilData.Length; i++)
					{
                        data.AppendLine($"Pupil {i} Eye:      {eyeTrackerData.PupilData[i].Eye}");
                        data.AppendLine($"Pupil {i} Diameter: {eyeTrackerData.PupilData[i].PupilDiameter}");
                    }

                    data.AppendLine($"Gaze Behaviour:          {eyeTrackerData.GazeBehaviorData.GazeBehaviorType}");
                    data.AppendLine($"Gaze Behaviour Duration: {eyeTrackerData.GazeBehaviorData.Duration}");
                    data.AppendLine($"Gaze Behaviour Time:     {eyeTrackerData.GazeBehaviorData.Time}");
                    data.AppendLine($"Gaze Behaviour Onset:    {eyeTrackerData.GazeBehaviorData.OnsetTime}");
                    data.AppendLine($"Gaze Behaviour Metadata Valid:     {eyeTrackerData.GazeBehaviorData.MetaData.Valid}");
                    data.AppendLine($"Gaze Behaviour Metadata Amplitude: {eyeTrackerData.GazeBehaviorData.MetaData.Amplitude}");
                    data.AppendLine($"Gaze Behaviour Metadata Direction: {eyeTrackerData.GazeBehaviorData.MetaData.Direction}");
                    data.AppendLine($"Gaze Behaviour Metadata Velocity:  {eyeTrackerData.GazeBehaviorData.MetaData.Velocity}");

                    data.AppendLine($"Gaze Position:        {eyeTrackerData.PosesData.GazePose.Pose.position}");
                    data.AppendLine($"Gaze Rotation:        {eyeTrackerData.PosesData.GazePose.Pose.rotation}");
                    data.AppendLine($"Gaze Duration:        {eyeTrackerData.PosesData.GazePose.Time}");
                    data.AppendLine($"Gaze Confidence:      {eyeTrackerData.PosesData.GazePose.Confidence}");
                    data.AppendLine($"Fixation Position:    {eyeTrackerData.PosesData.FixationPose.Pose.position}");
                    data.AppendLine($"Fixation Rotation:    {eyeTrackerData.PosesData.FixationPose.Pose.rotation}");
                    data.AppendLine($"Fixation Duration:    {eyeTrackerData.PosesData.FixationPose.Time}");
                    data.AppendLine($"Fixation Confidence:  {eyeTrackerData.PosesData.FixationPose.Confidence}");
                    data.AppendLine($"Left_Eye Position:    {eyeTrackerData.PosesData.RightPose.Pose.position}");
                    data.AppendLine($"Left_Eye Rotation:    {eyeTrackerData.PosesData.RightPose.Pose.rotation}");
                    data.AppendLine($"Left_Eye Duration:    {eyeTrackerData.PosesData.RightPose.Time}");
                    data.AppendLine($"Left_Eye Confidence:  {eyeTrackerData.PosesData.RightPose.Confidence}");
                    data.AppendLine($"Right_Eye Position:   {eyeTrackerData.PosesData.LeftPose.Pose.position}");
                    data.AppendLine($"Right_Eye Rotation:   {eyeTrackerData.PosesData.LeftPose.Pose.rotation}");
                    data.AppendLine($"Right_Eye Duration:   {eyeTrackerData.PosesData.LeftPose.Time}");
                    data.AppendLine($"Right_Eye Confidence: {eyeTrackerData.PosesData.LeftPose.Confidence}");

                    ImageSaver.InitNewFrame(Time.frameCount, unityTime, deviceTime);
					ImageSaver.SaveSensor(texture, $"eye{stream}", unityTime, deviceTime, sensorPose, data.ToString());
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

        eyeTrackerFeature?.DestroyEyeTracker();
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