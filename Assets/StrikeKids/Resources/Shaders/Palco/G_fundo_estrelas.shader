// FUNDO ATRÁS DOS PINOS (o fundo_estrelas.shader da edição Godot GLES2):
// cortina de veludo nas cores do cenário, com um HALO de luz atrás dos
// pinos, estrelinhas piscando e as pregas iluminadas pela normal. O sorteio
// das estrelinhas usa números pequenos (o pixel da Mali-400/450 é de meia
// precisão) e as pregas são iluminadas por vértice. As contas usam a UV do
// Godot (v para baixo) e a normal do Godot (CnGodot).
Shader "StrikeKids/G/fundo_estrelas"
{
    Properties
    {
        _cor_a ("cor_a (em cima)", Vector) = (0.1, 0.12, 0.45, 1)
        _cor_b ("cor_b (embaixo)", Vector) = (0.2, 0.35, 0.85, 1)
        _cor_halo ("cor_halo", Vector) = (0.55, 0.85, 1, 1)
        _fase ("fase", Float) = 0
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

            float4 _cor_a, _cor_b, _cor_halo;
            float _fase;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;      // a UV do Godot
                float dobra : TEXCOORD1;
            };

            float h(float2 p)
            {
                return frac(sin(p.x * 1.731 + p.y * 9.217) * 43.758);
            }

            v2f vert(entrada v)
            {
                v2f o;
                o.uv = float2(v.uv.x, 1.0 - v.uv.y);
                o.dobra = 0.72 + 0.45 * saturate(dot(CnGodot(v.normal), normalize(float3(0.75, 0.25, 0.6))));
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 UVg = i.uv;
                float2 uv = UVg * float2(6.0, 3.0);
                float3 c = lerp(_cor_b.rgb, _cor_a.rgb, UVg.y);
                float2 q = (UVg - float2(0.5, 0.82)) * float2(1.25, 1.6);
                float halo = exp(-dot(q, q) * 4.0);
                c = lerp(c, _cor_halo.rgb, halo * 0.55);
                float faixa = smoothstep(0.92, 1.0, sin(UVg.x * 18.0 + _fase * 6.2831) * 0.5 + 0.5);
                c += _cor_halo.rgb * faixa * 0.08;
                float2 cel = floor(uv * 3.0);
                float2 f = frac(uv * 3.0) - 0.5;
                float r = h(cel);
                if (r > 0.62)
                {
                    float pis = 0.5 + 0.5 * sin(_fase * 6.2831 * (0.5 + r) + r * 40.0);
                    float est = max(0.0, 1.0 - length(f) * 7.0);
                    float cruz = max(0.0, 1.0 - abs(f.x) * 30.0) * max(0.0, 1.0 - abs(f.y) * 4.0)
                        + max(0.0, 1.0 - abs(f.y) * 30.0) * max(0.0, 1.0 - abs(f.x) * 4.0);
                    c += float3(1.0, 0.97, 0.9) * (est * 1.3 + cruz * 0.7) * pis * (1.0 - halo * 0.7);
                }
                return float4(CnTela(c * i.dobra), 1.0);
            }
            ENDCG
        }
    }
}
