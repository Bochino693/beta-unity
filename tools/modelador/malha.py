# -*- coding: utf-8 -*-
"""
MODELADOR FACETADO DO STRIKE KIDS (edição Unity)
================================================

Peças de faces retas e quinas vivas (nada de "massinha" arredondada): cada
primitiva é uma malha indexada (V, F) que pode ser deformada antes de virar
triângulos. As normais saem por "suavização por ângulo" (como o Auto Smooth):
superfícies curvas ficam lisas por dentro e as quinas (mais que `suave`
graus) ficam vivas; suave=0 = tudo facetado (pedras, cristais).

Sistema de coordenadas: o da UNITY (mão esquerda, Y para cima). O
personagem olha para +Z; a esquerda DELE fica em -X. Um triângulo (a, b, c)
fica de frente para quem vê a normal cross(b - a, c - a) (horário visto de
frente, como a Unity desenha).

Cores: sRGB 0-255 (o jogo roda em espaço Gamma — o único da OpenGL ES 2.0).
"""
import math
import struct

import numpy as np

# ------------------------------------------------------------------ matrizes


def _rx(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]], dtype=np.float64)


def _ry(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]], dtype=np.float64)


def _rz(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]], dtype=np.float64)


def rot(ax=0.0, ay=0.0, az=0.0):
    """Rotação em graus na ordem da Unity (Quaternion.Euler): Ry · Rx · Rz."""
    return _ry(math.radians(ay)) @ _rx(math.radians(ax)) @ _rz(math.radians(az))


def trs(t=(0, 0, 0), r=(0, 0, 0), s=(1, 1, 1)):
    """Matriz 4x4: escala, depois rotação (graus, ordem da Unity), depois posição."""
    if np.isscalar(s):
        s = (s, s, s)
    M = np.eye(4)
    M[:3, :3] = rot(*r) @ np.diag(s)
    M[:3, 3] = t
    return M


def aplicar(M, V):
    V = np.asarray(V, dtype=np.float64)
    return V @ M[:3, :3].T + M[:3, 3]


def eixo_para(de, para):
    """Rotação 3x3 que leva a direção `de` até `para`."""
    a = np.asarray(de, float) / np.linalg.norm(de)
    b = np.asarray(para, float) / np.linalg.norm(para)
    v = np.cross(a, b)
    c = float(np.dot(a, b))
    if c < -0.999999:
        eixo = np.cross(a, [1, 0, 0])
        if np.linalg.norm(eixo) < 1e-6:
            eixo = np.cross(a, [0, 1, 0])
        eixo /= np.linalg.norm(eixo)
        return 2 * np.outer(eixo, eixo) - np.eye(3)
    k = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + k + k @ k * (1.0 / (1.0 + c))


def cor(h, a=255):
    """'#RRGGBB' -> (r, g, b, a) 0-255."""
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


def misturar(c1, c2, t):
    return tuple(int(round(c1[i] * (1 - t) + c2[i] * t)) for i in range(4))


def mais_escura(c, f):
    return (int(c[0] * f), int(c[1] * f), int(c[2] * f), c[3])


# ------------------------------------------------------------------ malha indexada


