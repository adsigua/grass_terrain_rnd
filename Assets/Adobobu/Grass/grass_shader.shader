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
        //ZWrite Off
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
                float3 windFactor : TEXCOORD6;
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
            float _GrassChunkCount;

            StructuredBuffer<float2> _VertexUVs;
            StructuredBuffer<Transform> _GrassTransforms;
 
            float getFlutterValue(float t, float offset)
            {
                float curveVal = -4.0 * (t - 0.5) * (t - 0.5) + 1.0;
                return offset * (1.0 - curveVal) + curveVal;
            }
            
            float4 getViewOrthoOffset(float2 uv, float3 grassPosWS, float3 tipDir, float3 posWS, float3 normalWS, out float dotResult)
            {
                // float3 camPosRight = _WorldSpaceCameraPos;
                // camPosRight.y = grassPosWS.y;
                //
                // float3 viewDir = normalize(UNITY_MATRIX_IT_MV[2].xyz);
                // viewDir = mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                // float3 viewDirRight = -viewDir;
                // viewDirRight.y = 0;
                // viewDirRight = normalize(viewDirRight);
                float3 viewDir = normalize(_WorldSpaceCameraPos - grassPosWS);
                float3 normalRight = cross(normalWS, float3(0,1,0));

                float rightDot = dot(normalize(normalRight), normalize(viewDir));
                float forwardDot = dot(normalize(normalWS), normalize(viewDir));
                
                float upDot = dot(tipDir, viewDir);

                float sideSmoothStep = smoothstep(0.95, 1.0, abs(rightDot));
                float sideFrontSmoothStep = smoothstep(0.2, 0.0, abs(forwardDot));
                float sideThickness = 0.04 * max(sideFrontSmoothStep,sideSmoothStep) * sign(rightDot) * sign(forwardDot);
                
                float topSmoothStep = smoothstep(0.99, 1.0, abs(upDot));
                float topThickness = 0.2 * smoothstep(0.95, 1.0, abs(upDot)) * sign(upDot);
               
                float3 sideNormalOffset = lerp(normalWS, -normalWS, uv.x) * sideThickness;
                float3 frontNormalOffset = lerp(0, -normalWS, uv.y) * topThickness;
                float3 finalPos = posWS + sideNormalOffset + frontNormalOffset;
                //float3 finalPos = posWS + sideNormalOffset + frontNormalOffset;
                //finalPos = posWS;
                
                float4 offsetCS = mul(UNITY_MATRIX_VP, float4(finalPos, 1.0));
                //float4 grassCS = mul(UNITY_MATRIX_VP, float4(normalWS, 1.0));
                dotResult = topSmoothStep;
                
                //offsetCS.xy = offsetCS.xy + grassCS.xy * sideThickness * 0.1;
                //float3 sidePos = posWS + lerp(-normalWS, normalWS, uv.x) * sideThickness * sideValue;
                //return lerp(sidePos, posWS, easeIn(facingViewDot, 4));
                //return lerp(sidePos, posWS, sideValue);
                
                return offsetCS;
            }
            
            fragData vert (uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
                fragData o;
                o.uv = _VertexUVs[vertexID];
                Transform transform = _GrassTransforms[instanceID];

                float height = transform.height;
                float halfWidth = transform.width;
                float3 grassWorldPos = transform.position;

                // float height = 3;
                // float halfWidth = 0.2;
                // float3 grassWorldPos =  transform.position;
                
                float2 tipPoint = transform.bezierEndPoint;
                float2 midPoint = transform.bezierMidPoint;
                
                float3x3 totalRot = transform.rotation;
                float bezierLengthOffset = 3.0 / (length(tipPoint) * 2.0 + length(midPoint) + length(tipPoint - midPoint));

                // float3 averageNormal = mul(totalRot, normalize(float3(0, tipPoint.x, -tipPoint.y)));
                // float3 tipDir = mul(totalRot, normalize(float3(0, tipPoint.y, tipPoint.x) * height * bezierLengthOffset));
                // float3 grassCenter = grassWorldPos + tipDir * (height * 0.5);
                //
                // float3 viewDir = normalize(_WorldSpaceCameraPos - grassCenter);
                //
                // float3 xaxis = normalize(cross(float3(0,1,0), viewDir));
                // float3 yaxis = normalize(cross(viewDir, xaxis));
                // float3x3 viewRot;
                // viewRot[0].x = xaxis.x;
                // viewRot[0].y = yaxis.x;
                // viewRot[0].z = viewDir.x;
                //
                // viewRot[1].x = xaxis.y;
                // viewRot[1].y = yaxis.y;
                // viewRot[1].z = viewDir.y;
                //
                // viewRot[2].x = xaxis.z;
                // viewRot[2].y = yaxis.z;
                // viewRot[2].z = viewDir.z;
                // float3x3 offsetRot = mul(viewRot, totalRot);
                //
                // float forwardDot = dot(normalize(averageNormal), viewDir);
                // float sideFrontSmoothStep = smoothstep(0.05, 0.0, abs(forwardDot));

                //totalRot = lerp(totalRot, offsetRot, sideFrontSmoothStep * 0.6);

                float3 bezierPos = computeBezierPos(o.uv, height, bezierLengthOffset, halfWidth, midPoint, tipPoint);

                o.normalWS = float3(0,0,-1);
                o.tangentWS = float3(0,1,0);
                computeNormals(o.uv, height, bezierLengthOffset, midPoint, tipPoint, totalRot, o.normalWS, o.tangentWS);
                
                float3 wpos = mul(totalRot, bezierPos) + grassWorldPos;
                
                o.wpos = wpos;
                
                o.pos = mul(UNITY_MATRIX_VP, float4(wpos, 1.0));
                //o.pos = getViewOrthoOffset(o.uv, grassCenter, tipDir, wpos, averageNormal, rightDot);
                
                o.grassPos = grassWorldPos;

                float4 csPos = mul(UNITY_MATRIX_VP, float4(wpos, 1.0));
                float depth = csPos.z / csPos.w;
                
                o.windFactor = transform.windFactor;
                //o.windFactor = depth.xxx;

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
                //surfaceData.albedo = frag.windFactor;
                //surfaceData.albedo *= color ;
                float4 pbrColor = UniversalFragmentPBR(inputData, surfaceData);
                
                //return pbrColor;
                //return float4(subSurfColor, 1.0);
                //return float4(lerp(inputData.normalWS, isFront.xxx, step(0.5,frag.uv.x)), 1.0);
                //return float4(lightDir, 1.0);

                float depthStep = step(_ValueHolderB, frag.windFactor.xxx);

                float4 xPos = mul(UNITY_MATRIX_VP, float4(frag.wpos, 1.0));

                float xScreenPos = xPos.x / xPos.w;
                float windFactorValue = lerp(frag.windFactor.x, frag.windFactor.y, step(0.0, xScreenPos));
                
                return float4(windFactorValue.xxx, 1.0);
                return float4(frag.windFactor.yyy, 1.0);

            }
            ENDHLSL
        }
    }
}
