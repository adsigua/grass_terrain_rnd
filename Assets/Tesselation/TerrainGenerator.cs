using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;

public class TerrainGenerator : MonoBehaviour
{
   [SerializeField] private ComputeShader _terrainGeneratorCompute;
   [SerializeField] private ComputeBuffer _terrainBuffer;
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
      _terrainBuffer = new ComputeBuffer(_terrainBufferWidth * _terrainBufferWidth, sizeof(float) * 4);
      _terrainTexture = new RenderTexture(_terrainBufferWidth, _terrainBufferWidth, 0, GraphicsFormat.R16G16B16A16_UNorm);
      _terrainTexture.enableRandomWrite = true;

      _terrainGeneratorCompute.SetBuffer(0, "_TerrainBuffer", _terrainBuffer);

      _hasInit = true;
      
      RunTerrainGenerator();
   }

   private void RunTerrainGenerator()
   {
      if(_hasInit == false)
         return;
      //_terrainGeneratorCompute.SetVector( "_TerrainCenter", Vector3.zero);
      //_terrainGeneratorCompute.SetFloat( "_TerrainSize", _terrainSize);
      _terrainGeneratorCompute.SetFloat( "_BufferWidth", _terrainBufferWidth);
      _terrainGeneratorCompute.SetFloat( "_TerrainHeight", _terrainHeight);
      _terrainGeneratorCompute.SetFloat( "_NormalComputeStepSize", _normalStepSize);
      _terrainGeneratorCompute.SetFloat( "_PerlinScale", _noiseScale / 100.0f );
      _terrainGeneratorCompute.SetFloat( "_TerrainFloor", _terrainFloor);
      
      int threadSize = Mathf.CeilToInt(_terrainBufferWidth / 16.0f);
      _terrainGeneratorCompute.Dispatch(0, threadSize, threadSize, 1);
      
      if (_blitToTexture)
      {
         _terrainGeneratorCompute.SetBuffer(1, "_TerrainBuffer", _terrainBuffer);
         _terrainGeneratorCompute.SetTexture(1, "_TerrainTexture", _terrainTexture);
         _terrainGeneratorCompute.Dispatch(1, threadSize, threadSize, 1);
      }
      
      Shader.SetGlobalBuffer("_TerrainBuffer", _terrainBuffer);
      Shader.SetGlobalFloat("_TerrainBufferWidth", _terrainBufferWidth);
      Shader.SetGlobalFloat("_TerrainSize", _terrainSize);
      Shader.SetGlobalVector("_TerrainCenter", transform.position);
   }

   private void OnValidate()
   {
      if(!Application.isPlaying)
         return;

      RunTerrainGenerator();
   }
   
 
   private void OnDestroy()
   {
      _terrainBuffer?.Release();
      _terrainTexture?.Release();
   }
   
}
