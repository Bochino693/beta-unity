// Partículas, brilhos e o rastro: textura x cor do vértice, transparente,
// sem gravar fundura, dos dois lados.
Shader "StrikeKids/Particula"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
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

            sampler2D _MainTex;

            struct entrada { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; float2 uv : TEXCOORD0; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.cor = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * i.cor; }
            ENDCG
        }
    }
}
