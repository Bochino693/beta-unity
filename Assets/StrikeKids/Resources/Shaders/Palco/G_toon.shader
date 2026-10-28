// TOON DO STRIKE KIDS (o toon.shader da edição Godot OpenGL ES 2.0). A cor
// vem da cor do vértice (sRGB, pintada no modelador) x tinta. Luz em duas
// faixas (sol / sombra colorida), brilho de borda e, nos rostos, o desenho
// do rosto projetado de frente (atlas de expressões).
// TUDO POR VÉRTICE: o pixel só pinta a cor pronta (e, no rosto, uma
// textura). A conta da luz é feita em linear e a cor sai para a tela com a
// mesma curva do Godot 4.
// A luz do cenário (_luz_dir, _luz_cor, _ambiente) é GLOBAL
// (Shader.SetGlobalVector) e vem como no Godot: as contas de mundo usam o
// ponto do Godot (CnGodot: o X ao contrário).
Shader "StrikeKids/G/toon"
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
        _brilho_luz ("Brilho das partes acesas", Float) = 1.4
        _usa_rosto ("Usa rosto", Float) = 0
        _rosto ("Rosto (atlas)", 2D) = "white" {}
        _rosto_centro ("Centro do rosto", Vector) = (0, 0.235, 0, 0)
        _rosto_tamanho ("Tamanho do rosto", Vector) = (0.46, 0.4, 0, 0)
        _rosto_grade ("Grade do atlas", Vector) = (4, 2, 0, 0)
        _rosto_celula ("Célula do atlas", Vector) = (0, 0, 0, 0)
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
            float _faixa, _luz_minima, _borda, _especular, _tamanho_especular, _flash, _brilho_luz;
            float _usa_rosto;
            sampler2D _rosto;
            float4 _rosto_centro, _rosto_tamanho, _rosto_grade, _rosto_celula;
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

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 cor : TEXCOORD0;
                half3 luz_lin : TEXCOORD1;
                float2 uv_rosto : TEXCOORD2;
                half mascara_rosto : TEXCOORD3;
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
                // partes que brilham (alfa do vértice = 0,5): núcleo do peito, antena, orelhas
                float brilho = 1.0 - saturate(abs(v.color.a - 0.5) * 5.0);
                soma += base * brilho * _brilho_luz;
                if (_especular > 0.0)
                {
                    float3 lv = normalize(mul((float3x3)UNITY_MATRIX_V, CnGodot(_luz_dir.xyz)));
                    float s = smoothstep(_tamanho_especular, _tamanho_especular + 0.02, dot(nv, normalize(vv + lv)));
                    soma += _luz_cor.xyz * s * _especular;
                }
                o.cor = GTela(base * l + soma);
                o.luz_lin = l;
                // o rosto projetado de frente, pelo ponto do Godot (uv como lá: v para baixo)
                float2 uv = (CnGodot(v.vertex.xyz).xy - _rosto_centro.xy) / _rosto_tamanho.xy + 0.5;
                o.uv_rosto = float2(uv.x, 1.0 - uv.y);
                o.mascara_rosto = smoothstep(0.75, 0.95, v.color.a) * smoothstep(0.05, 0.3, v.normal.z);
                o.pos = mul(UNITY_MATRIX_VP, pm);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c = i.cor;
                // (a leitura da textura só dentro da condição do material: a
                // máscara e o "dentro do quadro" mudam de pixel para pixel e
                // entram como peso)
                if (_usa_rosto > 0.5)
                {
                    float2 uv = i.uv_rosto;
                    float2 dentro = (1.0 - step(uv, float2(0.0, 0.0))) * (1.0 - step(float2(1.0, 1.0), uv));
                    half peso = (1.0 - step(i.mascara_rosto, 0.01)) * dentro.x * dentro.y;
                    float2 a = (clamp(uv, 0.004, 0.996) + _rosto_celula.xy) / _rosto_grade.xy;
                    // (a imagem na Unity tem o v para cima)
                    half4 f = tex2D(_rosto, float2(a.x, 1.0 - a.y));
                    c = lerp(c, GTelaRapida(f.rgb * f.rgb * i.luz_lin), f.a * i.mascara_rosto * peso);
                }
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
}
