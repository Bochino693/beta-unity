using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StrikeKids
{
    /// <summary>
    /// JSON sem dependências (ajustes, recordes, classificação, tabela de
    /// jogadas). Objetos viram Dictionary&lt;string, object&gt;, listas
    /// List&lt;object&gt;, números double, e o resto string/bool/null.
    /// </summary>
    public static class Json
    {
        public static object Ler(string texto)
        {
            if (string.IsNullOrEmpty(texto)) return null;
            int i = 0;
            try
            {
                object v = Valor(texto, ref i);
                return v;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void Espacos(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object Valor(string s, ref int i)
        {
            Espacos(s, ref i);
            if (i >= s.Length) throw new FormatException("fim");
            char c = s[i];
            if (c == '{') return Objeto(s, ref i);
            if (c == '[') return Lista(s, ref i);
            if (c == '"') return Texto(s, ref i);
            if (c == 't' && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return Numero(s, ref i);
        }

        static Dictionary<string, object> Objeto(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Espacos(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (i < s.Length)
            {
                Espacos(s, ref i);
                string chave = Texto(s, ref i);
                Espacos(s, ref i);
                if (s[i] != ':') throw new FormatException(":");
                i++;
                d[chave] = Valor(s, ref i);
                Espacos(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw new FormatException("objeto");
            }
            throw new FormatException("objeto aberto");
        }

        static List<object> Lista(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Espacos(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (i < s.Length)
            {
                l.Add(Valor(s, ref i));
                Espacos(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw new FormatException("lista");
            }
            throw new FormatException("lista aberta");
        }

        static string Texto(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("texto");
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\')
                {
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: sb.Append(e); break;
                    }
                }
                else sb.Append(c);
            }
            throw new FormatException("texto aberto");
        }

        static object Numero(string s, ref int i)
        {
            int ini = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(ini, i - ini), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------ escrita
        public static string Escrever(object v, bool bonito = false)
        {
            var sb = new StringBuilder();
            Escrever(sb, v, bonito, 0);
            return sb.ToString();
        }

        static void Recuo(StringBuilder sb, bool bonito, int n)
        {
            if (!bonito) return;
            sb.Append('\n');
            sb.Append(' ', n * 2);
        }

        static void Escrever(StringBuilder sb, object v, bool bonito, int n)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string t: EscreverTexto(sb, t); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case float f: sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case int k: sb.Append(k.ToString(CultureInfo.InvariantCulture)); break;
                case long k: sb.Append(k.ToString(CultureInfo.InvariantCulture)); break;
                case IDictionary dic:
                    {
                        sb.Append('{');
                        bool primeiro = true;
                        foreach (DictionaryEntry e in dic)
                        {
                            if (!primeiro) sb.Append(',');
                            primeiro = false;
                            Recuo(sb, bonito, n + 1);
                            EscreverTexto(sb, e.Key.ToString());
                            sb.Append(bonito ? ": " : ":");
                            Escrever(sb, e.Value, bonito, n + 1);
                        }
                        if (!primeiro) Recuo(sb, bonito, n);
                        sb.Append('}');
                        break;
                    }
                case IList lst:
                    {
                        sb.Append('[');
                        for (int i = 0; i < lst.Count; i++)
                        {
                            if (i > 0) sb.Append(',');
                            Escrever(sb, lst[i], bonito, n + 1);
                        }
                        sb.Append(']');
                        break;
                    }
                default:
                    if (v is IConvertible conv)
                        sb.Append(Convert.ToDouble(conv, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
                    else
                        EscreverTexto(sb, v.ToString());
                    break;
            }
        }

        static void EscreverTexto(StringBuilder sb, string t)
        {
            sb.Append('"');
            foreach (char c in t)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------ leitura tipada
        public static double Num(object v, double padrao = 0)
        {
            if (v == null) return padrao;
            if (v is double d) return d;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double r)) return r;
            if (v is IConvertible c)
            {
                try { return Convert.ToDouble(c, CultureInfo.InvariantCulture); } catch (Exception) { return padrao; }
            }
            return padrao;
        }

        public static int Int(object v, int padrao = 0) => v == null ? padrao : (int)Math.Round(Num(v, padrao));

        public static bool Bool(object v, bool padrao = false)
        {
            if (v == null) return padrao;
            if (v is bool b) return b;
            if (v is string s) return s == "true" || s == "1";
            return Num(v) != 0;
        }

        public static string Str(object v, string padrao = "")
        {
            if (v == null) return padrao;
            if (v is double d) return d.ToString(CultureInfo.InvariantCulture);
            return v.ToString();
        }

        public static Dictionary<string, object> Dic(object v) => v as Dictionary<string, object>;

        public static List<object> Lst(object v) => v as List<object>;

        public static object Pegar(Dictionary<string, object> d, string chave, object padrao = null)
        {
            if (d != null && d.TryGetValue(chave, out object v)) return v;
            return padrao;
        }
    }
}
