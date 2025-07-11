using NaughtyAttributes;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Adobobu.Utilities;

namespace Adobobu.Wind
{
    public class WindControl : MonoBehaviour
    {
        [Header("Ambient Wind")]
        [SerializeField] private RenderTexture _ambientWindTexture;
        [SerializeField] private Material _ambientWindMaterial;
        [SerializeField] private float _ambientWindSize = 10.0f;
        [SerializeField] private float _ambientWindSpeed = 10.0f;

        [SerializeField, MinMaxSlider(0.0f, 100.0f)]
        private Vector2 _ambientWindStrength = new Vector2(0.0f, 2.0f);
        [SerializeField] private float _ambientWindNoiseFrequency = 10.0f;
        [SerializeField, NaughtyAttributes.ReadOnly] private Vector2 _ambientWindDirection;
        [SerializeField,Range(0,1)] private float _ambientWindAngle = 0.25f;
        [SerializeField] private float _fluidMapWindStrength = 1.0f;
    
        [Header("Wind Fluid Sim")]
        [SerializeField] private Transform _impulseTransform;
        [SerializeField] private RenderTexture _fluidRenderTexture;

        [SerializeField] private ComputeShader _fluidSimComputeShader;

        [SerializeField] private float _simulationSize = 5.0f;
        [SerializeField] private SimulationResolution _resolution = SimulationResolution.x128;

        [SerializeField] private float _impulseRadius = 20f;
        [SerializeField] private float _impulseSpeed = 100.0f;
        [SerializeField] private float _impulseDensity = 1.0f;
    
        [SerializeField] private float _densityDissipation = 0.99f;
        [SerializeField] private float _velocityDissipation = 0.99f;
    
        [SerializeField] private float _densityViscosity = 0.01f;
        [SerializeField] private float _velocityViscosity = 0.01f;
    
        [SerializeField] private int _diffusionDensitySteps = 3;
        [SerializeField] private int _diffusionVelocitySteps = 3;
        [SerializeField] private int _pressureSolveSteps = 10;

        [SerializeField, Foldout("Sim Options")] private bool _advectDensity;
        [SerializeField, Foldout("Sim Options")] private bool _advectVelocity;
        [SerializeField, Foldout("Sim Options")] private bool _applyImpulse;
        [SerializeField, Foldout("Sim Options")] private bool _diffuseDensity;
        [SerializeField, Foldout("Sim Options")] private bool _diffuseVelocity;
        [SerializeField, Foldout("Sim Options")] private bool _solvePressure;
    
        private SimulationBuffer _densityBuffer;
        private SimulationBuffer _velocityBuffer;
        private SimulationBuffer _pressureBuffer;
        private SimulationBuffer _divergenceBuffer;
    
        [SerializeField] private PreviewType _previewType;
    
        private enum SimulationResolution
        {
            x16 = 16,
            x32 = 32,
            x64 = 64,
            x128 = 128,
            x256 = 256,
            x512 = 512,
            x1024 = 1024,
            x2048 = 2048,
        }
    
        private float _dt;
        private float _gridScale;
        private int _bufferSize;
        private int _simResolution;

        public enum PreviewType
        {
            Fluid, Density, Velocity, Pressure
        }
    
        private void Start()
        {
            _simResolution = (int)_resolution;
            _gridScale = _simResolution / 1.0f;
            _fluidSimComputeShader.SetInt(_Resolution, _simResolution);
            _fluidSimComputeShader.SetFloat(_GridScale, _gridScale);
        
            _ambientWindTexture = new RenderTexture(2048, 2048, GraphicsFormat.R32_SFloat, 0);
        
            InitKernels();
            InitBuffers();
        }
    
        private void Update()
        {
            Shader.SetGlobalVector(ShaderConstants.AmbientWindCenter, transform.position);
            Shader.SetGlobalFloat(ShaderConstants.AmbientWindSize, _ambientWindSize);
            Shader.SetGlobalVector(ShaderConstants.AmbientWindStrength, _ambientWindStrength / 100.0f);
            Shader.SetGlobalFloat(ShaderConstants.FluidMapWindStrength, _fluidMapWindStrength);
            float ambientWindDirAngle = _ambientWindAngle * Mathf.PI * 2.0f;
            _ambientWindDirection = (new Vector2(Mathf.Cos(ambientWindDirAngle), Mathf.Sin(ambientWindDirAngle))).normalized;
            Shader.SetGlobalVector(ShaderConstants.AmbientWindDirection, _ambientWindDirection);
        
            BlitAmbientWind();
            DoWindSimulation();
        }

