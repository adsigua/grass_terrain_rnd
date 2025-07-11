using UnityEngine;
using Adobobu.Utilities;
using NaughtyAttributes;

namespace Adobobu.Grass
{
    [CreateAssetMenu(fileName = "GrassRendererData", menuName = "Scriptable Objects/GrassRendererData")]
    public class GrassRendererData : ScriptableObject
    {
        [Header("Grass Properties")]
        public float grassOffsetStrength = 0.1f;
        public Vector2 grassWidthScaleRange = new Vector2(1f, 1f); 
        public Vector2 grassHeightScaleRange = new Vector2(1f, 1f);
        [Range(0,1)] public float grassDefaultAngle = 0.0f;
        [Range(0,1)] public float grassDirectionRandomness = 0.0f;
        public float grassOcclusionOffset = 0.1f;
    
        [Header("Grass Movement")]
        [Range(-1, 1)] public float grassTilt = 0.1f;
        [Range( 0, 3)] public float grassBend = 0.1f;
        [Range( 0, 1)] public float grassBendPos = 0.5f;
        [Range(0.8f, 1.0f)] public float grassWindDissipation = 0.99f;
        [Range(0.0f, 0.5f)] public float grassWindEffect = 0.01f;
        [Range( 0, 1)] public float grassFlutter = 1.0f;
        [Range( 0, 1)] public float grassStiffness = 0.0f;
    
        [Header("Random Values")]
        [Range( 0, 1)] public float valueHolderA = 0.99f;
        [Range(-1, 1)] public float valueHolderB = 0.99f;
        [Range(-1, 1)] public float valueHolderC = 0.99f;

        [Header("View Offsetting")]
        public bool applyViewOffset = false;
        [Range(0,1)] public float offsetFrameSmoothing = 0.1f;
        [MinMaxSlider(0.0f, 1.0f)]
        public Vector2 sideViewSmoothRange = new Vector2(0.98f, 1.0f);
        [Range(0,1)] public float sideViewRotOffset = 0.1f;
        [MinMaxSlider(0.0f, 1.0f)]
        public Vector2 topViewSmoothRange = new Vector2(0.98f, 1.0f);
        [Range(0,1)] public float topViewRotOffset = 0.1f;
    }
}
