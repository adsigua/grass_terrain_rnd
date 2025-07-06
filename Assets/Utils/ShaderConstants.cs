using UnityEngine;

namespace Utils
{
    public class ShaderConstants
    {
        public static readonly int VertexObjectSpacePositions = Shader.PropertyToID("_VertexObjectSpacePositions");
        public static readonly int GrassTransforms = Shader.PropertyToID("_GrassTransforms");
        public static readonly int GrassWindValues = Shader.PropertyToID("_GrassWindValues");
        public static readonly int GrassWindTexture = Shader.PropertyToID("_WindValuesTexture");
        public static readonly int VertexUVs = Shader.PropertyToID("_VertexUVs");
        
        public static readonly int ValueHolderA = Shader.PropertyToID("_ValueHolderA");
        public static readonly int ValueHolderB = Shader.PropertyToID("_ValueHolderB");
        public static readonly int ValueHolderC = Shader.PropertyToID("_ValueHolderC");
        
        public static readonly int Time = Shader.PropertyToID("_Time");
        public static readonly int DeltaTime = Shader.PropertyToID("_DeltaTime");
        public static readonly int RandomSeed = Shader.PropertyToID("_RandomSeed");
        
        public static readonly int TileCenter = Shader.PropertyToID("_TileCenter");
        public static readonly int TileSize = Shader.PropertyToID("_TileSize");
        public static readonly int TileDensity = Shader.PropertyToID("_TileDensity");
        
        public static readonly int GrassTilt = Shader.PropertyToID("_GrassTilt");
        public static readonly int GrassBend = Shader.PropertyToID("_GrassBend");
        public static readonly int GrassBendPos = Shader.PropertyToID("_GrassBendPos");
        public static readonly int GrassFlutter = Shader.PropertyToID("_GrassFlutter");
        public static readonly int GrassStiffness = Shader.PropertyToID("_GrassStiffness");
        public static readonly int GrassWindEffect = Shader.PropertyToID("_GrassWindEffect");
        public static readonly int TrueGrassCount = Shader.PropertyToID("_TrueGrassCount");
        public static readonly int GrassOffsetStrength = Shader.PropertyToID("_GrassOffsetStrength");
        public static readonly int GrassScalingRange = Shader.PropertyToID("_GrassScalingRange");
        public static readonly int GrassDefaultFacing = Shader.PropertyToID("_GrassDefaultFacing");
        public static readonly int GrassFacingRandomness = Shader.PropertyToID("_GrassFacingRandomness");

        public static readonly int AmbientWindMap = Shader.PropertyToID("_AmbientWindMap");
        public static readonly int AmbientWindSize = Shader.PropertyToID("_AmbientWindSize");
        public static readonly int AmbientWindMapSize = Shader.PropertyToID("_AmbientWindMapSize");
        public static readonly int AmbientWindCenter = Shader.PropertyToID("_AmbientWindCenter");
        public static readonly int AmbientWindDirection = Shader.PropertyToID("_AmbientWindDirection");
        public static readonly int AmbientWindStrength = Shader.PropertyToID("_AmbientWindStrength");
        
        public static readonly int AmbientWindSpeed = Shader.PropertyToID("_AmbientWindSpeed");
        public static readonly int AmbientWindFrequency = Shader.PropertyToID("_AmbientWindFrequency");
        
        public static readonly int FluidMap = Shader.PropertyToID("_FluidMap");
        public static readonly int FluidMapSize = Shader.PropertyToID("_FluidMapSize");
        public static readonly int FluidMapWindStrength = Shader.PropertyToID("_FluidMapWindStrength");
        
        public static readonly int WindDissipation = Shader.PropertyToID("_WindDissipation");
        
        public static readonly int TerrainDataBuffer = Shader.PropertyToID("_TerrainDataBuffer");
        public static readonly int TerrainBufferWidth = Shader.PropertyToID("_TerrainBufferWidth");
        public static readonly int TerrainSize = Shader.PropertyToID("_TerrainSize");
        public static readonly int TerrainCenter = Shader.PropertyToID("_TerrainCenter");
        public static readonly int TerrainHeight = Shader.PropertyToID("_TerrainHeight");
        public static readonly int TerrainNormalComputeStepSize = Shader.PropertyToID("_TerrainNormalComputeStepSize");
        public static readonly int TerrainPerlinScale = Shader.PropertyToID("_TerrainPerlinScale");
        public static readonly int TerrainFloor = Shader.PropertyToID("_TerrainFloor");
        public static readonly int TerrainTexture = Shader.PropertyToID("_TerrainTexture");
    }
}