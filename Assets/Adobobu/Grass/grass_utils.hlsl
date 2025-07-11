#ifndef GRASS_UTILS_H
#define GRASS_UTILS_H

#define _TOP 0
#define _BOTTOM 1
#define _LEFT 2
#define _RIGHT 3
#define _NEAR 4
#define _FAR 5

#define PI  3.141592653
#define TAU 6.283185307

struct GrassChunk
{
    float4 position;
};

struct Transform
{
    float3 position;
    float3x3 rotation;
    float2 bezierMidPoint;
    float2 bezierEndPoint;
    float3 windFactor;
    float width;
    float height;
};

struct TerrainData
{
    float height;
    float3 normal;
    float3 tangent;
};

struct Plane
{
    float3 normal;
    float distance;
};

struct Bounds
{
    float3 center;
    float3 extents;
};

int getPosBufferIndex(int2 id, int bufferWidth)
{
    return id.x + id.y * bufferWidth;
}

//random value generator 2d to 2d
//https://www.shadertoy.com/view/XlGcRh

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

float2 sampleBezier(float2 p, float2 l, float t)
{
    return 2.0f * p * t * (1.0f - t) + l * t * t;
}

float2 sampleBezierTangent(float2 p, float2 l, float t)
{
    return 2 * p * (1 - t) + 2 * t * (l - p);
}

float3 computeBezierPos(float2 uv, float height, float bezierLengthOffset, float halfWidth, float2 midPoint, float2 tipPoint)
{
    float2 sampleBez = sampleBezier(midPoint,tipPoint, uv.y);
    float3 bezierPos = float3(0, sampleBez.y, sampleBez.x) * height * bezierLengthOffset;
                
    bezierPos.x += lerp(-halfWidth, halfWidth, uv.x) * saturate(1.0 - easeIn(uv.y, 3));
    return bezierPos;
}

void computeNormals(float2 uv, float height, float bezierLengthOffset, float2 midPoint, float2 tipValue,
    float3x3 totalRot, out float3 normalWS, out float3 tangentWS)
{
    float2 bezTangent = sampleBezierTangent(midPoint, tipValue, uv.y) * height * bezierLengthOffset;
    tangentWS = normalize(float3(0, bezTangent.yx));
    normalWS = normalize(float3(0, bezTangent.x, -bezTangent.y));
                
    tangentWS = mul(totalRot, tangentWS);
    normalWS = mul(totalRot, normalWS);
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

float3x3 getTiltRotation(float tiltValue)
{
    //negate angle for LHR
    float angle = tiltValue * PI;
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

float3x3 getDirectionRotation(float3 direction)
{
    float3 xaxis = normalize(cross(float3(0,1,0), direction));
    float3 yaxis = normalize(cross(direction, xaxis));
    float3x3 rotation;
    rotation[0].x = xaxis.x;
    rotation[0].y = yaxis.x;
    rotation[0].z = direction.x;
    
    rotation[1].x = xaxis.y;
    rotation[1].y = yaxis.y;
    rotation[1].z = direction.y;
    
    rotation[2].x = xaxis.z;
    rotation[2].y = yaxis.z;
    rotation[2].z = direction.z;
    return rotation;
}
#endif