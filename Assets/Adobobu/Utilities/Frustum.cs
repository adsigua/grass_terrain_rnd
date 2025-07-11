using Unity.Mathematics;
using UnityEngine;

namespace Adobobu.Utilities
{
    public struct Frustum
    {
        public CustomPlane    topPlane;
        public CustomPlane bottomPlane;
        public CustomPlane   leftPlane;
        public CustomPlane  rightPlane;
        public CustomPlane   nearPlane;
        public CustomPlane    farPlane;
    
        public CustomPlane[] GetPlanesArray() => new[]
        {
            topPlane, 
            bottomPlane, 
            leftPlane, 
            rightPlane, 
            nearPlane, 
            farPlane
        };
    }

    public struct CustomPlane
    {
        public float3 normal;
        public float distance;

        public CustomPlane(Vector3 inPoint, Vector3 inNormal)
        {
            normal = Vector3.Normalize(inNormal);
            distance = Vector3.Dot(normal, inPoint);
        }
    
        public CustomPlane(Vector3 inNormal, float d)
        {
            normal = Vector3.Normalize(inNormal);
            distance = d;
        }
    }
}