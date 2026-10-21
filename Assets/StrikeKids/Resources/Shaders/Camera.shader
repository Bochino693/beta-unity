// IMAGEM DA CÂMERA -> FOTO QUADRADA (porte do shaders/camera.shader da edição
// Godot). Desenhada com Graphics.Blit numa RenderTexture 384x384 (Fotos.cs,
// CameraImagem.cs) — que é a imagem ao vivo e a foto.
//
// Converte o que a câmera manda: RGB (webcam do PC, câmera de teste) ou
// Y + U + V separados (TV Box: três texturas Alpha8, 4:2:0, a cor sai aqui).
// O recorte do quadrado do meio, o espelho (a criança se vê como num
// espelho) e o giro da câmera montada de lado são UMA conta afim nas
// coordenadas, feita no C# e aplicada no vértice (só 4 vértices): o
// fragmento só lê as texturas — o mais leve para a Mali-400/450 (que só tem
// precisão média no fragmento).
//   uv = (dot(_SkCamS.xyz, (s, t, 1)), dot(_SkCamT.xyz, (s, t, 1)))
Shader "StrikeKids/Camera"
{
    Properties
    {
        _MainTex ("Y (ou RGB)", 2D) = "black" {}
        _CamU ("U (Cb)", 2D) = "gray" {}
        _CamV ("V (Cr)", 2D) = "gray" {}
        _SkCamS ("u = s, t, 1", Vector) = (1, 0, 0, 0)
        _SkCamT ("v = s, t, 1", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile __ SK_CAMERA_YUV
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _CamU;
            sampler2D _CamV;
            float4 _SkCamS;
            float4 _SkCamT;

            struct entrada { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(entrada v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 st = float3(v.uv, 1.0);
                o.uv = float2(dot(_SkCamS.xyz, st), dot(_SkCamT.xyz, st));
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
            #ifdef SK_CAMERA_YUV
                // YCbCr de faixa cheia (os mesmos números da edição Godot)
                half y = tex2D(_MainTex, i.uv).a;
                half cb = tex2D(_CamU, i.uv).a - 0.5;
                half cr = tex2D(_CamV, i.uv).a - 0.5;
                half3 c = half3(y + 1.402 * cr, y - 0.344136 * cb - 0.714136 * cr, y + 1.772 * cb);
            #else
                half3 c = tex2D(_MainTex, i.uv).rgb;
            #endif
                return half4(saturate(c), 1.0);
            }
            ENDCG
        }
    }
}
