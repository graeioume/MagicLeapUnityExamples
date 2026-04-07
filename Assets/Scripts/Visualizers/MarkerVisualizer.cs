using System.Text;
using UnityEngine;
using MagicLeap.OpenXR.Features.MarkerUnderstanding;

namespace MagicLeap.Examples
{
    public class MarkerVisualizer : MonoBehaviour
    {
        [SerializeField]
        private TextMesh dataText;

        private StringBuilder stringBuilder = new StringBuilder();

        public void Set(MarkerData markerData, MarkerType currentMarkerType)
        {
            stringBuilder.Clear();

            stringBuilder.Append($"MarkerLength: {markerData.MarkerLength}\n");

     
            stringBuilder.Append($"MarkerNumber: {markerData.MarkerNumber}\n");
            stringBuilder.Append($"ReprojectionErrorMeters: {markerData.ReprojectionErrorMeters}\n");

            stringBuilder.Append($"Position: {markerData.MarkerPose?.position}\n");
            stringBuilder.Append($"Rotation: {markerData.MarkerPose?.rotation}");


            dataText.text = stringBuilder.ToString();

            transform.position = markerData.MarkerPose.Value.position;
            transform.rotation = markerData.MarkerPose.Value.rotation;
        }
    }
}
