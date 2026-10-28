// BOLA (o bola.shader da edição Godot OpenGL ES 2.0): textura (estrelas /
// corações) + luz em faixas + brilho de verniz (bolinha branca de reflexo)
// + borda iluminada. A luz é calculada por vértice (a esfera tem 32 x 16
// gomos); o pixel só lê a textura.
// A luz do cenário (_luz_dir, _luz_cor, _ambiente) é GLOBAL e vem como no
// Godot (CnGodot: o X ao contrário).
Shader "StrikeKids/G/bola"
{
    Properties
    {
        _textura ("Textura", 2D) = "white" {}
        _faixa ("Largura da divisa sol/sombra", Float) = 0.08
        _borda ("Borda iluminada", Float) = 0.28
        _cor_borda ("Cor da borda", Vector) = (1, 0.95, 0.88, 1)
        _brilho ("Brilho do verniz", Float) = 1.6
        _aceso ("Aceso (strike / replay)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Back
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "../StrikeCenario.cginc"

            sampler2D _textura;
            float _faixa, _borda, _brilho, _aceso;
            float4 _cor_borda;
            // a luz do cenário (global): direção PARA o sol (no mundo do
            // Godot) e as cores já em luz linear (cor x força)
            float4 _luz_dir, _luz_cor, _ambiente;

            // "filmic" (branco 1,6) + sRGB numa conta só, sem potência (erro < 3%)
            half3 GTelaRapida(half3 x)
            {
                x = max(x, 0.0);
                return CnVivaH(min(sqrt(x) * (1.29 - 0.1 * x) / (1.0 + 0.25 * x), 1.0));
            }

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 luz : TEXCOORD1;
                half3 soma : TEXCOORD2;
                half acesa : TEXCOORD3;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float4 pm = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                float3 nm = mul((float3x3)unity_ObjectToWorld, v.normal);
                float3 n = normalize(CnGodot(nm));
                float t = smoothstep(-_faixa, _faixa, dot(n, normalize(_luz_dir.xyz)));
                float3 l = _luz_cor.xyz * t + _ambiente.xyz;
                // (o espaço da câmera é o mesmo nas duas edições)
                float3 pv = mul(UNITY_MATRIX_V, pm).xyz;
                float3 nv = normalize(mul((float3x3)UNITY_MATRIX_V, nm));
                float3 vv = normalize(-pv);
                float r1 = 1.0 - saturate(dot(nv, vv));
                float rim = r1 * r1 * sqrt(r1);
                float3 lv = normalize(mul((float3x3)UNITY_MATRIX_V, CnGodot(_luz_dir.xyz)));
                float s = smoothstep(0.94, 0.975, dot(nv, normalize(vv + lv)));
                o.luz = l;
                o.soma = CnS2L(_cor_borda.rgb) * rim * _borda + _luz_cor.xyz * s * _brilho * 0.6;
                o.acesa = 1.0 + _aceso * 0.6;
                o.uv = v.uv;
                o.pos = mul(UNITY_MATRIX_VP, pm);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c = tex2D(_textura, i.uv).rgb;
                return half4(GTelaRapida(c * c * i.luz * i.acesa + i.soma), 1.0);
            }
            ENDCG
        }
    }
}