        private void OnDestroy()
        {
            _densityBuffer?.Dispose();
            _velocityBuffer?.Dispose();
            _pressureBuffer?.Dispose();
            _divergenceBuffer?.Dispose();
            _ambientWindTexture?.Release();
        }

        private void OnDrawGizmosSelected()
        {
            Color currentColor = Gizmos.color;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, new Vector3(_simulationSize, 1.0f, _simulationSize));
            Gizmos.color = currentColor;
            Gizmos.DrawWireCube(transform.position,new Vector3(_ambientWindSize, 1.0f, _ambientWindSize));
        }

        private void OnValidate()
        {
            if(Application.isPlaying)
                return;
            float ambientWindDirAngle = _ambientWindAngle * Mathf.PI * 2.0f;
            _ambientWindDirection = (new Vector2(Mathf.Cos(ambientWindDirAngle), Mathf.Sin(ambientWindDirAngle))).normalized;
        }

        private void BlitAmbientWind()
        {
            if(!_ambientWindTexture || !_ambientWindMaterial)
                return;
            _ambientWindMaterial.SetFloat(ShaderConstants.AmbientWindSpeed, _ambientWindSpeed);
            _ambientWindMaterial.SetFloat(ShaderConstants.AmbientWindFrequency, _ambientWindNoiseFrequency);
        
            Graphics.Blit(null, _ambientWindTexture, _ambientWindMaterial);
        
            Shader.SetGlobalTexture(ShaderConstants.AmbientWindMap, _ambientWindTexture);
        }

        private void DoWindSimulation()
        {
            _dt = Time.deltaTime;
        
            _fluidSimComputeShader.SetFloat(_DeltaTime, _dt);
            if (_advectDensity)
            {
                _fluidSimComputeShader.SetBuffer(_advectionDensityKernel, _DensityRead, _densityBuffer.readBuffer);
                ApplyAdvection(_advectionDensityKernel, _DensityWrite, _densityBuffer.writeBuffer, _DensityDissipation, _densityDissipation);
                _densityBuffer.Swap();
            }
        
            if (_advectVelocity)
            {
                ApplyAdvection(_advectionVelocityKernel, _VelocityWrite, _velocityBuffer.writeBuffer, _VelocityDissipation, _velocityDissipation);
                _velocityBuffer.Swap();
            }

            if (_applyImpulse)
            {
                ApplyImpulse();
            }

            float rGridScaleSq = 1.0f / (_gridScale * _gridScale);
            if (_diffuseDensity)
            {
                float alpha = rGridScaleSq / (_densityViscosity * _dt);
                float rbeta = 1.0f / (alpha + 4.0f);
                SolvePoisson(_diffuseDensityKernel, _diffusionDensitySteps, _DensityRead, _densityBuffer, 
                    _DensityRead, _DensityWrite, _densityBuffer, alpha, rbeta);
            }
        
            if (_diffuseVelocity)
            {
                float alpha = rGridScaleSq / (_velocityViscosity * _dt);
                float rbeta = 1.0f / (alpha + 4.0f);
                SolvePoisson(_diffuseVelocityKernel, _diffusionVelocitySteps, _VelocityRead, _velocityBuffer, 
                    _VelocityRead, _VelocityWrite, _velocityBuffer, alpha, rbeta);
            }

            if (_solvePressure)
            {
                SolvePressure();
            }

            BlitFluidMap();
        }

        private void ApplyAdvection(int kernel, int texWriteId, ComputeBuffer buffer, int dissipationId, float dissipation)
        {
            _fluidSimComputeShader.SetFloat(dissipationId, dissipation);
            _fluidSimComputeShader.SetBuffer(kernel, _VelocityRead, _velocityBuffer.readBuffer);
            _fluidSimComputeShader.SetBuffer(kernel, texWriteId, buffer);
            DispatchSimulationCompute(_fluidSimComputeShader, kernel);
        }

