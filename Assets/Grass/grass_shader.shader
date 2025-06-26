Shader "Unlit/grass_shader"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        _NormalMap ("Normal", 2D) = "white" {}
        _RoughnessMap ("Roughness", 2D) = "white" {}
        //_BaseColor ("Base Color", Color) = (0.0, 0.8, 0.0, 1.0)
        
        //_AmbientWindMap ("Texture", 2D) = "black" {}
        //_WindMap ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        cull off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 grassPos : TEXCOORD3;
                float3x3 tangentSpace : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct Transform
            {
                float3 position;
                float2 facing;
                float2 windVector;
                float width;
                float height;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _AmbientWindMap;
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
            float4 _BaseColor;

            float easeIn(float x, float p) {
                return x == 0 ? 0 : pow(x, p);
            }

            float easeOut(float x, float p) {
                return 1 - pow(1 - x, p);
            }

            float2 sampleBezier(float2 p, float l, float t)
            {
                return 2.0f * p * t * (1.0f - t) + float2(l, 1) * t * t;
            }

            float2 bezierTangent(float2 p, float l, float t)
            {
                return 2 * p * (1 - t) + 2 * t * (float2(l, 1) - p);
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

            float getFlutterValue(float t, float timeOffset)
            {
                float flutterLength = 0.5;
                float flutterSpeed= 5.0;
                float windFlutter = 3;

                float flutterTime = ((flutterLength * t + _Time.y) * flutterSpeed + timeOffset);
                float normFlutter = sin(flutterTime) * 0.5 + 1.0;
                return normFlutter * (windFlutter / 100.0);
            }

            //random value generator 2d to 2d
            //https://www.shadertoy.com/view/XlGcRh
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
            
            v2f vert (uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                o.uv = _VertexUVs[vertexID];

                float height = _GrassTransforms[instanceID].height;
                float halfWidth = _GrassTransforms[instanceID].width;

                float2 grassXZFacing = _GrassTransforms[instanceID].facing;
                float3 grassWorldPos = _GrassTransforms[instanceID].position;
                
                float2 randomValsA = (pcg2d(grassXZFacing.xy*379) % 100) / 100.0;
                float2 randomValsB = (pcg2d(grassXZFacing.xy*619) % 100) / 100.0;

                float2 windDir = _GrassTransforms[instanceID].windVector;
                float windFacingDot = dot(grassXZFacing, windDir);
                
                float ambientWind = lerp(-windDir.x, windDir.x, windFacingDot * 0.5 + 0.5)  * _AmbientWindStrength;
                
                float windBendScale = 0.6;
                float windTipScale = 1.0;
                float flutterValue = getFlutterValue(o.uv.y, randomValsA.x * 73)  * _AmbientWindStrength;

                float windTotal = (flutterValue + ambientWind * 0.1);
                
                float bendMultiplier = 0.0;
                float bendRandomOffset = lerp(1.0 - bendMultiplier, 1.0, randomValsA.y);
                float bendValue = (-_Bend + windTotal * windBendScale) * bendRandomOffset;
                float tipValue = windTotal * windTipScale; 
                float2 midPoint = float2(bendValue, _BendPos);

                float tiltMultiplier = 0.1;
                float tiltValue = _Tilt * (1 - tiltMultiplier + 2 * tiltMultiplier * randomValsB.x) + ambientWind * 0.05;

                float bezierLengthOffset = 3.0 / (length(float2(tipValue, 1)) * 2.0 + length(midPoint) + length(float2(tipValue, 1) - midPoint));
                float2 sampleBez = sampleBezier(midPoint, tipValue, o.uv.y);
                float3 bezierPos = float3(0, sampleBez.y, sampleBez.x) * height * bezierLengthOffset;
                
                bezierPos.x += lerp(-halfWidth, halfWidth, o.uv.x) * saturate(1.0 - easeIn(o.uv.y, 3));

                float3x3 tiltRotMat = getTiltRotation(float2(1.0 - abs(tiltValue), tiltValue));
                float3x3 facingRotMat = getFacingRotation(grassXZFacing);
                float3x3 totalRot = mul(facingRotMat, tiltRotMat);

                
                float2 bezTangent = bezierTangent(midPoint, tipValue, o.uv.y);
                //tangentsapce
                o.tangentSpace = float3x3(1,0,0,  0,1,0,  0,0,1);
                o.tangentSpace[0] = normalize(float3(0, bezTangent.yx));
                o.tangentSpace[2] = normalize(float3(0, bezTangent.x, -bezTangent.y));
                //rotate
                o.tangentSpace[0] = mul(totalRot, o.tangentSpace[0]);
                o.tangentSpace[2] = mul(totalRot, o.tangentSpace[2]);
                
                o.tangentSpace[1] = cross(o.tangentSpace[0], o.tangentSpace[1]);
                
                float3 grassNormal = o.tangentSpace[2];
                float3 wpos = mul(totalRot, bezierPos) + grassWorldPos;
                
                float2 viewDir = normalize(_WorldSpaceCameraPos.xz - wpos.xz);
                float facingViewDot = abs(dot(grassNormal.xz, viewDir));
               
                float sideThickness = 0.02;
                float3 sidePos = wpos + lerp(-grassNormal, grassNormal, o.uv.x) * sideThickness;
                
                wpos = lerp(sidePos, wpos, easeOut(facingViewDot, 4));
                
                o.wpos = wpos;
                o.pos = mul(UNITY_MATRIX_VP, float4(wpos, 1.0));
                o.grassPos = grassWorldPos;

                
                return o;
            }

            sampler2D _Albedo;
            sampler2D _NormalMap;
            sampler2D _RoughnessMap;
            
            float4 frag (v2f i) : SV_Target
            {
                //UNITY_SETUP_INSTANCE_ID(i);
                //float4 newCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float3 baseColor = tex2D(_Albedo, float2(i.uv.x, 0.5));
                float3 textureNormal = UnpackNormal(tex2D(_NormalMap, float2(i.uv.x, 0.5)));
                //float4 baseColor = tex2D(_Albedo, float2(i.uv.x, 0.5));UnpackNormal(tex2D(_BumpMap, i.uv));

                float3 normal = textureNormal.r * i.tangentSpace[0] +
                    textureNormal.g * i.tangentSpace[1] +
                    textureNormal.b * i.tangentSpace[2];
                
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                
                float diffuse = lerp(saturate(i.uv.y * i.uv.y), 1.0, 0.4);
                //float diffuse = 1;
                diffuse *= lerp(abs(dot(lightDir, normal)), 1.0, 0.4);
                return float4(baseColor * diffuse, 1);
                //return float4(lightDir, 1.0);
                return float4(normal, 1.0);

            }
            ENDHLSL
        }
    }
}

