// TOON DOS PERSONAGENS E PINOS (o toon_personagem.shader da edição Godot
// OpenGL ES 2.0). O mesmo toon (luz por vértice, rosto projetado de frente)
// com:
//  * escuro: escurece tudo (o pino caindo no poço do alçapão);
//  * ESTAMPA DA ROUPA desenhada aqui (não na malha):
//      1 = estrela de frente no peito; 2 = bolinhas em fileiras na saia;
//      3 = listras (camiseta de pirata).
//    Só aparece onde a cor do vértice é a da roupa (estampa_base). A borda
//    do desenho é suavizada com uma largura fixa (sem derivadas).
// A luz do cenário (_luz_dir, _luz_cor, _ambiente) é GLOBAL e vem como no
// Godot; os pontos e as direções do objeto e do mundo vão para o Godot
// (CnGodot: o X ao contrário) antes das contas.
Shader "StrikeKids/G/toon_personagem"
{
    Properties
    {
        _tinta ("Tinta", Vector) = (1, 1, 1, 1)
        _faixa ("Largura da divisa sol/sombra", Float) = 0.06
        _luz_minima ("Luz mínima na sombra", Float) = 0
        _borda ("Borda clara", Float) = 0.35
        _cor_borda ("Cor da borda", Vector) = (1, 0.95, 0.9, 1)
        _especular ("Especular", Float) = 0
        _tamanho_especular ("Tamanho do especular", Float) = 0.92
        _emissao ("Emissão", Vector) = (0, 0, 0, 1)
        _flash ("Flash", Float) = 0
        _escuro ("Escuro (1 = normal)", Float) = 1
        _brilho_luz ("Brilho das partes acesas", Float) = 1.4
        _usa_rosto ("Usa rosto", Float) = 0
        _rosto ("Rosto (atlas)", 2D) = "white" {}
        _rosto_centro ("Centro do rosto", Vector) = (0, 0.235, 0, 0)
        _rosto_tamanho ("Tamanho do rosto", Vector) = (0.46, 0.4, 0, 0)
        _rosto_grade ("Grade do atlas", Vector) = (4, 2, 0, 0)
        _rosto_celula ("Célula do atlas", Vector) = (0, 0, 0, 0)
        _estampa ("Estampa (0 nada, 1 estrela, 2 bolinhas, 3 listras)", Float) = 0
        _estampa_base ("Cor da roupa", Vector) = (1, 1, 1, 1)
        _estampa_cor ("Cor da estampa", Vector) = (1, 0.85, 0.2, 1)
        _estampa_centro ("Centro da estrela", Vector) = (0, 0.1, 0, 0)
        _estampa_raio ("Raio da estampa", Float) = 0.06
        _estampa_dentro ("Vale da estrela", Float) = 0.5
        _bolinhas_y0 ("Bolinhas: altura da 1a fileira", Float) = -0.05
        _bolinhas_dy ("Bolinhas: entre fileiras", Float) = 0.05
        _bolinhas_fileiras ("Bolinhas: fileiras (até 6)", Float) = 3
        _bolinhas_n0 ("Bolinhas: na 1a fileira", Float) = 12
        _bolinhas_y_max ("Bolinhas: altura máxima", Float) = 0.08
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

            float4 _tinta, _cor_borda, _emissao;
            float _faixa, _luz_minima, _borda, _especular, _tamanho_especular, _flash, _escuro, _brilho_luz;
            float _usa_rosto;
            sampler2D _rosto;
            float4 _rosto_centro, _rosto_tamanho, _rosto_grade, _rosto_celula;
            float _estampa;
            float4 _estampa_base, _estampa_cor, _estampa_centro;
            float _estampa_raio, _estampa_dentro;
            float _bolinhas_y0, _bolinhas_dy, _bolinhas_fileiras, _bolinhas_n0, _bolinhas_y_max;
            // a luz do cenário (global): direção PARA o sol (no mundo do
            // Godot) e as cores já em luz linear (cor x força)
            float4 _luz_dir, _luz_cor, _ambiente;

            // "filmic" (branco 1,6) + sRGB numa conta só, sem potência (erro < 3%)
            float3 GTela(float3 x)
            {
                x = max(x, 0.0);
                return CnViva(min(sqrt(x) * (1.29 - 0.1 * x) / (1.0 + 0.25 * x), 1.0));
            }

            // a mesma, em meia precisão, para o pixel
            half3 GTelaRapida(half3 x)
            {
                x = max(x, 0.0);
                return CnVivaH(min(sqrt(x) * (1.29 - 0.1 * x) / (1.0 + 0.25 * x), 1.0));
            }

            // o mod do GLSL (o fmod da Unity muda de sinal nos negativos)
            float GMod(float x, float y)
            {
                return x - y * floor(x / y);
            }

            float GEstrela(float2 p, float r, float vale)
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
                // o sign() feito com step (o da Unity devolve inteiro)
                float s = p.y * ba.x - p.x * ba.y;
                return length(p - ba * h) * (step(0.0, s) - step(s, 0.0));
            }

            float GBolinhas(float3 p)
            {
                float melhor = 1000.0;
                float raio_h = max(length(p.xz), 0.0001);
                float ang = atan2(p.x, p.z);
                for (int k = 0; k < 6; k++)
                {
                    float fk = (float)k;
                    // (as fileiras vêm do material: para no número delas)
                    if (fk > _bolinhas_fileiras - 0.5)
                        break;
                    float y = _bolinhas_y0 + fk * _bolinhas_dy;
                    float n = _bolinhas_n0 - fk;
                    float desloc = 0.5 * GMod(fk, 2.0);
                    float passo = 6.28318531 / n;
                    float ix = floor(ang / passo - desloc + 0.5);
                    float d_ang = ang - (ix + desloc) * passo;
                    float2 q = float2(d_ang * raio_h, p.y - y);
                    melhor = min(melhor, length(q) - _estampa_raio);
                }
                return melhor;
            }

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 cor : TEXCOORD0;
                half3 luz_lin : TEXCOORD1;
                // uv do rosto (xy), máscara do rosto (z), máscara do tecido
                // (w): juntos, para caber nas poucas variáveis da Mali-400
                float4 rosto_v : TEXCOORD2;
                float3 pos_local : TEXCOORD3;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float3 base = CnS2L(CnS2L(v.color.rgb)) * CnS2L(CnS2L(_tinta.rgb));
                float4 pm = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                float3 nm = mul((float3x3)unity_ObjectToWorld, v.normal);
                float3 n = normalize(CnGodot(nm));
                float ndl = dot(n, normalize(_luz_dir.xyz));
                float t = smoothstep(-_faixa, _faixa, ndl - 0.02);
                float3 l = _luz_cor.xyz * max(t, _luz_minima) + _ambiente.xyz;
                // (o espaço da câmera é o mesmo nas duas edições)
                float3 pv = mul(UNITY_MATRIX_V, pm).xyz;
                float3 nv = normalize(mul((float3x3)UNITY_MATRIX_V, nm));
                float3 vv = normalize(-pv);
                float r1 = 1.0 - saturate(dot(nv, vv));
                float rim = r1 * r1 * r1;
                float3 soma = CnS2L(CnS2L(_cor_borda.rgb)) * _borda * rim * base + CnS2L(CnS2L(_emissao.rgb)) + _flash * base;
                float brilho = 1.0 - saturate(abs(v.color.a - 0.5) * 5.0);
                soma += base * brilho * _brilho_luz;
                if (_especular > 0.0)
                {
                    float3 lv = normalize(mul((float3x3)UNITY_MATRIX_V, CnGodot(_luz_dir.xyz)));
                    float s = smoothstep(_tamanho_especular, _tamanho_especular + 0.02, dot(nv, normalize(vv + lv)));
                    soma += _luz_cor.xyz * s * _especular;
                }
                o.cor = GTela((base * l + soma) * _escuro);
                o.luz_lin = l * _escuro;
                // o ponto do Godot: o rosto e a estampa saem como lá
                float3 pg = CnGodot(v.vertex.xyz);
                float2 uv = (pg.xy - _rosto_centro.xy) / _rosto_tamanho.xy + 0.5;
                float tecido = 1.0 - smoothstep(0.05, 0.12, distance(CnS2L(v.color.rgb), CnS2L(_estampa_base.rgb)));
                if (_estampa > 0.5 && _estampa < 1.5)
                    tecido *= smoothstep(0.15, 0.4, v.normal.z);
                o.rosto_v = float4(uv.x, 1.0 - uv.y, smoothstep(0.75, 0.95, v.color.a) * smoothstep(0.05, 0.3, v.normal.z), tecido);
                o.pos_local = pg;
                o.pos = mul(UNITY_MATRIX_VP, pm);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c = i.cor;
                half mascara_rosto = i.rosto_v.z;
                half tecido = i.rosto_v.w;
                // (a leitura da textura só dentro da condição do material: a
                // máscara e o "dentro do quadro" entram como peso)
                if (_usa_rosto > 0.5)
                {
                    float2 uv = i.rosto_v.xy;
                    float2 dentro = (1.0 - step(uv, float2(0.0, 0.0))) * (1.0 - step(float2(1.0, 1.0), uv));
                    half peso = (1.0 - step(mascara_rosto, 0.01)) * dentro.x * dentro.y;
                    float2 a = (clamp(uv, 0.004, 0.996) + _rosto_celula.xy) / _rosto_grade.xy;
                    // (a imagem na Unity tem o v para cima)
                    half4 f = tex2D(_rosto, float2(a.x, 1.0 - a.y));
                    c = lerp(c, GTelaRapida(f.rgb * f.rgb * i.luz_lin), f.a * mascara_rosto * peso);
                }
                // (aqui dentro não há textura: pode depender do pixel)
                if (_estampa > 0.5 && tecido > 0.01)
                {
                    float3 p = i.pos_local;
                    float d = 1000.0;
                    half lado = 1.0;
                    if (_estampa < 1.5)
                    {
                        d = GEstrela(p.xy - _estampa_centro.xy, _estampa_raio, _estampa_dentro) - _estampa_raio * 0.06;
                        lado = step(0.0, p.z);
                    }
                    else if (_estampa < 2.5)
                    {
                        d = GBolinhas(p);
                        lado = 1.0 - smoothstep(_bolinhas_y_max - 0.01, _bolinhas_y_max, p.y);
                    }
                    else
                    {
                        float y = p.y - _bolinhas_y0;
                        d = abs(y - _bolinhas_dy * floor(y / _bolinhas_dy + 0.5)) - _estampa_raio;
                        lado = (1.0 - smoothstep(_bolinhas_y_max - 0.01, _bolinhas_y_max, p.y)) * step(-_estampa_raio, y);
                    }
                    // borda suave de ~1,5 mm (fixa: sem derivadas)
                    half m = (1.0 - smoothstep(-0.0015, 0.0015, d)) * tecido * lado;
                    // (no Godot 4 a cor da estampa e a tinta eram convertidas duas vezes)
                    half3 ec = _estampa_cor.rgb * _tinta.rgb;
                    half3 e2 = ec * ec;
                    half3 ce = GTelaRapida(e2 * e2 * i.luz_lin);
                    c = lerp(c, ce, m);
                }
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
}
