using System;
using Unity.Collections;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A IMAGEM DA CÂMERA -> FOTO QUADRADA: a "telinha" de LADO x LADO da edição
    /// Godot. A imagem da câmera passa pelo shader StrikeKids/Camera
    /// (Resources/Shaders/Camera.shader) numa RenderTexture — que é a imagem ao
    /// vivo (Fotos.AoVivo) e de onde sai a foto (ReadPixels).
    ///
    /// O recorte do quadrado do meio, o espelho e o giro (câmera montada de
    /// lado: Ajustes jogo/camera_giro e jogo/camera_espelho) são a MESMA conta
    /// do shader do Godot, na mesma ordem (espelha, gira, recorta), feita aqui
    /// uma vez e passada ao shader como uma conta afim nas coordenadas.
    /// </summary>
    internal sealed class CameraImagem
    {
        public readonly int Lado;
        public RenderTexture Alvo { get; private set; }
        public bool Pronta => _mat != null;

        readonly Material _mat;
        Texture2D _leitura;
        bool _yuv;
        const string YUV = "SK_CAMERA_YUV";
        static readonly int ID_U = Shader.PropertyToID("_CamU");
        static readonly int ID_V = Shader.PropertyToID("_CamV");
        static readonly int ID_S = Shader.PropertyToID("_SkCamS");
        static readonly int ID_T = Shader.PropertyToID("_SkCamT");

        public CameraImagem(int lado)
        {
            Lado = lado;
            Shader s = Resources.Load<Shader>("Shaders/Camera");
            if (s == null) s = Shader.Find("StrikeKids/Camera");
            if (s == null || !s.isSupported)
            {
                Debug.LogError("Fotos: sem o shader StrikeKids/Camera (Resources/Shaders/Camera.shader): sem foto");
            }
            else
            {
                _mat = new Material(s) { name = "Fotos.Camera", hideFlags = HideFlags.DontSave };
            }
            Alvo = new RenderTexture(lado, lado, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = "Fotos.AoVivo",
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            Alvo.Create();
            Limpar();
        }

        /// <summary>A RenderTexture pode sumir (contexto da placa de vídeo perdido): refaz, preta.</summary>
        void Garantir()
        {
            if (Alvo.IsCreated()) return;
            Alvo.Create();
            Limpar();
        }

        /// <summary>Preta (nada da partida anterior aparece na prévia).</summary>
        public void Limpar()
        {
            if (Alvo == null) return;
            if (!Alvo.IsCreated()) Alvo.Create();
            var antes = RenderTexture.active;
            RenderTexture.active = Alvo;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = antes;
        }

        /// <summary>
        /// Desenha a imagem da câmera na foto quadrada.
        /// principal: RGB, ou o Y (Alpha8) quando yuv (u e v: os planos Alpha8 de croma).
        /// largura/altura: da imagem da câmera (para o recorte do quadrado do meio).
        /// deBaixoParaCima: textura no jeito da Unity (WebCamTexture, câmera de
        /// teste); false = a primeira linha dos dados é a de cima (TV Box).
        /// giro: 0..3 (x 90°), já somado o do operador; deslocX: mexida da câmera de teste.
        /// </summary>
        public void Desenhar(Texture principal, Texture u, Texture v, bool yuv, int largura, int altura, bool deBaixoParaCima,
            int giro, bool espelhar, float deslocX)
        {
            if (_mat == null || principal == null || Alvo == null) return;
            Garantir();
            float ex = 1f, ey = 1f;
            if (largura > 1 && altura > 1)
            {
                float asp = (float)largura / altura;
                if (asp >= 1f) ex = 1f / asp;
                else ey = asp;
            }
            Mapear(0f, 0f, giro, espelhar, ex, ey, deBaixoParaCima, deslocX, out float u0, out float v0);
            Mapear(1f, 0f, giro, espelhar, ex, ey, deBaixoParaCima, deslocX, out float u1, out float v1);
            Mapear(0f, 1f, giro, espelhar, ex, ey, deBaixoParaCima, deslocX, out float u2, out float v2);
            _mat.SetVector(ID_S, new Vector4(u1 - u0, u2 - u0, u0, 0f));
            _mat.SetVector(ID_T, new Vector4(v1 - v0, v2 - v0, v0, 0f));
            if (yuv != _yuv)
            {
                _yuv = yuv;
                if (yuv) _mat.EnableKeyword(YUV);
                else _mat.DisableKeyword(YUV);
            }
            if (yuv)
            {
                _mat.SetTexture(ID_U, u);
                _mat.SetTexture(ID_V, v);
            }
            // (o Blit deixa a foto como alvo ativo: volta o de antes, para nada mais desenhar nela)
            var antes = RenderTexture.active;
            Graphics.Blit(principal, Alvo, _mat);
            RenderTexture.active = antes;
        }

        /// <summary>
        /// Um ponto da foto (s, t: 0..1, t para CIMA, como a Unity) -> o ponto da
        /// textura da câmera. A conta do camera.shader do Godot (y para baixo):
        /// espelha, gira em volta do centro, recorta o quadrado do meio.
        /// </summary>
        public static void Mapear(float s, float t, int giro, bool espelhar, float ex, float ey, bool deBaixoParaCima, float deslocX,
            out float u, out float v)
        {
            float x = s, y = 1f - t;
            if (espelhar) x = 1f - x;
            float qx = x - 0.5f, qy = y - 0.5f, rx, ry;
            switch (giro & 3)
            {
                case 1: rx = qy; ry = -qx; break;
                case 2: rx = -qx; ry = -qy; break;
                case 3: rx = -qy; ry = qx; break;
                default: rx = qx; ry = qy; break;
            }
            u = rx * ex + 0.5f + deslocX;
            v = ry * ey + 0.5f;
            if (deBaixoParaCima) v = 1f - v;
        }

        // ------------------------------------------------------------ a foto
        /// <summary>Lê a foto da placa de vídeo (ReadPixels: para o jogo um instante; só na hora da foto).</summary>
        public bool Ler()
        {
            if (Alvo == null || !Alvo.IsCreated()) return false;
            if (_leitura == null)
                _leitura = new Texture2D(Lado, Lado, TextureFormat.RGB24, false) { name = "Fotos.Leitura", hideFlags = HideFlags.DontSave };
            var antes = RenderTexture.active;
            RenderTexture.active = Alvo;
            _leitura.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0, false);
            RenderTexture.active = antes;
            return Dados().Length >= Lado * Lado * 3;
        }

        NativeArray<byte> Dados() => _leitura.GetRawTextureData<byte>();

        /// <summary>A foto lida presta? Com imagem, nem escura nem chapada (tampa na lente).</summary>
        public bool Boa() => !Escura() && Contraste() >= 0.04f;

        int Indice(int x, int yDeCima) => ((Lado - 1 - yDeCima) * Lado + x) * 3;

        /// <summary>Diferença entre o ponto mais claro e o mais escuro (grade 8x6): imagem
        /// "chapada" (lente tampada, câmera sem imagem) fica perto de zero.</summary>
        public float Contraste()
        {
            var d = Dados();
            float claro = 0f, escuro = 1f;
            for (int gx = 0; gx < 8; gx++)
                for (int gy = 0; gy < 6; gy++)
                {
                    int k = Indice((int)((gx + 0.5f) / 8f * Lado), (int)((gy + 0.5f) / 6f * Lado));
                    float v = (0.2126f * d[k] + 0.7152f * d[k + 1] + 0.0722f * d[k + 2]) / 255f;
                    if (v > claro) claro = v;
                    if (v < escuro) escuro = v;
                }
            return claro - escuro;
        }

        public bool Escura()
        {
            var d = Dados();
            float soma = 0f;
            for (int i = 0; i < 64; i++)
            {
                int k = Indice(i * 37 % Lado, i * 53 % Lado);
                soma += d[k] + d[k + 1] + d[k + 2];
            }
            return soma / (64f * 3f * 255f) < 0.03f;
        }

        /// <summary>Cópia dos pontos lidos: RGB24, de baixo para cima (o jeito da Unity).</summary>
        public byte[] Copiar()
        {
            int n = Lado * Lado * 3;
            var a = new byte[n];
            NativeArray<byte>.Copy(Dados(), a, n);
            return a;
        }

        public void Liberar()
        {
            if (Alvo != null)
            {
                Alvo.Release();
                UnityEngine.Object.Destroy(Alvo);
                Alvo = null;
            }
            if (_leitura != null) UnityEngine.Object.Destroy(_leitura);
            _leitura = null;
            if (_mat != null) UnityEngine.Object.Destroy(_mat);
        }
    }
}
