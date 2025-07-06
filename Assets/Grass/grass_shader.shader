Shader "Unlit/grass_shader"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal", 2D) = "white" {}
        _MRAO ("MRAO", 2D) = "white" {}
        _RoughnessScale ("Roughness Scale", Float) = 0.5
        _AOScale ("AO Scale", Float) = 1.0
        [HDR] _SubSurfColor ("SubSurfColor", Color) = (0.2, 1.0, 0.2)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalRenderPipeline" }
        LOD 100

        cull off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            //#pragma shader_feature_local _RECEIVE_SHADOWS_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _FORWARD_PLUS
            //#pragma multi_compile_instancing
            //#include "UnityCG.cginc"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "grass_utils.hlsl"
            
            struct fragData
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 grassPos : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 5);
                float3 windValue : TEXCOORD6;
            };

            uniform sampler2D _AmbientWindMap;
            float4 _AmbientWindMap_ST;
            float3 _AmbientWindCenter;
            float _AmbientWindSize;

            float _GrassBend;
            float _GrassBendPos;
            float _ValueHolderA;
            float _ValueHolderB;
            float _ValueHolderC;

            StructuredBuffer<float3> _VertexObjectSpacePositions;
            StructuredBuffer<float2> _VertexUVs;
            StructuredBuffer<Transform> _GrassTransforms;

            float getFlutterValue(float t, float offset)
            {
                float curveVal = -4.0 * (t - 0.5) * (t - 0.5) + 1.0;
                return offset * (1.0 - curveVal) + curveVal;
            }
            
            void computeNormals(float2 uv, float2 midPoint, float2 tipValue, float3x3 totalRot, out float3 normalWS, out float3 tangentWS)
            {
                float2 bezTangent = bezierTangent(midPoint, tipValue, uv.y);
                
                tangentWS = normalize(float3(0, bezTangent.yx));
                normalWS = normalize(float3(0, bezTangent.x, -bezTangent.y));
                
                tangentWS = mul(totalRot, tangentWS);
                normalWS = mul(totalRot, normalWS);
            }

            float3 getViewOrthoOffset(float uv, float3 posWS, float3 normalWS)
            {
                float2 viewDir = normalize(_WorldSpaceCameraPos.xz - posWS.xz);
                float facingViewDot = abs(dot(normalWS.xz, viewDir));
               
                float sideThickness = 0.02;
                float3 sidePos = posWS + lerp(-normalWS, normalWS, uv.x) * sideThickness;

                return lerp(sidePos, posWS, easeOut(facingViewDot, 4));
            }
            
            fragData vert (uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
                fragData o;
                o.uv = _VertexUVs[vertexID];
                Transform transform = _GrassTransforms[instanceID];

                float height = transform.height;
                float halfWidth = transform.width;

                float3 grassWorldPos = transform.position;
                float windFactor = transform.windFactor;
                //half3 randomVals = (pcg3d(grassWorldPos * 619) % 100) / 100.0;
                //
                // float stiffness = lerp(0, 1.0 - o.uv.y, _ValueHolderB);
                // float2 tipPoint = lerp(transform.bezierEndPoint, float2(0,1), stiffness);
                // float2 midPoint = lerp(transform.bezierMidPoint, float2(-_GrassBend*0.2, _GrassBendPos), stiffness);
                float2 tipPoint = transform.bezierEndPoint;
                float2 midPoint = transform.bezierMidPoint;
                
                //computeMidAndTipValues(o.uv,_GrassTransforms[instanceID].position.x,  windFactor, midPoint, tipPoint);
                
                float3 bezierPos = computeBezierPos(o.uv, height, halfWidth, midPoint, tipPoint);
                
                float3x3 totalRot = transform.rotation;
                computeNormals(o.uv, midPoint, tipPoint, totalRot, o.normalWS, o.tangentWS);
                
                float3 wpos = mul(totalRot, bezierPos) + grassWorldPos;
                
                wpos = getViewOrthoOffset(o.uv, wpos, o.normalWS);
                
                o.wpos = wpos;
                o.pos = mul(UNITY_MATRIX_VP, float4(wpos, 1.0));
                
                o.grassPos = grassWorldPos;
                o.windValue = float3(windFactor.xxx);

                OUTPUT_SH(o.normalWS, o.vertexSH);

                return o;
            }

            sampler2D _Albedo;
            sampler2D _NormalMap;
            sampler2D _MRAO;
            float4 _BaseColor;
            float3 _SubSurfColor;
            float _RoughnessScale;
            float _AOScale;

            SurfaceData generateSurfaceData(fragData frag)
            {
                float randomUVY = (pcg(frag.grassPos.x * frag.grassPos.y) % 100) / 100.0;
                float2 texUV =  float2(frag.uv.x, randomUVY);
                
                float3 baseColor = tex2D(_Albedo, float2(0,frag.uv.y));
                float3 textureNormal = UnpackNormal(tex2D(_NormalMap, texUV));

                float3 mrao = tex2D(_MRAO, float2(frag.uv.x, frag.uv.y));
                
                SurfaceData surfaceData;
                surfaceData.albedo = baseColor;
                surfaceData.specular = 0;
                surfaceData.metallic = mrao.r;
                surfaceData.smoothness = 1.0 - (mrao.g * _RoughnessScale);
                surfaceData.normalTS = textureNormal;
                surfaceData.emission = 0;
                surfaceData.occlusion = (mrao.b * _AOScale);
                surfaceData.alpha = 0;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;
                return surfaceData;
            }

            float getTranslucencyValue(float3 lightDirWS, float3 normalWS, float3 viewDirWS)
            {
                float normalInfluence = 0.6;

                float3 shiftedLight = -normalize(lightDirWS + (normalWS * normalInfluence));

                float lightViewDot = saturate(dot(shiftedLight, viewDirWS));

                float subSurfPower = 2.0;
                float subSurfIntensity = 1.0;

                return pow(lightViewDot, subSurfPower) * subSurfIntensity;
            }

            InputData generateInputData(fragData frag, float3 normalTS, bool isFront)
            {
                InputData inputData = (InputData)0;
                inputData.positionCS = frag.pos;
                inputData.positionWS = frag.wpos;

                //float3 normalWS = frag.normalWS;
                float3 tangetWS = frag.tangentWS;
                float3 normalWS = isFront ? frag.normalWS : -frag.normalWS;
                //float3 tangetWS = isFront ? frag.tangentWS : -frag.tangentWS;
                
                float3 biTangent = cross(normalWS, tangetWS);
                float3x3 tangentToWorld = float3x3(tangetWS, biTangent, normalWS);

                inputData.tangentToWorld = tangentToWorld;
                inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);

                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);

                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(frag.wpos);;

                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(frag.pos);

                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);

                inputData.bakedGI = SampleSHPixel(frag.vertexSH, inputData.normalWS);
                inputData.shadowMask = half4(1,1,1,1);

                return inputData;
            }
            
            float4 frag (fragData frag, bool isFront : SV_IsFrontFace) : SV_Target
            {
                SurfaceData surfaceData = generateSurfaceData(frag);
                InputData inputData = generateInputData(frag, surfaceData.normalTS, isFront);

                Light light = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                float3 lightDirWS = light.direction;
                
                float subSurfMask = getTranslucencyValue(lightDirWS, inputData.normalWS, inputData.viewDirectionWS);
                
                //float thickness = 1.0 - surfaceData.occlusion;
                float3 subSurfColor = _SubSurfColor * subSurfMask * surfaceData.occlusion;

                surfaceData.albedo += subSurfColor;

                float lightNormalDot = dot(light.direction, inputData.normalWS);
                float shadowFade = light.shadowAttenuation * lerp(1.0, 1, lightNormalDot + 1.0);

                inputData.bakedGI += shadowFade;

                float windVal = tex2D(_AmbientWindMap, frag.wpos.xz / _AmbientWindSize + 0.5).x;
                
                float4 color0 = float4(0,0,.8, 0);
                float4 color1 = float4(0,.8,0, 0.3333);   
                float4 color2 = float4(.8,0,0, 0.7);
                
                float3 color = color0;
                
                float colorPos = saturate((windVal - color0.w) / (color1.w - color0.w)) * step(1, 2);
                color = lerp(color, color1, colorPos);

                colorPos = saturate((windVal - color1.w) / (color2.w - color1.w)) * step(2, 2);
                color = lerp(color, color2, colorPos);

                // float windVal = frag.windValue.x;
                // float3 blueToGreen = lerp(float3(0,0,1), float3(0,1,0), saturate(windVal * 3));
                // float3 greenToRed = lerp(blueToGreen, float3(1,0,0), saturate((windVal - 0.66) * 3.0));
                
                //surfaceData.albedo = lerp(surfaceData.albedo, surfaceData.albedo * color, 0.6);
                //surfaceData.albedo = frag.windValue;
                //surfaceData.albedo *= color ;
                float4 pbrColor = UniversalFragmentPBR(inputData, surfaceData);
                
                return pbrColor;
                
                //return float4(subSurfColor, 1.0);
                //return float4(lerp(inputData.normalWS, isFront.xxx, step(0.5,frag.uv.x)), 1.0);
                //return float4(lightDir, 1.0);
                return float4(frag.grassPos, 1.0);

            }
            ENDHLSL
        }
    }
}
