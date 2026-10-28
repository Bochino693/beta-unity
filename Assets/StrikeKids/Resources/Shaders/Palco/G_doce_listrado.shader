// DOCE LISTRADO (os para-choques; o doce_listrado.shader da edição Godot
// GLES2): listras em espiral pela altura (UV.y do Godot, contada do começo de
// cada trecho do tubo: Mundo._tubo_doce) e pela volta (UV.x), com a luz de
// palco (Materiais.registrar: _luz_dir, _luz_cor e _ambiente são GLOBAIS,
// com a direção no espaço do Godot). As contas de luz saem do vértice já
// prontas (no sol e na sombra, com a borda clara) e por PIXEL só a divisa
// sol/sombra (1 clamp e 1 mix) e o brilho (1 smoothstep): o tubo fica liso
// de perto, sem as faixas de cada gomo.
Shader "StrikeKids/G/doce_listrado"
{
    Properties
    {
        _cor_a ("cor_a", Vector) = (1, 1, 1, 1)
        _cor_b ("cor_b", Vector) = (1, 0.23, 0.31, 1)
        _listras_por_metro ("listras_por_metro", Float) = 4
        _giro ("giro (voltas da espiral por listra)", Float) = 1
        _faixa ("faixa (0..0,5)", Float) = 0.08
        _borda ("borda (0..2)", Float) = 0.35
        _especular ("especular (0..2)", Float) = 0.5
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

            float4 _cor_a, _cor_b;
            float _listras_por_metro, _giro, _faixa, _borda, _especular;
            // a luz de palco (Shader.SetGlobalVector; lineares, a direção PARA
            // o sol no espaço do Godot)
            float4 _luz_dir;
            float4 _luz_cor;
            float4 _ambiente;

            struct entrada { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                // (rgb: luz no sol, a: a rampa sol/sombra) e (rgb: luz na
                // sombra, a: o cosseno do brilho)
                float4 v_sol : TEXCOORD0;
                float4 v_sombra : TEXCOORD1;
                // x: UV.x (a volta), y: y_local (o UV.y do Godot)
                float2 uv : TEXCOORD2;
            };

            float3 tela_rapida(float3 x)
            {
                // "filmic" (branco 1,6) + sRGB numa conta só, sem potência (erro < 3%)
                x = max(x, 0.0);
                return CnViva(min(sqrt(x) * (1.29 - 0.1 * x) / (1.0 + 0.25 * x), 1.0));
            }

            v2f vert(entrada v)
            {
                v2f o;
                o.uv = float2(v.uv.x, 1.0 - v.uv.y);
                float3 n = normalize(CnGodot(mul((float3x3)unity_ObjectToWorld, v.normal)));
                // a divisa como rampa de 0 (sombra) a 1 (sol) na largura da faixa:
                // conta linear, a interpolação entre os vértices não muda nada
                float virada = (dot(n, normalize(_luz_dir.xyz)) - 0.02) * (0.5 / _faixa) + 0.5;
                float3 pv = mul(UNITY_MATRIX_MV, float4(v.vertex.xyz, 1.0)).xyz;
                float3 nv = normalize(mul((float3x3)UNITY_MATRIX_MV, v.normal));
                float3 vv = normalize(-pv);
                float r1 = 1.0 - saturate(dot(nv, vv));
                float3 borda_v = r1 * r1 * r1 * _borda + _ambiente.xyz;
                float3 lv = normalize(mul((float3x3)UNITY_MATRIX_V, CnGodot(_luz_dir.xyz)));
                o.v_sol = float4(_luz_cor.xyz + borda_v, virada);
                o.v_sombra = float4(borda_v, dot(nv, normalize(vv + lv)));
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float ang = -0.25 - i.uv.x;
                float s = frac(i.uv.y * _listras_por_metro + ang * _giro) - 0.5;
                float m = saturate(0.5 - (abs(s) - 0.25) * 30.0);
                float3 base = lerp(_cor_a.rgb, _cor_b.rgb, m);
                float3 luz = lerp(i.v_sombra.rgb, i.v_sol.rgb, saturate(i.v_sol.a));
                float3 soma = _luz_cor.xyz * (smoothstep(0.93, 0.95, i.v_sombra.a) * _especular * 0.5);
                return float4(tela_rapida(base * base * luz + soma), 1.0);
            }
            ENDCG
        }
    }
}
