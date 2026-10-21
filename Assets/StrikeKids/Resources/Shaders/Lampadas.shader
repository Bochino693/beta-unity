// As LÂMPADAS (centenas, uma malha só): cada lâmpada tem a sua cor (cor do
// vértice) e o seu número (UV.x); o brilho anda nos vértices.
//   _Modo 0 = corrida (uma onda que passa), 1 = piscando todas (strike!),
//   2 = alternado (spare).
Shader "StrikeKids/Lampadas"
{
    Properties
    {
        _Fase ("Fase", Float) = 0
        _Modo ("Modo", Float) = 0
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

            float _Fase;
            float _Modo;

            struct entrada { float4 vertex : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float i = v.uv.x;
                float corrida = step(0.62, frac(_Fase * 2.2 - i * 0.045));
                float todas = step(0.5, frac(_Fase * 6.0));
                float alterna = step(0.5, frac(_Fase * 3.0 + i * 0.5));
                float liga = _Modo < 0.5 ? (0.45 + 0.55 * corrida) : (_Modo < 1.5 ? (0.25 + 0.75 * todas) : (0.3 + 0.7 * alterna));
                // o miolo (uv.y = 1) mais claro que a borda da bolinha
                half3 c = lerp(v.color.rgb * 0.55, lerp(v.color.rgb, half3(1, 1, 1), 0.45), (half)(v.uv.y * liga));
                c *= 0.55 + 0.6 * (half)liga;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.cor = half4(lerp(_SkNeblina.rgb, c, SkNevoa(pm)), 1.0);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target { return i.cor; }
            ENDCG
        }
    }
}