class Forma:
    """Malha indexada (V, F) de uma primitiva, antes de virar triângulos.
    `cores_f`: cor por face (opcional: senão a cor da peça)."""

    def __init__(self, V, F, cores_f=None):
        self.V = np.asarray(V, dtype=np.float64).reshape(-1, 3)
        self.F = np.asarray(F, dtype=np.int64).reshape(-1, 3)
        self.cores_f = cores_f

    def copia(self):
        return Forma(self.V.copy(), self.F.copy(), None if self.cores_f is None else list(self.cores_f))

    # --- transformações (devolvem a própria forma)
    def mover(self, t):
        self.V = self.V + np.asarray(t, float)
        return self

    def girar(self, ax=0.0, ay=0.0, az=0.0, centro=(0, 0, 0)):
        c = np.asarray(centro, float)
        self.V = (self.V - c) @ rot(ax, ay, az).T + c
        return self

    def escalar(self, s, centro=(0, 0, 0)):
        if np.isscalar(s):
            s = (s, s, s)
        c = np.asarray(centro, float)
        self.V = (self.V - c) * np.asarray(s, float) + c
        return self

    def matriz(self, M):
        det = np.linalg.det(M[:3, :3])
        self.V = aplicar(M, self.V)
        if det < 0:
            self.F = self.F[:, [0, 2, 1]]
        return self

    def espelhar_x(self):
        self.V = self.V * np.array([-1.0, 1.0, 1.0])
        self.F = self.F[:, [0, 2, 1]]
        return self

    def deformar(self, fn):
        """fn(V) -> V novo (n, 3)."""
        self.V = np.asarray(fn(self.V.copy()), float)
        return self

    def inverter(self):
        self.F = self.F[:, [0, 2, 1]]
        return self

    def pintar(self, fn):
        """fn(centro (3,), normal (3,)) -> cor ou None (fica a da peça)."""
        A, B, C = self.V[self.F[:, 0]], self.V[self.F[:, 1]], self.V[self.F[:, 2]]
        cen = (A + B + C) / 3.0
        n = np.cross(B - A, C - A)
        ln = np.linalg.norm(n, axis=1, keepdims=True)
        n = n / np.maximum(ln, 1e-12)
        atual = self.cores_f if self.cores_f is not None else [None] * len(self.F)
        self.cores_f = [fn(cen[i], n[i]) or atual[i] for i in range(len(self.F))]
        return self


def juntar(*formas):
    V, F, C = [], [], []
    base = 0
    tem_cor = any(f.cores_f is not None for f in formas)
    for f in formas:
        V.append(f.V)
        F.append(f.F + base)
        base += len(f.V)
        if tem_cor:
            C.extend(f.cores_f if f.cores_f is not None else [None] * len(f.F))
    return Forma(np.concatenate(V), np.concatenate(F), C if tem_cor else None)


# ------------------------------------------------------------------ fecho convexo


def fecho_convexo(P, eps=1e-9):
    """Fecho convexo 3D (incremental). P: (n, 3). Devolve Forma com as faces
    viradas para fora."""
    P = np.asarray(P, dtype=np.float64)
    n = len(P)
    escala = max(float(np.ptp(P, axis=0).max()), 1e-9)
    eps = eps * escala
    # tetraedro inicial
    i0 = 0
    d = np.linalg.norm(P - P[i0], axis=1)
    i1 = int(np.argmax(d))
    linha = P[i1] - P[i0]
    d2 = np.linalg.norm(np.cross(P - P[i0], linha), axis=1)
    i2 = int(np.argmax(d2))
    nrm = np.cross(P[i1] - P[i0], P[i2] - P[i0])
    d3 = np.abs((P - P[i0]) @ nrm)
    i3 = int(np.argmax(d3))
    if d3[i3] < eps * escala:
        raise ValueError("pontos coplanares")
    faces = []

    def plano(f):
        a, b, c = P[f[0]], P[f[1]], P[f[2]]
        nn = np.cross(b - a, c - a)
        ln = np.linalg.norm(nn)
        nn = nn / ln if ln > 0 else nn
        return nn, float(np.dot(nn, a))

    centro = (P[i0] + P[i1] + P[i2] + P[i3]) / 4.0
    for f in [(i0, i1, i2), (i0, i3, i1), (i1, i3, i2), (i2, i3, i0)]:
        nn, dd = plano(f)
        if np.dot(nn, centro) - dd > 0:
            f = (f[0], f[2], f[1])
        faces.append(f)
    feitos = {i0, i1, i2, i3}
    for p in range(n):
        if p in feitos:
            continue
        visiveis = []
        for k, f in enumerate(faces):
            nn, dd = plano(f)
            if np.dot(nn, P[p]) - dd > eps:
                visiveis.append(k)
        if not visiveis:
            continue
        arestas = {}
        for k in visiveis:
            f = faces[k]
            for a, b in ((f[0], f[1]), (f[1], f[2]), (f[2], f[0])):
                if (b, a) in arestas:
                    del arestas[(b, a)]
                else:
                    arestas[(a, b)] = True
        faces = [f for k, f in enumerate(faces) if k not in set(visiveis)]
        for (a, b) in arestas:
            faces.append((a, b, p))
        feitos.add(p)
    usados = sorted({i for f in faces for i in f})
    mapa = {v: i for i, v in enumerate(usados)}
    return Forma(P[usados], [[mapa[i] for i in f] for f in faces])


