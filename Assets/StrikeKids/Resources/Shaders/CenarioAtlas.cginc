// O TOON LEVE COM TEXTURA DA DECORAÇÃO (CenarioAtlas*.shader) — a mesma conta
// do toon_leve_tex.shader da edição Godot: a forma com poucos triângulos e o
// desenho (rostos, pintas, listras) no ATLAS da fase, já com a cor como sai
// na tela com luz 1 e com o sombreado dos cantinhos.
//  * o que é igual no material inteiro — a luz no sol, na sombra, o quanto a
//    borda clara soma em cada uma e a das partes que brilham — vem pronto da
//    CPU (Temas.Fatores: _FSol, _FSombra, _FBordaSol, _FBordaSombra, _FAcesa);
//    no vértice sobra uma curva de cor só (a da cor do vértice);
//  * por PIXEL só a divisa entre o sol e a sombra (1 clamp, 1 lerp) e a
//    leitura do atlas.
// Variantes (#define antes do #include):
//   CN_VENTO  as plantas balançam (o quanto cada vértice anda vem no UV3:
//             x = metros no topo, y = rapidez — Temas.ComVento)
//   CN_ASAS   as asas do pterodáctilo batem (giram no ombro, eixo Z)
#include "StrikeCenario.cginc"

sampler2D _MainTex;
float _Faixa;
half4 _Emissao;
float4 _FSol, _FSombra, _FBordaSol, _FBordaSombra, _FAcesa;
#ifdef CN_ASAS
float _AsaOmbro, _AsaAltura, _AsaAmplitude, _AsaTempo;
#endif

struct entrada
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
#ifdef CN_VENTO
    float2 vento : TEXCOORD2;
#endif
};

struct v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    half4 sol : TEXCOORD1;      // rgb: fator no sol, a: a rampa sol/sombra
    half4 sombra : TEXCOORD2;   // rgb: fator na sombra, a: neblina
};

v2f vert(entrada v)
{
    v2f o;
    float3 p = v.vertex.xyz;
    float3 nl = v.normal;
#ifdef CN_ASAS
    // tudo o que fica além do ombro gira em volta de uma dobradiça ao longo
    // do corpo, as duas asas juntas, com a junta macia (8 cm)
    float lado = sign(p.x);
    float d = abs(p.x) - _AsaOmbro;
    if (d > 0.0)
    {
        float th = _AsaAmplitude * sin(_AsaTempo) * smoothstep(0.0, 0.08, d);
        float c = cos(th);
        float s = sin(th);
        float ry = p.y - _AsaAltura;
        p.x = lado * (_AsaOmbro + d * c - ry * s);
        p.y = _AsaAltura + d * s + ry * c;
        float nx = nl.x * lado;
        nl = float3(lado * (nx * c - nl.y * s), nx * s + nl.y * c, nl.z);
    }
#endif
    float4 pm = mul(unity_ObjectToWorld, float4(p, 1.0));
#ifdef CN_VENTO
    // a fase sai do lugar no mundo (como na edição Godot): cada planta
    // balança no seu tempo e a onda atravessa a planta de leve
    float3 g = CnGodot(pm.xyz);
    float fase = dot(g.xz, float2(0.31, 0.17));
    float t = _Time.y * v.vento.y;
    float3 anda = float3(sin(t + fase) + 0.35 * sin(2.3 * t + 1.7 * fase), 0.0, 0.6 * cos(0.8 * t + 1.3 * fase)) * v.vento.x;
    pm.xyz += CnGodot(anda);
#endif
    o.pos = mul(UNITY_MATRIX_VP, pm);
    float3 n = normalize(UnityObjectToWorldNormal(nl));
    float3 ate = _WorldSpaceCameraPos.xyz - pm.xyz;
    float dist = max(length(ate), 0.0001);
    float neblina = 1.0 - exp(-_CnNeblina.a * dist);
    float rim = 1.0 - saturate(dot(n, ate) / dist);
    rim = rim * rim * rim;
    float acesa = 1.0 - saturate(abs(v.color.a - 0.5) * 5.0);
    // a cor do vértice como sai na tela, relativa ao branco (1 nas de desenho)
    float3 k = CnTela(CnS2L(v.color.rgb)) * ((1.0 - neblina) / CN_BRANCO);
    // a divisa já como rampa de 0 (sombra) a 1 (sol) na largura da faixa:
    // conta linear, a interpolação entre os vértices não muda nada
    float virada = (dot(n, _CnSolDir.xyz) - 0.02) * (0.5 / _Faixa) + 0.5;
    o.sol = half4(lerp(_FSol.rgb + _FBordaSol.rgb * rim, _FAcesa.rgb, acesa) * k, virada);
    o.sombra = half4(lerp(_FSombra.rgb + _FBordaSombra.rgb * rim, _FAcesa.rgb, acesa) * k, neblina);
    o.uv = v.uv;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    half3 f = lerp(i.sombra.rgb, i.sol.rgb, saturate(i.sol.a));
    half3 c = tex2D(_MainTex, i.uv).rgb * f + lerp(_Emissao.rgb, (half3)_CnNeblinaTela.rgb, i.sombra.a);
    return half4(c, 1.0);
}
