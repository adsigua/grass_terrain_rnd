using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;

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
    [SerializeField, Range(-1,1)] private float _grassBend = 0.1f;
    [SerializeField, Range(0,1)] private float _grassBendPos = 0.5f;
    [SerializeField, Range(0,1)] private float _grassWindDissipation = 0.99f;
    
    [FormerlySerializedAs("_grassDirection")] [SerializeField, ReadOnly] private Vector2 _grassDefaultDirection;
    [FormerlySerializedAs("_grassDefaultDirection")] [SerializeField, Range(0,1)] private float _grassDefaultAngle = 0.0f;
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
        _grassWindValues = new ComputeBuffer(_grassTrueSize, sizeof(float) * 2);
        _grassObjectTransforms = new ComputeBuffer(_grassTrueSize, sizeof(float) * (3+(3*3)+1+1+1), ComputeBufferType.Append);
        _readBackArgsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
       
        _windValuesTex = new RenderTexture(_grassDensity, _grassDensity, GraphicsFormat.R32G32_SFloat, 0);
        _windValuesTex.enableRandomWrite = true;
        
        _grassPositionComputeShader.SetBuffer(0, "_GrassTransforms", _grassObjectTransforms);
        _grassPositionComputeShader.SetBuffer(0, "_GrassWindValues", _grassWindValues);
        
        _grassPositionComputeShader.SetBuffer(1, "_GrassWindValues", _grassWindValues);
        _grassPositionComputeShader.SetTexture(1, "_WindValuesTexture", _windValuesTex);
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
        _grassPositionComputeShader.SetFloat("_DeltaTime", Time.deltaTime);
        _grassPositionComputeShader.SetFloat("_RandomSeed", _randomSeed);
        _grassPositionComputeShader.SetVector("_TileCenter", transform.position);
        _grassPositionComputeShader.SetFloat("_TileSize", _tileSize);
        _grassPositionComputeShader.SetFloat("_TileDensity", _grassDensity);
        _grassPositionComputeShader.SetFloat("_TrueGrassCount", _grassTrueSize);
        _grassPositionComputeShader.SetFloat("_GrassOffsetStrength", _grassOffsetStrength);
        _grassPositionComputeShader.SetVector("_GrassScalingRange", new Vector4(_grassWidthScaleRange.x, _grassWidthScaleRange.y, _grassHeightScaleRange.x, _grassHeightScaleRange.y));
        
        float defaultDirAngle = _grassDefaultAngle * Mathf.PI * 2.0f;
        _grassDefaultDirection = (new Vector2(Mathf.Cos(defaultDirAngle), Mathf.Sin(defaultDirAngle))).normalized;
        _grassPositionComputeShader.SetVector("_GrassDefaultFacing", _grassDefaultDirection);
        _grassPositionComputeShader.SetFloat("_GrassFacingRandomness", _grassDirectionRandomness);

        
        var windTex = Shader.GetGlobalTexture("_AmbientWindMap");
        var fluidTex = Shader.GetGlobalTexture("_FluidMap");

        Vector2 windTexSize = new Vector2(windTex.width, windTex.height);
        Vector2 fluidTexSize = new Vector2(fluidTex.width, fluidTex.height);
        _grassPositionComputeShader.SetTexture(0, "_AmbientWindMap", windTex);
        _grassPositionComputeShader.SetTexture(0, "_FluidMap", fluidTex);
        _grassPositionComputeShader.SetVector( "_AmbientWindMapSize", windTexSize);
        _grassPositionComputeShader.SetVector( "_FluidMapSize", fluidTexSize);
        
        _grassPositionComputeShader.SetFloat( "_GrassTilt", _grassTilt);
        _grassPositionComputeShader.SetFloat( "_TerrainBufferWidth", Shader.GetGlobalFloat("_TerrainBufferWidth"));
        _grassPositionComputeShader.SetFloat( "_TerrainSize", Shader.GetGlobalFloat("_TerrainSize"));
        _grassPositionComputeShader.SetVector( "_TerrainCenter", Shader.GetGlobalVector("_TerrainCenter"));

        _grassPositionComputeShader.SetVector( "_AmbientWindCenter", Shader.GetGlobalVector("_AmbientWindCenter"));
        _grassPositionComputeShader.SetFloat( "_AmbientWindSize", Shader.GetGlobalFloat("_AmbientWindSize"));
        _grassPositionComputeShader.SetVector( "_AmbientWindDirection", Shader.GetGlobalVector("_AmbientWindDirection"));
        _grassPositionComputeShader.SetFloat( "_AmbientWindStrength", Shader.GetGlobalFloat("_AmbientWindStrength"));
        _grassPositionComputeShader.SetFloat( "_FluidMapWindStrength", Shader.GetGlobalFloat("_FluidMapWindStrength"));
        _grassPositionComputeShader.SetFloat( "_WindDissipation", _grassWindDissipation);
        
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
        rp.worldBounds = new Bounds(Vector3.zero, 100*Vector3.one); // use tighter bounds
        rp.matProps = new MaterialPropertyBlock();
        rp.matProps.SetBuffer("_VertexObjectSpacePositions", _meshVertices);
        rp.matProps.SetBuffer("_GrassTransforms", _grassObjectTransforms);
        rp.matProps.SetBuffer("_VertexUVs", _meshUVs);
        rp.matProps.SetFloat("_Tilt", _grassTilt);
        rp.matProps.SetFloat("_Bend", _grassBend);
        rp.matProps.SetFloat("_BendPos", _grassBendPos);
        //rp.matProps.SetInt("_BaseVertexIndex", (int)_mesh.GetBaseVertex(0));
        //rp.matProps.SetMatrix("_ObjectToWorld", transform.localToWorldMatrix);
        //rp.matProps.SetFloat("_NumInstances", _grassCount);
        Graphics.RenderPrimitivesIndexed(rp, MeshTopology.Triangles, _meshTriangles, _meshTriangles.count, 0, _grassTrueSize);
        
    }
    
}