# ------------------------------------------------------------------ primitivas


def caixa(sx, sy, sz, chanfro=0.0, centro=(0, 0, 0)):
    """Caixa (tamanho total) com as quinas chanfradas (cortadas a 45°)."""
    hx, hy, hz = sx / 2.0, sy / 2.0, sz / 2.0
    ch = min(chanfro, hx * 0.9, hy * 0.9, hz * 0.9)
    pts = []
    for x in (-1, 1):
        for y in (-1, 1):
            for z in (-1, 1):
                if ch <= 0:
                    pts.append((x * hx, y * hy, z * hz))
                else:
                    pts.append((x * (hx - ch), y * hy, z * hz))
                    pts.append((x * hx, y * (hy - ch), z * hz))
                    pts.append((x * hx, y * hy, z * (hz - ch)))
    f = fecho_convexo(pts)
    return f.mover(centro)


def caixa_arredondada(sx, sy, sz, raio, segs=2, centro=(0, 0, 0)):
    """Caixa com quinas em poucas facetas (segs) — "arredondada" facetada."""
    hx, hy, hz = sx / 2.0, sy / 2.0, sz / 2.0
    r = min(raio, hx * 0.95, hy * 0.95, hz * 0.95)
    pts = []
    for sxg in (-1, 1):
        for syg in (-1, 1):
            for szg in (-1, 1):
                c = np.array([sxg * (hx - r), syg * (hy - r), szg * (hz - r)])
                for i in range(segs + 1):
                    for j in range(segs + 1):
                        a = math.pi / 2 * i / segs
                        b = math.pi / 2 * j / segs
                        d = np.array([math.cos(a) * math.cos(b) * sxg, math.sin(b) * syg, math.sin(a) * math.cos(b) * szg])
                        pts.append(c + d * r)
    f = fecho_convexo(np.unique(np.round(np.array(pts), 9), axis=0))
    return f.mover(centro)


def prisma(n, raio, altura, chanfro=0.0, raio_topo=None, fase=0.0, centro=(0, 0, 0)):
    """Prisma de n lados em pé (eixo Y), base em y = 0. raio_topo: tronco de cone."""
    rt = raio if raio_topo is None else raio_topo
    pts = []
    for i in range(n):
        a = 2 * math.pi * (i + fase) / n
        c, s = math.cos(a), math.sin(a)
        if chanfro > 0:
            pts.append((c * (raio - chanfro), 0, s * (raio - chanfro)))
            pts.append((c * raio, chanfro, s * raio))
            pts.append((c * rt, altura - chanfro, s * rt))
            pts.append((c * (rt - chanfro), altura, s * (rt - chanfro)))
        else:
            pts.append((c * raio, 0, s * raio))
            pts.append((c * rt, altura, s * rt))
    return fecho_convexo(pts).mover(centro)


def esfera(raio=1.0, gomos=12, aneis=8, centro=(0, 0, 0), escala=(1, 1, 1)):
    """Esfera UV (gomos x aneis). Com suave=0 fica facetada (bola de cristal);
    com suave grande, lisa."""
    V = [(0, raio, 0)]
    for j in range(1, aneis):
        b = math.pi * j / aneis
        y = math.cos(b) * raio
        r = math.sin(b) * raio
        for i in range(gomos):
            a = 2 * math.pi * i / gomos
            V.append((math.cos(a) * r, y, math.sin(a) * r))
    V.append((0, -raio, 0))
    F = []
    for i in range(gomos):
        F.append((0, 1 + (i + 1) % gomos, 1 + i))
    for j in range(aneis - 2):
        for i in range(gomos):
            a = 1 + j * gomos + i
            b = 1 + j * gomos + (i + 1) % gomos
            c = 1 + (j + 1) * gomos + i
            d = 1 + (j + 1) * gomos + (i + 1) % gomos
            F.append((a, b, d))
            F.append((a, d, c))
    ult = len(V) - 1
    base = 1 + (aneis - 2) * gomos
    for i in range(gomos):
        F.append((ult, base + i, base + (i + 1) % gomos))
    f = Forma(V, F)
    f.escalar(escala)
    return f.mover(centro)


