// Para-choques listrados: a cor do vértice diz a listra (R = 1ª cor, G = 2ª).
Shader "StrikeKids/Bumper"
{
    Properties
    {
        _Cor1 ("Cor 1", Color) = (1, 1, 1, 1)
        _Cor2 ("Cor 2", Color) = (1, 0.23, 0.31, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeLuz.cginc"

            half4 _Cor1, _Cor2;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                half3 a = lerp(_Cor2.rgb, _Cor1.rgb, step(0.5, v.color.r));
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half nevoa;
                half3 c = SkIluminar(pm, n, a, nevoa);
                half3 vn = (half3)normalize(_WorldSpaceCameraPos.xyz - pm);
                half3 h = normalize((half3)_SkLuzDir.xyz + vn);
                c += _SkLuzCor.rgb * pow(saturate(dot(n, h)), 30.0) * 0.5;
                o.cor = half4(lerp(_SkNeblina.rgb, c, nevoa), 1.0);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.cor; }
            ENDCG
        }
    }
}
