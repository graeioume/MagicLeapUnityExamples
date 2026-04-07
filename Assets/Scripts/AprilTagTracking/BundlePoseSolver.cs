using System.Collections.Generic;

namespace UnityEngine.XR.MagicLeap.AprilTagTracking
{
    public class BundlePoseSolver
    {
        /// <summary>
        /// Computes the average pose of all detected markers in the bundle.
        /// </summary>
        /// <param name="bundle">Known tag pose in bundle</param>
        /// <param name="detections">Observed tag pose in world</param>
        /// <returns></returns>
        // public TagPoseData Solve(TagBundleDefinition bundle, List<DetectedMarker> detections)
        public TagPoseData Solve(TagBundleDefinition bundle, List<TagPoseData> detections)
        {
            int count = 0;

            Vector3 accumPos = Vector3.zero;
            Quaternion accumRot = Quaternion.identity;

            foreach (var marker in detections)
            {
                if (marker.Id == null || !bundle.TagLocalPoses.TryGetValue(marker.Id, out var localPose))
                    continue;

                // Compute candidate bundle pose
                var invLocalRot = Quaternion.Inverse(localPose.Rotation);
                var worldRot = marker.Rotation * invLocalRot;

                var worldPos = marker.Position - (worldRot * localPose.Position);

                if (count == 0)
                {
                    accumRot = worldRot;
                    accumPos = worldPos;
                }
                else
                {
                    // Rotation averaging (incremental slerp)
                    float w = 1.0f / (count + 1);
                    accumRot = Quaternion.Slerp(accumRot, worldRot, w);
                    accumPos = Vector3.Lerp(accumPos, worldPos, w);
                }

                count++;
            }

            return new TagPoseData
            {
                Position = accumPos,
                Rotation = accumRot,
                IsValid = count > 0
            };
        }
    }
}