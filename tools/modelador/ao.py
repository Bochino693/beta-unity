# -*- coding: utf-8 -*-
"""
SOMBREADO DOS CANTINHOS (oclusão de ambiente) ASSADO NA COR DOS VÉRTICES.

Cada vértice olha para o céu em várias direções (o hemisfério da normal) e
conta quantas o próprio modelo tampa: o queixo embaixo da cabeça, o meio
das pernas, a junta dos dedos, o vão das escamas do dinossauro... ficam mais
escuros, como numa foto. No jogo não custa nada (é só a cor do vértice).

Como: para cada direção d, as superfícies do modelo viram um "mapa de
altura" visto de d (pontinhos espalhados nos triângulos, o mais alto de cada
casinha). O vértice está à vista em d se nada na casinha dele estiver acima
dele. O chão (y = 0, quando o modelo pisa nele) escurece só o que está bem
pertinho dele (sombra de contato).
"""
import math

import numpy as np


def _direcoes(n):
    """n direções espalhadas por igual na esfera (espiral de Fibonacci)."""
    k = np.arange(n) + 0.5
    phi = np.arccos(1.0 - 2.0 * k / n)
    teta = math.pi * (1.0 + 5 ** 0.5) * k
    return np.stack([np.cos(teta) * np.sin(phi), np.cos(phi), np.sin(teta) * np.sin(phi)], axis=1)


def _base(d):
    a = np.array([0.0, 1.0, 0.0]) if abs(d[1]) < 0.9 else np.array([1.0, 0.0, 0.0])
    u = np.cross(a, d)
    u /= np.linalg.norm(u)
    v = np.cross(d, u)
    return u, v


def _amostras(tris, por_m2, minimo=3, semente=7):
    """Pontos espalhados nos triângulos (proporcional à área). tris: (n, 3, 3)."""
    rng = np.random.default_rng(semente)
    A, B, C = tris[:, 0], tris[:, 1], tris[:, 2]
    area = 0.5 * np.linalg.norm(np.cross(B - A, C - A), axis=1)
    k = np.maximum(np.ceil(area * por_m2).astype(np.int64), minimo)
    k = np.minimum(k, 4000)
    idx = np.repeat(np.arange(len(tris)), k)
    r1 = np.sqrt(rng.random(len(idx)))
    r2 = rng.random(len(idx))
    P = A[idx] * (1 - r1)[:, None] + B[idx] * (r1 * (1 - r2))[:, None] + C[idx] * (r1 * r2)[:, None]
    # e os próprios cantos (bordas finas não somem do mapa)
    return np.concatenate([P, tris.reshape(-1, 3)])


