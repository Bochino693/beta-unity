#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// O ARDUINO NO USB DA TV BOX: a ponte com o StrikeSerial.java (driver
    /// USB-serial próprio: CDC, CH340, FTDI e CP210x). Só a TV Box (Android);
    /// quem usa é o ArduinoBridge.
    ///
    /// As perguntas de todo quadro (Estado, TemLinhas) não criam lixo; o texto
    /// (Ler) só é pedido quando chegou linha.
    /// </summary>
    internal sealed class UsbSerial
    {
        public const int FECHADA = 0, ABRINDO = 1, ABERTA = 2, ERRO = 3;
        const string CLASSE = "com.lazersport.strikekids.StrikeSerial";

        readonly UsbJava _j;
        readonly IntPtr _iniciar, _listar, _listada, _pendente, _pedir, _codigo, _erro, _abrir, _ler, _temLinhas,
            _escrever, _fechar, _relatorio;
        readonly jvalue[] _a1 = new jvalue[1];
        readonly jvalue[] _a2 = new jvalue[2];

        /// <summary>null se o APK não tem a classe Java.</summary>
        public static UsbSerial Criar()
        {
            var j = UsbJava.Abrir(CLASSE);
            if (j == null) return null;
            var s = new UsbSerial(j);
            return s._codigo == IntPtr.Zero ? null : s;
        }

        UsbSerial(UsbJava j)
        {
            _j = j;
            _iniciar = j.Metodo("iniciar", "(Landroid/app/Activity;)V");
            _listar = j.Metodo("listar", "()Ljava/lang/String;");
            _listada = j.Metodo("listada", "()Z");
            _pendente = j.Metodo("permissaoPendente", "()Z");
            _pedir = j.Metodo("pedirPermissao", "(Ljava/lang/String;)Ljava/lang/String;");
            _codigo = j.Metodo("codigoEstado", "()I");
            _erro = j.Metodo("erro", "()Ljava/lang/String;");
            _abrir = j.Metodo("abrir", "(Ljava/lang/String;I)V");
            _ler = j.Metodo("ler", "()Ljava/lang/String;");
            _temLinhas = j.Metodo("temLinhas", "()Z");
            _escrever = j.Metodo("escrever", "(Ljava/lang/String;)Z");
            _fechar = j.Metodo("fechar", "()V");
            _relatorio = j.Metodo("relatorio", "()Ljava/lang/String;");
        }

        /// <summary>Passa a atividade da Unity ao Java (USB, janela de permissão, fechar ao sair).</summary>
        public void Iniciar()
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

        /// <summary>"nome|VID:PID|tipo|permissão(1/0)|produto" por linha (a lista vem pronta, refeita em segundo plano).</summary>
        public string Listar() => _j.Texto(_listar);

        /// <summary>A primeira lista já ficou pronta?</summary>
        public bool Listada() => _j.Bool(_listada);

        public bool PermissaoPendente() => _j.Bool(_pendente);

        public string PedirPermissao(string nome) => _j.TextoComTexto(_pedir, _a1, nome);

        /// <summary>FECHADA, ABRINDO, ABERTA ou ERRO.</summary>
        public int Estado() => _j.Int(_codigo);

        public string Erro() => _j.Texto(_erro);

        public void Abrir(string nome, int baud)
        {
            IntPtr s = UsbJava.NovoTexto(nome);
            try
            {
                _a2[0] = new jvalue { l = s };
                _a2[1] = new jvalue { i = baud };
                _j.Nada(_abrir, _a2);
            }
            finally
            {
                _a2[0] = new jvalue();
                UsbJava.ApagarLocal(s);
            }
        }

        public bool TemLinhas() => _j.Bool(_temLinhas);

        /// <summary>As linhas que chegaram, separadas por \n ("" = nenhuma).</summary>
        public string Ler() => _j.Texto(_ler);

        public bool Escrever(string linha)
        {
            IntPtr s = UsbJava.NovoTexto(linha);
            try
            {
                _a1[0] = new jvalue { l = s };
                return _j.Bool(_escrever, _a1);
            }
            finally
            {
                _a1[0] = new jvalue();
                UsbJava.ApagarLocal(s);
            }
        }

        public void Fechar() => _j.Nada(_fechar);

        public string Relatorio() => _j.Texto(_relatorio);
    }
}
#endif
