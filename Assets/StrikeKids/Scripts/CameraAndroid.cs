#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// A CÂMERA DA TV BOX pelo StrikeCamera.java (câmera do sistema, Camera2 e
    /// UVC direta, com a vigia que passa de um caminho para o outro). Só a TV
    /// Box (Android); quem usa é o Fotos.
    ///
    /// O QUADRO: o Java já entrega o QUADRADO DO MEIO no lado da foto, em três
    /// planos (Y, U, V — 4:2:0, a primeira linha em cima); aqui eles viram três
    /// texturas Alpha8 que o shader StrikeKids/Camera junta em cor.
    ///
    /// SEM LIXO A CADA QUADRO: o Java copia o quadro direto para memória do
    /// jogo, embrulhada num ByteBuffer direto do Java, e a textura sobe dali
    /// (LoadRawTextureData). Três jeitos, o primeiro que funcionar (cada um é
    /// conferido — o C# escreve e o Java lê, depois o Java escreve e o C# lê —
    /// antes de valer):
    ///   1. NativeArray + AndroidJNI.NewDirectByteBuffer (Unity 2022.2+, achado
    ///      por reflexão: compila em qualquer Unity);
    ///   2. Marshal.AllocHGlobal + StrikeCamera.embrulhar (o construtor que o
    ///      JNI usa, por reflexão no Java);
    ///   3. reserva: um byte[] novo por quadro (AndroidJNI.FromSByteArray) —
    ///      funciona sempre, mas cria lixo (só durante a prévia/foto).
    /// </summary>
    internal sealed class CameraAndroid
    {
        const string CLASSE = "com.lazersport.strikekids.StrikeCamera";
        const int CONFERIR = 64;

        readonly UsbJava _j;
        readonly IntPtr _iniciar, _temPermissao, _procurar, _listagens, _lista, _ver, _quadros, _parada, _estado, _fonte,
            _abrir, _fechar, _usbPendente, _pedirUsb, _relatorio, _copiar, _bytes, _embrulhar, _marcar, _somar;
        readonly jvalue[] _a1 = new jvalue[1];
        readonly jvalue[] _a2 = new jvalue[2];
        readonly jvalue[] _a4 = new jvalue[4];

        public Texture2D TexY { get; private set; }
        public Texture2D TexU { get; private set; }
        public Texture2D TexV { get; private set; }
        /// <summary>Como o quadro chega: "nativo", "memoria", "bytes" (reserva) ou "" (ainda não).</summary>
        public string Caminho { get; private set; } = "";

        enum Modo { Nenhum, Nativo, Memoria, Bytes }
        Modo _modo = Modo.Nenhum;
        IntPtr _buffer;               // referência global ao ByteBuffer direto do Java
        NativeArray<byte> _nativo;
        IntPtr _memoria;
        int _capacidade;
        int _contados;
        static MethodInfo _novoDireto;
        static bool _procurouNovoDireto;

        /// <summary>null se o APK não tem a classe Java.</summary>
        public static CameraAndroid Criar()
        {
            var j = UsbJava.Abrir(CLASSE);
            if (j == null) return null;
            var c = new CameraAndroid(j);
            if (c._abrir == IntPtr.Zero || c._copiar == IntPtr.Zero) return null;
            c.Iniciar();
            return c;
        }

        CameraAndroid(UsbJava j)
        {
            _j = j;
            _iniciar = j.Metodo("iniciar", "(Landroid/app/Activity;)V");
            _temPermissao = j.Metodo("temPermissao", "()Z");
            _procurar = j.Metodo("procurar", "()V");
            _listagens = j.Metodo("listagens", "()I");
            _lista = j.Metodo("lista", "()Ljava/lang/String;");
            _ver = j.Metodo("ver", "(Z)V");
            _quadros = j.Metodo("quadros", "()I");
            _parada = j.Metodo("parada", "()Z");
            _estado = j.Metodo("estado", "()Ljava/lang/String;");
            _fonte = j.Metodo("fonte", "()Ljava/lang/String;");
            _abrir = j.Metodo("abrir", "(Ljava/lang/String;III)Z");
            _fechar = j.Metodo("fechar", "()V");
            _usbPendente = j.Metodo("usbPendente", "()Z");
            _pedirUsb = j.Metodo("pedirUsb", "()Ljava/lang/String;");
            _relatorio = j.Metodo("relatorio", "()Ljava/lang/String;");
            _copiar = j.Metodo("copiarQuadro", "(Ljava/nio/ByteBuffer;I)I");
            _bytes = j.Metodo("quadroBytes", "(I)[B");
            _embrulhar = j.Metodo("embrulhar", "(JI)Ljava/nio/ByteBuffer;");
            _marcar = j.Metodo("marcar", "(Ljava/nio/ByteBuffer;I)Z");
            _somar = j.Metodo("somar", "(Ljava/nio/ByteBuffer;I)I");
        }

        void Iniciar()
        {
            IntPtr a = UsbJava.Atividade();
            if (a == IntPtr.Zero) return;
            try
            {
                _a1[0] = new jvalue { l = a };
                _j.Nada(_iniciar, _a1);
            }
            finally
            {
                _a1[0] = new jvalue();
                UsbJava.ApagarLocal(a);
            }
        }

        // ------------------------------------------------------------ consulta
        public bool TemPermissao() => _j.Bool(_temPermissao);

        /// <summary>Faz a lista de câmeras em segundo plano; pronta quando Listagens() mudar.</summary>
        public void Procurar() => _j.Nada(_procurar);

        public int Listagens() => _j.Int(_listagens);

        /// <summary>"id|lado|api|permissao" por câmera (linhas com # são avisos).</summary>
        public string Lista() => _j.Texto(_lista);

        public void Ver(bool sim)
        {
            _a1[0] = new jvalue { z = sim };
            _j.Nada(_ver, _a1);
        }

        public int Quadros() => _j.Int(_quadros);

        /// <summary>A câmera foi solta (pausa da TV Box, fechar)?</summary>
        public bool Parada() => _j.Bool(_parada);

        /// <summary>O que a câmera está fazendo (a última anotação do Java).</summary>
        public string Estado() => _j.Texto(_estado);

        /// <summary>"sistema", "camera2", "uvc" ou "".</summary>
        public string Fonte() => _j.Texto(_fonte);

        public bool Abrir(string id, int largura, int altura, int lado)
        {
            IntPtr s = UsbJava.NovoTexto(id);
            try
            {
                _a4[0] = new jvalue { l = s };
                _a4[1] = new jvalue { i = largura };
                _a4[2] = new jvalue { i = altura };
                _a4[3] = new jvalue { i = lado };
                return _j.Bool(_abrir, _a4);
            }
            finally
            {
                _a4[0] = new jvalue();
                UsbJava.ApagarLocal(s);
            }
        }

        public void Fechar() => _j.Nada(_fechar);

        public bool UsbPendente() => _j.Bool(_usbPendente);

        public string PedirUsb() => _j.Texto(_pedirUsb);

        public string Relatorio()
        {
            string r = _j.Texto(_relatorio);
            return Caminho != "" ? r + "quadro para o jogo: " + Caminho + "\n" : r;
        }

        // ------------------------------------------------------------ o quadro
        static int Tamanho(int lado) => lado * lado + 2 * (lado / 2) * (lado / 2);

        /// <summary>
        /// Pega o quadro novo (se houver) e sobe para TexY/TexU/TexV. Volta um
        /// número que muda a cada quadro (0 = nada novo).
        /// </summary>
        public int LerQuadro(int lado)
        {
            Preparar(lado);
            if (_modo == Modo.Bytes) return LerBytes(lado);
            _a2[0] = new jvalue { l = _buffer };
            _a2[1] = new jvalue { i = lado };
            int r = _j.Int(_copiar, _a2);
            if (r == 0) return 0;
            if (r < 0)
            {
                // (o destino ficou pequeno: refaz no próximo quadro — só se precisar mesmo de mais)
                if (-r > _capacidade) Soltar();
                return 0;
            }
            Texturas(lado);
            int n = lado * lado, m = (lado / 2) * (lado / 2);
            if (_modo == Modo.Nativo)
            {
                TexY.LoadRawTextureData(_nativo.GetSubArray(0, n));
                TexU.LoadRawTextureData(_nativo.GetSubArray(n, m));
                TexV.LoadRawTextureData(_nativo.GetSubArray(n + m, m));
            }
            else
            {
                TexY.LoadRawTextureData(_memoria, n);
                TexU.LoadRawTextureData(IntPtr.Add(_memoria, n), m);
                TexV.LoadRawTextureData(IntPtr.Add(_memoria, n + m), m);
            }
            Subir();
            return r;
        }

        int LerBytes(int lado)
        {
            _a1[0] = new jvalue { i = lado };
            IntPtr arr = _j.Objeto(_bytes, _a1);
            if (arr == IntPtr.Zero) return 0;
            sbyte[] s;
            try
            {
                s = AndroidJNI.FromSByteArray(arr);
            }
            finally
            {
                UsbJava.ApagarLocal(arr);
            }
            int n = lado * lado, m = (lado / 2) * (lado / 2);
            if (s == null || s.Length < n + 2 * m) return 0;
            Texturas(lado);
            var fixo = GCHandle.Alloc(s, GCHandleType.Pinned);
            try
            {
                IntPtr p = fixo.AddrOfPinnedObject();
                TexY.LoadRawTextureData(p, n);
                TexU.LoadRawTextureData(IntPtr.Add(p, n), m);
                TexV.LoadRawTextureData(IntPtr.Add(p, n + m), m);
            }
            finally
            {
                fixo.Free();
            }
            Subir();
            return ++_contados;
        }

        void Subir()
        {
            TexY.Apply(false, false);
            TexU.Apply(false, false);
            TexV.Apply(false, false);
        }

        void Texturas(int lado)
        {
            if (TexY != null && TexY.width == lado) return;
            Destruir();
            TexY = NovaTextura(lado, "Fotos.Y");
            TexU = NovaTextura(lado / 2, "Fotos.U");
            TexV = NovaTextura(lado / 2, "Fotos.V");
        }

        static Texture2D NovaTextura(int lado, string nome)
        {
            return new Texture2D(lado, lado, TextureFormat.Alpha8, false)
            {
                name = nome,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        void Destruir()
        {
            if (TexY != null) UnityEngine.Object.Destroy(TexY);
            if (TexU != null) UnityEngine.Object.Destroy(TexU);
            if (TexV != null) UnityEngine.Object.Destroy(TexV);
            TexY = TexU = TexV = null;
        }

        // ---------------------------------------------------------------- o destino do quadro
        void Preparar(int lado)
        {
            int n = Tamanho(lado);
            if (_modo != Modo.Nenhum && (_modo == Modo.Bytes || _capacidade >= n)) return;
            Soltar();
            if (TentarNativo(n))
            {
                _modo = Modo.Nativo;
                Caminho = "nativo";
            }
            else if (TentarMemoria(n))
            {
                _modo = Modo.Memoria;
                Caminho = "memoria";
            }
            else
            {
                _modo = Modo.Bytes;
                Caminho = "bytes";
                Debug.LogWarning("Fotos: o quadro da câmera vem como byte[] (cria lixo durante a prévia)");
            }
        }

        /// <summary>1. NativeArray embrulhado pela própria Unity (AndroidJNI.NewDirectByteBuffer, 2022.2+).</summary>
        bool TentarNativo(int n)
        {
            if (!_procurouNovoDireto)
            {
                _procurouNovoDireto = true;
                try
                {
                    foreach (var mi in typeof(AndroidJNI).GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (mi.Name != "NewDirectByteBuffer" || mi.ReturnType != typeof(IntPtr)) continue;
                        var p = mi.GetParameters();
                        if (p.Length == 1 && p[0].ParameterType == typeof(NativeArray<byte>))
                        {
                            _novoDireto = mi;
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    _novoDireto = null;
                }
            }
            if (_novoDireto == null) return false;
            var arr = new NativeArray<byte>(n, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            IntPtr global = IntPtr.Zero;
            try
            {
                var local = (IntPtr)_novoDireto.Invoke(null, new object[] { arr });
                if (local != IntPtr.Zero)
                {
                    global = AndroidJNI.NewGlobalRef(local);
                    UsbJava.ApagarLocal(local);
                }
                if (global != IntPtr.Zero && ConferirNativo(global, arr))
                {
                    _nativo = arr;
                    _buffer = global;
                    _capacidade = n;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Fotos: NewDirectByteBuffer não serviu: " + e.Message);
            }
            UsbJava.ApagarGlobal(global);
            arr.Dispose();
            return false;
        }

        /// <summary>Primeiro o Java só LÊ (o C# escreveu): endereço errado nunca vira escrita fora do lugar.</summary>
        bool ConferirNativo(IntPtr buffer, NativeArray<byte> arr)
        {
            int esperado = 0;
            for (int i = 0; i < CONFERIR; i++)
            {
                arr[i] = (byte)(i * 13 + 5);
                esperado += (byte)(i * 13 + 5);
            }
            if (Somar(buffer) != esperado || !Marcar(buffer)) return false;
            for (int i = 0; i < CONFERIR; i++)
                if (arr[i] != (byte)(i * 7 + 3)) return false;
            return true;
        }

        /// <summary>2. Memória do C# embrulhada pelo Java (o construtor do JNI, por reflexão).</summary>
        bool TentarMemoria(int n)
        {
            if (_embrulhar == IntPtr.Zero) return false;
            IntPtr mem = IntPtr.Zero, global = IntPtr.Zero;
            try
            {
                mem = Marshal.AllocHGlobal(n);
                _a2[0] = new jvalue { j = mem.ToInt64() };
                _a2[1] = new jvalue { i = n };
                IntPtr local = _j.Objeto(_embrulhar, _a2);
                if (local != IntPtr.Zero)
                {
                    global = AndroidJNI.NewGlobalRef(local);
                    UsbJava.ApagarLocal(local);
                }
                if (global != IntPtr.Zero && ConferirMemoria(global, mem))
                {
                    _memoria = mem;
                    _buffer = global;
                    _capacidade = n;
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Fotos: memória embrulhada não serviu: " + e.Message);
            }
            UsbJava.ApagarGlobal(global);
            if (mem != IntPtr.Zero) Marshal.FreeHGlobal(mem);
            return false;
        }

        bool ConferirMemoria(IntPtr buffer, IntPtr mem)
        {
            int esperado = 0;
            for (int i = 0; i < CONFERIR; i++)
            {
                Marshal.WriteByte(mem, i, (byte)(i * 13 + 5));
                esperado += (byte)(i * 13 + 5);
            }
            if (Somar(buffer) != esperado || !Marcar(buffer)) return false;
            for (int i = 0; i < CONFERIR; i++)
                if (Marshal.ReadByte(mem, i) != (byte)(i * 7 + 3)) return false;
            return true;
        }

        bool Marcar(IntPtr buffer)
        {
            _a2[0] = new jvalue { l = buffer };
            _a2[1] = new jvalue { i = CONFERIR };
            return _j.Bool(_marcar, _a2);
        }

        int Somar(IntPtr buffer)
        {
            _a2[0] = new jvalue { l = buffer };
            _a2[1] = new jvalue { i = CONFERIR };
            return _j.Int(_somar, _a2);
        }

        /// <summary>Solta o destino do quadro (a referência do Java antes da memória).</summary>
        void Soltar()
        {
            UsbJava.ApagarGlobal(_buffer);
            _buffer = IntPtr.Zero;
            if (_nativo.IsCreated) _nativo.Dispose();
            if (_memoria != IntPtr.Zero) Marshal.FreeHGlobal(_memoria);
            _memoria = IntPtr.Zero;
            _capacidade = 0;
            if (_modo != Modo.Bytes) _modo = Modo.Nenhum;
        }

        public void Liberar()
        {
            Soltar();
            _modo = Modo.Nenhum;
            Destruir();
        }
    }
}
#endif
