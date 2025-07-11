using System;
using System.Collections.Generic;
using Adobobu.Utilities;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Adobobu.Grass
{
    public class GrassRenderer : MonoBehaviour
    {
        [SerializeField] private Material _material;
        [SerializeField] private Mesh _mesh;
        [SerializeField] private ComputeShader _grassPositionComputeShader;
        public ComputeShader grassPositionComputeShader => _grassPositionComputeShader;
        [SerializeField] private ComputeShader _grassVisibilityComputeShader;
        public ComputeShader grassVisibilityComputeShader => _grassVisibilityComputeShader;

        public Vector3Int visibilityThreadSizes
        {
            get
            {
                int chunkThreadSize = Mathf.CeilToInt(_chunksPerTile / 16.0f);
                return new Vector3Int(chunkThreadSize, chunkThreadSize, 1);
            }
        }
        [SerializeField] private bool _drawOnEndContext = false;
        
        [SerializeField] private Camera _mainCamera;

        //[SerializeField] private int _grassDensity = 10;
        [SerializeField] private float _tileSize = 10.0f;
        [SerializeField] private float _grassChunkSize = 1.0f;
        [SerializeField] private int _grassPerChunk = 16;

        [SerializeField, ReadOnly] private int _chunksCount;
        [SerializeField, ReadOnly] private int _grassCount;

        [SerializeField] private GrassRendererData _grassRendererData;

        //private GraphicsBuffer _meshVerticesBuffer;
        //TODO: bake indices & uvs inside the grass shader or on a separate file
        private GraphicsBuffer _meshTrianglesBuffer;
        private GraphicsBuffer _meshUVsBuffer;

        private ComputeBuffer _grassFrameDataBuffer;
        private ComputeBuffer _grassChunksBuffer;
        private ComputeBuffer _grassTransformsBuffer;
        private ComputeBuffer _readBackArgsBuffer;
        private ComputeBuffer _cameraFrustumBuffer;
        private ComputeBuffer _boundsOffsetsBuffer;

        //[SerializeField] private RenderTexture _windValuesTex;
        private float _trueChunkSize;
        private int _trueChunksCount;
        private int _chunksPerTile;

        private int _trueGrassCount;

        private Vector4 _grassScaling;

        private GlobalKeyword _viewOffsetKeyword;

        private static Vector3[] _boundsOffsets = 
        {
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            
            new Vector3( 1.0f, -1.0f, -1.0f),
            new Vector3( 1.0f, -1.0f,  1.0f),
            new Vector3( 1.0f,  1.0f, -1.0f),
            new Vector3( 1.0f,  1.0f,  1.0f)
        };

        private void Start()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            InitMeshData();
            InitGrassData();
        }

        private void InitMeshData()
        {
            // note: remember to check "Read/Write" on the mesh asset to get access to the geometry data
            //_meshVerticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _mesh.vertices.Length, 3 * sizeof(float));
            //_meshVerticesBuffer.SetData(_mesh.vertices);
            _meshTrianglesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _mesh.triangles.Length, sizeof(int));
            _meshTrianglesBuffer.SetData(_mesh.triangles);
            _meshUVsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _mesh.uv.Length, 2 * sizeof(float));
            _meshUVsBuffer.SetData(_mesh.uv);
        }

        // void CheckShaderKeywordState()
        // {
        //     // Iterate over the local keywords
        //     foreach (var localKeyword in Shader.globalKeywords)
        //     {
        //         var state = Shader.IsKeywordEnabled(localKeyword) ? "enabled" : "disabled";
        //         Debug.Log("Local keyword with name of " + localKeyword.name + " is " + state);
        //     }
        // }

        private void InitGrassData()
        {
            _viewOffsetKeyword = GlobalKeyword.Create("APPLY_VIEW_OFFSETTING");
            
            _cameraFrustumBuffer = new ComputeBuffer(6, sizeof(float) * 4);
            _boundsOffsetsBuffer = new ComputeBuffer(8, sizeof(float) * 3);
            _boundsOffsetsBuffer.SetData(_boundsOffsets);

            _chunksPerTile = Mathf.RoundToInt(_tileSize / (float)_grassChunkSize);
            _trueChunkSize = _tileSize / _chunksPerTile;
            _trueChunksCount = _chunksPerTile * _chunksPerTile;
            _grassChunksBuffer = new ComputeBuffer(_trueChunksCount, sizeof(float) * 4, ComputeBufferType.Append);
            
            _trueGrassCount = Mathf.FloorToInt(_grassPerChunk * _grassPerChunk * _trueChunksCount);
            _grassTransformsBuffer = new ComputeBuffer(_trueGrassCount, sizeof(float) * grassTransformsStride,
                ComputeBufferType.Append);
                
            _grassFrameDataBuffer = new ComputeBuffer(_trueGrassCount, sizeof(float) * 4);
            
            _readBackArgsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

            _grassVisibilityComputeShader.SetBuffer(0, ShaderConstants.GrassChunks, _grassChunksBuffer);
            _grassVisibilityComputeShader.SetBuffer(0, ShaderConstants.BoundsOffets, _boundsOffsetsBuffer);

            _grassPositionComputeShader.SetBuffer(0, ShaderConstants.GrassTransforms, _grassTransformsBuffer);
            _grassPositionComputeShader.SetBuffer(0, ShaderConstants.GrassFrameData, _grassFrameDataBuffer);
            _grassPositionComputeShader.SetBuffer(0, ShaderConstants.GrassChunks, _grassChunksBuffer);

            //_grassPositionComputeShader.SetBuffer(1, ShaderConstants.GrassWindValues, _grassAccumWindBuffer);
            //_grassPositionComputeShader.SetTexture(1, ShaderConstants.GrassWindTexture, _windValuesTex);
            CheckOffsetViewApply();
        }

        private int grassTransformsStride
        {
            get => sizeof(float) * (
                3 + //position (ws)
                3 * 3 + //rotation (mat3x3)
                2 + //bezierMidPoint (2d)
                2 + //bezierEndPoint (2d)
                3 + //windFactor (rgb)
                1 + //width
                1 //height
            );
        }

        void OnDestroy()
        {
            //_meshVerticesBuffer?.Dispose();
            //_meshVerticesBuffer = null;
            _meshTrianglesBuffer?.Dispose();
            _meshTrianglesBuffer = null;
            _meshUVsBuffer?.Dispose();
            _meshUVsBuffer = null;
            
            _grassTransformsBuffer?.Dispose();
            _grassTransformsBuffer = null;
            _grassChunksBuffer?.Dispose();
            _grassChunksBuffer = null;
            _grassFrameDataBuffer?.Dispose();
            _grassFrameDataBuffer = null;
            _readBackArgsBuffer?.Dispose();
            _readBackArgsBuffer = null;
            //_windValuesTex?.Release();
        }

        private void OnDrawGizmosSelected()
        {
            Color currentColor = Gizmos.color;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(_tileSize, 2.0f, _tileSize));
            Gizmos.color = currentColor;
        }

        private void OnValidate()
        {
            if(Application.isPlaying)
                return;
            int chunksPerTile = Mathf.RoundToInt(_tileSize / _grassChunkSize);
            _chunksCount = chunksPerTile * chunksPerTile;
            _grassCount = _grassPerChunk * _grassPerChunk * _chunksCount;
        }

        private void OnEnable()
        {
            RenderPipelineManager.endContextRendering += DrawOnEndRendering;
        }
        
        private void OnDisable()
        {
            RenderPipelineManager.endContextRendering -= DrawOnEndRendering;
        }

        private void DrawOnEndRendering(ScriptableRenderContext renderContext, List<Camera> cameraList)
        {
            if (_drawOnEndContext)
            {
                DrawGrass();
            }
        }

        private void Update()
        {
            if (_drawOnEndContext)
                return;
            DrawGrass();
        }
        
        void DrawGrass()
        {
            if (!Shader.GetGlobalTexture(ShaderConstants.CopyDepthTexture))
            {
                return;
            }
            _grassScaling = new Vector4(_grassRendererData.grassWidthScaleRange.x, _grassRendererData.grassWidthScaleRange.y,
                _grassRendererData.grassHeightScaleRange.x, _grassRendererData.grassHeightScaleRange.y);
            ComputeChunksVisibility();
            if(_chunksCount <= 0)
                return;
            ComputeGrassTransforms();
            RenderGrass();
        }
        
        private void ComputeChunksVisibility()
        {
            Frustum frustum = Utils.CreateFrustumFromCamera(_mainCamera);
            var planes = frustum.GetPlanesArray();
            _cameraFrustumBuffer.SetData(planes);
            _grassVisibilityComputeShader.SetMatrix(ShaderConstants.CameraViewProjectionMatrix, GL.GetGPUProjectionMatrix(_mainCamera.projectionMatrix, false) * _mainCamera.worldToCameraMatrix);
            _grassVisibilityComputeShader.SetMatrix(ShaderConstants.CameraViewMatrix, _mainCamera.worldToCameraMatrix);
            float zNear = _mainCamera.nearClipPlane;
            float zFar = _mainCamera.farClipPlane;
            float paramY = zFar / zNear;
            float paramX = 1.0f - paramY;
            _grassVisibilityComputeShader.SetVector(ShaderConstants.CameraZBufferParams, 
                new Vector4(paramX, paramY, paramX / zFar, paramY / zFar));
            _grassVisibilityComputeShader.SetVector(ShaderConstants.CameraProjectionParams, 
                new Vector4(1.0f, zNear, zFar, 1.0f / zFar));
            
            _grassVisibilityComputeShader.SetTextureFromGlobal(0, ShaderConstants.DepthTexture, ShaderConstants.CopyDepthTexture);
            
            float actualChunkWidth = _trueChunkSize + _grassRendererData.grassOffsetStrength * 2.0f + 2.0f;
            float chunkHeight = _grassRendererData.grassHeightScaleRange.y;
            var chunkExtents = new Vector3(actualChunkWidth, chunkHeight, actualChunkWidth) * 0.5f;
            var chunkCenter = Vector3.up * (chunkHeight * 0.5f);
            
            _grassVisibilityComputeShader.SetBuffer(0,ShaderConstants.CameraPlanes, _cameraFrustumBuffer);
            
            _grassVisibilityComputeShader.SetFloat(ShaderConstants.TileSize, _tileSize);
            _grassVisibilityComputeShader.SetVector(ShaderConstants.TileCenter, transform.position);
            _grassVisibilityComputeShader.SetFloat(ShaderConstants.GrassChunkSize, _trueChunkSize);
            _grassVisibilityComputeShader.SetFloat(ShaderConstants.GrassOcclusionOffset, _grassRendererData.grassOcclusionOffset);
            
            _grassVisibilityComputeShader.SetFloat(ShaderConstants.GrassChunksPerTile, _chunksPerTile);
            
            _grassVisibilityComputeShader.SetVector(ShaderConstants.GrassChunkLocalBoundsExtents, chunkExtents);
            _grassVisibilityComputeShader.SetVector(ShaderConstants.GrassChunkLocalBoundsCenter, chunkCenter);
            
            _grassVisibilityComputeShader.SetFromGlobalFloat(ShaderConstants.TerrainBufferWidth);
            _grassVisibilityComputeShader.SetFromGlobalVector(ShaderConstants.TerrainCenter);
            _grassVisibilityComputeShader.SetFromGlobalFloat(ShaderConstants.TerrainSize);

            _grassVisibilityComputeShader.SetFromGlobalTexture(0, ShaderConstants.CopyDepthTexture);
            
            _grassVisibilityComputeShader.SetFloat(ShaderConstants.ValueHolderB, _grassRendererData.valueHolderB);

            _grassChunksBuffer.SetCounterValue(0);
            int chunkThreadSize = Mathf.CeilToInt(_chunksPerTile / 16.0f);
            _grassVisibilityComputeShader.Dispatch(0, chunkThreadSize, chunkThreadSize, 1);

            _chunksCount = GetCountFromBuffer(_grassChunksBuffer);
        }
        
        private int GetCountFromBuffer(ComputeBuffer buffer)
        {
            ComputeBuffer.CopyCount(buffer, _readBackArgsBuffer, 0);
            int[] appendBufferCount = new int[1];
            _readBackArgsBuffer.GetData(appendBufferCount);
            return appendBufferCount[0];
        }
    
        private void ComputeGrassTransforms()
        {
            CheckOffsetViewApply();
        
            _grassPositionComputeShader.SetFloat(ShaderConstants.DeltaTime, Time.deltaTime);
            _grassPositionComputeShader.SetFloat(ShaderConstants.Time, Time.time);
        
            _grassPositionComputeShader.SetVector(ShaderConstants.CameraDirection, _mainCamera.transform.forward);
            _grassPositionComputeShader.SetVector(ShaderConstants.CameraPosition, _mainCamera.transform.position);
            
            _grassPositionComputeShader.SetFloat(ShaderConstants.TileSize, _tileSize);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassChunkSize, _trueChunkSize);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassPerChunk, _grassPerChunk);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassChunksPerTile, _chunksPerTile);
            
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassRotOffsetFrameSmoothing, _grassRendererData.offsetFrameSmoothing);
            _grassPositionComputeShader.SetVector(ShaderConstants.GrassSideViewSmoothRange, _grassRendererData.sideViewSmoothRange);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassSideViewRotOffset, _grassRendererData.sideViewRotOffset);
            _grassPositionComputeShader.SetVector(ShaderConstants.GrassTopViewSmoothRange, _grassRendererData.topViewSmoothRange);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassTopViewRotOffset, _grassRendererData.topViewRotOffset);

            _grassPositionComputeShader.SetFloat(ShaderConstants.ChunkCount, _chunksCount);
            
            ApplyGrassComputeTerrainData();
            ApplyGrassComputeWindData();
            ApplyGrassRendererData();

            _grassTransformsBuffer.SetCounterValue(0);
            int grassThread = Mathf.CeilToInt(_grassPerChunk / 8.0f);
            int chunkThread = Mathf.CeilToInt(_chunksCount / 8.0f);
            _grassPositionComputeShader.Dispatch(0, grassThread, grassThread, chunkThread);
        
            _grassCount = GetCountFromBuffer(_grassTransformsBuffer);
        }

        private void CheckOffsetViewApply()
        {
            if (_grassRendererData.applyViewOffset)
            {
                Shader.EnableKeyword(_viewOffsetKeyword);
            }
            else
            {
                Shader.DisableKeyword(_viewOffsetKeyword);
            }
        }

        private void ApplyGrassComputeTerrainData()
        {
            _grassPositionComputeShader.SetFromGlobalFloat(ShaderConstants.TerrainBufferWidth);
            _grassPositionComputeShader.SetFromGlobalFloat(ShaderConstants.TerrainSize);
            _grassPositionComputeShader.SetFromGlobalVector(ShaderConstants.TerrainCenter);
        }
        
        private void ApplyGrassComputeWindData()
        {
            _grassPositionComputeShader.SetFromGlobalVector(ShaderConstants.AmbientWindCenter);
            _grassPositionComputeShader.SetFromGlobalFloat(ShaderConstants.AmbientWindSize);
            _grassPositionComputeShader.SetFromGlobalVector(ShaderConstants.AmbientWindDirection);
            _grassPositionComputeShader.SetFromGlobalVector(ShaderConstants.AmbientWindStrength);
            _grassPositionComputeShader.SetFromGlobalFloat(ShaderConstants.FluidMapWindStrength);
            
            _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindCenter, Shader.GetGlobalVector(ShaderConstants.AmbientWindCenter));
            _grassPositionComputeShader.SetFloat(ShaderConstants.AmbientWindSize, Shader.GetGlobalFloat(ShaderConstants.AmbientWindSize));
            _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindDirection, Shader.GetGlobalVector(ShaderConstants.AmbientWindDirection));
            _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindStrength, Shader.GetGlobalVector(ShaderConstants.AmbientWindStrength));
            _grassPositionComputeShader.SetFloat(ShaderConstants.FluidMapWindStrength, Shader.GetGlobalFloat(ShaderConstants.FluidMapWindStrength));

            var windTex = Shader.GetGlobalTexture(ShaderConstants.AmbientWindMap);
            Vector2 windTexSize = new Vector2(windTex.width, windTex.height);
            _grassPositionComputeShader.SetTexture(0, ShaderConstants.AmbientWindMap, windTex);
            _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindMapSize, windTexSize);
        
            var fluidTex = Shader.GetGlobalTexture(ShaderConstants.FluidMap);
            Vector2 fluidTexSize = new Vector2(fluidTex.width, fluidTex.height);
            _grassPositionComputeShader.SetTexture(0,ShaderConstants.FluidMap, fluidTex);
            _grassPositionComputeShader.SetVector(ShaderConstants.FluidMapSize, fluidTexSize);
        }

        private void ApplyGrassRendererData()
        {
            _grassPositionComputeShader.SetVector(ShaderConstants.GrassScalingRange, _grassScaling);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassOffsetStrength, _grassRendererData.grassOffsetStrength);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassTilt, _grassRendererData.grassTilt);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassBend, _grassRendererData.grassBend);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassBendPos, _grassRendererData.grassBendPos);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassFlutter, _grassRendererData.grassFlutter);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassStiffness, _grassRendererData.grassStiffness);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassWindEffect, _grassRendererData.grassWindEffect);
            _grassPositionComputeShader.SetFloat(ShaderConstants.WindDissipation, _grassRendererData.grassWindDissipation);
        
            float defaultDirAngle = _grassRendererData.grassDefaultAngle * Mathf.PI * 2.0f;
            var grassDefaultDirection = (new Vector2(Mathf.Cos(defaultDirAngle), Mathf.Sin(defaultDirAngle))).normalized;
            _grassPositionComputeShader.SetVector(ShaderConstants.GrassDefaultFacing, grassDefaultDirection);
            _grassPositionComputeShader.SetFloat(ShaderConstants.GrassFacingRandomness, _grassRendererData.grassDirectionRandomness);
        
            _grassPositionComputeShader.SetFloat(ShaderConstants.ValueHolderA, _grassRendererData.valueHolderA);
            _grassPositionComputeShader.SetFloat(ShaderConstants.ValueHolderB, _grassRendererData.valueHolderB);
            _grassPositionComputeShader.SetFloat(ShaderConstants.ValueHolderC, _grassRendererData.valueHolderC);
        }
        
        private void RenderGrass()
        {
            RenderParams rp = new RenderParams(_material);
            rp.worldBounds = new Bounds(Vector3.zero, 1000*Vector3.one); // use tighter bounds
            rp.matProps = new MaterialPropertyBlock();
            //rp.matProps.SetBuffer(ShaderConstants.VertexObjectSpacePositions, _meshVerticesBuffer);
            rp.matProps.SetBuffer(ShaderConstants.GrassTransforms, _grassTransformsBuffer);
            rp.matProps.SetBuffer(ShaderConstants.VertexUVs, _meshUVsBuffer);
        
            rp.matProps.SetFloat(ShaderConstants.GrassBend, _grassRendererData.grassBend);
            rp.matProps.SetFloat(ShaderConstants.GrassBendPos, _grassRendererData.grassBendPos);
            rp.matProps.SetFloat(ShaderConstants.ValueHolderA, _grassRendererData.valueHolderA);
            rp.matProps.SetFloat(ShaderConstants.ValueHolderB, _grassRendererData.valueHolderB);
            rp.matProps.SetFloat(ShaderConstants.ValueHolderC, _grassRendererData.valueHolderC);
            rp.matProps.SetFloat(ShaderConstants.GrassChunkCount, _chunksCount);
        
            Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, _meshTrianglesBuffer, _meshTrianglesBuffer.count,
                0, _grassCount);
        }
    }
}
