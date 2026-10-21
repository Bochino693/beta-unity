// As peças do jogo (personagens, cenário, pista; lisas ou com quina): cor do vértice x
// tinta, luz por vértice (StrikeLuz.cginc). _Flash acende (personagem da
// vez); _Escuro escurece (pino caindo no poço).
Shader "StrikeKids/Facetado"
{
    Properties
    {
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Flash ("Flash", Range(0, 1)) = 0
        _Escuro ("Escuro", Range(0, 1)) = 1
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

            half4 _Tinta;
            half _Flash;
            half _Escuro;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half nevoa;
                half3 c = SkIluminar(pm, n, v.color.rgb * _Tinta.rgb, nevoa);
                c = lerp(c, half3(1.0, 0.97, 0.88), _Flash * 0.35) * _Escuro;
                o.cor = half4(lerp(_SkNeblina.rgb, c, nevoa), 1.0);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.cor; }
            ENDCG
        }
    }
}
