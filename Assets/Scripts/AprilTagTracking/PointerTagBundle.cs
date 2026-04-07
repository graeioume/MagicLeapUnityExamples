namespace UnityEngine.XR.MagicLeap.AprilTagTracking
{
    public class PointerTagBundle
    {
        public TagBundleDefinition Bundle = new TagBundleDefinition();

        /// Constructor
        PointerTagBundle()
        {
            // AprilTag ID 0
            Bundle.TagLocalPoses.Add(0, new TagPoseData{
                Position = new Vector3(0, 0, 0), // meters
                Rotation = new Quaternion(0, 0, 0, 1),
                IsValid = true
            });
            // AprilTag ID 1
            Bundle.TagLocalPoses.Add(1, new TagPoseData{
                Position = new Vector3(0.0295f, 0, 0),  // meters
                Rotation = new Quaternion(0, 0, 0, 1),
                IsValid = true
            });
            // AprilTag ID 2
            Bundle.TagLocalPoses.Add(2, new TagPoseData{
                Position = new Vector3(0.059f, 0, 0), // meters
                Rotation = new Quaternion(0, 0, 0, 1),
                IsValid = true
            });

        }
    }
}