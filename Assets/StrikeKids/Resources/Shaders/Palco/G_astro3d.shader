// SOL, LUA E BALEIA EM 3D (o astro3d.shader da edição Godot): o objeto fica
// NA DIREÇÃO do sol do céu e sempre à mesma distância da câmera — "no
// infinito", atrás de todo o cenário —, mas é um objeto de verdade: a malha
// tem volume, o sombreado é de desenho (claro em cima, mais escuro embaixo
// e uma borda escura na volta, pela normal: sem passada de contorno) e ele
// mexe no vértice — balança a cabeça, respira, os raios do sol giram, o
// rabo da baleia bate. O rosto é pintado por cima e pisca pelo relógio.
// Eixos do objeto (os do Godot): X para a direita da tela, Y para cima, Z
// para a câmera. Todas as contas no Godot (CnGodot: o X ao contrário); a
// direção vem como lá. Sem descartar as faces de trás.
Shader "StrikeKids/G/astro3d"
{
    Properties
    {
        _rosto ("Rosto (2 quadros: aberto | piscando)", 2D) = "white" {}
        _direcao ("Direção (no Godot)", Vector) = (0.146, 0.358, -0.922, 0)
        _distancia ("Distância", Float) = 140
        _tamanho ("Tamanho", Float) = 17.8
        _rosto_lado ("Rosto: 0 na frente, 1 dos dois lados", Float) = 0
        _rosto_centro ("Centro do rosto", Vector) = (0, 0, 0, 0)
        _rosto_escala ("Escala do rosto", Float) = 1.6
        _piscar_cada ("Piscar a cada (s)", Float) = 4.5
        _giro_y ("Giro em Y (rad)", Float) = 0
        _deslocamento ("Deslocamento", Vector) = (0, 0, 0, 0)
        _balanco ("Balanço da cabeça (rad)", Float) = 0.1
        _respira ("Respira", Float) = 0.03
        _giro_raios ("Giro dos raios (voltas/s)", Float) = 0.03
        _rabo ("Batida do rabo", Float) = 0
        _cor_borda ("Cor da borda", Vector) = (0.8, 0.35, 0.05, 1)
        _tinta ("Tinta", Vector) = (1, 1, 1, 1)
        _nevoa ("Névoa (a: quanto)", Vector) = (0.5, 0.8, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Off
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "../StrikeCenario.cginc"

            sampler2D _rosto;
            float4 _direcao, _rosto_centro, _deslocamento;
            float _distancia, _tamanho, _rosto_lado, _rosto_escala, _piscar_cada;
            float _giro_y, _balanco, _respira, _giro_raios, _rabo;
            half4 _cor_borda, _tinta, _nevoa;

            // o mod do GLSL (o fmod da Unity muda de sinal nos negativos)
            float GMod(float x, float y)
            {
                return x - y * floor(x / y);
            }

            struct entrada
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv2 : TEXCOORD1;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                half4 cor : TEXCOORD0;
                float3 p_local : TEXCOORD1;
                half luz : TEXCOORD2;
                half borda : TEXCOORD3;
                float2 uv_rosto : TEXCOORD4;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float3 p = CnGodot(v.vertex.xyz);
                float3 n = CnGodot(v.normal);
                o.p_local = p;
                float tempo = _Time.y;
                // raios do sol: giram em volta do Z (vértices com UV2.x = 1)
                if (v.uv2.x > 0.5)
                {
                    float a = frac(tempo * _giro_raios) * 6.2831853;
                    float ca = cos(a);
                    float sa = sin(a);
                    p.xy = float2(ca * p.x - sa * p.y, sa * p.x + ca * p.y);
                    n.xy = float2(ca * n.x - sa * n.y, sa * n.x + ca * n.y);
                }
                // rabo da baleia: sobe e desce, mais na ponta (UV2.y do Godot:
                // 0 no corpo, 1 na ponta; na Unity o v vem ao contrário)
                float ponta = 1.0 - v.uv2.y;
                p.y += _rabo * sin(tempo * 2.4) * ponta * ponta;
                // respira (estica para cima) e balança a cabeça
                p.y *= 1.0 + _respira * sin(tempo * 1.7);
                p.x *= 1.0 - _respira * 0.5 * sin(tempo * 1.7);
                float b = _balanco * sin(tempo * 0.9);
                float cb = cos(b);
                float sb = sin(b);
                p.xy = float2(cb * p.x - sb * p.y, sb * p.x + cb * p.y);
                n.xy = float2(cb * n.x - sb * n.y, sb * n.x + cb * n.y);
                // a baleia vira para o lado que nada
                float cy = cos(_giro_y);
                float sy = sin(_giro_y);
                p.xz = float2(cy * p.x + sy * p.z, -sy * p.x + cy * p.z);
                n.xz = float2(cy * n.x + sy * n.z, -sy * n.x + cy * n.z);
                p.xy += _deslocamento.xy;
                // no céu, sempre de frente para a câmera e de pé no mundo
                float3 cam = CnGodot(_WorldSpaceCameraPos.xyz);
                float3 f = normalize(_direcao.xyz);
                float3 dir = normalize(cross(f, float3(0.0, 1.0, 0.0)));
                float3 cima = cross(dir, f);
                float3 mundo = cam + f * _distancia + (dir * p.x + cima * p.y - f * p.z) * _tamanho;
                o.pos = mul(UNITY_MATRIX_VP, float4(CnGodot(mundo), 1.0));
                // sombreado de desenho: luz de cima/esquerda, borda pela normal de frente
                float3 nn = normalize(n);
                o.luz = 0.78 + 0.32 * dot(nn, normalize(float3(-0.45, 0.65, 0.6)));
                o.borda = nn.z;
                float pisca = step(_piscar_cada - 0.16, GMod(tempo, _piscar_cada));
                float2 q = (o.p_local.xy - _rosto_centro.xy) / _rosto_escala;
                float2 uv = float2((clamp(q.x + 0.5, 0.002, 0.998) + pisca) * 0.5, 0.5 - q.y);
                // (a imagem na Unity tem o v para cima)
                o.uv_rosto = float2(uv.x, 1.0 - uv.y);
                o.cor = v.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c = i.cor.rgb * i.luz;
                // o rosto (só na frente; na baleia, dos dois lados)
                half frente = _rosto_lado > 0.5 ? step(0.05, abs(i.p_local.z)) : step(0.0, i.p_local.z);
                float2 q = (i.p_local.xy - _rosto_centro.xy) / _rosto_escala;
                half dentro = step(abs(q.x), 0.5) * step(abs(q.y), 0.5) * frente;
                half4 r = tex2D(_rosto, i.uv_rosto);
                c = lerp(c, r.rgb, r.a * dentro);
                // a borda escura de desenho, na volta (lisa: pela normal interpolada)
                c = lerp(_cor_borda.rgb, c, smoothstep(0.12, 0.3, i.borda));
                c *= _tinta.rgb;
                return half4(lerp(c, _nevoa.rgb, _nevoa.a), 1.0);
            }
            ENDCG
        }
    }
}
