// O CHÃO de cada fase (terreno facetado com morros): a cor do vértice (R)
// diz a "mancha" e o shader troca pela cor da fase: 0 = base, 1 = base
// escura, 2 = detalhe (flor, cratera, concha), 3 = detalhe 2.
Shader "StrikeKids/Chao"
{
    Properties
    {
        _Cor0 ("Base", Color) = (0.42, 0.8, 0.36, 1)
        _Cor1 ("Base escura", Color) = (0.36, 0.71, 0.31, 1)
        _Cor2 ("Detalhe", Color) = (1, 1, 1, 1)
        _Cor3 ("Detalhe 2", Color) = (1, 0.9, 0.45, 1)
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

            half4 _Cor0, _Cor1, _Cor2, _Cor3;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                half r = v.color.r;
                half3 a = _Cor0.rgb;
                a = lerp(a, _Cor1.rgb, step(0.2, r));
                a = lerp(a, _Cor2.rgb, step(0.55, r));
                a = lerp(a, _Cor3.rgb, step(0.85, r));
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half nevoa;
                half3 c = SkIluminar(pm, n, a, nevoa);
                o.cor = half4(lerp(_SkNeblina.rgb, c, nevoa), 1.0);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.cor; }
            ENDCG
        }
    }
}
