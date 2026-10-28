// PISO DA ÁREA DOS JOGADORES (o piso.shader da edição Godot GLES2): madeira
// envernizada, pontinhos coloridos e a estrela "pise aqui". Bordas suaves
// de largura fixa e o reflexo do céu (fresnel) com a direção da câmera vinda
// do vértice. As mesmas contas do Godot 4 (cores cruas; a curva de tela no
// fim). Lugar no mundo como no Godot (CnGodot).
Shader "StrikeKids/G/piso"
{
    Properties
    {
        _madeira ("madeira", 2D) = "white" {}
        _escala_uv ("escala_uv", Vector) = (4.2, 3.2, 0, 0)
        _tom ("tom", Vector) = (0.86, 0.69, 0.52, 1)
        _luz_cena ("luz_cena", Vector) = (1.2, 1.2, 1.22, 0)
        _cor_ceu ("cor_ceu", Vector) = (0.85, 0.93, 1, 1)
        _verniz ("verniz", Float) = 0.15
        _pulso ("pulso", Float) = 0
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

            sampler2D _madeira;
            float4 _escala_uv, _tom, _luz_cena, _cor_ceu;
            float _verniz, _pulso;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 pm : TEXCOORD1;
                float3 olhar : TEXCOORD2;
            };

            float cobre(float dist)
            {
                return saturate(0.5 - dist * 220.0);
            }

            float sd_estrela(float2 p, float r, float vale)
            {
                float2 k1 = float2(0.809016994375, -0.587785252292);
                float2 k2 = float2(-0.809016994375, -0.587785252292);
                p.x = abs(p.x);
                p -= 2.0 * max(dot(k1, p), 0.0) * k1;
                p -= 2.0 * max(dot(k2, p), 0.0) * k2;
                p.x = abs(p.x);
                p.y -= r;
                float2 ba = vale * float2(-k1.y, k1.x) - float2(0.0, 1.0);
                float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, r);
                // (o sinal com step: o sign da HLSL devolve inteiro)
                float s = p.y * ba.x - p.x * ba.y;
                return length(p - ba * h) * (step(0.0, s) - step(s, 0.0));
            }

            float3 adesivo(float3 c, float dist, float3 cor, float contorno)
            {
                c = lerp(c, float3(1.0, 1.0, 1.0), cobre(dist - contorno));
                return lerp(c, cor, cobre(dist));
            }

            v2f vert(entrada v)
            {
                v2f o;
                // UV x escala_uv na conta do Godot (v para baixo), de volta para
                // a imagem da Unity (conta linear: no vértice dá o mesmo)
                float2 g = float2(v.uv.x, 1.0 - v.uv.y) * _escala_uv.xy;
                o.uv = float2(g.x, 1.0 - g.y);
                o.pm = CnGodot(mul(unity_ObjectToWorld, v.vertex).xyz);
                o.olhar = CnGodot(_WorldSpaceCameraPos.xyz) - o.pm;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_madeira, i.uv).rgb * _tom.rgb;
                float3 pm = i.pm;
                if (abs(pm.z - 0.55) < 0.1 && abs(pm.x) < 0.62)
                {
                    float ip = clamp(floor(pm.x / 0.17 + 0.5), -3.0, 3.0);
                    float dd = length(float2(pm.x - ip * 0.17, pm.z - 0.55)) - 0.032;
                    float3 cor = float3(1.0, 0.23, 0.33);
                    if (ip > -2.5) { cor = float3(1.0, 0.56, 0.13); }
                    if (ip > -1.5) { cor = float3(1.0, 0.85, 0.15); }
                    if (ip > -0.5) { cor = float3(0.35, 0.86, 0.33); }
                    if (ip > 0.5) { cor = float3(0.2, 0.8, 1.0); }
                    if (ip > 1.5) { cor = float3(0.27, 0.47, 1.0); }
                    if (ip > 2.5) { cor = float3(0.72, 0.38, 1.0); }
                    c = adesivo(c, dd, cor, 0.011);
                }
                if (abs(pm.z - 1.45) < 0.32 && abs(pm.x) < 0.32)
                {
                    float de = sd_estrela(float2(pm.x, 1.45 - pm.z), 0.2, 0.48) - 0.015;
                    c += float3(1.0, 0.85, 0.2) * exp(-max(de, 0.0) * 20.0) * (0.15 + 0.15 * _pulso);
                    c = adesivo(c, de, float3(1.0, 0.82, 0.15), 0.02);
                }
                // MADEIRA VIVA, como a da pista: menos luz, mais cor e o reflexo
                // do céu fraco e tingido da madeira
                c *= _luz_cena.xyz * 0.86;
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                c = max(lerp(float3(lum, lum, lum), c, 1.3), 0.0);
                float3 vv = normalize(i.olhar);
                float f1 = 1.0 - saturate(vv.y);
                float f2 = f1 * f1;
                return float4(CnTela(lerp(c, _cor_ceu.rgb * c * 2.2, f2 * f2 * _verniz * 0.5)), 1.0);
            }
            ENDCG
        }
    }
}
