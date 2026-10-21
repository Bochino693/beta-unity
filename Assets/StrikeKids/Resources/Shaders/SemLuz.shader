// Peças que brilham sozinhas (rostos desenhados, neon, raios do arco,
// lâmpadas apagadas, túnel escuro): cor do vértice x tinta, com neblina.
Shader "StrikeKids/SemLuz"
{
    Properties
    {
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Brilho ("Brilho", Float) = 1
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
            half _Brilho;

            struct entrada { float4 vertex : POSITION; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 c = v.color.rgb * _Tinta.rgb * _Brilho;
                o.cor = half4(lerp(_SkNeblina.rgb, c, SkNevoa(pm)), 1.0);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.cor; }
            ENDCG
        }
    }
}
