using System.Collections.Generic;

namespace UnityEngine.XR.MagicLeap.AprilTagTracking
{
    [System.Serializable]
    public class TagBundleDefinition
    {
        public string Name;

        // Tag ID → local pose in bundle frame
        public Dictionary<ulong?, TagPoseData> TagLocalPoses = new();
    }
    
}