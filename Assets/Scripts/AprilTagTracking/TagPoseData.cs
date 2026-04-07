namespace UnityEngine.XR.MagicLeap.AprilTagTracking
{
    public struct TagPoseData
    {
        public ulong? Id;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsValid;
    }
}