// Imagem sem luz com o fundo recortado (logo dos letreiros, rostinho do sol).
Shader "StrikeKids/Recorte"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Corte ("Corte", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeLuz.cginc"

            sampler2D _MainTex;
            half4 _Tinta;
            half _Corte;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 nev : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nev = half4(_SkNeblina.rgb, SkNevoa(pm));
                o.uv = v.uv;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv) * _Tinta;
                clip(t.a - _Corte);
                return half4(lerp(i.nev.rgb, t.rgb, i.nev.a), 1.0);
            }
            ENDCG
        }
    }
}