def icosfera(raio=1.0, sub=1, centro=(0, 0, 0), escala=(1, 1, 1)):
    t = (1.0 + 5 ** 0.5) / 2.0
    V = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6),
         (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7),
         (9, 8, 1)]
    V = [np.array(v, float) / np.linalg.norm(v) for v in V]
    for _ in range(sub):
        meio = {}
        F2 = []

        def m(a, b):
            k = (min(a, b), max(a, b))
            if k not in meio:
                p = V[a] + V[b]
                V.append(p / np.linalg.norm(p))
                meio[k] = len(V) - 1
            return meio[k]
        for a, b, c in F:
            ab, bc, ca = m(a, b), m(b, c), m(c, a)
            F2 += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        F = F2
    f = Forma(np.array(V) * raio, F)
    # a orientação das faces do icosaedro acima é anti-horária: vira
    f.inverter()
    _orientar_para_fora(f)
    f.escalar(escala)
    return f.mover(centro)


def _orientar_para_fora(f, centro=None):
    """Para formas estreladas em volta de `centro`: cada face com a normal
    apontando para longe do centro."""
    c = f.V.mean(axis=0) if centro is None else np.asarray(centro, float)
    A, B, C = f.V[f.F[:, 0]], f.V[f.F[:, 1]], f.V[f.F[:, 2]]
    n = np.cross(B - A, C - A)
    fora = ((A + B + C) / 3.0 - c)
    trocar = np.einsum("ij,ij->i", n, fora) < 0
    f.F[trocar] = f.F[trocar][:, [0, 2, 1]]
    return f


def torno(perfil, gomos=16, fase=0.0, tampa_baixo=True, tampa_cima=True, fechar=True):
    """Superfície de revolução em volta do eixo Y. perfil: [(raio, y), ...] de
    baixo para cima. Raio 0 nas pontas fecha em bico (sem tampa)."""
    perfil = [(float(r), float(y)) for r, y in perfil]
    V = []
    idx = []
    for (r, y) in perfil:
        if r <= 1e-9:
            idx.append([len(V)] * gomos)
            V.append((0.0, y, 0.0))
        else:
            linha = []
            for i in range(gomos):
                a = 2 * math.pi * (i + fase) / gomos
                linha.append(len(V))
                V.append((math.cos(a) * r, y, math.sin(a) * r))
            idx.append(linha)
    F = []
    for j in range(len(perfil) - 1):
        L0, L1 = idx[j], idx[j + 1]
        for i in range(gomos):
            if not fechar and i == gomos - 1:
                continue
            a, b = L0[i], L0[(i + 1) % gomos]
            c, d = L1[i], L1[(i + 1) % gomos]
            # parede virada para fora (subindo em y, o ângulo cresce no sentido -> normal para fora)
            if a != b:
                F.append((a, c, b))
            if c != d:
                F.append((b, c, d))
    if tampa_baixo and perfil[0][0] > 1e-9:
        cb = len(V)
        V.append((0.0, perfil[0][1], 0.0))
        L = idx[0]
        for i in range(gomos):
            F.append((cb, L[i], L[(i + 1) % gomos]))
    if tampa_cima and perfil[-1][0] > 1e-9:
        ct = len(V)
        V.append((0.0, perfil[-1][1], 0.0))
        L = idx[-1]
        for i in range(gomos):
            F.append((ct, L[(i + 1) % gomos], L[i]))
    f = Forma(V, F)
    # conferência da orientação: a primeira parede com raio > 0 deve apontar para fora
    return f


