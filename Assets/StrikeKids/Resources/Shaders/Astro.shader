// SOL E LUA COM ROSTINHO: um quadradinho sempre de frente para a câmera,
// NA DIREÇÃO do sol do céu e sempre à mesma distância da câmera — como o
// céu, fica "no infinito". Os raios giram devagar e o rosto pisca de vez em
// quando (tudo calculado por vértice).
Shader "StrikeKids/Astro"
{
    Properties
    {
        _MainTex ("Rosto (2 quadros: aberto | piscando)", 2D) = "white" {}
        _Raios ("Raios", 2D) = "black" {}
        _UsaRaios ("Usa raios", Float) = 1
        _Giro ("Voltas por segundo", Float) = 0.02
        _PiscarCada ("Piscar a cada (s)", Float) = 4.5
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Direcao ("Direção", Vector) = (0.146, 0.358, -0.922, 0)
        _Distancia ("Distância", Float) = 140
        _Tamanho ("Tamanho", Float) = 17.8
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+500" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex, _Raios;
            half _UsaRaios;
            float _Giro, _PiscarCada;
            half4 _Tinta;
            float4 _Direcao;
            float _Distancia, _Tamanho;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uvRosto : TEXCOORD0; float2 uvRaios : TEXCOORD1; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 cam = _WorldSpaceCameraPos.xyz;
                float3 centro = cam + normalize(_Direcao.xyz) * _Distancia;
                float3 dir = UNITY_MATRIX_V[0].xyz;
                float3 cima = UNITY_MATRIX_V[1].xyz;
                float3 p = centro + (dir * v.vertex.x + cima * v.vertex.y) * _Tamanho;
                o.pos = mul(UNITY_MATRIX_VP, float4(p, 1.0));
                float a = frac(_Time.y * _Giro) * 6.2831853;
                float2 q = v.uv - 0.5;
                o.uvRaios = float2(q.x * cos(a) - q.y * sin(a), q.x * sin(a) + q.y * cos(a)) + 0.5;
                float pisca = step(_PiscarCada - 0.16, fmod(_Time.y, _PiscarCada));
                o.uvRosto = float2((clamp(v.uv.x, 0.002, 0.998) + pisca) * 0.5, v.uv.y);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 fundo = tex2D(_Raios, i.uvRaios) * _UsaRaios;
                half4 r = tex2D(_MainTex, i.uvRosto);
                half a = r.a + fundo.a * (1.0 - r.a);
                half3 c = r.rgb * r.a + fundo.rgb * fundo.a * (1.0 - r.a);
                return half4(c / max(a, 0.0001) * _Tinta.rgb, a * _Tinta.a);
            }
            ENDCG
        }
    }
}
