// As plantas perto da pista balançando ao vento (o quanto vem no UV3).
// As contas: CenarioAtlas.cginc (as mesmas da edição Godot).
Shader "StrikeKids/CenarioAtlasVento"
{
    Properties
    {
        _MainTex ("Atlas da fase", 2D) = "white" {}
        _Faixa ("Largura da divisa sol/sombra", Float) = 0.1
        _Emissao ("O que soma por cima (já como sai na tela)", Color) = (0, 0, 0, 1)
        _FSol ("Fator no sol", Vector) = (1, 1, 1, 0)
        _FSombra ("Fator na sombra", Vector) = (0.5, 0.5, 0.5, 0)
        _FBordaSol ("Borda clara no sol", Vector) = (0, 0, 0, 0)
        _FBordaSombra ("Borda clara na sombra", Vector) = (0, 0, 0, 0)
        _FAcesa ("Partes que brilham", Vector) = (2, 2, 2, 0)
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
            #define CN_VENTO
            #include "CenarioAtlas.cginc"
            ENDCG
        }
    }
}
