# -*- coding: utf-8 -*-
"""
PRÉVIA DOS MODELOS (sem a Unity): rasterizador em numpy com a MESMA conta de
luz do shader do jogo (Resources/Shaders/Facetado.shader):

    n     = normal no mundo, L = direção PARA o sol, V = direção para a câmera
    amb   = mistura(chao, ceu, n.y * 0.5 + 0.5)
    dif   = faixa(n·L)            (luz "macia" com um degrauzinho de desenho)
    cor   = albedo * (amb + sol * dif) + borda (contraluz) * (1 - n·V)^3
    neblina exponencial pela distância

Tudo em espaço Gamma (como o jogo na OpenGL ES 2.0). Antisserrilhado por
superamostragem.
"""
import math

import numpy as np
from PIL import Image

from malha import aplicar

LUZ_PADRAO = {
    "dir": (0.35, 0.79, 0.5),          # para onde está o sol (normalizado no uso)
    "sol": (1.0, 0.95, 0.86),
    "sol_forca": 0.62,
    "ceu": (0.74, 0.80, 0.92),
    "chao": (0.46, 0.44, 0.48),
    "amb_forca": 0.62,
    "borda": (1.0, 0.97, 0.92),
    "borda_forca": 0.22,
    "neblina": (0.84, 0.92, 1.0),
    "neblina_dens": 0.0,
}


def luz_vertice(P, N, C, cam, luz):
    """P, N: (k, 3) mundo; C: (k, 4) 0-1. Devolve rgb (k, 3) — a conta do shader."""
    L = np.asarray(luz["dir"], float)
    L = L / np.linalg.norm(L)
    ndl = N @ L
    # faixa: transição curta perto do terminador (desenho), mais um pouco de gradiente
    dif = np.clip((ndl + 0.08) / 0.32, 0.0, 1.0)
    dif = dif * dif * (3.0 - 2.0 * dif)
    dif = 0.72 * dif + 0.28 * np.clip(ndl, 0.0, 1.0)
    t = np.clip(N[:, 1] * 0.5 + 0.5, 0.0, 1.0)[:, None]
    amb = (np.asarray(luz["chao"]) * (1 - t) + np.asarray(luz["ceu"]) * t) * luz["amb_forca"]
    sol = np.asarray(luz["sol"]) * luz["sol_forca"]
    V = cam[None, :] - P
    dist = np.linalg.norm(V, axis=1)
    V = V / np.maximum(dist[:, None], 1e-9)
    ndv = np.clip(np.einsum("ij,ij->i", N, V), 0.0, 1.0)
    borda = ((1.0 - ndv) ** 3)[:, None] * np.asarray(luz["borda"]) * luz["borda_forca"]
    rgb = C[:, :3] * (amb + sol[None, :] * dif[:, None]) + borda * (0.35 + 0.65 * np.clip(N[:, 1:2] * 0.5 + 0.5, 0, 1))
    if luz.get("neblina_dens", 0.0) > 0:
        f = np.exp(-luz["neblina_dens"] * dist)[:, None]
        rgb = rgb * f + np.asarray(luz["neblina"]) * (1 - f)
    return np.clip(rgb, 0.0, 1.0)


class Camera:
    def __init__(self, pos, alvo, fov=40.0, cima=(0, 1, 0)):
        self.pos = np.asarray(pos, float)
        self.alvo = np.asarray(alvo, float)
        self.fov = fov
        f = self.alvo - self.pos
        f /= np.linalg.norm(f)
        r = np.cross(cima, f)          # mão esquerda: direita = cima x frente
        r /= np.linalg.norm(r)
        u = np.cross(f, r)
        self.R = np.stack([r, u, f])   # linhas: eixos da câmera


