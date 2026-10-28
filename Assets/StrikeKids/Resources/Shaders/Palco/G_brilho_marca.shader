// BRILHO DA MARCA (o brilho_marca.shader da edição Godot, de interface): o
// brilho que passa pela logo Lazer & Sport na abertura — faixa diagonal
// clara, só onde a imagem existe. _pos anda de -0,4 a 1,6.
// (A conta usa a uv do Godot: v para baixo; a cor do vértice da Unity faz
// o papel do modulate.)
Shader "StrikeKids/G/brilho_marca"
{
    Properties
    {
        [PerRendererData] _MainTex ("Imagem", 2D) = "white" {}
        _pos ("Posição do brilho", Float) = -1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _pos;

            struct entrada { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : TEXCOORD1; float2 uv : TEXCOORD0; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.cor = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv);
                float d = i.uv.x * 0.8 + (1.0 - i.uv.y) * 0.3 - _pos;
                half b = (1.0 - smoothstep(0.0, 0.06, abs(d))) * 0.6;
                return half4(min(c.rgb + b, 1.0), c.a) * i.cor;
            }
            ENDCG
        }
    }
}
