// POÇO DO ALÇAPÃO (o poco.shader da edição Godot GLES2): um poço SEM FUNDO
// — fica preto com a fundura (a caixa vem virada do avesso: de cima só
// aparecem o fundo e as paredes de dentro).
Shader "StrikeKids/G/poco"
{
    Properties
    {
        _cor_topo ("cor_topo", Vector) = (0.1, 0.2, 0.45, 1)
        _cor_fundo ("cor_fundo", Vector) = (0, 0.004, 0.02, 1)
        _y_topo ("y_topo", Float) = -0.14
        _y_fundo ("y_fundo", Float) = -1.75
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

            float4 _cor_topo, _cor_fundo;
            float _y_topo, _y_fundo;

            struct entrada { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float fundura : TEXCOORD0;
            };

            v2f vert(entrada v)
            {
                v2f o;
                // (o y é o mesmo nas duas edições)
                o.fundura = _y_topo - mul(unity_ObjectToWorld, v.vertex).y;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float t = saturate(i.fundura / (_y_topo - _y_fundo));
                float3 c = lerp(_cor_topo.rgb, _cor_fundo.rgb, sqrt(t));
                return float4(CnTela(c), 1.0);
            }
            ENDCG
        }
    }
}