        private void ApplyImpulse()
        {
            Vector3 impulsePos3D = (_impulseTransform.position - transform.position)  / (_simulationSize);
            Vector2 impulsePos = new Vector2(
                Mathf.Clamp01(impulsePos3D.x + 0.5f), 
                Mathf.Clamp01(impulsePos3D.z + 0.5f)
            );
            Vector2 impulseDir = new Vector2(_impulseTransform.forward.x, _impulseTransform.forward.z).normalized;

            _fluidSimComputeShader.SetFloat(_ImpulseRadius, _impulseRadius);
            _fluidSimComputeShader.SetFloat(_ImpulseDensity, _impulseDensity);
            _fluidSimComputeShader.SetFloat(_ImpulseSpeed, _impulseSpeed);
            _fluidSimComputeShader.SetVector(_ImpulseUV, impulsePos * _simResolution);
            _fluidSimComputeShader.SetVector(_ImpulseDirection, impulseDir);
        
            _fluidSimComputeShader.SetBuffer(_applyImpulseKernel, _VelocityRead, _velocityBuffer.readBuffer);
            _fluidSimComputeShader.SetBuffer(_applyImpulseKernel, _VelocityWrite, _velocityBuffer.writeBuffer);
            _fluidSimComputeShader.SetBuffer(_applyImpulseKernel, _DensityRead, _densityBuffer.readBuffer);
            _fluidSimComputeShader.SetBuffer(_applyImpulseKernel, _DensityWrite, _densityBuffer.writeBuffer);
        
            DispatchSimulationCompute(_fluidSimComputeShader, _applyImpulseKernel);
        
            _densityBuffer.Swap();
            _velocityBuffer.Swap();
        }

        private void SolvePoisson(int kernel, int steps, 
            int centerId, SimulationBuffer centerBuffer,
            int sampleId, int writeId, SimulationBuffer writeBuffer, float alpha, float rBeta)
        {
            _fluidSimComputeShader.SetFloat(_PoissonAlpha, alpha);
            _fluidSimComputeShader.SetFloat(_PoissonRBeta, rBeta);
            _fluidSimComputeShader.SetBuffer(kernel, centerId, centerBuffer.readBuffer);
            for (int i = 0; i < steps; i++)
            {
                _fluidSimComputeShader.SetBuffer(kernel, sampleId, writeBuffer.readBuffer);
                _fluidSimComputeShader.SetBuffer(kernel, writeId, writeBuffer.writeBuffer);
                DispatchSimulationCompute(_fluidSimComputeShader, kernel);
                writeBuffer.Swap();
            }
        }
    
        private void SolvePressure()
        {
            //solve divergence
            _fluidSimComputeShader.SetBuffer(_projectionKernel, _VelocityRead, _velocityBuffer.readBuffer);
            _fluidSimComputeShader.SetBuffer(_projectionKernel, _Divergence, _divergenceBuffer.writeBuffer);
            DispatchSimulationCompute(_fluidSimComputeShader, _projectionKernel);
            _divergenceBuffer.Swap();
        
            //solve pressure
            SolvePoisson(_solvePressureKernel, _pressureSolveSteps, _Divergence, _divergenceBuffer,
                _PressureRead, _PressureWrite, _divergenceBuffer, -1.0f, 1.0f / 4.0f);
        
            //subtract gradient
            _fluidSimComputeShader.SetBuffer(_gradientSubtractionKernel, _PressureRead, _pressureBuffer.readBuffer);
            _fluidSimComputeShader.SetBuffer(_gradientSubtractionKernel, _VelocityRead, _velocityBuffer.readBuffer);
            _fluidSimComputeShader.SetBuffer(_gradientSubtractionKernel, _VelocityWrite, _velocityBuffer.writeBuffer);
            DispatchSimulationCompute(_fluidSimComputeShader, _gradientSubtractionKernel);
            _velocityBuffer.Swap();
        }

        private void BlitFluidMap()
        {
            switch (_previewType)
            {
                case PreviewType.Density:
                    DoBlitDispatch(_blitDensityKernel);
                    break;
                case PreviewType.Velocity:
                    DoBlitDispatch(_blitVelocityKernel);
                    break;
                case PreviewType.Pressure:
                default:
                    DoBlitDispatch(_blitToFluidMapKernel);
                    break;
            }
        }

