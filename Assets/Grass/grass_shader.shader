Shader "Unlit/grass_shader"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal", 2D) = "white" {}
        _MRAO ("MRAO", 2D) = "white" {}
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
            //#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            
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

            struct Transform
            {
                float3 position;
                float3x3 rotation;
                float windFactor;
                float width;
                float height;
            };

            uniform sampler2D _AmbientWindMap;
            float4 _AmbientWindMap_ST;
            float4 _AmbientWindMap_TexelSize;

            float3 _AmbientWindCenter;
            float _AmbientWindSize;
            float _AmbientWindStrength;

            StructuredBuffer<float3> _VertexObjectSpacePositions;
            StructuredBuffer<float2> _VertexUVs;
            StructuredBuffer<Transform> _GrassTransforms;

            uniform float _Tilt = 0.5;
            uniform float _Bend = 0.1;
            uniform float _BendPos = 0.1;

            //https://www.shadertoy.com/view/XlGcRh
            //random value generator 1d and 2d
            uint pcg(uint v)
            {
	            uint state = v * 747796405u + 2891336453u;
	            uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
	            return (word >> 22u) ^ word;
            }
            
            uint2 pcg2d(uint2 v)
            {
                v = v * 1664525u + 1013904223u;

                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;

                v = v ^ (v>>16u);

                v.x += v.y * 1664525u;
                v.y += v.x * 1664525u;

                v = v ^ (v>>16u);

                return v;
            }

            uint3 pcg3d(uint3 v) {

                v = v * 1664525u + 1013904223u;

                v.x += v.y*v.z;
                v.y += v.z*v.x;
                v.z += v.x*v.y;

                v ^= v >> 16u;

                v.x += v.y*v.z;
                v.y += v.z*v.x;
                v.z += v.x*v.y;

                return v;
            }

            float easeIn(float x, float p) {
                return x == 0 ? 0 : pow(x, p);
            }

            float easeOut(float x, float p) {
                return 1 - pow(1 - x, p);
            }

            float easeOutOffset(float x, float power, float offset)
            {
                return (1.0 - offset) * (1 - pow(1 - x, power)) + offset;
            }

            float2 sampleBezier(float2 p, float l, float t)
            {
                return 2.0f * p * t * (1.0f - t) + float2(l, 1-abs(l)) * t * t;
            }

            float2 bezierTangent(float2 p, float l, float t)
            {
                return 2 * p * (1 - t) + 2 * t * (float2(l, 1-abs(l)) - p);
            }

            float2 bezierNormal(float2 p, float l, float t)
            {
                float2 tangent =  2 * p * (1 - t) + 2 * t * (float2(l, 1) - p);
                return float2(tangent.y, -tangent.x);
            }

            float3x3 getTiltRotation(float2 tiltDir)
            {
                //negate angle for LHR
                float angle = atan2(tiltDir.y, tiltDir.x);
                float sina, cosa;
                sincos(angle, sina, cosa);
                return  float3x3(
                    1, 0, 0,
                    0, cosa, -sina,
                    0, sina, cosa);
            }

            float3x3 getFacingRotation(float2 facingDir)
            {
                //negate angle for LHR
                float angle = -atan2(-facingDir.x, facingDir.y);
                float sina, cosa;
                sincos(angle, sina, cosa);
                return  float3x3(
                    cosa, 0, sina,
                    0, 1, 0,
                    -sina, 0, cosa);
            }

            half getFlutterValue(float t, float2 timeOffset)
            {
                half flutterLength = 0.5;
                half flutterSpeed = 5;
                half windFlutter = min(pow(_AmbientWindStrength + 0.5, 0.3) * 4, 24);

                float flutterTime = ((flutterLength * t + _Time.y) * flutterSpeed + (timeOffset * 73) );
                float normFlutter = sin(flutterTime) * 0.5 + 1.0;
                return normFlutter * (windFlutter / 100.0);
            }

            void computeMidAndTipValues(float2 uv, float hashVal, float ambientWind, out float2 midPoint, out float tipValue)
            {
                half flutterLength = 0.5;
                half flutterSpeed = 5;
                half windFlutter = min(pow(_AmbientWindStrength + 0.5, 0.3) * 4, 24);

                float flutterTime = ((flutterLength * uv.y + _Time.y) * flutterSpeed + (hashVal * 73));
                float normFlutter = sin(flutterTime) * 0.5 + 1.0;
                float fullterValue = normFlutter * (windFlutter / 100.0);
                //fullterValue = 0;
                
                float windTipScale = 1.0;
                tipValue = (fullterValue + ambientWind * 0.3) * windTipScale;
                //tipValue = _AmbientWindStrength;
                
                float windBendScale = 0.3;
                float bendValue = (-_Bend + (fullterValue) * windBendScale);
                bendValue = lerp(bendValue, _BendPos, abs(tipValue));
                
                //midPoint = float2(lerp(bendValue, 0, abs(tipValue)),_BendPos);
                midPoint = float2(bendValue, lerp(_BendPos, 0.1, abs(tipValue)));
            }
            
            float3 computeBezierPos(float2 uv, float height, float halfWidth, float2 midPoint, float tipValue)
            {
                float bezierLengthOffset = 3.0 / (length(float2(tipValue, 1-abs(tipValue))) * 2.0 + length(midPoint) + length(float2(tipValue, 1-abs(tipValue)) - midPoint));
                float2 sampleBez = sampleBezier(midPoint,tipValue, uv.y);
                float3 bezierPos = float3(0, sampleBez.y, sampleBez.x) * height * bezierLengthOffset;
                
                bezierPos.x += lerp(-halfWidth, halfWidth, uv.x) * saturate(1.0 - easeIn(uv.y, 3));
                return bezierPos;
            }

            void computeNormals(float2 uv, float2 midPoint, float tipValue, float3x3 totalRot, out float3 normalWS, out float3 tangentWS)
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

                float height = _GrassTransforms[instanceID].height;
                float halfWidth = _GrassTransforms[instanceID].width;

                //float2 grassXZFacing = _GrassTransforms[instanceID].facing;
                float3 grassWorldPos = _GrassTransforms[instanceID].position;
                float windFactor = _GrassTransforms[instanceID].windFactor;
                //windFactor = 0;
                half3 randomVals = (pcg3d(grassWorldPos * 619) % 100) / 100.0;

                //half flutterValue = getFlutterValue(o.uv.y, _GrassTransforms[instanceID].position.xz) * _AmbientWindStrength;
                //flutterValue = 0;

                float2 midPoint = float2(0, _BendPos);
                float tipValue = 0;
                computeMidAndTipValues(o.uv,_GrassTransforms[instanceID].position.x,  windFactor, midPoint, tipValue);
                
                float3 bezierPos = computeBezierPos(o.uv, height, halfWidth, midPoint, tipValue);

                //float tiltValue = _Tilt + windFactor * 0.05 * 0;
                //float3x3 tiltRotMat = getTiltRotation(float2(1.0 - abs(tiltValue), tiltValue));
                //float3x3 facingRotMat = getFacingRotation(grassXZFacing);
                //float3x3 totalRot = mul(_GrassTransforms[instanceID].rotation, tiltRotMat);

                float3x3 totalRot = _GrassTransforms[instanceID].rotation;
                computeNormals(o.uv, midPoint, tipValue, totalRot, o.normalWS, o.tangentWS);
                
                float3 wpos = mul(totalRot, bezierPos) + grassWorldPos;
                
                wpos = getViewOrthoOffset(o.uv, wpos, o.normalWS);
                
                o.wpos = wpos;
                o.pos = mul(UNITY_MATRIX_VP, float4(wpos, 1.0));
                //o.viewDirWS = normalize(_WorldSpaceCameraPos - wpos);
                o.grassPos = grassWorldPos;
                o.windValue = float3(windFactor.xxx);

                OUTPUT_SH(o.normalWS, o.vertexSH);

                //OUTPUT_SH4(o.wpos, o.normalWS, GetWorldSpaceNormalizeViewDir(o.normalWS), o.vertexSH, output.probeOcclusion);
                
                return o;
            }

            sampler2D _Albedo;
            sampler2D _NormalMap;
            sampler2D _MRAO;
            float4 _BaseColor;
            float3 _SubSurfColor;

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
                surfaceData.smoothness = 1.0 - mrao.g;
                surfaceData.normalTS = textureNormal;
                surfaceData.emission = 0;
                surfaceData.occlusion = mrao.b;
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

                //float windVal = frag.windValue.x;
                //float3 blueToGreen = lerp(float3(0,0,1), float3(0,1,0), saturate(windVal * 3));
                //float3 greenToRed = lerp(blueToGreen, float3(1,0,0), saturate((windVal - 0.66) * 3.0));
                
                //surfaceData.albedo = lerp(surfaceData.albedo, surfaceData.albedo * color, 0.6);
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
