// CORTINA DA CAÇAPA DAS BOLAS (o cortina.shader da edição Godot GLES2): as 7
// tiras de borracha numa malha só, com o desenho do cenário
// (texturas/cortina_bola.png, uma faixa por cenário) cortado entre elas.
// Cada tira gira presa em cima, para DENTRO do túnel, pelo ângulo dela
// (a0..a6, o Mundo manda quando a bola passa). UV2.x = qual tira; o vértice
// vem com a dobradiça em y = 0.
// Lá é cull_disabled com o verso escuro (FRONT_FACING); aqui, sem VFACE no
// shader model 2.0, são duas passadas com a mesma conta: a frente e o verso.
Shader "StrikeKids/G/cortina"
{
    Properties
    {
        _desenho ("desenho", 2D) = "white" {}
        _linha ("linha (o cenário: faixa da imagem, 0..3)", Float) = 0
        _brilho ("brilho", Float) = 1
        _a0 ("a0", Float) = 0
        _a1 ("a1", Float) = 0
        _a2 ("a2", Float) = 0
        _a3 ("a3", Float) = 0
        _a4 ("a4", Float) = 0
        _a5 ("a5", Float) = 0
        _a6 ("a6", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        // a FRENTE
        Pass
        {
            Cull Back
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "../StrikeCenario.cginc"

            sampler2D _desenho;
            float _linha, _brilho;
            float _a0, _a1, _a2, _a3, _a4, _a5, _a6;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv_d : TEXCOORD0;
                float u_tira : TEXCOORD1;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float i = v.uv2.x;
                float a = _a0;
                if (i > 5.5) { a = _a6; }
                else if (i > 4.5) { a = _a5; }
                else if (i > 3.5) { a = _a4; }
                else if (i > 2.5) { a = _a3; }
                else if (i > 1.5) { a = _a2; }
                else if (i > 0.5) { a = _a1; }
                // gira em volta do eixo x (a dobradiça): a ponta de baixo vai para -z
                float3 g = CnGodot(v.vertex.xyz);
                g = float3(g.x, g.y * cos(a), g.y * sin(a));
                o.pos = UnityObjectToClipPos(CnGodot(g));
                // a conta da UV é a do Godot (v para baixo); na imagem da Unity
                // o v vai ao contrário
                float2 d = float2((i + v.uv.x) / 7.0, (_linha + (1.0 - v.uv.y)) / 4.0);
                o.uv_d = float2(d.x, 1.0 - d.y);
                o.u_tira = v.uv.x;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_desenho, i.uv_d).rgb;
                // a borda de cada tira um pouco mais escura (lê-se a borracha)
                float borda = smoothstep(0.0, 0.05, min(i.u_tira, 1.0 - i.u_tira));
                c *= (0.6 + 0.4 * borda) * _brilho;
                return float4(c, 1.0);
            }
            ENDCG
        }
        // o VERSO (escuro): a mesma conta, x 0,3
        Pass
        {
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "../StrikeCenario.cginc"

            sampler2D _desenho;
            float _linha, _brilho;
            float _a0, _a1, _a2, _a3, _a4, _a5, _a6;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv_d : TEXCOORD0;
                float u_tira : TEXCOORD1;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float i = v.uv2.x;
                float a = _a0;
                if (i > 5.5) { a = _a6; }
                else if (i > 4.5) { a = _a5; }
                else if (i > 3.5) { a = _a4; }
                else if (i > 2.5) { a = _a3; }
                else if (i > 1.5) { a = _a2; }
                else if (i > 0.5) { a = _a1; }
                float3 g = CnGodot(v.vertex.xyz);
                g = float3(g.x, g.y * cos(a), g.y * sin(a));
                o.pos = UnityObjectToClipPos(CnGodot(g));
                float2 d = float2((i + v.uv.x) / 7.0, (_linha + (1.0 - v.uv.y)) / 4.0);
                o.uv_d = float2(d.x, 1.0 - d.y);
                o.u_tira = v.uv.x;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_desenho, i.uv_d).rgb;
                float borda = smoothstep(0.0, 0.05, min(i.u_tira, 1.0 - i.u_tira));
                c *= (0.6 + 0.4 * borda) * _brilho;
                c *= 0.3;
                return float4(c, 1.0);
            }
            ENDCG
        }
    }
}
