// AS CONTAS DE COR DO CENÁRIO — as mesmas da edição Godot (strike-kids-gles2,
// shaders/toon_leve*.shader, terreno_leve.shader, ceu_leve.shader), para a
// decoração, o chão e o céu saírem com a mesma cara nas duas edições:
//   CnS2L  sRGB -> linear sem potência (polinômio, erro ~1%; barato no vértice)
//   CnViva realça as cores menos saturadas (céu, madeira, chão), com um pouco
//          mais de contraste
//   CnTela a cor "crua" como sai na tela (a curva medida no Godot 4: sRGB ->
//          linear, filmic com branco 1,6 e linear -> sRGB) numa conta só,
//          sem potência nem divisão (erro < 2%; satura em 1,25)
// A LUZ DO CENÁRIO de agora vem do Temas.Luz (Shader.SetGlobal*): para onde
// está o sol, a cor e a densidade da neblina (linear) e a cor dela na tela.
#ifndef STRIKE_CENARIO_INCLUDED
#define STRIKE_CENARIO_INCLUDED

#include "UnityCG.cginc"

float4 _CnSolDir;       // xyz = para onde está o sol (coordenadas da Unity)
float4 _CnNeblina;      // rgb = cor da neblina (linear), a = densidade (por metro)
float4 _CnNeblinaTela;  // rgb = a cor da neblina como sai na tela

// o branco como sai na tela com luz 1: CnTela(1)
#define CN_BRANCO 0.97495

float3 CnS2L(float3 c)
{
    return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
}

float3 CnViva(float3 c)
{
    float lum = dot(c, float3(0.299, 0.587, 0.114));
    float sat = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
    c = lerp(float3(lum, lum, lum), c, 1.14 + 0.34 * (1.0 - sat));
    return saturate(c * 1.08 - 0.045);
}

float3 CnTela(float3 x)
{
    x = clamp(x, 0.0, 1.25);
    return CnViva(x * (1.2094 - x * (0.015 + 0.25 * x)));
}

// (as mesmas contas em meia precisão, para o pixel)
half3 CnVivaH(half3 c)
{
    half lum = dot(c, half3(0.299, 0.587, 0.114));
    half sat = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
    c = lerp(half3(lum, lum, lum), c, 1.14 + 0.34 * (1.0 - sat));
    return saturate(c * 1.08 - 0.045);
}

half3 CnTelaH(half3 x)
{
    x = clamp(x, 0.0, 1.25);
    return CnVivaH(x * (1.2094 - x * (0.015 + 0.25 * x)));
}

// as edições Godot têm o X ao contrário (Medidas.G): as contas que dependem
// do lugar no mundo (a fase do vento, as listras do chão, a nebulosa)
// usam o ponto como ele é lá, para o desenho sair igual
float3 CnGodot(float3 p)
{
    return float3(-p.x, p.y, p.z);
}

#endif