        private void DoBlitDispatch(int kernel)
        {
            _fluidSimComputeShader.SetBuffer(kernel, _VelocityRead, _velocityBuffer.readBuffer);
            _fluidSimComputeShader.SetBuffer(kernel, _DensityRead, _densityBuffer.readBuffer);
            _fluidSimComputeShader.SetTexture(kernel, _FluidMap, _fluidRenderTexture);
            _fluidSimComputeShader.SetVector(_FluidMapTS, new Vector2(_fluidRenderTexture.width, _simResolution));

            _fluidSimComputeShader.Dispatch(
                kernel, 
                _fluidRenderTexture.width / 8, 
                _fluidRenderTexture.width / 8, 
                1);
        
            Shader.SetGlobalTexture(ShaderConstants.FluidMap, _fluidRenderTexture);
        }

        [SerializeField] private Texture _inputTexture;
    
        [Button]
        private void AddImpulseInput()
        {
            _fluidSimComputeShader.SetTexture(_addImpulseToVelocityKernel, _InputTexture, _inputTexture);
            _fluidSimComputeShader.SetBuffer(_addImpulseToVelocityKernel, _VelocityWrite, _velocityBuffer.writeBuffer);
            _fluidSimComputeShader.SetVector( _InputTextureSize, new Vector2(_inputTexture.width, _inputTexture.height));
            DispatchSimulationCompute(_fluidSimComputeShader, _addImpulseToVelocityKernel);
            _velocityBuffer.Swap();
        }
    
        private void DispatchSimulationCompute(ComputeShader computeShader, int kernel)
        {
            computeShader.Dispatch(
                kernel, 
                _simResolution / 8, 
                _simResolution / 8, 
                1);
        }

        private void InitBuffers()
        {
            _bufferSize = _simResolution * _simResolution;
            _velocityBuffer = new SimulationBuffer(_bufferSize, sizeof(float) * 2);
            _densityBuffer = new SimulationBuffer(_bufferSize, sizeof(float));
            _pressureBuffer = new SimulationBuffer(_bufferSize, sizeof(float));
            _divergenceBuffer = new SimulationBuffer(_bufferSize, sizeof(float));
            NativeArray<Vector2> initVelocityVal = new NativeArray<Vector2>(_bufferSize, Allocator.Temp);
            _velocityBuffer.InitializeData(initVelocityVal);
            NativeArray<float> initFloatVals = new NativeArray<float>(_bufferSize, Allocator.Temp);
            _densityBuffer.InitializeData(initFloatVals);
            _pressureBuffer.InitializeData(initFloatVals);
            _divergenceBuffer.InitializeData(initFloatVals);
        }

        private void InitKernels()
        {
            _advectionDensityKernel = _fluidSimComputeShader.FindKernel("AdvectionDensity");
            _advectionVelocityKernel = _fluidSimComputeShader.FindKernel("AdvectionVelocity");
            _applyImpulseKernel = _fluidSimComputeShader.FindKernel("AddImpulse");
            _diffuseDensityKernel = _fluidSimComputeShader.FindKernel("DiffuseDensity");
            _diffuseVelocityKernel = _fluidSimComputeShader.FindKernel("DiffuseVelocity");
            _projectionKernel = _fluidSimComputeShader.FindKernel("Projection");
            _solvePressureKernel = _fluidSimComputeShader.FindKernel("SolvePressure");
            _gradientSubtractionKernel = _fluidSimComputeShader.FindKernel("GradientSubtraction");
            _blitToFluidMapKernel = _fluidSimComputeShader.FindKernel("BlitToFluidMap");
            _blitDensityKernel = _fluidSimComputeShader.FindKernel("BlitDensity");
            _blitVelocityKernel = _fluidSimComputeShader.FindKernel("BlitVelocity");
            _addImpulseToVelocityKernel = _fluidSimComputeShader.FindKernel("AddImpulseToVelocity");
        }
    
        #region shader variables
        private int _advectionDensityKernel;
        private int _advectionVelocityKernel;
        private int _applyImpulseKernel;
        private int _diffuseDensityKernel;
        private int _diffuseVelocityKernel;
        private int _projectionKernel;
        private int _solvePressureKernel;
        private int _gradientSubtractionKernel;
        private int _blitToFluidMapKernel;
        private int _blitDensityKernel;
        private int _blitVelocityKernel;
        private int _addImpulseToVelocityKernel;
    
