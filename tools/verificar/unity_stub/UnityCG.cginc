// Imitação mínima do UnityCG.cginc para conferir a sintaxe dos shaders com o
// glslangValidator (HLSL) fora da Unity. Não é usado no jogo.
#ifndef UNITY_CG_INCLUDED
#define UNITY_CG_INCLUDED
#define fixed half
#define fixed2 half2
#define fixed3 half3
#define fixed4 half4
uniform float4x4 unity_ObjectToWorld;
uniform float4x4 unity_WorldToObject;
uniform float4x4 unity_MatrixV;
uniform float4x4 unity_MatrixVP;
uniform float4x4 unity_MatrixMVP;
uniform float4x4 glstate_matrix_projection;
uniform float4x4 unity_CameraToWorld;
uniform float3 _WorldSpaceCameraPos;
uniform float4 _Time;
uniform float4 _ScreenParams;
uniform float4 _ProjectionParams;
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_MVP unity_MatrixMVP
#define UNITY_MATRIX_P glstate_matrix_projection
#define UNITY_MATRIX_MV mul(unity_MatrixV, unity_ObjectToWorld)
#define UNITY_MATRIX_IT_MV transpose(mul(unity_WorldToObject, unity_MatrixInvV))
uniform float4x4 unity_MatrixInvV;
#define TRANSFORM_TEX(tex, name) (tex.xy * name##_ST.xy + name##_ST.zw)
inline float4 UnityObjectToClipPos(float4 pos) { return mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(pos.xyz, 1.0))); }
inline float4 UnityObjectToClipPos(float3 pos) { return mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(pos, 1.0))); }
inline float3 UnityObjectToWorldNormal(float3 n) { return normalize(mul(n, (float3x3)unity_WorldToObject)); }
inline float3 UnityObjectToWorldDir(float3 d) { return normalize(mul((float3x3)unity_ObjectToWorld, d)); }
inline float4 ComputeScreenPos(float4 p) { float4 o = p * 0.5; o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w; o.zw = p.zw; return o; }
#endif
