// PISTA (o pista.shader da edição Godot GLES2). A decoração que não mexe
// (madeira envernizada, linha de falta, pontos, trilha de estrelas, setas
// arco-íris, marcas e holofote dos pinos) vem pronta na textura
// `_pista_pronta` (tools: assar_pista.py). Aqui só o que mexe: as estrelas
// piscando em sequência até os pinos, o verniz refletindo o céu e o portal,
// o brilho do sol, o NEON das bordas com as luzes correndo e o brilho que
// acompanha a bola. Contas em luz linear; a saída vai para a tela com a
// curva do Godot 4 (tela_rapida). As fases vêm da CPU.
// Coordenadas do MUNDO como no Godot (CnGodot): linha de falta em z = 0,
// pinos em z_pino1.
Shader "StrikeKids/G/pista"
{
    Properties
    {
        _pista_pronta ("pista_pronta (RGB = raiz(cor / 1,6), A = estrelas)", 2D) = "white" {}
        _meia_largura ("meia_largura", Float) = 0.675
        _z_pino1 ("z_pino1", Float) = -10.5
        _z_inicio ("z_inicio (começo da textura)", Float) = 0.1
        _z_fim ("z_fim (fim da textura)", Float) = -11.95
        _bola_z ("bola_z", Float) = 100
        _luz ("luz (0..1, acende na jogada)", Float) = 0
        _fase ("fase (luzes que correm)", Float) = 0
        _fase_lenta ("fase_lenta (onda das estrelas)", Float) = 0
        _pulso ("pulso", Float) = 0
        _cor_luz ("cor_luz (neon das bordas)", Vector) = (0.35, 0.85, 1, 1)
        _cor_reflexo_a ("cor_reflexo_a (portal no verniz)", Vector) = (1, 0.76, 0.1, 1)
        _cor_reflexo_b ("cor_reflexo_b", Vector) = (0.91, 0.22, 0.31, 1)
        _luz_cena ("luz_cena (linear)", Vector) = (1.2, 1.2, 1.22, 0)
        _cor_ceu ("cor_ceu", Vector) = (0.85, 0.93, 1, 1)
        _dir_sol ("dir_sol (Godot)", Vector) = (0.35, 0.79, 0.5, 0)
        _verniz ("verniz", Float) = 0.24
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

            sampler2D _pista_pronta;
            float _meia_largura, _z_pino1, _z_inicio, _z_fim, _bola_z;
            float _luz, _fase, _fase_lenta;
            float _pulso;   // (não entra na conta; fica pelo nome)
            float4 _cor_luz, _cor_reflexo_a, _cor_reflexo_b, _luz_cena, _cor_ceu, _dir_sol;
            float _verniz;

            struct entrada { float4 vertex : POSITION; };
            // a coordenada da textura vai direto do vértice para a leitura (na
            // Mali-400 assim ela não perde precisão); `xd` = (x, distância da
            // linha de falta)
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv_p : TEXCOORD0;
                float2 xd : TEXCOORD1;
                float3 olhar : TEXCOORD2;
            };

            // a mesma curva de tela do Godot 4 ("filmic", branco 1,6, + sRGB),
            // aproximada para a meia precisão da Mali-400 (erro < 3%)
            float3 tela_rapida(float3 x)
            {
                x = max(x, 0.0);
                return CnViva(min(sqrt(x) * (1.29 - 0.1 * x) / (1.0 + 0.25 * x), 1.0));
            }

            v2f vert(entrada v)
            {
                v2f o;
                float3 pm = CnGodot(mul(unity_ObjectToWorld, v.vertex).xyz);
                float2 g = float2(pm.x / (_meia_largura * 2.0) + 0.5, (pm.z - _z_inicio) / (_z_fim - _z_inicio));
                // (a UV acima é a do Godot: na imagem da Unity o v vai ao contrário)
                o.uv_p = float2(g.x, 1.0 - g.y);
                o.xd = float2(pm.x, -pm.z);
                o.olhar = CnGodot(_WorldSpaceCameraPos.xyz) - pm;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 t = tex2D(_pista_pronta, i.uv_p);
                float3 c = t.rgb * t.rgb * 1.6;
                float d = i.xd.y;
                float ax = abs(i.xd.x);
                float dp = -_z_pino1;
                float chamado = 1.0 - _luz;   // esperando a bola: a pista "chama" a jogada

                // TRILHA DE ESTRELAS: acendem em sequência até os pinos (a textura
                // tem as estrelas apagadas e a máscara de cada uma)
                if (t.a > 0.004)
                {
                    float k = clamp(floor((d - 2.3) / 1.35 + 0.5), 0.0, 5.0);
                    float onda = step(0.72, frac(_fase_lenta - k * 0.14));
                    float acesa = lerp(0.35, 1.0, max(onda * chamado, _luz * 0.7));
                    c *= 1.0 + t.a * ((0.75 + 0.6 * acesa) / 0.96 - 1.0);
                }

                // luz da cena + verniz: reflete o céu quando se olha "deitado",
                // o portal colorido lá no fundo e um brilho do sol (a madeira
                // com mais cor e o reflexo do céu fraco, tingido da madeira)
                c *= _luz_cena.xyz * 0.84;
                float lum = dot(c, float3(0.299, 0.587, 0.114));
                c = max(lerp(float3(lum, lum, lum), c, 1.3), 0.0);
                float3 vv = normalize(i.olhar);
                float f1 = 1.0 - saturate(vv.y);
                float f2 = f1 * f1;
                float fres = f2 * f2;
                c = lerp(c, _cor_ceu.rgb * _cor_ceu.rgb * c * 2.2, fres * _verniz * 0.35);
                if (d > dp - 6.0)
                {
                    float fundo = smoothstep(dp - 6.0, dp + 0.5, d);
                    float3 portal = lerp(_cor_reflexo_b.rgb, _cor_reflexo_a.rgb, 0.5 + 0.5 * sin(i.xd.x * 9.0));
                    c += portal * portal * fres * fundo * 0.28;
                }
                float3 refl = float3(-vv.x, vv.y, -vv.z);
                float b = max(dot(refl, _dir_sol.xyz), 0.0);
                float b2 = b * b;
                float b4 = b2 * b2;
                float b16 = b4 * b4;
                b16 *= b16;
                c += float3(1.0, 0.95, 0.85) * (b16 * b16 * b16 * _verniz * 0.6);   // b elevado a 48

                // BORDAS DE NEON: sempre acesas na cor do cenário; esperando a
                // bola, luzes correm para os pinos (convite); na jogada, correm
                // forte e um brilho acompanha a bola
                float3 neon = _cor_luz.rgb * _cor_luz.rgb;
                float borda = smoothstep(_meia_largura - 0.085, _meia_largura - 0.015, ax);
                float espalha = exp(-(_meia_largura - ax) * 9.0) * 0.3;
                float fr = frac(d * 1.1 - _fase);
                float corre = smoothstep(0.42, 0.58, fr) * smoothstep(0.98, 0.84, fr);
                float onda_bola = 0.0;
                if (_luz > 0.001)
                {
                    onda_bola = exp(-abs(d - _bola_z) * 1.6);
                }
                float forca = 0.75 + 0.45 * corre * chamado + _luz * (corre * 0.6 + onda_bola * 1.4);
                float3 e = neon * ((borda + espalha) * forca + onda_bola * _luz * 0.18 * (1.0 - borda));
                return float4(tela_rapida(c + e), 1.0);
            }
            ENDCG
        }
    }
}