        private static int _DensityRead = Shader.PropertyToID("_DensityRead");
        private static int _DensityWrite = Shader.PropertyToID("_DensityWrite");
        private static int _VelocityRead = Shader.PropertyToID("_VelocityRead");
        private static int _VelocityWrite = Shader.PropertyToID("_VelocityWrite");
        private static int _Divergence = Shader.PropertyToID("_Divergence");
        private static int _PressureRead = Shader.PropertyToID("_PressureRead");
        private static int _PressureWrite = Shader.PropertyToID("_PressureWrite");
        private static int _ImpulseUV = Shader.PropertyToID("_ImpulseUV");
        private static int _ImpulseDirection = Shader.PropertyToID("_ImpulseDirection");
        private static int _ImpulseSpeed = Shader.PropertyToID("_ImpulseSpeed");
        private static int _ImpulseDensity = Shader.PropertyToID("_ImpulseDensity");
        private static int _ImpulseRadius = Shader.PropertyToID("_ImpulseRadius");
        private static int _VelocityDissipation = Shader.PropertyToID("_VelocityDissipation");
        private static int _DensityDissipation = Shader.PropertyToID("_DensityDissipation");
        private static int _DeltaTime = Shader.PropertyToID("_DeltaTime");
        private static int _GridScale = Shader.PropertyToID("_GridScale");
        private static int _Resolution = Shader.PropertyToID("_Resolution");
    
        private static int _FluidMap = Shader.PropertyToID("_FluidMap");
    
        private static int _PoissonAlpha = Shader.PropertyToID("_PoissonAlpha");
        private static int _PoissonRBeta = Shader.PropertyToID("_PoissonRBeta");
    
        private static int _InputTexture = Shader.PropertyToID("_InputTexture");
        private static int _InputTextureSize = Shader.PropertyToID("_InputTextureSize");
    
        private static int _FluidMapTS = Shader.PropertyToID("_FluidMapTS");

        #endregion
    
        // [System.Serializable]
        // private class SimulationTexture
        // {
        //     public RenderTexture[] _renderTextures;
        //     public RenderTexture read => _renderTextures[0];
        //     public RenderTexture write => _renderTextures[1];
        //
        //     public SimulationTexture(RenderTexture renderTexture) :
        //         this(renderTexture.width, renderTexture.graphicsFormat) { }
        //
        //     public SimulationTexture(int size, GraphicsFormat format)
        //     {
        //         _renderTextures = new RenderTexture[2]
        //         {
        //             new RenderTexture(size, size, 0, format),
        //             new RenderTexture(size, size, 0, format),
        //         };
        //         foreach (var renderTexture in _renderTextures)
        //         {
        //             renderTexture.enableRandomWrite = true;
        //         }
        //     }
        //
        //     public void Swap()
        //     {
        //         (_renderTextures[0], _renderTextures[1]) = (_renderTextures[1], _renderTextures[0]);
        //     }
        //
        //     public void Dispose()
        //     {
        //         foreach (var renderTexture in _renderTextures)
        //         {
        //             renderTexture.Release();
        //         }
        //     }
        // }
    
        [System.Serializable]
        private class SimulationBuffer
        {
            public ComputeBuffer[] _computeBuffers;
            public ComputeBuffer readBuffer => _computeBuffers[0];
            public ComputeBuffer writeBuffer => _computeBuffers[1];

            public SimulationBuffer(int size, int stride)
            {
                _computeBuffers = new ComputeBuffer[2]
                {
                    new ComputeBuffer(size, stride),
                    new ComputeBuffer(size, stride),
                };
            }
        
            public void InitializeData<T>(NativeArray<T> initData) where T : struct
            {
                readBuffer.SetData(initData);
                writeBuffer.SetData(initData);
            }

            public void Swap()
            {
                (_computeBuffers[0], _computeBuffers[1]) = (_computeBuffers[1], _computeBuffers[0]);
            }

            public void Dispose()
            {
                foreach (var computeBuffer in _computeBuffers)
                {
                    computeBuffer.Release();
                }
            }
        }
    }
}
