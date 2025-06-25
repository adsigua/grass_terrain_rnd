Shader "Unlit/grass_shader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.0, 0.8, 0.0, 1.0)
        
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
                float3 normal : TEXCOORD2;
                float3 grassPos : TEXCOORD3;
                float3 ambientWindCol : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct Transform
            {
                float3 position;
                float2 facing;
                float width;
                float height;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            uniform sampler2D _WindMap;
            float4 _WindMap_ST;

                uniform sampler2D _AmbientWindMap;
            float4 _AmbientWindMap_ST;

            float3 _AmbientWindCenter;
            float _AmbientWindSize;
            float _AmbientWindStrength;

            StructuredBuffer<float3> _VertexObjectSpacePositions;
            StructuredBuffer<float2> _VertexUVs;
            StructuredBuffer<Transform> _GrassTransforms;

            uniform float _Tilt = 0.5;
            uniform float _Bend = 0.1;
            float4 _BaseColor;

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

            float3 rotateToFacing(float3 vertPos, float2 facingDir)
            {
                float degrees = atan2(facingDir.y, facingDir.x);
                float sina, cosa;
                sincos(degrees, sina, cosa);
                float2x2 rotMat = float2x2(
                    cosa, -sina,
                    sina, cosa);
                float2 rotatedPos = mul(rotMat, float2(vertPos.z,-vertPos.x));
                return float3(rotatedPos.x, vertPos.y, rotatedPos.y);
            }
            
            float easeInOutQuart(float x) {
                return x < 0.5 ? 16 * x * x * x * x * x : 1 - pow(-2 * x + 2, 5) / 2;
            }

            float easeIn(float x, float p) {
                return x == 0 ? 0 : pow(x, p);
            }

            float easeOut(float x, float p) {
                return 1 - pow(1 - x, p);
            }
            
            v2f vert (uint vertexID: SV_VertexID, uint instanceID : SV_InstanceID)
            {
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
            }
                
            float4 frag (v2f i) : SV_Target
            {
                //UNITY_SETUP_INSTANCE_ID(i);
                //float4 newCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float3 lightDir = _WorldSpaceLightPos0.xyz;
                
                float diffuse = lerp(saturate(i.uv.y * i.uv.y), 1.0, 0.4);
                //float diffuse = 1;
                diffuse *= lerp(abs(dot(lightDir, i.normal)), 1.0, 0.4);
                //return _BaseColor * diffuse;
                //return _BaseColor * diffuse;
                //return float4(lightDir, 1.0);
                //return float4(i.normal, 1.0);

                float2 ambientWindUV = (i.grassPos - _AmbientWindCenter).xz / _AmbientWindSize;
                float3 ambientWind = tex2D(_AmbientWindMap, ambientWindUV+0.5);
                
                return float4(ambientWind, 1.0);
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