def tubo(pontos, raios, gomos=8, tampas=True, achatar=(1.0, 1.0), torcao=0.0, cima=(0, 1, 0)):
    """Tubo ao longo de uma linha (pontos), raio em cada ponto (0 = bico).
    achatar: escala da seção (largura, altura)."""
    P = np.asarray(pontos, float)
    n = len(P)
    raios = list(raios) if hasattr(raios, "__len__") else [raios] * n
    T = []
    for i in range(n):
        if i == 0:
            t = P[1] - P[0]
        elif i == n - 1:
            t = P[-1] - P[-2]
        else:
            t = (P[i + 1] - P[i - 1])
        T.append(t / max(np.linalg.norm(t), 1e-12))
    up = np.asarray(cima, float)
    V = []
    aneis = []
    for i in range(n):
        t = T[i]
        x = np.cross(up, t)
        if np.linalg.norm(x) < 1e-6:
            x = np.cross([1, 0, 0], t)
        x /= np.linalg.norm(x)
        y = np.cross(t, x)
        r = raios[i]
        if r <= 1e-9:
            aneis.append([len(V)] * gomos)
            V.append(tuple(P[i]))
            continue
        anel = []
        for k in range(gomos):
            a = 2 * math.pi * k / gomos + torcao * i
            d = x * math.cos(a) * r * achatar[0] + y * math.sin(a) * r * achatar[1]
            anel.append(len(V))
            V.append(tuple(P[i] + d))
        aneis.append(anel)
    F = []
    for i in range(n - 1):
        A, B = aneis[i], aneis[i + 1]
        for k in range(gomos):
            a, b = A[k], A[(k + 1) % gomos]
            c, d = B[k], B[(k + 1) % gomos]
            if a != b:
                F.append((a, b, c))
            if c != d:
                F.append((b, d, c))
    if tampas:
        if raios[0] > 1e-9:
            c0 = len(V)
            V.append(tuple(P[0]))
            A = aneis[0]
            for k in range(gomos):
                F.append((c0, A[(k + 1) % gomos], A[k]))
        if raios[-1] > 1e-9:
            c1 = len(V)
            V.append(tuple(P[-1]))
            B = aneis[-1]
            for k in range(gomos):
                F.append((c1, B[k], B[(k + 1) % gomos]))
    f = Forma(V, F)
    return _orientar_tubo(f, P)


def _orientar_tubo(f, P):
    """Confere a orientação pela primeira face: tem de apontar para longe da linha."""
    if len(f.F) == 0:
        return f
    a, b, c = f.V[f.F[0]]
    n = np.cross(b - a, c - a)
    cen = (a + b + c) / 3.0
    # ponto da linha mais perto
    d = np.linalg.norm(P - cen, axis=1)
    if np.dot(n, cen - P[int(np.argmin(d))]) < 0:
        f.inverter()
    return f


def _area2(poly):
    s = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i]
        x1, y1 = poly[(i + 1) % len(poly)]
        s += x0 * y1 - x1 * y0
    return s


def triangular(poly):
    """Ear clipping de um polígono simples (anti-horário). Devolve índices."""
    idx = list(range(len(poly)))
    if _area2(poly) < 0:
        idx.reverse()
    tri = []
    P = [np.asarray(p, float) for p in poly]

    def dentro(p, a, b, c):
        def s(p1, p2, p3):
            return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])
        d1, d2, d3 = s(p, a, b), s(p, b, c), s(p, c, a)
        neg = (d1 < 0) or (d2 < 0) or (d3 < 0)
        pos = (d1 > 0) or (d2 > 0) or (d3 > 0)
        return not (neg and pos)
    guarda = 0
    while len(idx) > 3 and guarda < 10000:
        guarda += 1
        achou = False
        for k in range(len(idx)):
            i0, i1, i2 = idx[k - 1], idx[k], idx[(k + 1) % len(idx)]
            a, b, c = P[i0], P[i1], P[i2]
            cr = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
            if cr <= 1e-12:
                continue
            ok = True
            for j in idx:
                if j in (i0, i1, i2):
                    continue
                if dentro(P[j], a, b, c):
                    ok = False
                    break
            if ok:
                tri.append((i0, i1, i2))
                idx.pop(k)
                achou = True
                break
        if not achou:
            break
    if len(idx) == 3:
        tri.append(tuple(idx))
    return tri


def _encolher(poly, d):
    """Desloca o contorno (anti-horário) para dentro por d (esquinas em mitra)."""
    poly = [np.asarray(p, float) for p in poly]
    if _area2([tuple(p) for p in poly]) < 0:
        d = -d
    n = len(poly)
    out = []
    for i in range(n):
        p0, p1, p2 = poly[i - 1], poly[i], poly[(i + 1) % n]
        e1 = p1 - p0
        e2 = p2 - p1
        n1 = np.array([-e1[1], e1[0]]) / max(np.linalg.norm(e1), 1e-12)
        n2 = np.array([-e2[1], e2[0]]) / max(np.linalg.norm(e2), 1e-12)
        m = n1 + n2
        lm = np.linalg.norm(m)
        if lm < 1e-9:
            m = n1
            lm = 1.0
        m = m / lm
        cosang = max(float(np.dot(m, n1)), 0.25)
        out.append(p1 + m * d / cosang)
    return [tuple(p) for p in out]


