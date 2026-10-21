// O REFLEXO DA BOLA no verniz da pista: como o Reflexo.shader, com a
// textura da bola (Reflexos.cs).
Shader "StrikeKids/ReflexoBola"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Forca ("Força", Range(0, 1)) = 0.38
        _Fundo ("Some a (m)", Float) = 0.32
        _Largura ("Meia largura do deck", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry-5" "IgnoreProjector" = "True" }
        Pass
        {
            ZWrite Off
            ZTest Greater
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeLuz.cginc"

            sampler2D _MainTex;
            half _Forca;
            float _Fundo;
            float _Largura;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half3 L = half3(_SkLuzDir.x, -_SkLuzDir.y, _SkLuzDir.z);
                half dif = saturate(dot(n, L));
                half t = saturate(-n.y * 0.5 + 0.5);
                half3 c = lerp(_SkChao.rgb, _SkCeu.rgb, t) + _SkLuzCor.rgb * dif;
                half fundo = saturate(1.0 + pm.y / _Fundo);
                half dentro = saturate((_Largura - abs(pm.x)) / 0.06);
                o.cor = half4(c, _Forca * fundo * dentro);
                o.uv = v.uv;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                return half4(t.rgb * i.cor.rgb, i.cor.a);
            }
            ENDCG
        }
    }
}
