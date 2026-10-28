// ARCO DO PORTAL (o raios.shader da edição Godot GLES2; em cima do
// letreiro, no fim da pista): raios de sol alternando duas cores do cenário.
// Os raios são a própria malha (um leque de triângulos: COLOR.r diz a cor do
// raio, COLOR.g a distância do centro) — tudo por vértice: a placa só pinta.
Shader "StrikeKids/G/raios"
{
    Properties
    {
        _cor_a ("cor_a", Vector) = (1, 0.76, 0.1, 1)
        _cor_b ("cor_b", Vector) = (0.91, 0.22, 0.31, 1)
        _pulso ("pulso (brilho do centro, vem da CPU)", Float) = 0
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

            float4 _cor_a, _cor_b;
            float _pulso;

            struct entrada { float4 vertex : POSITION; float4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 cor : TEXCOORD0;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float r = v.color.g;
                float3 c = lerp(_cor_b.rgb, _cor_a.rgb, v.color.r);
                c *= 1.12 - 0.35 * r;
                c += float3(1.0, 0.95, 0.8) * (0.3 + 0.12 * _pulso) * saturate(1.0 - r * 2.2);
                o.cor = CnTela(c);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(i.cor, 1.0);
            }
            ENDCG
        }
    }
}