def extrudar(poly, z0, z1, chanfro=0.0):
    """Contorno 2D (x, y) puxado de z0 a z1 (z0 < z1), tampas nas pontas.
    chanfro: as bordas das tampas cortadas a 45° (contorno encolhido)."""
    poly = [(float(x), float(y)) for x, y in poly]
    if _area2(poly) < 0:
        poly = poly[::-1]
    n = len(poly)
    camadas = []
    if chanfro > 0:
        dentro = _encolher(poly, chanfro)
        camadas = [(dentro, z0), (poly, z0 + chanfro), (poly, z1 - chanfro), (dentro, z1)]
    else:
        camadas = [(poly, z0), (poly, z1)]
    V = []
    aneis = []
    for (pl, z) in camadas:
        anel = []
        for (x, y) in pl:
            anel.append(len(V))
            V.append((x, y, z))
        aneis.append(anel)
    F = []
    for j in range(len(aneis) - 1):
        A, B = aneis[j], aneis[j + 1]
        for i in range(n):
            a, b = A[i], A[(i + 1) % n]
            c, d = B[i], B[(i + 1) % n]
            F.append((a, c, b))
            F.append((b, c, d))
    tri = triangular(camadas[0][0])
    for (i0, i1, i2) in tri:
        A = aneis[0]
        F.append((A[i0], A[i1], A[i2]))
    tri = triangular(camadas[-1][0])
    for (i0, i1, i2) in tri:
        B = aneis[-1]
        F.append((B[i0], B[i2], B[i1]))
    f = Forma(V, F)
    # conferência: a tampa de z0 tem de olhar para -z
    a, b, c = f.V[f.F[-1]]
    nn = np.cross(b - a, c - a)
    if nn[2] < 0:
        f.inverter()
    return f


def estrela2d(pontas, r_fora, r_dentro, fase=90.0):
    pts = []
    for i in range(pontas * 2):
        a = math.radians(fase) + math.pi * i / pontas
        r = r_fora if i % 2 == 0 else r_dentro
        pts.append((math.cos(a) * r, math.sin(a) * r))
    return pts


def circulo2d(r, n=16, centro=(0, 0), escala=(1, 1)):
    return [(centro[0] + math.cos(2 * math.pi * i / n) * r * escala[0],
             centro[1] + math.sin(2 * math.pi * i / n) * r * escala[1]) for i in range(n)]


def retangulo2d(w, h, raio=0.0, segs=2, centro=(0, 0)):
    if raio <= 0:
        return [(centro[0] - w / 2, centro[1] - h / 2), (centro[0] + w / 2, centro[1] - h / 2),
                (centro[0] + w / 2, centro[1] + h / 2), (centro[0] - w / 2, centro[1] + h / 2)]
    r = min(raio, w / 2 * 0.99, h / 2 * 0.99)
    pts = []
    cantos = [(w / 2 - r, -h / 2 + r, -90), (w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180)]
    for (cx, cy, a0) in cantos:
        for i in range(segs + 1):
            a = math.radians(a0 + 90.0 * i / segs)
            pts.append((centro[0] + cx + math.cos(a) * r, centro[1] + cy + math.sin(a) * r))
    return pts


def plano(w, d, divx=1, divz=1, centro=(0, 0, 0)):
    """Plano XZ virado para +Y."""
    V = []
    for j in range(divz + 1):
        for i in range(divx + 1):
            V.append((-w / 2 + w * i / divx, 0.0, -d / 2 + d * j / divz))
    F = []
    for j in range(divz):
        for i in range(divx):
            a = j * (divx + 1) + i
            b = a + 1
            c = a + divx + 1
            dd = c + 1
            F.append((a, c, b))
            F.append((b, c, dd))
    return Forma(V, F).mover(centro)


