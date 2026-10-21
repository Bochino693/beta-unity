// Peças LUSTROSAS (os pinos): a luz do Facetado + o reflexo do MATCAP da
// fase (Materiais.DefinirMatcap: céu e luzes do teto em cima, a pista
// embaixo, o neon da fase na borda; no alfa, os pontos de brilho das luzes).
// Tudo por vértice; no pixel só a leitura do matcap (a coordenada vem pronta
// do vértice: na Mali-450 a leitura direta é a mais barata).
Shader "StrikeKids/Brilho"
{
    Properties
    {
        _Tinta ("Tinta", Color) = (1, 1, 1, 1)
        _Flash ("Flash", Range(0, 1)) = 0
        _Escuro ("Escuro", Range(0, 1)) = 1
        _Reflexo ("Reflexo", Range(0, 1)) = 0.5
        _Brilho ("Brilho das luzes", Range(0, 1)) = 0.6
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

            sampler2D _SkMatcap;
            half4 _Tinta;
            half _Flash;
            half _Escuro;
            half _Reflexo;
            half _Brilho;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; half4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 cor : COLOR0;       // rgb = a cor com luz; a = quanto reflete
                half4 luz : COLOR1;       // rgb = cor do brilho das luzes
                float2 mc : TEXCOORD0;    // onde ler o matcap
            };

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = mul(unity_ObjectToWorld, v.vertex).xyz;
                half3 n = (half3)UnityObjectToWorldNormal(v.normal);
                half nevoa;
                half3 c = SkIluminar(pm, n, v.color.rgb * _Tinta.rgb, nevoa);
                c = lerp(c, half3(1.0, 0.97, 0.88), _Flash * 0.35) * _Escuro;
                // a normal vista da câmera escolhe o ponto do matcap
                float3 nv = normalize(mul((float3x3)UNITY_MATRIX_V, (float3)n));
                o.mc = nv.xy * 0.5 + 0.5;
                half f = 1.0 - saturate((half)nv.z);
                f = _Reflexo * (0.14 + 0.86 * f * f * f);
                half k = nevoa * _Escuro;
                o.cor = half4(lerp(_SkNeblina.rgb, c, nevoa), f * k);
                o.luz = half4(_SkLuzCor.rgb * (_Brilho * k), 0.0);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 m = tex2D(_SkMatcap, i.mc);
                half3 c = lerp(i.cor.rgb, m.rgb, i.cor.a) + m.a * i.luz.rgb;
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
}