/**
            //P = (1−t)²P0 + 2(1−t)P1 + t²P2 quadtratic bezier
            float3 sampleBezier(float3 p0, float3 p1, float3 p2, float t)
            {
                return (1.0f - t) * (1.0f - t) * p0 +
                    2.0f * (1.0f - t) * t * p1 +
                    t * t * p2;
            }

            //2 * (1 - t) * (P1 - P0) + 2 * t * (P2 - P1).
            float3 bezierNormal(float3 p0, float3 p1, float3 p2, float3 orthoNormal, float t)
            {
                float3 tangent =  2 * (1 - t) * (p1 - p0) + 2 * t * (p2 - p1);
                //return float2(tangent.y, -tangent.x);
                return cross(tangent, orthoNormal);
            }

                v2f o;
                o.uv = _VertexUVs[vertexID];

                float2 transformFacing = _GrassTransforms[instanceID].facing;
                float3 objectWorldPos = mul(unity_ObjectToWorld, _GrassTransforms[instanceID].position);
                float height = _GrassTransforms[instanceID].height;
                float halfWidth = _GrassTransforms[instanceID].width;
                
                float3 facingDir = normalize(float3(transformFacing.x, 0, transformFacing.y));

                float3 grassDir = normalize(lerp(float3(0,1,0), facingDir, _Tilt));
                float3 grassTip = grassDir * height;
                float3 tipNormal = -facingDir;
                tipNormal.y = _Tilt;
                float3 bezierMidPoint = (grassTip * 0.7) + tipNormal * _Bend;

                float3 bezierPos = sampleBezier(float3(0,0,0), bezierMidPoint, grassTip, o.uv.y); 

                float3 orthoNormal = cross(facingDir, float3(0,1,0));
                
                float3 pos = bezierPos;
                pos.xz += lerp(-orthoNormal.xz, orthoNormal.xz, o.uv.x) * halfWidth * saturate(1.0 - easeIn(o.uv.y, 3));
                o.normal = mul(unity_ObjectToWorld, bezierNormal(float3(0,0,0), bezierMidPoint, grassTip, orthoNormal, o.uv.y));

                float3 wpos = mul(unity_ObjectToWorld, pos) + objectWorldPos;
                
                float2 viewDir = normalize(_WorldSpaceCameraPos.xz - wpos.xz);
                float facingViewDot = abs(dot(facingDir.xz, viewDir));
                
                float sideThickness = 0.02;
                float3 sidePos = wpos + lerp(-o.normal, o.normal, o.uv.x) * sideThickness;

                wpos = lerp(sidePos, wpos, easeOut(facingViewDot, 4));
                
                o.pos = mul(UNITY_MATRIX_VP, float4(wpos, 1.0));
                o.wpos = wpos;
                o.grassPos = objectWorldPos;
                
                return o;
**/