def ruido_pontos(V, amp, semente=1):
    rnd = np.random.RandomState(semente)
    return V + rnd.uniform(-amp, amp, size=V.shape)


def pedra(raio=1.0, pontos=18, achatar=(1.0, 0.7, 1.0), semente=1, irregular=0.25):
    """Pedra facetada: fecho convexo de pontos sorteados numa esfera."""
    rnd = np.random.RandomState(semente)
    P = rnd.normal(size=(pontos, 3))
    P /= np.linalg.norm(P, axis=1, keepdims=True)
    P *= (1.0 + rnd.uniform(-irregular, irregular, size=(pontos, 1)))
    P *= np.asarray(achatar) * raio
    return fecho_convexo(P)


def cristal(raio, altura, lados=6, ponta=0.35, semente=0):
    """Cristal: prisma com ponta de pirâmide."""
    pts = []
    for i in range(lados):
        a = 2 * math.pi * i / lados
        pts.append((math.cos(a) * raio, 0, math.sin(a) * raio))
        pts.append((math.cos(a) * raio, altura * (1 - ponta), math.sin(a) * raio))
    pts.append((0, altura, 0))
    return fecho_convexo(pts)


# ------------------------------------------------------------------ peça (malha final)


class Peca:
    """Uma malha final (um MeshRenderer na Unity): soma de formas, cada uma
    com cor e suavização própria. material: "" (facetado, cor do vértice),
    "brilho" (mesmo, com reflexo), "luz" (sem luz: lâmpadas, neon),
    "textura:<nome>" (textura de Resources/Texturas, UV na malha)."""

    def __init__(self, nome="", material=""):
        self.nome = nome
        self.material = material
        self.pos = []
        self.nrm = []
        self.cor = []
        self.uv = []
        self.tem_uv = False

    def add(self, forma, cor_padrao=(255, 255, 255, 255), suave=0.0, M=None, uv=None):
        """suave: ângulo (graus) até onde as normais se juntam (0 = facetado).
        uv: função (V) -> (n, 2) (opcional)."""
        f = forma.copia()
        if M is not None:
            f.matriz(M)
        V, F = f.V, f.F
        if len(F) == 0:
            return self
        A, B, C = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
        fn = np.cross(B - A, C - A)
        area = np.linalg.norm(fn, axis=1)
        ok = area > 1e-14
        F = F[ok]
        fn = fn[ok] / area[ok][:, None]
        cores_f = f.cores_f
        if cores_f is not None:
            cores_f = [c for c, k in zip(cores_f, ok) if k]
        nf = len(F)
        normais = np.repeat(fn[:, None, :], 3, axis=1)
        if suave > 0:
            limite = math.cos(math.radians(suave))
            # faces de cada vértice
            por_v = [[] for _ in range(len(V))]
            for i in range(nf):
                for k in range(3):
                    por_v[F[i, k]].append(i)
            # ângulo de cada canto (peso)
            for i in range(nf):
                for k in range(3):
                    v = F[i, k]
                    soma = np.zeros(3)
                    for j in por_v[v]:
                        if np.dot(fn[j], fn[i]) >= limite:
                            soma += fn[j] * _angulo_canto(V, F[j], v)
                    ln = np.linalg.norm(soma)
                    if ln > 1e-12:
                        normais[i, k] = soma / ln
        uvs = None
        if uv is not None:
            uvs = np.asarray(uv(V), float)
            self.tem_uv = True
        for i in range(nf):
            c = cor_padrao if (cores_f is None or cores_f[i] is None) else cores_f[i]
            for k in range(3):
                self.pos.append(V[F[i, k]])
                self.nrm.append(normais[i, k])
                self.cor.append(c)
                self.uv.append(uvs[F[i, k]] if uvs is not None else (0.0, 0.0))
        return self

    def triangulos(self):
        return len(self.pos) // 3

    def limites(self):
        P = np.array(self.pos)
        return P.min(axis=0), P.max(axis=0)


def _angulo_canto(V, f, v):
    k = list(f).index(v)
    a = V[f[k]]
    b = V[f[(k + 1) % 3]]
    c = V[f[(k + 2) % 3]]
    u = b - a
    w = c - a
    lu, lw = np.linalg.norm(u), np.linalg.norm(w)
    if lu < 1e-12 or lw < 1e-12:
        return 0.0
    return math.acos(max(-1.0, min(1.0, float(np.dot(u, w) / (lu * lw)))))


