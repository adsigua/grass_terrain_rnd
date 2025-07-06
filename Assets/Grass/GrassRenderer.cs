using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
using Utils;

public class GrassRenderer : MonoBehaviour
{
    [SerializeField] private Material _material;
    [SerializeField] private Mesh _mesh;
    [SerializeField] private ComputeShader _grassPositionComputeShader;
    [SerializeField] private float _tileSize = 10.0f;
    [SerializeField] private int _grassDensity = 10;
    [SerializeField] private float _grassOffsetStrength = 0.1f;
    [SerializeField] private Vector2 _grassWidthScaleRange = new Vector2(1f, 1f);
    [SerializeField] private Vector2 _grassHeightScaleRange = new Vector2(1f, 1f);
    
    [SerializeField] private int _randomSeed = 0;
    [SerializeField, Range(-1,1)] private float _grassTilt = 0.1f;
    [SerializeField, Range(0,3)] private float _grassBend = 0.1f;
    [SerializeField, Range(0,1)] private float _grassBendPos = 0.5f;
    [SerializeField, Range(0.8f, 1.0f)] private float _grassWindDissipation = 0.99f;
    [SerializeField, Range(0.0f, 0.5f)] private float _grassWindEffect = 0.01f;
    [SerializeField, Range(0, 1)] private float _grassFlutter = 1.0f;
    [SerializeField, Range(0, 1)] private float _grassStiffness = 0.0f;
    [SerializeField, Range(0, 1)] private float _valueHolderA = 0.99f;
    [SerializeField, Range(-1,1)] private float _valueHolderB = 0.99f;
    [SerializeField, Range(-1,1)] private float _valueHolderC = 0.99f;
    
    [SerializeField, ReadOnly] private Vector2 _grassDefaultDirection;
    [SerializeField, Range(0,1)] private float _grassDefaultAngle = 0.0f;
    [SerializeField, Range(0,1)] private float _grassDirectionRandomness = 0.0f;
    
    private GraphicsBuffer _meshTriangles;
    private GraphicsBuffer _meshVertices;
    private GraphicsBuffer _meshUVs;
    private ComputeBuffer _grassWindValues;
    private ComputeBuffer _grassObjectTransforms;
    private ComputeBuffer _readBackArgsBuffer;

    [SerializeField] private RenderTexture _windValuesTex;
    [SerializeField, ReadOnly] private int _grassCount;
    private int _grassTrueSize;
    
