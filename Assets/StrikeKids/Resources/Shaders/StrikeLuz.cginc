// A LUZ DO STRIKE KIDS (edição Unity, OpenGL ES 2.0 / Mali-450): calculada
// POR VÉRTICE (o processador de vértices da Mali-450 é separado e folgado; o
// de pixels é o gargalo). Sem luzes da Unity: o sol, o céu, o chão, a
// contraluz e a neblina de cada fase vêm de variáveis globais
// (Materiais.cs, Shader.SetGlobal*). Mesma conta do tools/modelador/previa.py.
#ifndef STRIKE_LUZ_INCLUDED
#define STRIKE_LUZ_INCLUDED

#include "UnityCG.cginc"

float4 _SkLuzDir;    // xyz = direção PARA o sol (normalizada)
half4 _SkLuzCor;     // rgb = cor x força do sol
half4 _SkCeu;        // rgb = luz que vem do céu (ambiente de cima)
half4 _SkChao;       // rgb = luz que vem do chão (ambiente de baixo)
half4 _SkBorda;      // rgb = contraluz (borda clara)
half4 _SkNeblina;    // rgb = cor da neblina, a = densidade (por metro)

// cor iluminada de um vértice; `nevoa` volta 1 (perto) .. 0 (só neblina)
half3 SkIluminar(float3 pm, half3 n, half3 albedo, out half nevoa)
{
    half ndl = dot(n, (half3)_SkLuzDir.xyz);
    half dif = saturate((ndl + 0.08) / 0.32);
    dif = dif * dif * (3.0 - 2.0 * dif);
    dif = 0.72 * dif + 0.28 * saturate(ndl);
    half t = saturate(n.y * 0.5 + 0.5);
    half3 amb = lerp(_SkChao.rgb, _SkCeu.rgb, t);
    float3 v = _WorldSpaceCameraPos.xyz - pm;
    float dist = length(v);
    half3 vn = (half3)(v / max(dist, 0.0001));
    half b = 1.0 - saturate(dot(n, vn));
    b = b * b * b;
    half3 c = albedo * (amb + _SkLuzCor.rgb * dif) + _SkBorda.rgb * b * (0.35 + 0.65 * t);
    nevoa = (half)exp(-_SkNeblina.a * dist);
    return c;
}

half SkNevoa(float3 pm)
{
    float dist = length(_WorldSpaceCameraPos.xyz - pm);
    return (half)exp(-_SkNeblina.a * dist);
}

#endif
