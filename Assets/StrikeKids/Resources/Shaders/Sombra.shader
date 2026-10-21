// Sombrinha redonda no chão (personagens, pinos, bola): a textura sombra.png
// (degradê redondo) escurece o chão. Um pouco à frente na fundura (sem
// "brigar" com o chão).
Shader "StrikeKids/Sombra"
{
    Properties
    {
        _MainTex ("Sombra", 2D) = "white" {}
        _Forca ("Força", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+50" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -1, -2
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half _Forca;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.cor = half4(0.05, 0.04, 0.12, _Forca * v.color.a);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half a = tex2D(_MainTex, i.uv).a;
                return half4(i.cor.rgb, a * i.cor.a);
            }
            ENDCG
        }
    }
}
