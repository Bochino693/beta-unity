using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace StrikeKids
{
    /// <summary>
    /// AS MALHAS DO JOGO (Resources/Malhas/*.bytes), feitas pelo modelador
    /// facetado (tools/modelador). Um arquivo é um PACOTE de partes com pai e
    /// pivô (um personagem: corpo &gt; cabeça...; a pista: deck, canaletas...).
    ///
    /// Formato SKP1 (little-endian):
    ///   "SKP1" | u16 partes
    ///   por parte: str nome | i16 pai | f32x3 pos | str material | u8 flags (1 = uv)
    ///              u32 nv | f32 escala | i16x3 pos*nv | i8x3 normal*nv | u8x4 cor*nv
    ///              [f32x2 uv*nv] | u32 ni | u16 indice*ni
    ///   str = u8 tamanho + utf-8
    /// </summary>
    public static class Malhas
    {
        public class Parte
        {
            public string nome;
            public int pai;
            public Vector3 pos;
            public string material;
            public Mesh malha;          // null = só o nó
            public Vector3[] vertices;  // (lâmpadas, colisão) — guardados só quando pedidos
        }

        public class Pacote
        {
            public string nome;
            public List<Parte> partes = new List<Parte>();

            public Parte Achar(string nome)
            {
                foreach (var p in partes) if (p.nome == nome) return p;
                return null;
            }

            public Mesh Malha(string nome)
            {
                var p = Achar(nome);
                return p != null ? p.malha : null;
            }
        }

        static readonly Dictionary<string, Pacote> _cache = new Dictionary<string, Pacote>();

        public static Pacote Ler(string nome, bool guardarVertices = false)
        {
            if (_cache.TryGetValue(nome, out var pc)) return pc;
            var ta = Resources.Load<TextAsset>("Malhas/" + nome);
            if (ta == null)
            {
                Debug.LogWarning("malha que falta: " + nome);
                pc = new Pacote { nome = nome };
                _cache[nome] = pc;
                return pc;
            }
            pc = Decodificar(nome, ta.bytes, guardarVertices);
            Resources.UnloadAsset(ta);
            _cache[nome] = pc;
            return pc;
        }

        /// <summary>Solta o pacote da memória (as roupinhas que saem de cena).</summary>
        public static void Soltar(string nome)
        {
            if (!_cache.TryGetValue(nome, out var pc)) return;
            foreach (var p in pc.partes)
                if (p.malha != null) UnityEngine.Object.Destroy(p.malha);
            _cache.Remove(nome);
        }

        public static bool Existe(string nome) => _cache.ContainsKey(nome) || Resources.Load<TextAsset>("Malhas/" + nome) != null;

        static string Texto(BinaryReader r)
        {
            int n = r.ReadByte();
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }

        public static Pacote Decodificar(string nome, byte[] dados, bool guardarVertices)
        {
            var pc = new Pacote { nome = nome };
            using (var r = new BinaryReader(new MemoryStream(dados)))
            {
                var magia = Encoding.ASCII.GetString(r.ReadBytes(4));
                if (magia != "SKP1") throw new InvalidDataException("não é SKP1: " + nome);
                int np = r.ReadUInt16();
                for (int k = 0; k < np; k++)
                {
                    var p = new Parte
                    {
                        nome = Texto(r),
                        pai = r.ReadInt16(),
                        pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                        material = Texto(r),
                    };
                    int flags = r.ReadByte();
                    int nv = (int)r.ReadUInt32();
                    float escala = r.ReadSingle();
                    if (nv == 0)
                    {
                        r.ReadUInt32();
                        pc.partes.Add(p);
                        continue;
                    }
                    var V = new Vector3[nv];
                    float q = escala / 32767f;
                    for (int i = 0; i < nv; i++)
                        V[i] = new Vector3(r.ReadInt16() * q, r.ReadInt16() * q, r.ReadInt16() * q);
                    var N = new Vector3[nv];
                    for (int i = 0; i < nv; i++)
                        N[i] = new Vector3(r.ReadSByte() / 127f, r.ReadSByte() / 127f, r.ReadSByte() / 127f).normalized;
                    var C = new Color32[nv];
                    for (int i = 0; i < nv; i++)
                        C[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                    Vector2[] U = null;
                    if ((flags & 1) != 0)
                    {
                        U = new Vector2[nv];
                        for (int i = 0; i < nv; i++) U[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    }
                    int ni = (int)r.ReadUInt32();
                    var I = new int[ni];
                    for (int i = 0; i < ni; i++) I[i] = r.ReadUInt16();
                    var m = new Mesh { name = nome + "/" + p.nome };
                    m.vertices = V;
                    m.normals = N;
                    m.colors32 = C;
                    if (U != null) m.uv = U;
                    m.SetTriangles(I, 0, true);
                    m.UploadMeshData(false);   // continua legível (lote estático, colisão)
                    p.malha = m;
                    if (guardarVertices) p.vertices = V;
                    pc.partes.Add(p);
                }
            }
            return pc;
        }

        /// <summary>
        /// Monta o pacote como objetos (a hierarquia com os pivôs). `material`
        /// escolhe o material de cada parte pelo nome do material gravado.
        /// Devolve a raiz; `nos` (opcional) recebe nome -> Transform.
        /// </summary>
        public static GameObject Montar(Pacote pc, Transform pai, Dictionary<string, Transform> nos = null,
            Func<Parte, Material> material = null, ICollection<string> pular = null)
        {
            var raiz = new GameObject(pc.nome);
            raiz.transform.SetParent(pai, false);
            var lista = new Transform[pc.partes.Count];
            for (int i = 0; i < pc.partes.Count; i++)
            {
                var p = pc.partes[i];
                if (pular != null && pular.Contains(p.nome)) continue;
                var go = new GameObject(p.nome);
                Transform t = go.transform;
                Transform paiT = p.pai >= 0 && lista[p.pai] != null ? lista[p.pai] : raiz.transform;
                t.SetParent(paiT, false);
                t.localPosition = p.pos;
                lista[i] = t;
                if (nos != null) nos[p.nome] = t;
                if (p.malha != null)
                {
                    go.AddComponent<MeshFilter>().sharedMesh = p.malha;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = material != null ? material(p) : Materiais.DoPacote(p.material);
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }
            }
            return raiz;
        }

        /// <summary>Um objeto com uma malha só (uma parte do pacote).</summary>
        public static GameObject Objeto(string nome, Mesh malha, Material mat, Transform pai)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            if (malha != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            return go;
        }

        /// <summary>Quadrado (2 triângulos) virado para +Z, com UV 0..1, do tamanho dado.</summary>
        public static Mesh Quadrado(float largura, float altura)
        {
            var m = new Mesh { name = "quadrado" };
            float w = largura / 2f, h = altura / 2f;
            m.vertices = new[] { new Vector3(-w, -h, 0), new Vector3(w, -h, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0) };
            m.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            // de frente para +Z (quem olha de +Z vê o lado da frente)
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Quadrado deitado (virado para +Y) — sombras e adesivos no chão.</summary>
        public static Mesh QuadradoDeitado(float largura, float comprimento)
        {
            var m = new Mesh { name = "deitado" };
            float w = largura / 2f, c = comprimento / 2f;
            m.vertices = new[] { new Vector3(-w, 0, -c), new Vector3(w, 0, -c), new Vector3(w, 0, c), new Vector3(-w, 0, c) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }
    }
}
