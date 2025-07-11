Shader "Unlit/Terrain"
{
    Properties
    {
        _BaseColor("Color", Color) = (1,1,1,1)
        _MixColor("Mix Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _HeightMap ("HeightMap", 2D) = "white" {}
        _TerrainSize ("Terrain Size", Float) = 200
        _TerrainHeight ("Terrain Height", Float) = 100
        _TerrainPerlinScale ("Perlin Scale", Float) = 100
        _TessellationEdgeLength("Tessellation Edge Length", Range(0.0, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalRenderPipeline"  }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma target 5.0

            #if !defined(TESSELLATION_INCLUDED)
            #define TESSELLATION_INCLUDED
            
            #pragma vertex vert
            #pragma hull hull
            #pragma domain domain
            //#pragma geometry geom
            //#pragma fragment fragGeom

            #pragma fragment frag

            
            //#include "UnityCG.cginc"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "terrain_utils.hlsl"

            struct vertData
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct TessControlPoint
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : INTERNALTESSPOS;
                float3 normalWS : NORMAL;
            };

            struct TessFactors
            {
                float edge[3] : SV_TessFactor;
                float inside : SV_InsideTessFactor;
            };

            struct geomData
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 barycentric : BARYCENTRIC;
            };
            
            struct fragData
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            
            sampler2D _HeightMap;
            float4 _HeightMap_ST;
            float4 _HeightMap_TexelSize;

            TessControlPoint vert (vertData IN)
            {
                TessControlPoint OUT;

                OUT.positionWS =  mul(unity_ObjectToWorld, float4(IN.positionOS, 1.0));
                OUT.positionCS = mul(UNITY_MATRIX_VP, float4(OUT.positionWS, 1.0));
                OUT.normalWS = mul(unity_ObjectToWorld, float4(IN.normalOS, 1.0));
                
                return OUT;
            }

            [domain("tri")]
            [outputcontrolpoints(3)]
            [outputtopology("triangle_cw")]
            [patchconstantfunc("PatchConstantFunction")]
            [partitioning("fractional_odd")]
            TessControlPoint hull(InputPatch<TessControlPoint, 3> patch, uint id : SV_OutputControlPointID)
            {
                return patch[id];
            }
            
            float _TessellationEdgeLength;
            float _FrustumCullingTolerance = 0.8;
            float _BackFaceCullingTolerance = 0.8;

            bool IsOutOfBounds(float3 p, float3 lower, float3 higher)
            {
                return p.x < lower.x || p.x > higher.x ||
                    p.y < lower.y || p.y > higher.y ||
                    p.z < lower.z || p.z > higher.z;
            }

            bool IsPointOutOfFrustum(float4 posCS, float tolerance)
            {
                float3 cullingPos  = posCS.xyz;
                float w = posCS.w;
                float lowerW = -w - tolerance;
                float higherW = w + tolerance;
                float farClipValue = -1;
                #if UNITY_REVERSED_Z
                    farClipValue *= -1;
                #endif
                float3 lowerBounds = float3(lowerW, lowerW, lowerW * farClipValue);
                float3 higherBounds = float3(higherW, higherW, higherW);
                return IsOutOfBounds(cullingPos, lowerBounds, higherBounds);
            }

            bool ShouldBackFaceCull(float4 p0PosCS, float4 p1PosCS, float4 p2PosCS, float tolerance)
            {
                float3 point0 = p0PosCS.xyz / p0PosCS.w;
                float3 point1 = p1PosCS.xyz / p1PosCS.w;
                float3 point2 = p2PosCS.xyz / p2PosCS.w;
                #if UNITY_REVERSED_Z
                    return cross(point1 - point0, point2 - point0).z < -tolerance;
                #else
                    return cross(point1 - point0, point2 - point0).z > tolerance;
                #endif
            }
            
            bool ShouldClipPatch(float4 p0PosCS, float4 p1PosCS, float4 p2PosCS)
            {
                bool pointsOutside = IsPointOutOfFrustum(p0PosCS, _FrustumCullingTolerance) &&
                    IsPointOutOfFrustum(p1PosCS, _FrustumCullingTolerance) &&
                    IsPointOutOfFrustum(p2PosCS, _FrustumCullingTolerance);
                bool isBackFace = ShouldBackFaceCull(p0PosCS, p1PosCS, p2PosCS, _BackFaceCullingTolerance);
                return isBackFace;
                return false;
            }

            float TessellationEdgeFactor (TessControlPoint cp0, TessControlPoint cp1)
            {
	            float3 p0 = cp0.positionWS;
		        float3 p1 = cp1.positionWS;
		        float edgeLength = distance(p0, p1);

		        float3 edgeCenter = (p0 + p1) * 0.5;
		        float viewDistance = distance(edgeCenter, _WorldSpaceCameraPos);

		        return edgeLength / (_TessellationEdgeLength * viewDistance);
            }

            TessFactors PatchConstantFunction(InputPatch<TessControlPoint, 3> patch)
            {
                TessFactors f;
                // if (ShouldClipPatch(patch[0].positionCS,patch[1].positionCS,patch[2].positionCS))
                // {
                // f.edge[0] = 16;
                // f.edge[1] = 16;
                // f.edge[2] = 16;
                // f.inside =  16;
                // }
                // else
                // {
                    f.edge[0] = TessellationEdgeFactor(patch[1], patch[2]);
                    f.edge[1] = TessellationEdgeFactor(patch[2], patch[0]);
                    f.edge[2] = TessellationEdgeFactor(patch[0], patch[1]);
                    f.inside =  (TessellationEdgeFactor(patch[1], patch[2]) +
                        TessellationEdgeFactor(patch[2], patch[0]) +
                        TessellationEdgeFactor(patch[0], patch[1])) * (1.0/3.0);
                // }
                return f;
            }

            #define BARYCENTRIC_INTERP(fieldName) \
                patch[0].fieldName * barycentricCoordinates.x + \
                patch[1].fieldName * barycentricCoordinates.y + \
                patch[2].fieldName * barycentricCoordinates.z

            uniform StructuredBuffer<TerrainData> _TerrainDataBuffer;
            uniform float _TerrainBufferWidth;
            uniform float _TerrainSize;
            uniform float3 _TerrainCenter;

            int getPosBufferIndex(int2 id)
            {
                return id.x + id.y * _TerrainBufferWidth;
            }

            TerrainData SampleTerrainBufferBilerp(float2 posXZ)
            {
                float2 uv = ((posXZ - _TerrainCenter.xz) / _TerrainSize) + 0.5;
                float2 texelPos = uv * _TerrainBufferWidth;
                int2 floorPos = texelPos;
                float2 blend = texelPos - floorPos;

                TerrainData xL = _TerrainDataBuffer[getPosBufferIndex(floorPos)];
                TerrainData xR = _TerrainDataBuffer[getPosBufferIndex(floorPos + int2(1.0, 0.0))];
                TerrainData xB = _TerrainDataBuffer[getPosBufferIndex(floorPos + int2(0.0, 1.0))];
                TerrainData xT = _TerrainDataBuffer[getPosBufferIndex(floorPos + int2(1.0, 1.0))];
                
                float3 normalX = lerp(xL.normal, xR.normal, blend.x);
                float3 normalY = lerp(xB.normal, xT.normal, blend.x);

                float3 tangentX = lerp(xL.tangent, xR.tangent, blend.x);
                float3 tangentY = lerp(xB.tangent, xT.tangent, blend.x);

                float heightX = lerp(xL.height, xR.height, blend.x);
                float heightY = lerp(xB.height, xT.height, blend.x);

                TerrainData td;
                td.height = lerp(heightX, heightY, blend.y);
                td.normal = lerp(normalX, normalY, blend.y);
                td.tangent = lerp(tangentX, tangentY, blend.y);
                return td;
            }
            
            // float4 SampleTerrainBufferBilerp(float2 posXZ)
            // {
            //     float2 uv = ((posXZ - _TerrainCenter.xz) / _TerrainSize) + 0.5;
            //     float2 texelPos = uv * _TerrainBufferWidth;
            //     int2 floorPos = texelPos;
            //     float2 blend = texelPos - floorPos;
            //
            //     float4 xL = _TerrainBuffer[getPosBufferIndex(floorPos)];
            //     float4 xR = _TerrainBuffer[getPosBufferIndex(floorPos + int2(1.0, 0.0))];
            //     float4 xB = _TerrainBuffer[getPosBufferIndex(floorPos + int2(0.0, 1.0))];
            //     float4 xT = _TerrainBuffer[getPosBufferIndex(floorPos + int2(1.0, 1.0))];
            //     
            //     float4 col_x = lerp(xL, xR, blend.x);
            //     float4 col_y = lerp(xB, xT, blend.x);
            //     //return xR;
            //     return lerp(col_x, col_y, blend.y);
            // }
            
            [domain("tri")]
            fragData domain(TessFactors factors, OutputPatch<TessControlPoint,3> patch, float3 barycentricCoordinates : SV_DomainLocation)
            {
                fragData frag;

                //UNITY_SETUP_INSTANCE_ID(patch[0]);
                //UNITY_TRANSFER_INSTANCE_ID(patch[0], frag);
                //UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(frag);

                float3 positionWS = BARYCENTRIC_INTERP(positionWS);

                //float4 displacement = SampleTerrainBufferBilerp(positionWS.xz);
                TerrainData td = SampleTerrainBufferBilerp(positionWS.xz);
		        positionWS.y += td.height;
                
                frag.positionWS =  positionWS;
                frag.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                frag.normalWS = td.normal;

                return frag;
            }

            // [maxvertexcount(3)]
            // void geom(triangle fragData patch[3], inout TriangleStream<geomData> stream)
            // {
            //     geomData gs;
            //     for (uint i = 0; i < 3; i++)
            //     {
            //         gs.positionWS = patch[i].positionWS;
            //         gs.positionCS = patch[i].positionCS;
            //         gs.normalWS = patch[i].normalWS;
            //         gs.barycentric = float2(fmod(i,2.0), step(2.0,i));
            //         stream.Append(gs);
            //     }
            //     stream.RestartStrip();
            // }
            
            // fixed4 fragGeom (geomData i) : SV_Target
            // {
            //     
            //     float3 coord = float3(i.barycentric, 1.0 - i.barycentric.x - i.barycentric.y);
            //     coord = smoothstep(fwidth(coord)*0.1, fwidth(coord)*0.1 + fwidth(coord), coord);
            //     float val = 1.0 - min(coord.x, min(coord.y, coord.z));
            //     return float4(val.xxx, 0);
            //     
            //     return float4((i.positionWS / 10.0), 1.0);
            // }

            float4 _BaseColor;
            float4 _MixColor;
            
            float4 frag (fragData i) : SV_Target
            {
                //float3 lighDir = _WorldSpaceLightPos0;
                Light light = GetMainLight();
                
                float diffuseAmount = saturate(dot(light.direction, i.normalWS));
                
                return float4(lerp(_MixColor.rgb, _BaseColor.rgb, diffuseAmount), 1.0);
                return float4(diffuseAmount.xxx, 1.0);
                return float4(i.normalWS, 1.0);
            }

            #endif
            ENDHLSL
        }
    }
}
