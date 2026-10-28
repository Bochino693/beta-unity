// O CÉU de cada fase (o ceu_leve.shader da edição Godot): uma esfera em
// volta da câmera (o shader a leva junto), desenhada primeiro e sem gravar
// fundura. Degradê topo -> meio -> horizonte por pixel, brilho do sol e os
// extras: ESTRELAS + nebulosa (espaço; as estrelas vêm da textura, pela UV
// da esfera — na Mali-400/450 o sorteio por pixel piscaria), RAIOS de luz
// vindos de cima (fundo do mar) e o FUNDO PINTADO da fase (_Faixa: a
// paisagem distante numa faixa em volta da frente da pista — montanhas e
// vulcão, castelo e circo, nebulosas e planetas, algas e recifes —, já com
// a cor da tela; nas bordas a faixa se mistura ao céu). A nebulosa e os
// raios mudam devagar: por vértice. As cores são "cruas" e passam pela
// mesma curva de tela da decoração (CnTela).
Shader "StrikeKids/Ceu"
{
    Properties
    {
        _CorTopo ("Topo", Color) = (0.18, 0.5, 1, 1)
        _CorMeio ("Meio", Color) = (0.42, 0.73, 1, 1)
        _CorHorizonte ("Horizonte", Color) = (0.89, 0.96, 1, 1)
        _CorChao ("Chão", Color) = (0.55, 0.82, 0.55, 1)
        _CorSol ("Sol", Color) = (1, 0.95, 0.75, 1)
        _ForcaSol ("Força do sol", Float) = 1
        _SolDir ("Direção do sol", Vector) = (0.15, 0.4, -0.9, 0)
        _Estrelas ("Estrelas", Range(0, 1)) = 0
        _CorNebulosa ("Nebulosa", Color) = (0.9, 0.4, 1, 1)
        _Raios ("Raios de luz", Range(0, 1)) = 0
        _MainTex ("Estrelas (textura)", 2D) = "black" {}
        _Faixa ("Fundo pintado", 2D) = "black" {}
        _FaixaLiga ("Fundo pintado ligado", Float) = 0
        _FaixaArea ("Faixa (graus: arco para cada lado, alto, baixo)", Vector) = (75, 40, -6, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeCenario.cginc"

            half4 _CorTopo, _CorMeio, _CorHorizonte, _CorChao, _CorSol, _CorNebulosa;
            half _ForcaSol, _Estrelas, _Raios, _FaixaLiga;
            float4 _SolDir, _FaixaArea;
            sampler2D _MainTex, _Faixa;
            float4 _MainTex_ST;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float2 uvFaixa : TEXCOORD2;
                half2 extra : TEXCOORD3;     // x: nebulosa, y: raio de luz
            };

            v2f vert(entrada v)
            {
                v2f o;
                float3 d = normalize(v.vertex.xyz);
                o.dir = d;
                // (as contas de lugar como na edição Godot: o X ao contrário)
                float3 g = CnGodot(d);
                float az = degrees(atan2(g.x, -g.z));
                float el = degrees(asin(clamp(g.y, -1.0, 1.0)));
                o.uvFaixa = float2(az / (2.0 * _FaixaArea.x) + 0.5, (_FaixaArea.y - el) / (_FaixaArea.y - _FaixaArea.z));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float n = sin(g.x * 3.1 + sin(g.z * 2.3) * 1.7) * sin(g.y * 4.2 + g.z * 1.3) + 0.5 * sin(g.x * 7.0 - g.z * 5.0);
                float neb = smoothstep(0.35, 1.2, n) * 0.22 * _Estrelas;
                float a = max(0.0, sin(g.x * 21.0 + g.z * 9.0) * 0.5 + 0.5);
                float b = max(0.0, sin(g.x * 13.0 - g.z * 15.0 + 1.7) * 0.5 + 0.5);
                float a2 = a * a;
                float a8 = a2 * a2;
                a8 *= a8;
                float b2 = b * b;
                float b4 = b2 * b2;
                float raio = (a8 * a2 + b4 * b4 * b4 * b2 * 0.7) * smoothstep(0.05, 0.7, g.y) * 0.35 * _Raios;
                o.extra = half2(neb, raio);
                // a esfera fica sempre em volta da câmera, perto do fim da visão
                float3 pm = _WorldSpaceCameraPos.xyz + d * 150.0;
                o.pos = mul(UNITY_MATRIX_VP, float4(pm, 1.0));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 d = (half3)normalize(i.dir);
                half y = d.y;
                half3 c;
                half3 h = _CorHorizonte.rgb;
                if (y > 0.0)
                {
                    c = lerp(h, _CorMeio.rgb, smoothstep(0.0, 0.18, y));
                    c = lerp(c, _CorTopo.rgb, smoothstep(0.18, 0.75, y));
                }
                else
                {
                    c = lerp(h, _CorChao.rgb, smoothstep(0.0, -0.08, y));
                }
                // (as leituras de textura só dentro de condições do material: o
                // que muda de pixel para pixel entra como peso)
                if (_Estrelas > 0.0)
                {
                    half e = tex2D(_MainTex, i.uv).r;
                    c += (_CorNebulosa.rgb * i.extra.x + half3(0.9, 0.92, 0.95) * e * 2.2 * _Estrelas) * step(-0.02, y);
                }
                if (_Raios > 0.0 && y > 0.0)
                    c += half3(0.75, 1.0, 0.95) * i.extra.y;
                half sol = max(dot(d, (half3)normalize(_SolDir.xyz)), 0.0);
                half s2 = sol * sol;
                half s4 = s2 * s2;
                half s8 = s4 * s4;
                c += _CorSol.rgb * (s8 * s4 * 0.25) * _ForcaSol;
                half3 cor = CnTelaH(c);
                if (_FaixaLiga > 0.5)
                {
                    float2 u = i.uvFaixa;
                    half k = saturate(min(u.x, 1.0 - u.x) * 10.0) * saturate(u.y * 7.0) * step(u.y, 1.0) * step(d.z, 0.0);
                    cor = lerp(cor, tex2D(_Faixa, float2(u.x, 1.0 - u.y)).rgb, k);
                }
                return half4(cor, 1.0);
            }
            ENDCG
        }
    }
}
