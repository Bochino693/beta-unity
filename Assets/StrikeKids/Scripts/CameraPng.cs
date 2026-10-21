using System;
using System.IO;
using System.IO.Compression;

namespace StrikeKids
{
    /// <summary>
    /// O PNG DAS FOTOS, sem a Unity: grava e lê em qualquer linha de trabalho
    /// (o EncodeToPNG / LoadImage da Unity só rodam na linha principal, e numa
    /// TV Box fraca levam dezenas a centenas de ms — davam tranco no fim da
    /// partida e na tela de espera, como na edição Godot).
    ///
    /// Só o que as fotos usam: 8 bits, RGB ou RGBA, sem entrelaçamento (o que
    /// o próprio jogo grava). Outro PNG qualquer: Decodificar volta null e quem
    /// chama usa a Unity (ImageConversion.LoadImage).
    ///
    /// Os pixels vêm/vão "de baixo para cima" quando pedido (o jeito da Unity:
    /// a primeira linha da textura é a de baixo); o PNG é sempre de cima para
    /// baixo.
    /// </summary>
    public static class CameraPng
    {
        static readonly byte[] ASSINATURA = { 137, 80, 78, 71, 13, 10, 26, 10 };
        static readonly uint[] CRC = TabelaCrc();

        static uint[] TabelaCrc()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        // ------------------------------------------------------------ gravar
        /// <summary>PNG de 8 bits (3 canais = RGB, 4 = RGBA). Filtro Paeth em todas as linhas (bom para foto).</summary>
        public static byte[] Codificar(byte[] pixels, int largura, int altura, int canais, bool deBaixoParaCima)
        {
            if (canais != 3 && canais != 4) throw new ArgumentException("canais: " + canais);
            if (largura <= 0 || altura <= 0) throw new ArgumentException("tamanho");
            int linha = largura * canais;
            if (pixels == null || pixels.Length < linha * altura) throw new ArgumentException("pixels curtos");
            var cru = new byte[(linha + 1) * altura];
            int o = 0;
            for (int y = 0; y < altura; y++)
            {
                int src = (deBaixoParaCima ? altura - 1 - y : y) * linha;
                int ant = y == 0 ? -1 : (deBaixoParaCima ? altura - y : y - 1) * linha;
                cru[o++] = 4;
                for (int i = 0; i < linha; i++)
                {
                    int a = i >= canais ? pixels[src + i - canais] : 0;
                    int b = ant >= 0 ? pixels[ant + i] : 0;
                    int c = ant >= 0 && i >= canais ? pixels[ant + i - canais] : 0;
                    cru[o++] = (byte)(pixels[src + i] - Paeth(a, b, c));
                }
            }
            byte[] z = Comprimir(cru);
            using (var ms = new MemoryStream(z.Length + 64))
            {
                ms.Write(ASSINATURA, 0, ASSINATURA.Length);
                var ihdr = new byte[13];
                Escrever32(ihdr, 0, largura);
                Escrever32(ihdr, 4, altura);
                ihdr[8] = 8;                               // bits por canal
                ihdr[9] = (byte)(canais == 4 ? 6 : 2);     // RGBA / RGB
                Pedaco(ms, "IHDR", ihdr, ihdr.Length);
                Pedaco(ms, "IDAT", z, z.Length);
                Pedaco(ms, "IEND", ihdr, 0);
                return ms.ToArray();
            }
        }

        static byte[] Comprimir(byte[] dados)
        {
            using (var ms = new MemoryStream(dados.Length / 2 + 64))
            {
                // zlib: cabeçalho (deflate, janela de 32 KB) + deflate + Adler-32
                ms.WriteByte(0x78);
                ms.WriteByte(0x9C);
                using (var d = new DeflateStream(ms, CompressionLevel.Optimal, true))
                    d.Write(dados, 0, dados.Length);
                uint ad = Adler32(dados);
                ms.WriteByte((byte)(ad >> 24));
                ms.WriteByte((byte)(ad >> 16));
                ms.WriteByte((byte)(ad >> 8));
                ms.WriteByte((byte)ad);
                return ms.ToArray();
            }
        }

        static void Pedaco(Stream s, string tipo, byte[] dados, int n)
        {
            var cab = new byte[8];
            Escrever32(cab, 0, n);
            for (int i = 0; i < 4; i++) cab[4 + i] = (byte)tipo[i];
            s.Write(cab, 0, 8);
            if (n > 0) s.Write(dados, 0, n);
            uint c = 0xFFFFFFFFu;
            for (int i = 4; i < 8; i++) c = CRC[(c ^ cab[i]) & 0xFF] ^ (c >> 8);
            for (int i = 0; i < n; i++) c = CRC[(c ^ dados[i]) & 0xFF] ^ (c >> 8);
            c ^= 0xFFFFFFFFu;
            var fim = new byte[4];
            Escrever32(fim, 0, (int)c);
            s.Write(fim, 0, 4);
        }

