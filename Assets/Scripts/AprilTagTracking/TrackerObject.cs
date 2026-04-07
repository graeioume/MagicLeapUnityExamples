using System;

namespace UnityEngine.XR.MagicLeap.AprilTagTracking
{
    public class TrackerObject : MonoBehaviour
    {
        [SerializeField] TrackerType trackerType;
        public enum TrackerType
        {
            Pointer,
            Hook,
            Femur,
            Tibia
        }
        
        [SerializeField] private Vector3 offset;
        
        private void Update()
        {
        }

        public void SetMarkerPose(Pose? inputPose)
        {
            if (!inputPose.HasValue)
                return;
            transform.SetPositionAndRotation( inputPose.Value.position + offset, inputPose.Value.rotation);
        }
    }
}