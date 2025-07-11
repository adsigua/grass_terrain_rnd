using UnityEngine;

namespace Adobobu.Utilities
{
    public static class Utils
    {
        public static void SetFromGlobalVector(this ComputeShader compute, string nameID)
        {
            compute.SetVector(nameID, Shader.GetGlobalVector(nameID));
        }
        
        public static void SetFromGlobalVector(this ComputeShader compute, int nameID)
        {
            compute.SetVector(nameID, Shader.GetGlobalVector(nameID));
        }
        
        public static void SetFromGlobalFloat(this ComputeShader compute, string nameID)
        {
            compute.SetFloat(nameID, Shader.GetGlobalFloat(nameID));
        }
    
        public static void SetFromGlobalFloat(this ComputeShader compute, int nameID)
        {
            compute.SetFloat(nameID, Shader.GetGlobalFloat(nameID));
        }
        
        public static void SetFromGlobalTexture(this ComputeShader compute, int kernelIndex, string nameID)
        {
            compute.SetTexture(kernelIndex, nameID, Shader.GetGlobalTexture(nameID));
        }
        
        public static void SetFromGlobalTexture(this ComputeShader compute, int kernelIndex, int nameID)
        {
            compute.SetTexture(kernelIndex, nameID, Shader.GetGlobalTexture(nameID));
        }
        
        public static Frustum CreateFrustumFromCamera(Camera camera)
        {
            Transform camTransform = camera.transform;
            float zFar = camera.farClipPlane, 
                zNear = camera.nearClipPlane,
                fovY = camera.fieldOfView, 
                aspect = camera.aspect;
            float halfVSide = zFar * Mathf.Tan(fovY * Mathf.Deg2Rad * 0.5f);
            float halfHSide = halfVSide * aspect;
            Vector3 frontMultFar = zFar * camTransform.forward;

            var frustum = new Frustum();
            frustum.nearPlane = new CustomPlane(camTransform.position + zNear * camTransform.forward, camTransform.forward);
            frustum.farPlane = new CustomPlane( camTransform.position + frontMultFar, -camTransform.forward);
            frustum.rightPlane = new CustomPlane( camTransform.position,
                Vector3.Cross(frontMultFar + camTransform.right * halfHSide, camTransform.up) );
            frustum.leftPlane = new CustomPlane( camTransform.position,
                Vector3.Cross(camTransform.up,frontMultFar - camTransform.right * halfHSide) );
            frustum.topPlane = new CustomPlane( camTransform.position,
                Vector3.Cross(camTransform.right, frontMultFar + camTransform.up * halfVSide) );
            frustum.bottomPlane = new CustomPlane( camTransform.position,
                Vector3.Cross(frontMultFar - camTransform.up * halfVSide, camTransform.right) );
            return frustum;
        }
    }
}
