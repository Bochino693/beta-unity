// Foto redonda das crianças (e os retratos dos mascotes) com aro colorido.
// O círculo vem da 2ª coordenada (0..1 no quadrado da peça): o recorte da
// foto (uvRect) não deforma o círculo.
Shader "StrikeKids/UI/Foto"
{
    Properties
    {
        [PerRendererData] _MainTex ("Foto", 2D) = "white" {}
        _CorAro ("Aro", Color) = (1, 1, 1, 1)
        _Aro ("Largura do aro", Range(0, 0.5)) = 0.075
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
            fixed4 _CorAro;
            half _Aro;

            struct entrada { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float2 q : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; fixed4 cor : COLOR; float2 uv : TEXCOORD0; float2 q : TEXCOORD1; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.q = v.q * 2.0 - 1.0;
                o.cor = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half r = length(i.q);
                fixed4 c = tex2D(_MainTex, i.uv);
                half aro = smoothstep(1.0 - _Aro - 0.025, 1.0 - _Aro, r);
                c.rgb = lerp(c.rgb, _CorAro.rgb, aro);
                c.a = 1.0 - smoothstep(0.975, 1.0, r);
                return c * i.cor;
            }
            ENDCG
        }
    }
}
