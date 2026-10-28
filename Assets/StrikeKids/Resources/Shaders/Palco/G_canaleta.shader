// CANALETA DE CADA CENÁRIO (o canaleta.shader da edição Godot GLES2) — os
// efeitos ficam aqui, POR BAIXO dos para-choques (a mesma meia-cana; o
// desenho muda):
//   0 = doce listrado com luzinhas correndo para os pinos (Parque),
//   1 = metal de nave com rebites e luzes que correm (Espaço),
//   2 = água com reflexos e bolhinhas indo para os pinos (Fundo do Mar),
//   3 = bambu com nós, fibras e um brilho verde correndo (Selva).
// As CORES já saem prontas do vértice (a conta da tela é cara); o pixel só
// mistura as três cores da calha e soma os efeitos. Bordas suaves de largura
// fixa (sem derivadas) e o TEMPO vindo da CPU já dentro de uma volta (fase,
// onda1, onda2, bolhas): o pixel da Mali-450 é de meia precisão.
// COLOR.r = sombreado da calha (mais escura no fundo, clara nos lábios).
Shader "StrikeKids/G/canaleta"
{
    Properties
    {
        _modo ("modo (0 doce, 1 metal, 2 água, 3 bambu)", Float) = 0
        _cor_a ("cor_a", Vector) = (1, 0.48, 0.72, 1)
        _cor_b ("cor_b", Vector) = (1, 0.95, 0.88, 1)
        _cor_c ("cor_c", Vector) = (1, 0.82, 0.25, 1)
        _luz_cena ("luz_cena", Vector) = (1.2, 1.2, 1.22, 0)
        _centro ("centro (|x| do meio da calha)", Float) = 0.8
        _fase ("fase (0..1)", Float) = 0
        _onda1 ("onda1 (0..2pi)", Float) = 0
        _onda2 ("onda2 (0..2pi)", Float) = 0
        _bolhas ("bolhas (0..3,52 m)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Back
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "../StrikeCenario.cginc"

            float _modo;
            float4 _cor_a, _cor_b, _cor_c, _luz_cena;
            float _centro, _fase, _onda1, _onda2, _bolhas;

            struct entrada { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                // as cores da calha já na tela (com a luz e o sombreado do vértice)
                float3 ta : TEXCOORD0;
                float3 tb : TEXCOORD1;
                float3 tc : TEXCOORD2;
                // x: distância do meio da calha, y: z (ao longo da pista), z: lábio
                float3 pos_c : TEXCOORD3;
            };

            // o mod da GLSL (x - y * floor(x / y)): o fmod da HLSL muda com o
            // sinal, e o z da pista é negativo
            float mod_g(float x, float y)
            {
                return x - y * floor(x / y);
            }

            v2f vert(entrada v)
            {
                v2f o;
                float3 pw = CnGodot(mul(unity_ObjectToWorld, v.vertex).xyz);
                o.pos_c = float3(abs(pw.x) - _centro, pw.z, smoothstep(-0.032, -0.012, pw.y));
                float3 luz = v.color.r * min(_luz_cena.xyz, 1.3);
                o.ta = CnTela(_cor_a.rgb * luz);
                o.tb = CnTela(_cor_b.rgb * luz);
                o.tc = CnTela(_cor_c.rgb * luz);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // luzinhas que correm pelo meio da calha até os pinos (a cada 1,25 m)
            float pulso(float ux, float z, float largura)
            {
                float q = (frac(z * 0.8 + _fase) - 0.5) * 9.0;
                return exp(-q * q) * (1.0 - smoothstep(largura * 0.4, largura, abs(ux)));
            }

            float4 frag(v2f i) : SV_Target
            {
                float ux = i.pos_c.x;
                float z = i.pos_c.y;
                float labio = i.pos_c.z;
                float3 ta = i.ta;
                float3 tb = i.tb;
                float3 tc = i.tc;
                float3 c = float3(0.0, 0.0, 0.0);
                if (_modo < 0.5)
                {
                    float t = z * 2.4 + ux * 6.0;
                    float m = saturate(0.5 - (abs(frac(t) - 0.5) - 0.25) * 40.0);
                    c = lerp(tb, ta, m);
                    c += tc * pulso(ux, z, 0.06) * 0.9;
                    c = lerp(c, tc, labio);
                }
                else if (_modo < 1.5)
                {
                    // painéis de 0,5 m com emenda e rebites dos lados
                    float ds = abs(frac(z * 2.0 + 0.5) - 0.5) * 0.5;
                    float emenda = 1.0 - smoothstep(0.004, 0.008, ds);
                    float rebite = 1.0 - smoothstep(0.006, 0.01, length(float2(abs(ux) - 0.1, ds - 0.06)));
                    c = ta * (1.0 - 0.35 * emenda) + tb * 0.4 * rebite;
                    float filete = 1.0 - smoothstep(0.004, 0.008, abs(ux));
                    c += tc * (pulso(ux, z, 0.07) * 1.1 + filete * 0.45);
                    c = lerp(c, tc, labio);
                }
                else if (_modo < 2.5)
                {
                    float o = sin(z * 7.0 + sin(ux * 38.0 + _onda1) * 1.3 + _onda2);
                    float reflexo = smoothstep(0.82, 0.92, o);
                    c = lerp(ta, tb, 0.25 + 0.35 * labio) + float3(0.8, 0.95, 0.95) * reflexo * 0.3;
                    // bolhinhas: uma por célula (0,08 x 0,22 m), indo devagar para
                    // os pinos (o sorteio repete a cada 16 células: a volta de `bolhas`)
                    float2 bq = float2(ux / 0.08, (z + _bolhas) / 0.22);
                    float2 cel = floor(bq);
                    float cy = mod_g(cel.y, 16.0);
                    float h = frac(cel.x * 0.618 + cy * 0.7548 + cel.x * cy * 0.137);
                    float2 cb = cel + float2(0.25 + 0.5 * h, 0.2 + 0.6 * frac(h * 7.3));
                    float2 db = (bq - cb) * float2(0.08, 0.22);
                    float raio = 0.007 + 0.009 * frac(h * 13.1);
                    float bolha = (1.0 - smoothstep(0.0015, 0.0035, abs(length(db) - raio))) * step(0.45, h);
                    c = lerp(c, float3(0.92, 1.0, 1.0), bolha * 0.85);
                    c = lerp(c, tc, labio);
                }
                else
                {
                    float dn = abs(frac(z / 0.42 + 0.5) - 0.5) * 0.42;
                    float no = 1.0 - smoothstep(0.011, 0.016, dn);
                    float anel = 1.0 - smoothstep(0.007, 0.012, abs(dn - 0.03));
                    float fibra = 0.9 + 0.1 * sin(ux * 95.0 + sin(z * 2.7) * 0.6);
                    c = ta * fibra;
                    c = lerp(c, tb, anel * 0.7);
                    c = lerp(c, ta * 0.45, no);
                    c += float3(0.55, 0.9, 0.25) * pulso(ux, z, 0.07) * 0.6;
                    c = lerp(c, tc, labio);
                }
                return float4(c, 1.0);
            }
            ENDCG
        }
    }
}