        // ------------------------------------------------------------ ler
        /// <summary>
        /// Os pixels de um PNG de 8 bits RGB/RGBA sem entrelaçamento (canais = 3
        /// ou 4), ou null se não for desse tipo (ou estiver estragado).
        /// </summary>
        public static byte[] Decodificar(byte[] png, bool deBaixoParaCima, out int largura, out int altura, out int canais)
        {
            largura = altura = canais = 0;
            try
            {
                if (png == null || png.Length < 8 + 25) return null;
                for (int i = 0; i < 8; i++)
                    if (png[i] != ASSINATURA[i]) return null;
                int w = 0, h = 0, c = 0;
                bool cabecalho = false;
                using (var idat = new MemoryStream(png.Length))
                {
                    int p = 8;
                    while (p + 12 <= png.Length)
                    {
                        int n = Ler32(png, p);
                        int ini = p + 8;
                        if (n < 0 || ini + n + 4 > png.Length) return null;
                        if (Tipo(png, p + 4, "IHDR"))
                        {
                            if (n < 13) return null;
                            w = Ler32(png, ini);
                            h = Ler32(png, ini + 4);
                            int bits = png[ini + 8], cor = png[ini + 9];
                            if (bits != 8 || png[ini + 10] != 0 || png[ini + 11] != 0 || png[ini + 12] != 0) return null;
                            if (cor == 2) c = 3;
                            else if (cor == 6) c = 4;
                            else return null;
                            cabecalho = true;
                        }
                        else if (Tipo(png, p + 4, "IDAT")) idat.Write(png, ini, n);
                        else if (Tipo(png, p + 4, "IEND")) break;
                        p = ini + n + 4;   // (pula o CRC)
                    }
                    if (!cabecalho || w <= 0 || h <= 0 || w > 8192 || h > 8192) return null;
                    int linha = w * c;
                    var cru = new byte[(linha + 1) * h];
                    byte[] z = idat.GetBuffer();
                    int tz = (int)idat.Length;
                    if (tz < 3 || (z[0] & 0x0F) != 8) return null;
                    using (var ms = new MemoryStream(z, 2, tz - 2, false))
                    using (var d = new DeflateStream(ms, CompressionMode.Decompress))
                    {
                        int lido = 0;
                        while (lido < cru.Length)
                        {
                            int k = d.Read(cru, lido, cru.Length - lido);
                            if (k <= 0) break;
                            lido += k;
                        }
                        if (lido < cru.Length) return null;
                    }
                    var saida = new byte[linha * h];
                    for (int y = 0; y < h; y++)
                    {
                        int f = cru[y * (linha + 1)];
                        int src = y * (linha + 1) + 1;
                        int dst = (deBaixoParaCima ? h - 1 - y : y) * linha;
                        int ant = y == 0 ? -1 : (deBaixoParaCima ? h - y : y - 1) * linha;
                        if (f > 4) return null;
                        for (int i = 0; i < linha; i++)
                        {
                            int x = cru[src + i];
                            int a = i >= c ? saida[dst + i - c] : 0;
                            int b = ant >= 0 ? saida[ant + i] : 0;
                            switch (f)
                            {
                                case 1: x += a; break;
                                case 2: x += b; break;
                                case 3: x += (a + b) >> 1; break;
                                case 4: x += Paeth(a, b, ant >= 0 && i >= c ? saida[ant + i - c] : 0); break;
                            }
                            saida[dst + i] = (byte)x;
                        }
                    }
                    largura = w;
                    altura = h;
                    canais = c;
                    return saida;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ------------------------------------------------------------ contas
        static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        static uint Adler32(byte[] d)
        {
            uint s1 = 1, s2 = 0;
            int i = 0;
            while (i < d.Length)
            {
                int fim = Math.Min(d.Length, i + 5552);   // (o maior bloco sem estourar os 32 bits)
                for (; i < fim; i++)
                {
                    s1 += d[i];
                    s2 += s1;
                }
                s1 %= 65521;
                s2 %= 65521;
            }
            return (s2 << 16) | s1;
        }

        static void Escrever32(byte[] b, int i, int v)
        {
            b[i] = (byte)(v >> 24);
            b[i + 1] = (byte)(v >> 16);
            b[i + 2] = (byte)(v >> 8);
            b[i + 3] = (byte)v;
        }

        static int Ler32(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

        static bool Tipo(byte[] b, int i, string t) => b[i] == t[0] && b[i + 1] == t[1] && b[i + 2] == t[2] && b[i + 3] == t[3];
    }
}