def renderizar(itens, cam, largura=900, altura=900, luz=None, fundo=(0.93, 0.95, 0.98), ss=2, sem_luz=False):
    """itens: [(Peca, M 4x4), ...]. Devolve PIL.Image."""
    luz = dict(LUZ_PADRAO, **(luz or {}))
    W, H = largura * ss, altura * ss
    tri_p, tri_c = [], []
    vidros = []
    for (peca, M) in itens:
        if peca is None or not peca.pos:
            continue
        if peca.material == "vidro":
            vidros.append((peca, M))
            continue
        P = aplicar(M, np.array(peca.pos))
        Nn = np.array(peca.nrm) @ np.linalg.inv(M[:3, :3]).T
        Nn /= np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-12)
        C = np.array(peca.cor, float) / 255.0
        if sem_luz or peca.material == "luz":
            rgb = C[:, :3]
        else:
            rgb = luz_vertice(P, Nn, C, cam.pos, luz)
        tri_p.append(P.reshape(-1, 3, 3))
        tri_c.append(rgb.reshape(-1, 3, 3))
    if not tri_p:
        return Image.new("RGB", (largura, altura))
    TP = np.concatenate(tri_p)
    TC = np.concatenate(tri_c)
    # câmera
    rel = TP - cam.pos
    vs = rel @ cam.R.T                       # (n, 3, 3): x, y, z (z para a frente)
    perto = 0.02
    ok = (vs[:, :, 2] > perto).all(axis=1)
    # só as faces viradas para a câmera
    A, B, Cc = TP[:, 0], TP[:, 1], TP[:, 2]
    nf = np.cross(B - A, Cc - A)
    cen = (A + B + Cc) / 3.0
    ok &= np.einsum("ij,ij->i", nf, cam.pos - cen) > 0
    vs = vs[ok]
    TC = TC[ok]
    th = math.tan(math.radians(cam.fov) / 2.0)
    asp = W / H
    sx = (vs[:, :, 0] / (vs[:, :, 2] * th * asp) * 0.5 + 0.5) * W
    sy = (1.0 - (vs[:, :, 1] / (vs[:, :, 2] * th) * 0.5 + 0.5)) * H
    z = vs[:, :, 2]
    img = np.empty((H, W, 3), np.float32)
    img[:] = np.asarray(fundo, np.float32)
    zb = np.full((H, W), np.inf, np.float32)
    # de perto para longe (menos sobrescrita)
    ordem = np.argsort(z.mean(axis=1))
    for i in ordem:
        x0, x1 = sx[i].min(), sx[i].max()
        y0, y1 = sy[i].min(), sy[i].max()
        if x1 < 0 or y1 < 0 or x0 >= W or y0 >= H:
            continue
        ix0, ix1 = max(int(math.floor(x0)), 0), min(int(math.ceil(x1)), W - 1)
        iy0, iy1 = max(int(math.floor(y0)), 0), min(int(math.ceil(y1)), H - 1)
        if ix1 < ix0 or iy1 < iy0:
            continue
        xs = np.arange(ix0, ix1 + 1) + 0.5
        ys = np.arange(iy0, iy1 + 1) + 0.5
        X, Y = np.meshgrid(xs, ys)
        ax, ay = sx[i, 0], sy[i, 0]
        bx, by = sx[i, 1], sy[i, 1]
        cx, cy = sx[i, 2], sy[i, 2]
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        w0 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / den
        w1 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / den
        w2 = 1.0 - w0 - w1
        m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        if not m.any():
            continue
        # profundidade com correção de perspectiva
        iz = w0 / z[i, 0] + w1 / z[i, 1] + w2 / z[i, 2]
        zz = 1.0 / np.maximum(iz, 1e-9)
        sub = zb[iy0:iy1 + 1, ix0:ix1 + 1]
        m &= zz < sub
        if not m.any():
            continue
        p0 = w0 / z[i, 0] * zz
        p1 = w1 / z[i, 1] * zz
        p2 = w2 / z[i, 2] * zz
        col = p0[..., None] * TC[i, 0] + p1[..., None] * TC[i, 1] + p2[..., None] * TC[i, 2]
        sub[m] = zz[m]
        img[iy0:iy1 + 1, ix0:ix1 + 1][m] = col[m]
    if vidros:
        img = _vidros(img, zb, vidros, cam, luz, W, H)
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    if ss > 1:
        im = im.resize((largura, altura), Image.LANCZOS)
    return im


def itens_do_modelo(modelo, M_raiz=None, poses=None, esconder=()):
    """Achata a hierarquia do Modelo em [(Peca, M mundo)]. poses: nome -> 4x4
    local extra (rotação da junta)."""
    M_raiz = np.eye(4) if M_raiz is None else M_raiz
    poses = poses or {}
    mundo = {}
    itens = []
    for (nome, pai, pos, peca) in modelo.partes:
        L = np.eye(4)
        L[:3, 3] = pos
        if nome in poses:
            L = L @ poses[nome]
        M = (mundo[pai] if pai is not None else M_raiz) @ L
        mundo[nome] = M
        if peca is not None and nome not in esconder:
            itens.append((peca, M))
    return itens


