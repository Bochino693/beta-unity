// O capacete de vidro do astronauta (e bolhas): transparente, com a borda
// clara (contraluz) mais forte. Desenhado depois do opaco, sem gravar fundura.
Shader "StrikeKids/Vidro"
{
    Properties
    {
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeLuz.cginc"

            half4 _Tinta;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half nevoa;
                half3 c = SkIluminar(pm, n, v.color.rgb * _Tinta.rgb, nevoa);
                half3 vn = (half3)normalize(_WorldSpaceCameraPos.xyz - pm);
                half b = 1.0 - saturate(dot(n, vn));
                half a = saturate(v.color.a * _Tinta.a + b * b * 0.55);
                // brilho do sol no vidro
                half3 h = normalize((half3)_SkLuzDir.xyz + vn);
                half esp = pow(saturate(dot(n, h)), 40.0);
                c += _SkLuzCor.rgb * esp * 0.8;
                a = saturate(a + esp * 0.6);
                o.cor = half4(lerp(_SkNeblina.rgb, c, nevoa), min(a, 0.92));
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.cor; }
            ENDCG
        }
    }
}
