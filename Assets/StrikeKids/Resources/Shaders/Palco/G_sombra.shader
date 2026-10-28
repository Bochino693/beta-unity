// SOMBRA REDONDA (o sombra.shader da edição Godot): o blob embaixo de bola,
// pinos e personagens — dá a noção de altura (o 3D "salta") e custa quase
// nada.
Shader "StrikeKids/G/sombra"
{
    Properties
    {
        _textura ("Sombra", 2D) = "white" {}
        _forca ("Força", Float) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _textura;
            half _forca;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half alfa : TEXCOORD1; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.alfa = v.color.a;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 t = tex2D(_textura, i.uv);
                // no Godot 4 a mistura era em luz linear: a mesma escuridão,
                // misturando na cor da tela, pede menos opacidade
                half a = saturate(t.a * _forca * i.alfa);
                return half4(t.rgb, 1.0 - pow(1.0 - a, 0.4545));
            }
            ENDCG
        }
    }
}
