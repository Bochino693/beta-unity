// LÂMPADAS DO LETREIRO (o lampadas.shader da edição Godot GLES2). Lá é um
// MultiMesh; aqui as lâmpadas vêm numa malha só: a cor da instância já
// multiplicada na cor do vértice e o número (INSTANCE_CUSTOM.r) no uv.x. O
// padrão do pisca vem de `fase` (calculada na CPU). Tudo por vértice (a
// lâmpada é pequena).
Shader "StrikeKids/G/lampadas"
{
    Properties
    {
        _fase ("fase", Float) = 0
        _modo ("modo (0 corrida, 1 todas piscando, 2 alternado)", Float) = 0
        _intensidade ("intensidade", Float) = 1.6
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

            float _fase, _modo, _intensidade;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 cor : TEXCOORD0;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float n = v.uv.x;
                float on;
                if (_modo < 0.5)
                {
                    on = 0.25 + 0.75 * step(0.6, frac(n * 0.25 - _fase));
                }
                else if (_modo < 1.5)
                {
                    on = 0.3 + 0.7 * step(0.5, frac(_fase * 2.0));
                }
                else
                {
                    on = 0.25 + 0.75 * step(0.5, frac(n * 0.5 + _fase * 2.0));
                }
                float3 pv = mul(UNITY_MATRIX_MV, float4(v.vertex.xyz, 1.0)).xyz;
                float3 nv = normalize(mul((float3x3)UNITY_MATRIX_MV, v.normal));
                float rim = saturate(dot(nv, normalize(-pv)));
                o.cor = CnTela(v.color.rgb * (0.35 + rim * 0.65) * on * _intensidade + float3(0.12, 0.12, 0.12) * rim);
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
