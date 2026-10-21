// A logo da Lazer & Sport com um brilho passando (faixa diagonal em _Pos).
Shader "StrikeKids/UI/Brilho"
{
    Properties
    {
        [PerRendererData] _MainTex ("Imagem", 2D) = "white" {}
        _Pos ("Posição do brilho", Float) = -1
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
            half _Pos;

            struct entrada { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 cor : COLOR; float2 uv : TEXCOORD0; half faixa : TEXCOORD1; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.cor = v.color;
                o.faixa = v.uv.x + (1.0 - v.uv.y) * 0.35 - _Pos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.cor;
                half b = exp(-i.faixa * i.faixa * 90.0) * 0.75;
                c.rgb += b * c.a;
                return c;
            }
            ENDCG
        }
    }
}
