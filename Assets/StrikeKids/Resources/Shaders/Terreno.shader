// O CHÃO DOS CENÁRIOS (o terreno_leve.shader da edição Godot): o relevo é um
// só (Malhas/terreno, com os patamares das peças grandes) e o desenho de
// cada fase sai daqui. A luz e a neblina são por vértice; o desenho grande
// (listras do gramado, ondinhas da areia, manchas da selva) por vértice ou
// numa conta só por pixel; os detalhes miúdos (florzinhas, crateras e
// poeira da lua, conchinhas) vêm de uma textura que se repete (_Detalhe =
// Texturas/cenario/chao_<modo>: os PESOS de cada cor, sem compressão).
//   _Modo 0 = gramado (parque), 1 = lua (espaço), 2 = areia (mar), 3 = selva
Shader "StrikeKids/Terreno"
{
    Properties
    {
        _Modo ("Modo (0 gramado, 1 lua, 2 areia, 3 selva)", Float) = 0
        _LinA ("Cor A (linear)", Vector) = (0.14, 0.6, 0.11, 0)
        _LinB ("Cor B (linear)", Vector) = (0.1, 0.48, 0.08, 0)
        _LinC ("Detalhe 1 (linear)", Vector) = (1, 0.34, 0.64, 0)
        _LinD ("Detalhe 2 (linear)", Vector) = (1, 0.83, 0.17, 0)
        _Terra ("Terra da selva", Vector) = (0.45, 0.32, 0.17, 0)
        _Faixa ("Largura da divisa sol/sombra", Float) = 0.25
        _LuzMinima ("Luz mínima na sombra", Float) = 0
        _SolCor ("Sol (linear)", Vector) = (1, 1, 1, 0)
        _Ambiente ("Ambiente (linear)", Vector) = (0.4, 0.4, 0.4, 0)
        _NeblinaCor ("Neblina (linear)", Vector) = (0.7, 0.8, 0.95, 0)
        _NeblinaDensidade ("Densidade da neblina", Float) = 0
        _Detalhe ("Detalhes (pesos R G B)", 2D) = "black" {}
        _DetalheTile ("Metros de um ladrilho", Float) = 25
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "StrikeCenario.cginc"

            float _Modo, _Faixa, _LuzMinima, _NeblinaDensidade, _DetalheTile;
            float4 _LinA, _LinB, _LinC, _LinD, _Terra, _SolCor, _Ambiente, _NeblinaCor;
            sampler2D _Detalhe;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;        // o lugar no chão, como na edição Godot (x ao contrário)
                float2 uvDet : TEXCOORD1;
                half4 luz : TEXCOORD2;       // rgb: luz, a: neblina
                half mancha : TEXCOORD3;
            };

            v2f vert(entrada v)
            {
                v2f o;
                float4 pm = mul(unity_ObjectToWorld, v.vertex);
                float2 p = CnGodot(pm.xyz).xz;
                o.p = p;
                // (a Unity conta o V da imagem de baixo para cima: o mesmo desenho do Godot)
                o.uvDet = float2(p.x, -p.y) / _DetalheTile;
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float t = smoothstep(-_Faixa, _Faixa, dot(n, _CnSolDir.xyz) - 0.02);
                float alt = saturate(pm.y / 4.0);
                float3 luz = (_Ambiente.rgb + _SolCor.rgb * max(t, _LuzMinima)) * (1.0 + 0.23 * alt);
                float neblina = 1.0 - exp(-_NeblinaDensidade * length(_WorldSpaceCameraPos.xyz - pm.xyz));
                o.luz = half4(luz, neblina);
                // desenhos grandes e suaves: por vértice (o terreno tem malha fina)
                float mancha = 0.0;
                if (_Modo > 1.5 && _Modo < 2.5)
                    mancha = sin(p.x * 1.6 + sin(p.y * 0.35) * 2.0) * 0.5 + 0.5;
                else if (_Modo > 2.5)
                    mancha = sin(p.x * 0.45 + 1.3) * sin(p.y * 0.38) + 0.5 * sin(p.x * 1.1 - p.y * 0.9);
                o.mancha = mancha;
                o.pos = mul(UNITY_MATRIX_VP, pm);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c;
                if (_Modo < 0.5)
                {
                    // listras do corte de grama
                    half s = frac((i.p.y + i.p.x * 0.15) / 4.4) - 0.5;
                    c = lerp(_LinA.rgb, _LinB.rgb, saturate(0.5 - (abs(s) - 0.25) * 30.0));
                }
                else if (_Modo < 1.5)
                {
                    // lua: cinza (as crateras vêm da textura)
                    c = _LinA.rgb;
                }
                else if (_Modo < 2.5)
                {
                    c = lerp(_LinA.rgb, _LinB.rgb, smoothstep(0.35, 0.65, i.mancha));
                }
                else
                {
                    c = lerp(_LinB.rgb, _LinA.rgb, saturate(0.5 + i.mancha * 8.0));
                    c = lerp(c, _LinD.rgb * _Terra.rgb, saturate(0.5 - (1.05 - i.mancha) * 8.0));
                }
                // florzinhas, crateras, poeira, conchinhas (pesos de cada cor)
                half3 w = tex2D(_Detalhe, i.uvDet).rgb;
                c += ((half3)_LinB.rgb - c) * w.r + ((half3)_LinC.rgb - c) * w.g + ((half3)_LinD.rgb - c) * w.b;
                return half4(CnTelaH(lerp(c * i.luz.rgb, (half3)_NeblinaCor.rgb, i.luz.a)), 1.0);
            }
            ENDCG
        }
    }
}