# ------------------------------------------------------------------ modelo (várias peças, com hierarquia)


class Modelo:
    """Conjunto de peças com pais e pivôs (um personagem: corpo > cabeça...).
    Cada parte: nome, pai (nome ou None), posição local do pivô, Peca (no
    espaço local do pivô) ou None (só o nó)."""

    def __init__(self, nome):
        self.nome = nome
        self.partes = []   # [nome, pai, pos(3), peca|None]

    def parte(self, nome, pai=None, pos=(0, 0, 0), peca=None):
        self.partes.append([nome, pai, tuple(float(x) for x in pos), peca])
        return peca

    def achar(self, nome):
        for p in self.partes:
            if p[0] == nome:
                return p
        return None


# ------------------------------------------------------------------ gravação (.bytes)
# Formato SKP1 (little-endian), lido por Scripts/Malhas.cs:
#   "SKP1" | u16 partes
#   por parte: str nome | i16 pai | f32x3 pos | str material | u8 flags (1 = uv)
#              u32 nv | f32 escala | i16x3 pos*nv (pos = q/32767*escala)
#              i8x3 normal*nv | u8x4 cor*nv | [f32x2 uv*nv] | u32 ni | u16 indice*ni
#   str = u8 tamanho + utf-8


def _str(s):
    b = s.encode("utf-8")
    return struct.pack("<B", len(b)) + b


def _dados_peca(peca):
    if peca is None or not peca.pos:
        return 0, b""
    P = np.array(peca.pos, dtype=np.float64)
    N = np.array(peca.nrm, dtype=np.float64)
    C = np.array(peca.cor, dtype=np.uint8)
    U = np.array(peca.uv, dtype=np.float64)
    escala = float(np.abs(P).max()) or 1.0
    q = np.round(P / escala * 32767).astype(np.int16)
    nq = np.clip(np.round(N * 127), -127, 127).astype(np.int8)
    chave = np.concatenate([q.astype(np.int64), nq.astype(np.int64), C.astype(np.int64),
                            np.round(U * 100000).astype(np.int64)], axis=1)
    _, primeiro, inverso = np.unique(chave, axis=0, return_index=True, return_inverse=True)
    inverso = inverso.reshape(-1)
    # mantém a ordem de primeira aparição (cache de vértices melhor)
    ordem = np.argsort(primeiro)
    novo = np.empty_like(ordem)
    novo[ordem] = np.arange(len(ordem))
    idx = novo[inverso]
    sel = primeiro[ordem]
    if len(sel) > 65535:
        raise ValueError("peça %s com %d vértices (máx 65535)" % (peca.nome, len(sel)))
    out = struct.pack("<If", len(sel), escala)
    out += q[sel].tobytes() + nq[sel].tobytes() + C[sel].tobytes()
    if peca.tem_uv:
        out += U[sel].astype(np.float32).tobytes()
    out += struct.pack("<I", len(idx)) + idx.astype(np.uint16).tobytes()
    return len(sel), out


def gravar(modelo, caminho):
    nomes = [p[0] for p in modelo.partes]
    out = b"SKP1" + struct.pack("<H", len(modelo.partes))
    total_v = 0
    total_t = 0
    for (nome, pai, pos, peca) in modelo.partes:
        ipai = nomes.index(pai) if pai is not None else -1
        out += _str(nome) + struct.pack("<h", ipai) + struct.pack("<3f", *pos)
        mat = peca.material if peca is not None else ""
        flags = 1 if (peca is not None and peca.tem_uv) else 0
        out += _str(mat) + struct.pack("<B", flags)
        nv, dados = _dados_peca(peca)
        if nv == 0:
            out += struct.pack("<If", 0, 1.0) + struct.pack("<I", 0)
        else:
            out += dados
        total_v += nv
        total_t += peca.triangulos() if peca is not None else 0
    with open(caminho, "wb") as f:
        f.write(out)
    return total_v, total_t


def modelo_simples(nome, peca):
    m = Modelo(nome)
    m.parte(nome, None, (0, 0, 0), peca)
    return m
