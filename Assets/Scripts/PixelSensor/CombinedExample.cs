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

public class CombinedExample : MonoBehaviour
{
    public DepthStreamVisualizer streamVisualizer;

    [Header("False == depth, true == IR light used for depth")]  public bool UseRawDepth = false;
    [Range(0.2f, 5.00f)] public float DepthRange;
    [Header("ShortRange =< 1m")] public ShortRangeUpdateRate SRUpdateRate;
    [Header("LongRange > 1m")] public LongRangeUpdateRate LRUpdateRate;
    private const string depthCameraSensorPath = "/pixelsensor/depth/center";



    private MagicLeapPixelSensorFeature pixelSensorFeature;
    private PixelSensorId? sensorId;
    private List<uint> configuredStreams = new List<uint>();
    public uint targetStream => DepthRange > 1.0f ? (uint)0 : (uint)1;

    void Start()
    {
        pixelSensorFeature = OpenXRSettings.Instance.GetFeature<MagicLeapPixelSensorFeature>();
        if (pixelSensorFeature == null || !pixelSensorFeature.enabled)
        {
            Debug.LogError("Pixel Sensor Feature not found or not enabled!");
            enabled = false;
            return;
        }
        
        Permissions.RequestPermission(MLPermission.DepthCamera,
            (string permission) => { // granted
                if (permission.Contains(MLPermission.DepthCamera))
                    FindAndInitializeSensor();
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

    private void FindAndInitializeSensor()
    {
		List<PixelSensorId> sensors = pixelSensorFeature.GetSupportedSensors();

        foreach (PixelSensorId sensor in sensors)
        {
            Debug.Log("Sensor Name Found: " + sensor.XrPathString);
            if (sensor.XrPathString.Contains(depthCameraSensorPath))
            {
                sensorId = sensor;
                break;
            }
        }

        if (!sensorId.HasValue)
        {
            Debug.LogError($"`{depthCameraSensorPath}` sensor not found.");
            return;
        }

        // Subscribe to the Availability changed callback if the sensor becomes available.
        pixelSensorFeature.OnSensorAvailabilityChanged += (PixelSensorId id, bool available) => {
            if (sensorId.HasValue && id == sensorId && available)
            {
                Debug.Log("Sensor became available.");
                TryInitializeSensor();
            }
        };

        TryInitializeSensor();
    }

    private void TryInitializeSensor()
    {
        if (sensorId.HasValue && pixelSensorFeature.GetSensorStatus(sensorId.Value) ==
            PixelSensorStatus.Undefined && pixelSensorFeature.CreatePixelSensor(sensorId.Value))
        {
            Debug.Log("Sensor created successfully.");
            ConfigureSensorStreams();
        }
        else
        {
            Debug.LogWarning("Failed to create sensor. Will retry when it becomes available.");
        }
    }

    // The capabilities that the script will edit
    private PixelSensorCapabilityType[] targetCapabilityTypes = new[]
    {
        PixelSensorCapabilityType.UpdateRate,
        PixelSensorCapabilityType.Format,
        PixelSensorCapabilityType.Resolution,
        PixelSensorCapabilityType.Depth,
    };


    private void ConfigureSensorStreams()
    {
        if (!sensorId.HasValue)
        {
            Debug.LogError("Sensor ID not set.");
            return;
        }

        uint streamCount = pixelSensorFeature.GetStreamCount(sensorId.Value);
        if (streamCount < 1)
        {
            Debug.LogError("Expected at least one stream from the sensor.");
            return;
        }

        // Only add the target
        configuredStreams.Add(targetStream);
        pixelSensorFeature.GetPixelSensorCapabilities(sensorId.Value, targetStream, out PixelSensorCapability[] capabilities);
        foreach (PixelSensorCapability pixelSensorCapability in capabilities)
        {
            if (!targetCapabilityTypes.Contains(pixelSensorCapability.CapabilityType))
                continue;

            // More details about the capability
            if (pixelSensorFeature.QueryPixelSensorCapability(sensorId.Value, pixelSensorCapability.CapabilityType, targetStream, out PixelSensorCapabilityRange range) && range.IsValid)
            {
                if (range.CapabilityType == PixelSensorCapabilityType.UpdateRate)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.IntValue = DepthRange > 1 ? (uint)LRUpdateRate : (uint)SRUpdateRate;
                    pixelSensorFeature.ApplySensorConfig(sensorId.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Format)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.IntValue = (uint)range.FrameFormats[UseRawDepth ? 1 : 0];
                    pixelSensorFeature.ApplySensorConfig(sensorId.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Resolution)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.VectorValue = range.ExtentValues[0];
                    pixelSensorFeature.ApplySensorConfig(sensorId.Value, configData);
                }
                else if (range.CapabilityType == PixelSensorCapabilityType.Depth)
                {
					PixelSensorConfigData configData = new PixelSensorConfigData(range.CapabilityType, targetStream);
                    configData.FloatValue = DepthRange;
                    pixelSensorFeature.ApplySensorConfig(sensorId.Value, configData);
                }
            }
        }

        StartCoroutine(ConfigureStreamsAndStartSensor());
    }

    private IEnumerator ConfigureStreamsAndStartSensor()
    {
		PixelSensorAsyncOperationResult configureOperation = pixelSensorFeature.ConfigureSensor(sensorId.Value, configuredStreams.ToArray());

        yield return configureOperation;
        if (configureOperation.DidOperationSucceed)
        {
            Debug.Log("Sensor configured with defaults successfully.");
        }
        else
        {
            Debug.LogError("Failed to configure sensor.");
            yield break;
        }


        Dictionary<uint, PixelSensorMetaDataType[]> supportedMetadataTypes = new Dictionary<uint, PixelSensorMetaDataType[]>();
        foreach (uint stream in configuredStreams)
            if (pixelSensorFeature.EnumeratePixelSensorMetaDataTypes(sensorId.Value, stream, out PixelSensorMetaDataType[] metaDataTypes))
                supportedMetadataTypes[stream] = metaDataTypes;

        // Assuming that `configuredStreams` is correctly populated with the intended stream indices
        PixelSensorAsyncOperationResult startOperation = pixelSensorFeature.StartSensor(sensorId.Value, configuredStreams, supportedMetadataTypes);
        yield return startOperation;

        if (startOperation.DidOperationSucceed)
        {
            Debug.Log("Sensor started successfully. Monitoring data...");
            StartCoroutine(MonitorSensorData());
        }
        else
        {
            Debug.LogError("Failed to start sensor.");
        }
    }

    private IEnumerator MonitorSensorData()
    {
        Quaternion frameRotation = pixelSensorFeature.GetSensorFrameRotation(sensorId.Value);

        streamVisualizer.Initialize(targetStream, frameRotation, pixelSensorFeature, sensorId.Value);
        while (pixelSensorFeature.GetSensorStatus(sensorId.Value) == PixelSensorStatus.Started)
        {
            foreach (uint stream in configuredStreams)
            {
                if (pixelSensorFeature.GetSensorData(sensorId.Value, stream, out PixelSensorFrame frame, out PixelSensorMetaData[] metaData, Allocator.Temp, shouldFlipTexture: true))
                {
                    Pose devicePose = pixelSensorFeature.GetSensorPose(sensorId.Value);
                    DateTimeOffset deviceTime = DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
                    float unityTime = Time.realtimeSinceStartup;
                    ImageSaver.InitNewFrame(Time.frameCount, unityTime, deviceTime, devicePose);

					Texture2D depth = streamVisualizer.ProcessFrame(frame);
                    ImageSaver.SaveSensor(depth, "depth", unityTime, deviceTime, devicePose);

					PixelSensorDepthConfidenceBuffer confidenceMetadata = metaData.OfType<PixelSensorDepthConfidenceBuffer>().FirstOrDefault();
                    if (confidenceMetadata != null)
                    {
						Texture2D confidence = streamVisualizer.ProcessDepthConfidenceData(in confidenceMetadata);
						ImageSaver.SaveSensor(confidence, "confidence", unityTime, deviceTime, devicePose);
					}

					PixelSensorDepthFlagBuffer flagMetadata = metaData.OfType<PixelSensorDepthFlagBuffer>().FirstOrDefault();
					if (flagMetadata != null)
					{
						Texture2D flags = streamVisualizer.ProcessDepthFlagData(in flagMetadata);
						ImageSaver.SaveSensor(flags, "flags", unityTime, deviceTime, devicePose);
					}
				}

                ImageSaver.CloseLastFile();
				yield return null;
            }
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
        if (sensorId.HasValue)
        {
            PixelSensorAsyncOperationResult stopSensorAsyncResult = pixelSensorFeature.StopSensor(sensorId.Value, configuredStreams);
            yield return stopSensorAsyncResult;

            if (stopSensorAsyncResult.DidOperationSucceed)
            {
                Debug.Log("Sensor stopped successfully.");
                pixelSensorFeature.ClearAllAppliedConfigs(sensorId.Value);
                // Free the sensor so it can be marked available and used in other scripts.
                pixelSensorFeature.DestroyPixelSensor(sensorId.Value);
            }
            else
            {
                Debug.LogError("Failed to stop the sensor.");
            }
        }
    }

    public enum LongRangeUpdateRate { OneFps = 1, FiveFps = 5 }
    public enum ShortRangeUpdateRate { FiveFps = 5, ThirtyFps = 30, SixtyFps = 60 }
}