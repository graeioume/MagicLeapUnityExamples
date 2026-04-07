// %BANNER_BEGIN%
// ---------------------------------------------------------------------
// %COPYRIGHT_BEGIN%
// Copyright (c) (2024) Magic Leap, Inc. All Rights Reserved.
// Use of this file is governed by the Software License Agreement, located here: https://www.magicleap.com/software-license-agreement-ml2
// Terms and conditions applicable to third-party materials accompanying this distribution may also be found in the top-level NOTICE file appearing herein.
// %COPYRIGHT_END%
// ---------------------------------------------------------------------
// %BANNER_END%
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.OpenXR;
using MagicLeap.OpenXR.Features.MarkerUnderstanding;

namespace MagicLeap.Examples
{
    public class AprilTagTrackingDemo : MonoBehaviour
    {
        [SerializeField]
        private MarkerVisualizer markerVisualPrefab;

        [SerializeField]
        private Text statusTextDisplay;


        private Dictionary<MarkerDetector, HashSet<MarkerVisualizer>> markerVisuals = new();
        private MagicLeapMarkerUnderstandingFeature markerFeature;
        private MarkerDetectorSettings markerDetectorSettings;

        void Start()
        {
            MarkerDetectorSettings markerDetectorSettings = default;
            markerDetectorSettings.MarkerDetectorProfile = MarkerDetectorProfile.Default;
            markerDetectorSettings.MarkerType = MarkerType.AprilTag;
            markerDetectorSettings.AprilTagSettings.AprilTagType = AprilTagType.Dictionary_36H11;
            markerDetectorSettings.AprilTagSettings.AprilTagLength = 0.02f; // (20mm = 0.02 m)
            markerDetectorSettings.AprilTagSettings.EstimateAprilTagLength = false;
            
            markerFeature = OpenXRSettings.Instance.GetFeature<MagicLeapMarkerUnderstandingFeature>();
            markerFeature.CreateMarkerDetector(markerDetectorSettings);
            markerFeature.UpdateMarkerDetectors();
            markerVisuals = new Dictionary<MarkerDetector, HashSet<MarkerVisualizer>>();
        }

        void Update()
        {
            if (!markerFeature || !markerFeature.enabled)
                return;

            var sb = new StringBuilder($"Marker Detectors Created: {markerFeature.MarkerDetectors.Count}");
            
            if (markerFeature.MarkerDetectors.Count == 0)
            {
                statusTextDisplay.text = sb.ToString();
                return;
            }

            sb.AppendLine("\n");
            
            // Updates the status and data for all actively tracked marker detectors.
            markerFeature.UpdateMarkerDetectors();

            int trackerIndex = 0;
            foreach (var markerDetector in markerFeature.MarkerDetectors)
            {
                sb.AppendLine($"<b>#{trackerIndex} {markerDetector.Settings.MarkerType}/{markerDetector.Settings.MarkerDetectorProfile}</b>");
                sb.AppendLine($"Detected markers: {markerDetector.Data.Count}");
                sb.AppendLine();

                // Find how many marker detectors will need visual representations
                int expectedVisualCount = markerDetector.Data.Count(d => d.MarkerPose != null);
                if (expectedVisualCount > 0 && !markerVisuals.ContainsKey(markerDetector))
                {
                    markerVisuals.Add(markerDetector, new HashSet<MarkerVisualizer>());
                }

                // If there are more visuals than we will need representations for, destroy all visuals for this marker detector
                if (markerVisuals.TryGetValue(markerDetector, out var currentVisualSet))
                {
                    if (currentVisualSet.Count > expectedVisualCount)
                    {
                        foreach (var visual in currentVisualSet)
                            Destroy(visual.gameObject);
                        currentVisualSet.Clear();
                    }
                }
                
                for (int i = 0; i < markerDetector.Data.Count; i++)
                {
                    if (markerDetector.Data[i].MarkerPose != null)
                    {
                        var markerVisual = Instantiate(markerVisualPrefab);
                        if (currentVisualSet != null)
                        {
                            currentVisualSet.Add(markerVisual);
                        }
                        markerVisual.Set(markerDetector.Data[i], markerDetector.Settings.MarkerType);
                    }
                    sb.AppendLine($"<b>Marker {i}</b>");

                    if (markerDetector.Settings.MarkerType == MarkerType.Aruco || markerDetector.Settings.MarkerType == MarkerType.AprilTag)
                    {
                        sb.AppendLine($"Data: {markerDetector.Data[i].MarkerNumber}");
                    }
                    else
                    {
                        sb.AppendLine($"Data: {markerDetector.Data[i].MarkerString}");
                    }

                    sb.AppendLine($"Length: {markerDetector.Data[i].MarkerLength}");

                    if (markerDetector.Settings.MarkerType == MarkerType.QR)
                    {
                        sb.AppendLine($"Reprojection Error: {markerDetector.Data[i].ReprojectionErrorMeters}");
                    }
                }

                if (trackerIndex < markerFeature.MarkerDetectors.Count - 1)
                    sb.AppendLine("--------\n");

                trackerIndex++;
            }

            statusTextDisplay.text = sb.ToString();
        }

        void OnDestroy()
        {
            DestroyMarkerTrackers();
        }


        private void DestroyMarkerTrackers()
        {
            foreach (var markerDetector in markerFeature.MarkerDetectors)
            {
                if (markerVisuals.TryGetValue(markerDetector, out var visuals))
                {
                    foreach (var visual in visuals)
                    {
                        Destroy(visual.gameObject);
                    }
                    visuals.Clear();
                }
            }
            markerVisuals.Clear();
            markerFeature.DestroyAllMarkerDetectors();
        }
    }
}