    private void Start()
    {
        // note: remember to check "Read/Write" on the mesh asset to get access to the geometry data
        _meshTriangles = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _mesh.triangles.Length, sizeof(int));
        _meshTriangles.SetData(_mesh.triangles);
        _meshVertices = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _mesh.vertices.Length, 3 * sizeof(float));
        _meshVertices.SetData(_mesh.vertices);
        _meshUVs = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _mesh.uv.Length, 2 * sizeof(float));
        _meshUVs.SetData(_mesh.uv);
        _grassTrueSize = _grassDensity * _grassDensity;
        _grassWindValues = new ComputeBuffer(_grassTrueSize, sizeof(float) * 4);
        _grassObjectTransforms = new ComputeBuffer(_grassTrueSize, sizeof(float) * (3+(3*3)+2+2+1+1+1), ComputeBufferType.Append);
        _readBackArgsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
       
        _windValuesTex = new RenderTexture(_grassDensity, _grassDensity, GraphicsFormat.R32G32_SFloat, 0);
        _windValuesTex.enableRandomWrite = true;
        
        _grassPositionComputeShader.SetBuffer(0, ShaderConstants.GrassTransforms, _grassObjectTransforms);
        _grassPositionComputeShader.SetBuffer(0, ShaderConstants.GrassWindValues, _grassWindValues);
        
        _grassPositionComputeShader.SetBuffer(1, ShaderConstants.GrassWindValues, _grassWindValues);
        _grassPositionComputeShader.SetTexture(1, ShaderConstants.GrassWindTexture, _windValuesTex);
    }
    
    void OnDestroy()
    {
        _meshTriangles?.Dispose();
        _meshTriangles = null;
        _meshVertices?.Dispose();
        _meshVertices = null;
        _meshUVs?.Dispose();
        _meshUVs = null;
        _grassWindValues?.Dispose();
        _grassWindValues = null;
        _grassObjectTransforms?.Dispose();
        _grassObjectTransforms = null;
        _readBackArgsBuffer?.Dispose();
        _readBackArgsBuffer = null;
        _windValuesTex?.Release();
    }

    private void OnDrawGizmosSelected()
    {
        Color currentColor = Gizmos.color;
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(_tileSize, 2.0f, _tileSize));
        Gizmos.color = currentColor;
    }
    
    void Update()
    {
        InitGrass();
        RenderGrass();
    }
    
   

    private void InitGrass()
    {
        _grassPositionComputeShader.SetFloat(ShaderConstants.DeltaTime, Time.deltaTime);
        _grassPositionComputeShader.SetFloat(ShaderConstants.Time, Time.time);
        
        _grassPositionComputeShader.SetFloat(ShaderConstants.TerrainBufferWidth, Shader.GetGlobalFloat(ShaderConstants.TerrainBufferWidth));
        _grassPositionComputeShader.SetFloat(ShaderConstants.TerrainSize, Shader.GetGlobalFloat(ShaderConstants.TerrainSize));
        _grassPositionComputeShader.SetVector(ShaderConstants.TerrainCenter, Shader.GetGlobalVector(ShaderConstants.TerrainCenter));
        
        _grassPositionComputeShader.SetVector(ShaderConstants.TileCenter, transform.position);
        _grassPositionComputeShader.SetFloat(ShaderConstants.TileSize, _tileSize);
        _grassPositionComputeShader.SetFloat(ShaderConstants.TileDensity, _grassDensity);
        
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassTilt, _grassTilt);
        _grassPositionComputeShader.SetFloat(ShaderConstants.RandomSeed, _randomSeed);
        _grassPositionComputeShader.SetFloat(ShaderConstants.TrueGrassCount, _grassTrueSize);
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassOffsetStrength, _grassOffsetStrength);
        _grassPositionComputeShader.SetVector(ShaderConstants.GrassScalingRange, new Vector4(_grassWidthScaleRange.x, _grassWidthScaleRange.y, _grassHeightScaleRange.x, _grassHeightScaleRange.y));
        
        float defaultDirAngle = _grassDefaultAngle * Mathf.PI * 2.0f;
        _grassDefaultDirection = (new Vector2(Mathf.Cos(defaultDirAngle), Mathf.Sin(defaultDirAngle))).normalized;
        _grassPositionComputeShader.SetVector(ShaderConstants.GrassDefaultFacing, _grassDefaultDirection);
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassFacingRandomness, _grassDirectionRandomness);
        
        var windTex = Shader.GetGlobalTexture(ShaderConstants.AmbientWindMap);
        Vector2 windTexSize = new Vector2(windTex.width, windTex.height);
        _grassPositionComputeShader.SetTexture(0, ShaderConstants.AmbientWindMap, windTex);
        _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindMapSize, windTexSize);
        
        var fluidTex = Shader.GetGlobalTexture(ShaderConstants.FluidMap);
        Vector2 fluidTexSize = new Vector2(fluidTex.width, fluidTex.height);
        _grassPositionComputeShader.SetTexture(0,ShaderConstants.FluidMap, fluidTex);
        _grassPositionComputeShader.SetVector(ShaderConstants.FluidMapSize, fluidTexSize);

        _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindCenter, Shader.GetGlobalVector(ShaderConstants.AmbientWindCenter));
        _grassPositionComputeShader.SetFloat(ShaderConstants.AmbientWindSize, Shader.GetGlobalFloat(ShaderConstants.AmbientWindSize));
        _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindDirection, Shader.GetGlobalVector(ShaderConstants.AmbientWindDirection));
        _grassPositionComputeShader.SetVector(ShaderConstants.AmbientWindStrength, Shader.GetGlobalVector(ShaderConstants.AmbientWindStrength));
        _grassPositionComputeShader.SetFloat(ShaderConstants.FluidMapWindStrength, Shader.GetGlobalFloat(ShaderConstants.FluidMapWindStrength));
        _grassPositionComputeShader.SetFloat(ShaderConstants.WindDissipation, _grassWindDissipation);
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassBend, _grassBend);
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassBendPos, _grassBendPos);
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassFlutter, _grassFlutter);
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassStiffness, _grassStiffness);
        _grassPositionComputeShader.SetFloat(ShaderConstants.GrassWindEffect, _grassWindEffect);
        
        _grassPositionComputeShader.SetFloat(ShaderConstants.ValueHolderA, _valueHolderA);
        _grassPositionComputeShader.SetFloat(ShaderConstants.ValueHolderB, _valueHolderB);
        _grassPositionComputeShader.SetFloat(ShaderConstants.ValueHolderC, _valueHolderC);
        
        _grassObjectTransforms.SetCounterValue(0);
        int threadSize = Mathf.CeilToInt(_grassDensity / 16.0f);
        _grassPositionComputeShader.Dispatch(0, threadSize, threadSize, 1);
        _grassPositionComputeShader.Dispatch(1, threadSize, threadSize, 1);
        
        SetGrassCount();
    }
    
    private void SetGrassCount()
    {
        ComputeBuffer.CopyCount(_grassObjectTransforms, _readBackArgsBuffer, 0);
        int[] appendBufferCount = new int[1];
        _readBackArgsBuffer.GetData(appendBufferCount);
        _grassCount = appendBufferCount[0];
    }
    
    private void RenderGrass()
    {
        RenderParams rp = new RenderParams(_material);
        rp.worldBounds = new Bounds(Vector3.zero, 1000*Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer(ShaderConstants.VertexObjectSpacePositions, _meshVertices);
        rp.matProps.SetBuffer(ShaderConstants.GrassTransforms, _grassObjectTransforms);
        rp.matProps.SetBuffer(ShaderConstants.VertexUVs, _meshUVs);
        
        rp.matProps.SetFloat(ShaderConstants.GrassBend, _grassBend);
        rp.matProps.SetFloat(ShaderConstants.GrassBendPos, _grassBendPos);
        rp.matProps.SetFloat(ShaderConstants.ValueHolderA, _valueHolderA);
        rp.matProps.SetFloat(ShaderConstants.ValueHolderB, _valueHolderB);
        rp.matProps.SetFloat(ShaderConstants.ValueHolderC, _valueHolderC);
        
        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, _meshTriangles, _meshTriangles.count,
            0, _grassTrueSize);
    }
    
}