def folha(imagens, colunas, fundo=(255, 255, 255)):
    w, h = imagens[0].size
    linhas = (len(imagens) + colunas - 1) // colunas
    out = Image.new("RGB", (w * colunas, h * linhas), fundo)
    for k, im in enumerate(imagens):
        out.paste(im, ((k % colunas) * w, (k // colunas) * h))
    return out


def volta(itens, alvo, dist, altura, angulos=(0, 45, 90, 180, 270, 315), fov=34, tam=420, luz=None, fundo=(0.93, 0.95, 0.98)):
    """Várias vistas em volta (graus: 0 = de frente, olhando de +Z)."""
    ims = []
    for a in angulos:
        r = math.radians(a)
        pos = (alvo[0] + math.sin(r) * dist, alvo[1] + altura, alvo[2] + math.cos(r) * dist)
        ims.append(renderizar(itens, Camera(pos, alvo, fov), tam, tam, luz, fundo))
    return ims


def _vidros(img, zb, vidros, cam, luz, W, H):
    """Peças de vidro: mistura com alfa (de trás para a frente), sem gravar profundidade."""
    th = math.tan(math.radians(cam.fov) / 2.0)
    asp = W / H
    for (peca, M) in vidros:
        P = aplicar(M, np.array(peca.pos))
        Nn = np.array(peca.nrm) @ np.linalg.inv(M[:3, :3]).T
        Nn /= np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-12)
        C = np.array(peca.cor, float) / 255.0
        rgb = luz_vertice(P, Nn, C, cam.pos, dict(LUZ_PADRAO, **(luz or {})))
        # vidro: brilho na borda (contraluz) mais forte
        V = cam.pos[None, :] - P
        V /= np.maximum(np.linalg.norm(V, axis=1, keepdims=True), 1e-9)
        ndv = np.clip(np.einsum("ij,ij->i", Nn, V), 0, 1)
        alfa = np.clip(C[:, 3] + (1 - ndv) ** 2 * 0.55, 0, 0.9)
        TP = P.reshape(-1, 3, 3)
        TC = rgb.reshape(-1, 3, 3)
        TA = alfa.reshape(-1, 3)
        vs = (TP - cam.pos) @ cam.R.T
        ok = (vs[:, :, 2] > 0.02).all(axis=1)
        A, B, Cc = TP[:, 0], TP[:, 1], TP[:, 2]
        nf = np.cross(B - A, Cc - A)
        ok &= np.einsum("ij,ij->i", nf, cam.pos - (A + B + Cc) / 3.0) > 0
        vs, TC, TA = vs[ok], TC[ok], TA[ok]
        sx = (vs[:, :, 0] / (vs[:, :, 2] * th * asp) * 0.5 + 0.5) * W
        sy = (1.0 - (vs[:, :, 1] / (vs[:, :, 2] * th) * 0.5 + 0.5)) * H
        z = vs[:, :, 2]
        for i in np.argsort(-z.mean(axis=1)):
            x0, x1 = max(int(sx[i].min()), 0), min(int(math.ceil(sx[i].max())), W - 1)
            y0, y1 = max(int(sy[i].min()), 0), min(int(math.ceil(sy[i].max())), H - 1)
            if x1 < x0 or y1 < y0:
                continue
            X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            ax, ay, bx, by, cx, cy = sx[i, 0], sy[i, 0], sx[i, 1], sy[i, 1], sx[i, 2], sy[i, 2]
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-12:
                continue
            w0 = ((by - cy) * (X - cx) + (cx - bx) * (Y - cy)) / den
            w1 = ((cy - ay) * (X - cx) + (ax - cx) * (Y - cy)) / den
            w2 = 1.0 - w0 - w1
            m = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
            zz = 1.0 / np.maximum(w0 / z[i, 0] + w1 / z[i, 1] + w2 / z[i, 2], 1e-9)
            m &= zz < zb[y0:y1 + 1, x0:x1 + 1]
            if not m.any():
                continue
            col = w0[..., None] * TC[i, 0] + w1[..., None] * TC[i, 1] + w2[..., None] * TC[i, 2]
            a = (w0 * TA[i, 0] + w1 * TA[i, 1] + w2 * TA[i, 2])[..., None]
            sub = img[y0:y1 + 1, x0:x1 + 1]
            sub[m] = (sub * (1 - a) + col * a)[m]
    return img
