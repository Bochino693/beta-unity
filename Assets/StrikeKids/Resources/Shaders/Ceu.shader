// O CÉU: uma esfera em volta da câmera (o Mundo a leva junto), desenhada
// primeiro e sem gravar fundura: degradê de 4 cores pela altura, o brilho
// do sol na direção dele e, no espaço, as estrelas (textura pela UV).
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
        _MainTex ("Estrelas (textura)", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" }
        Cull Front
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            half4 _CorTopo, _CorMeio, _CorHorizonte, _CorChao, _CorSol;
            half _ForcaSol;
            float4 _SolDir;
            half _Estrelas;
            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 cor : COLOR0; };

            v2f vert(entrada v)
            {
                v2f o;
                float3 d = normalize(v.vertex.xyz);
                half y = d.y;
                half3 c;
                if (y > 0.0)
                {
                    half t1 = saturate(y / 0.35);
                    half t2 = saturate((y - 0.35) / 0.65);
                    c = lerp(lerp(_CorHorizonte.rgb, _CorMeio.rgb, t1), _CorTopo.rgb, t2);
                }
                else
                {
                    c = lerp(_CorHorizonte.rgb, _CorChao.rgb, saturate(-y / 0.12));
                }
                half s = saturate(dot(d, normalize(_SolDir.xyz)));
                c += _CorSol.rgb * (pow(s, 64.0) * 0.6 + pow(s, 8.0) * 0.18) * _ForcaSol;
                o.cor = half4(c, saturate(y * 4.0) * _Estrelas);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                // a esfera fica sempre em volta da câmera, perto do fim da visão
                float3 pm = _WorldSpaceCameraPos.xyz + d * 150.0;
                o.pos = mul(UNITY_MATRIX_VP, float4(pm, 1.0));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half e = tex2D(_MainTex, i.uv).r;
                return half4(i.cor.rgb + e * i.cor.a, 1.0);
            }
            ENDCG
        }
    }
}
