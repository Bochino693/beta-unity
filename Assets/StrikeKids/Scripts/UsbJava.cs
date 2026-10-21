#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace StrikeKids
{
    /// <summary>
    /// A PONTE COM AS CLASSES JAVA DO USB (Arduino: StrikeSerial; câmera:
    /// StrikeCamera — Assets/Plugins/Android). Os métodos estáticos são chamados
    /// direto pelo JNI, com os ids guardados: as perguntas de todo quadro
    /// (estado, linhas novas, quadro novo) não criam lixo para o coletor do C#.
    ///
    /// Qualquer erro do Java ou do JNI vira o valor padrão (0, false, "") e um
    /// aviso no log (no máximo um a cada 10 s) — nunca uma exceção no Update.
    /// Toda referência local criada aqui é apagada na hora (a tabela delas é
    /// pequena e estoura numa TV Box se acumular).
    /// </summary>
    internal sealed class UsbJava
    {
        public readonly string Nome;
        readonly AndroidJavaClass _classe;   // guarda a referência global da classe
        readonly IntPtr _raw;
        long _ultimoAviso = -100000;
        static readonly jvalue[] NADA = new jvalue[0];
        static readonly Stopwatch _relogio = Stopwatch.StartNew();

        UsbJava(string nome, AndroidJavaClass classe)
        {
            Nome = nome;
            _classe = classe;
            _raw = classe.GetRawClass();
        }

        /// <summary>A classe Java (null se ela não está no APK).</summary>
        public static UsbJava Abrir(string nome)
        {
            try
            {
                var c = new AndroidJavaClass(nome);
                if (c.GetRawClass() == IntPtr.Zero)
                {
                    c.Dispose();
                    return null;
                }
                return new UsbJava(nome, c);
            }
            catch (Exception e)
            {
                Debug.LogWarning(nome + ": classe Java indisponível: " + e.Message);
                LimparExcecao();
                return null;
            }
        }

        /// <summary>Id de um método estático (ex.: "abrir", "(Ljava/lang/String;I)V"). Zero se não existe.</summary>
        public IntPtr Metodo(string nome, string assinatura)
        {
            try
            {
                IntPtr m = AndroidJNI.GetStaticMethodID(_raw, nome, assinatura);
                if (Excecao(nome) || m == IntPtr.Zero)
                {
                    Debug.LogWarning(Nome + ": sem o método " + nome + assinatura);
                    return IntPtr.Zero;
                }
                return m;
            }
            catch (Exception e)
            {
                Avisar(nome, e.Message);
                return IntPtr.Zero;
            }
        }

        // ------------------------------------------------------------ chamadas
        public int Int(IntPtr m, jvalue[] a = null)
        {
            if (m == IntPtr.Zero) return 0;
            try
            {
                int r = AndroidJNI.CallStaticIntMethod(_raw, m, a ?? NADA);
                return Excecao("int") ? 0 : r;
            }
            catch (Exception e)
            {
                Avisar("int", e.Message);
                return 0;
            }
        }

        public bool Bool(IntPtr m, jvalue[] a = null)
        {
            if (m == IntPtr.Zero) return false;
            try
            {
                bool r = AndroidJNI.CallStaticBooleanMethod(_raw, m, a ?? NADA);
                return !Excecao("bool") && r;
            }
            catch (Exception e)
            {
                Avisar("bool", e.Message);
                return false;
            }
        }

        public string Texto(IntPtr m, jvalue[] a = null)
        {
            if (m == IntPtr.Zero) return "";
            try
            {
                string r = AndroidJNI.CallStaticStringMethod(_raw, m, a ?? NADA);
                return Excecao("texto") ? "" : (r ?? "");
            }
            catch (Exception e)
            {
                Avisar("texto", e.Message);
                return "";
            }
        }

        public void Nada(IntPtr m, jvalue[] a = null)
        {
            if (m == IntPtr.Zero) return;
            try
            {
                AndroidJNI.CallStaticVoidMethod(_raw, m, a ?? NADA);
                Excecao("void");
            }
            catch (Exception e)
            {
                Avisar("void", e.Message);
            }
        }

        /// <summary>Um objeto Java (referência LOCAL: quem chama apaga com ApagarLocal). Zero = null/erro.</summary>
        public IntPtr Objeto(IntPtr m, jvalue[] a = null)
        {
            if (m == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                IntPtr r = AndroidJNI.CallStaticObjectMethod(_raw, m, a ?? NADA);
                if (Excecao("objeto"))
                {
                    if (r != IntPtr.Zero) AndroidJNI.DeleteLocalRef(r);
                    return IntPtr.Zero;
                }
                return r;
            }
            catch (Exception e)
            {
                Avisar("objeto", e.Message);
                return IntPtr.Zero;
            }
        }

        /// <summary>Um texto Java novo (referência LOCAL: apague com ApagarLocal).</summary>
        public static IntPtr NovoTexto(string s)
        {
            try
            {
                return AndroidJNI.NewStringUTF(s ?? "");
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }

        public static void ApagarLocal(IntPtr r)
        {
            if (r == IntPtr.Zero) return;
            try { AndroidJNI.DeleteLocalRef(r); } catch (Exception) { }
        }

        public static void ApagarGlobal(IntPtr r)
        {
            if (r == IntPtr.Zero) return;
            try { AndroidJNI.DeleteGlobalRef(r); } catch (Exception) { }
        }

        /// <summary>Chama `m` com um texto (e mais nada) e volta o texto que ele devolver.</summary>
        public string TextoComTexto(IntPtr m, jvalue[] a1, string arg)
        {
            IntPtr s = NovoTexto(arg);
            try
            {
                a1[0] = new jvalue { l = s };
                return Texto(m, a1);
            }
            finally
            {
                a1[0] = new jvalue();
                ApagarLocal(s);
            }
        }

        /// <summary>A atividade da Unity (referência LOCAL nova: apague com ApagarLocal).</summary>
        public static IntPtr Atividade()
        {
            try
            {
                using (var a = AndroidPonte.AtividadeAtual())
                {
                    if (a == null) return IntPtr.Zero;
                    IntPtr r = a.GetRawObject();
                    return r == IntPtr.Zero ? IntPtr.Zero : AndroidJNI.NewLocalRef(r);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("atividade da Unity indisponível: " + e.Message);
                LimparExcecao();
                return IntPtr.Zero;
            }
        }

        // ------------------------------------------------------------ erros
        /// <summary>Exceção pendente do Java? Limpa e avisa (senão a próxima chamada JNI derruba o jogo).</summary>
        bool Excecao(string onde)
        {
            IntPtr ex = AndroidJNI.ExceptionOccurred();
            if (ex == IntPtr.Zero) return false;
            AndroidJNI.ExceptionClear();
            AndroidJNI.DeleteLocalRef(ex);
            Avisar(onde, "exceção do Java");
            return true;
        }

        static void LimparExcecao()
        {
            try
            {
                IntPtr ex = AndroidJNI.ExceptionOccurred();
                if (ex == IntPtr.Zero) return;
                AndroidJNI.ExceptionClear();
                AndroidJNI.DeleteLocalRef(ex);
            }
            catch (Exception) { }
        }

        void Avisar(string onde, string msg)
        {
            long agora = _relogio.ElapsedMilliseconds;
            if (agora - _ultimoAviso < 10000) return;
            _ultimoAviso = agora;
            Debug.LogWarning(Nome + " (" + onde + "): " + msg);
        }
    }
}
#endif
