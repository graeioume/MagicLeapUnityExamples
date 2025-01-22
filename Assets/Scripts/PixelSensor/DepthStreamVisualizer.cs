using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MagicLeap.OpenXR.Features.PixelSensors;

public class DepthStreamVisualizer : MonoBehaviour
{
    public Renderer TargetRenderer;

    public Material DepthMaterial;

    private Texture2D depthConfidenceTexture;
    private Texture2D depthFlagColorKeyTexture;
    private Texture2D depthFlagTexture;

    private Texture2D targetTexture;

    private int metadataTextureKey;
    private int depthFlagTextureKey;
    private int depthBufferKey;
    private int maxDepthKey;
    private int minDepthKey;

    private float minDepth;
    private float maxDepth = 5;

    public enum DepthMode
    {
        Depth,
        DepthWithConfidence,
        DepthWithFlags,
    }

    public DepthMode currentDepthMode = DepthMode.Depth;

    void Start()
    {
        metadataTextureKey = Shader.PropertyToID("_MetadataTex");
        maxDepthKey = Shader.PropertyToID("_MaxDepth");
        minDepthKey = Shader.PropertyToID("_MinDepth");
        depthBufferKey = Shader.PropertyToID("_Buffer");
        depthFlagTextureKey = Shader.PropertyToID("_FlagTex");

        InitializeDepthFlagColorKey();
    }


    private void OnDestroy()
    {
        Destroy(depthFlagColorKeyTexture);
        Destroy(targetTexture);
        Destroy(depthConfidenceTexture);
        Destroy(depthFlagTexture);
    }

    public void Initialize(uint streamId, Quaternion frameRotation, MagicLeapPixelSensorFeature pixelSensorFeature, PixelSensorId sensorType)
    {
        TargetRenderer.gameObject.transform.rotation *= frameRotation;

        if (pixelSensorFeature.QueryPixelSensorCapability(sensorType, PixelSensorCapabilityType.Depth, streamId, out var range))
        {
            if (range.IntRange.HasValue)
            {
                minDepth = range.IntRange.Value.Min;
                maxDepth = range.IntRange.Value.Max;
            }

            if (range.FloatRange.HasValue)
            {
                minDepth = range.FloatRange.Value.Min;
                maxDepth = range.FloatRange.Value.Max;
            }

            Debug.Log($"Ygor: minDepth {minDepth} maxDepth {maxDepth}, <3 ");
        }
    }

    public Texture2D ProcessFrame(in PixelSensorFrame frame)
    {
        if (!frame.IsValid || TargetRenderer == null || frame.Planes.Length == 0)
			return null;

		// You can obtain the capture time as well. Note it is returned as a long and needs to be converted.
		// ie : DateTimeOffset.FromUnixTimeMilliseconds(frame.CaptureTime / 1000);
		if (targetTexture == null)
        {
            var frameType = frame.FrameType;
            ref var plane = ref frame.Planes[0];
            targetTexture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.RFloat, false);

            var materialToUse = DepthMaterial;
            TargetRenderer.material = materialToUse;
            TargetRenderer.material.mainTexture = targetTexture;
			TargetRenderer.material.SetFloat(maxDepthKey, maxDepth);
			TargetRenderer.material.SetFloat(minDepthKey, minDepth);
			TargetRenderer.material.SetInt(depthBufferKey, (int)currentDepthMode);
			TargetRenderer.material.SetTexture(depthFlagTextureKey, depthFlagColorKeyTexture);
			TargetRenderer.material.SetTexture(metadataTextureKey, Texture2D.whiteTexture);
		}
       
        targetTexture.LoadRawTextureData(frame.Planes[0].ByteData);
        targetTexture.Apply();
        return targetTexture;
    }

    public Texture2D ProcessDepthConfidenceData(in PixelSensorDepthConfidenceBuffer confidenceBuffer)
    {
        var frame = confidenceBuffer.Frame;
        if (!frame.IsValid || TargetRenderer == null || frame.Planes.Length == 0)
			return null;

		if (depthConfidenceTexture == null)
        {
            ref var plane = ref frame.Planes[0];
            depthConfidenceTexture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.RFloat, false);
            TargetRenderer.material.SetTexture(metadataTextureKey, depthConfidenceTexture);
        }

        depthConfidenceTexture.LoadRawTextureData(frame.Planes[0].ByteData);
        depthConfidenceTexture.Apply();
        return depthConfidenceTexture;
    }

    public Texture2D ProcessDepthFlagData(in PixelSensorDepthFlagBuffer flagBuffer)
    {
        var frame = flagBuffer.Frame;
        if (!frame.IsValid || TargetRenderer == null || frame.Planes.Length == 0)
            return null;

		if (depthFlagTexture == null)
        {
            ref var plane = ref frame.Planes[0];
            depthFlagTexture = new Texture2D((int)plane.Width, (int)plane.Height, TextureFormat.RFloat, false);
            TargetRenderer.material.SetTexture(metadataTextureKey, depthFlagTexture);
        }

        depthFlagTexture.LoadRawTextureData(frame.Planes[0].ByteData);
        depthFlagTexture.Apply();
        return depthFlagTexture;
    }

    // Create a texture that has the keys for each of the depth flags. This will be applied in the material
    private void InitializeDepthFlagColorKey()
    {
        depthFlagColorKeyTexture = new Texture2D(16, 1, TextureFormat.RGBA32, false);

        Dictionary<int, Color> depthValueTable = new Dictionary<int, Color>()
        {
            {(int)PixelSensorDepthFlags.Valid,  Color.white},
            {(int)PixelSensorDepthFlags.Invalid, Color.black},
            {(int)PixelSensorDepthFlags.Saturated, Color.yellow},
            {(int)PixelSensorDepthFlags.Inconsistent, Color.red},
            {(int)PixelSensorDepthFlags.LowSignal, Color.magenta},
            {(int)PixelSensorDepthFlags.FlyingPixel, Color.cyan},
            {(int)PixelSensorDepthFlags.MaskedBit, Color.black},
            {(int)PixelSensorDepthFlags.Sbi, Color.gray},
            {(int)PixelSensorDepthFlags.StrayLight, Color.blue},
            {(int)PixelSensorDepthFlags.ConnectedComponents, Color.green},
        };

        var appliedColors = new Color[16];
        for (var i = 0; i < 16; i++)
        {
            var enumValue = (int)Mathf.Pow(2, i);
            if (depthValueTable.TryGetValue(enumValue, out var color))
            {
                appliedColors[i] = color;
            }
            else
            {
                appliedColors[i] = Color.white;
            }
        }

        depthFlagColorKeyTexture.SetPixels(appliedColors);
        depthFlagColorKeyTexture.Apply();
    }

	private void ReadDepthFlag()
	{
		var pixels = depthFlagTexture.GetPixels();
		for (int i = 0; i < pixels.Length; i++)
		{
			// Extract the red channel as it holds the flag data
			float flagValue = pixels[i].r;
			int flags = Mathf.FloorToInt(flagValue * 255);

			// Process the flag value to determine what flags are set
			//ProcessFlag((PixelSensorDepthFlags)flags, i);
		}
	}
}