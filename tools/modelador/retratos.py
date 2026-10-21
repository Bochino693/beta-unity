# -*- coding: utf-8 -*-
"""
RETRATOS DO BOB E DA MIA (Assets/StrikeKids/Resources/UI/retrato_*.png),
tirados dos MESMOS modelos facetados do jogo — os cartões, o placar, a
escolha do bichinho e a classificação mostram o bichinho como ele é no 3D.

    python3 tools/modelador/retratos.py

Busto de três quartos, acenando, com a mesma luz do shader do jogo
(previa.py). Fundo transparente: renderiza duas vezes (fundo preto e fundo
branco) e tira a transparência da diferença (as bordas ficam lisas).
"""
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)

import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

from malha import trs  # noqa: E402
from previa import Camera, itens_do_modelo, renderizar  # noqa: E402
import personagens  # noqa: E402

SAIDA = os.path.normpath(os.path.join(AQUI, "..", "..", "Assets", "StrikeKids", "Resources", "UI"))
TAM = 384
# expressões que não aparecem no retrato (fica: olhos abertos e o sorriso)
ESCONDER = ("fechado", "feliz", "boca_aberta", "boca_triste", "boca_o", "boca_reta")
LUZ = {"dir": (-0.45, 0.75, 0.6), "sol_forca": 0.66, "amb_forca": 0.64, "borda_forca": 0.3}


def pose_acenando():
    """Braço direito (+X) erguido para o lado, antebraço para cima (dando tchau);
    cabeça um pouquinho inclinada."""
    return {
        "braco_d": trs(r=(0, 0, 84)),
        "antebraco_d": trs(r=(0, 0, 58)),
        "braco_e": trs(r=(0, 0, -10)),
        "cabeca": trs(r=(4, -6, 5)),
    }


def retrato(modelo):
    esconder = [p[0] for p in modelo.partes if p[0].startswith(ESCONDER)]
    itens = itens_do_modelo(modelo, poses=pose_acenando(), esconder=esconder)
    alvo = np.array([-0.02, 0.76, 0.0])
    ang = np.radians(-16.0)
    dist = 1.78
    pos = alvo + np.array([np.sin(ang) * dist, 0.1, np.cos(ang) * dist])
    cam = Camera(pos, alvo, fov=34)
    preto = renderizar(itens, cam, TAM, TAM, LUZ, fundo=(0, 0, 0), ss=3)
    branco = renderizar(itens, cam, TAM, TAM, LUZ, fundo=(1, 1, 1), ss=3)
    p = np.asarray(preto, np.float32) / 255.0
    b = np.asarray(branco, np.float32) / 255.0
    a = np.clip(1.0 - (b - p).mean(axis=2), 0.0, 1.0)
    cor = np.where(a[..., None] > 1e-3, p / np.maximum(a[..., None], 1e-3), 0.0)
    rgba = np.dstack([np.clip(cor, 0, 1), a])
    return Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA")


def main():
    os.makedirs(SAIDA, exist_ok=True)
    for nome, f in (("bob", personagens.bob), ("mia", personagens.mia)):
        im = retrato(f(""))
        caminho = os.path.join(SAIDA, "retrato_" + nome + ".png")
        im.save(caminho, optimize=True)
        print("ok:", caminho)


if __name__ == "__main__":
    main()
