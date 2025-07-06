using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Utils;

public class TerrainGenerator : MonoBehaviour
{
   [SerializeField] private ComputeShader _terrainGeneratorCompute;
   [SerializeField] private ComputeBuffer _terrainDataBuffer;
   [SerializeField] private bool _blitToTexture = false;
   [SerializeField, ShowIf("_blitToTexture")] private RenderTexture _terrainTexture;
   [SerializeField] private int _terrainBufferWidth = 512;
   [SerializeField] private float _terrainSize = 100.0f;
   [SerializeField] private float _terrainFloor = 0.1f;
   [SerializeField] private float _terrainHeight = 100.0f;
   [SerializeField] private float _normalStepSize = 0.1f;
   [SerializeField] private float _noiseScale = 0.01f;

   [SerializeField] private Material _terrainMaterial;

   private bool _hasInit = false;

   private void Start()
   {
      _terrainDataBuffer = new ComputeBuffer(_terrainBufferWidth * _terrainBufferWidth, sizeof(float) * (1 + 3 + 3));
      _terrainTexture = new RenderTexture(_terrainBufferWidth, _terrainBufferWidth, 0, GraphicsFormat.R16G16B16A16_UNorm);
      _terrainTexture.enableRandomWrite = true;

      _terrainGeneratorCompute.SetBuffer(0, ShaderConstants.TerrainDataBuffer, _terrainDataBuffer);

      _hasInit = true;
      
      RunTerrainGenerator();
   }

   private void RunTerrainGenerator()
   {
      if(_hasInit == false)
         return;
      //_terrainGeneratorCompute.SetVector( "_TerrainCenter", Vector3.zero);
      //_terrainGeneratorCompute.SetFloat( "_TerrainSize", _terrainSize);
      _terrainGeneratorCompute.SetFloat( ShaderConstants.TerrainBufferWidth, _terrainBufferWidth);
      _terrainGeneratorCompute.SetFloat( ShaderConstants.TerrainHeight, _terrainHeight);
      _terrainGeneratorCompute.SetFloat( ShaderConstants.TerrainNormalComputeStepSize, _normalStepSize);
      _terrainGeneratorCompute.SetFloat( ShaderConstants.TerrainPerlinScale, _noiseScale / 100.0f );
      _terrainGeneratorCompute.SetFloat( ShaderConstants.TerrainFloor, _terrainFloor);
      
      int threadSize = Mathf.CeilToInt(_terrainBufferWidth / 16.0f);
      _terrainGeneratorCompute.Dispatch(0, threadSize, threadSize, 1);
      
      if (_blitToTexture)
      {
         _terrainGeneratorCompute.SetBuffer(1, ShaderConstants.TerrainDataBuffer, _terrainDataBuffer);
         _terrainGeneratorCompute.SetTexture(1, ShaderConstants.TerrainTexture, _terrainTexture);
         _terrainGeneratorCompute.Dispatch(1, threadSize, threadSize, 1);
      }
      
      Shader.SetGlobalBuffer(ShaderConstants.TerrainDataBuffer, _terrainDataBuffer);
      Shader.SetGlobalFloat(ShaderConstants.TerrainBufferWidth, _terrainBufferWidth);
      Shader.SetGlobalFloat(ShaderConstants.TerrainSize, _terrainSize);
      Shader.SetGlobalVector(ShaderConstants.TerrainCenter, transform.position);
   }

   private void OnValidate()
   {
      if(!Application.isPlaying)
         return;

      RunTerrainGenerator();
   }
   
 
   private void OnDestroy()
   {
      _terrainDataBuffer?.Release();
      _terrainTexture?.Release();
   }
   
}