def ocultacao(tris, V, N, n_dir=96, res=None, chao=None, alcance_chao=0.22):
    """tris: (n, 3, 3) todas as superfícies que fazem sombra (no mundo);
    V, N: (k, 3) vértices e normais a sombrear. chao: altura do chão (ou None).
    Devolve (k,) de 0 (todo tampado) a 1 (céu aberto)."""
    V = np.asarray(V, float)
    N = np.asarray(N, float)
    lo = tris.reshape(-1, 3).min(axis=0)
    hi = tris.reshape(-1, 3).max(axis=0)
    tam = float(np.max(hi - lo))
    centro = (lo + hi) / 2.0
    if res is None:
        # casinhas de ~2,5 cm nos modelos grandes (máx. 1024), 224 nos pequenos
        res = int(min(1024, max(224, math.ceil(tam * 1.8 / 0.025))))
    # densidade: ~4 pontos por casinha do mapa
    celula = tam * 1.8 / res
    pts = _amostras(tris, por_m2=4.0 / (celula * celula))
    dirs = _direcoes(n_dir)
    vis = np.zeros(len(V))
    peso = np.zeros(len(V))
    for d in dirs:
        c = N @ d
        ok = c > 0.02
        if not ok.any():
            continue
        u, v = _base(d)
        # mapa de altura (o mais alto em cada casinha), visto de d
        pu = (pts - centro) @ u
        pv = (pts - centro) @ v
        ph = pts @ d
        iu = np.clip(((pu / (tam * 1.8)) + 0.5) * res, 0, res - 1).astype(np.int64)
        iv = np.clip(((pv / (tam * 1.8)) + 0.5) * res, 0, res - 1).astype(np.int64)
        mapa = np.full(res * res, -np.inf)
        np.maximum.at(mapa, iu * res + iv, ph)
        Vs = V[ok]
        cs = c[ok]
        qu = np.clip((((Vs - centro) @ u) / (tam * 1.8) + 0.5) * res, 0, res - 1).astype(np.int64)
        qv = np.clip((((Vs - centro) @ v) / (tam * 1.8) + 0.5) * res, 0, res - 1).astype(np.int64)
        h = Vs @ d
        # folga: a própria superfície inclinada dentro da casinha não conta
        sen = np.sqrt(np.maximum(1.0 - cs * cs, 0.0))
        folga = celula * (1.2 + 1.6 * sen / np.maximum(cs, 0.25))
        livre = h + folga >= mapa[qu * res + qv]
        if chao is not None and d[1] < 0:
            # o chão tampa só o que está pertinho dele nessa direção
            t = (Vs[:, 1] - chao) / max(-d[1], 1e-6)
            livre &= t > alcance_chao
        vis[ok] += cs * livre
        peso[ok] += cs
    return np.where(peso > 0, vis / np.maximum(peso, 1e-9), 1.0)


# ------------------------------------------------------------------ no modelo inteiro

# só as peças com luz ganham o sombreado (as "luz" brilham sozinhas; vidro e
# texturas ficam como estão; o chão gigante da fase nem entra na conta)
RECEBEM = ("", "brilho")
NAO_TAMPAM = ("vidro", "chao")


def assar_modelo(modelo, modo="modelo", forca=0.5):
    """Escurece os cantinhos na cor dos vértices do modelo (pose de repouso).
    modo "modelo": as peças fazem sombra umas nas outras (personagem, cenário,
    a pista de cada fase — tudo montado no lugar); "peca": cada peça sozinha
    (pacotes de peças soltas ou alternativas: os pinos com as 8 caras e o
    casco de colisão, as peças da máquina); None: nada."""
    if not modo:
        return
    desl = {}
    for (nome, pai, pos, peca) in modelo.partes:
        base = desl[pai] if pai is not None else np.zeros(3)
        desl[nome] = base + np.asarray(pos, float)
    pecas = []
    for (nome, pai, pos, peca) in modelo.partes:
        if peca is None or not peca.pos or peca.material in NAO_TAMPAM or peca.material.startswith("textura"):
            continue
        P = np.asarray(peca.pos, float) + desl[nome]
        pecas.append((peca, P))
    receptores = [(p, P) for (p, P) in pecas if p.material in RECEBEM]
    if not receptores:
        return
    if modo == "modelo":
        # uma conta só para o modelo inteiro (os mapas de altura servem a todos)
        grupos = [(np.concatenate([P.reshape(-1, 3, 3) for (_, P) in pecas]), receptores)]
    else:
        grupos = [(P.reshape(-1, 3, 3), [(p, P)]) for (p, P) in receptores]
    for (tris, rec) in grupos:
        piso = float(tris.reshape(-1, 3)[:, 1].min())
        chao = 0.0 if abs(piso) < 0.03 else None
        V = np.concatenate([P for (_, P) in rec])
        N = np.concatenate([np.asarray(p.nrm, float) for (p, _) in rec])
        a = ocultacao(tris, V, N, chao=chao)
        k = 0
        for (peca, P) in rec:
            f = 1.0 - forca * (1.0 - a[k:k + len(P)])
            k += len(P)
            C = np.asarray(peca.cor, float)
            C[:, :3] = np.clip(np.round(C[:, :3] * f[:, None]), 0, 255)
            peca.cor = [tuple(int(x) for x in c) for c in C]
