# -*- coding: utf-8 -*-
"""
GERA AS MALHAS DO JOGO (Assets/StrikeKids/Resources/Malhas/*.bytes).

    python3 tools/modelador/gerar.py            # tudo
    python3 tools/modelador/gerar.py bob pinos  # só alguns

Personagens (Bob e Mia, a roupa de sempre e as 4 roupinhas), pinos (8
expressões + casco de colisão) e a bola. O cenário de cada fase sai do
gerar_cenario.py; a pista e a máquina do pista.py.
"""
import os
import sys
import time

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)

from malha import gravar  # noqa: E402
import objetos  # noqa: E402
import personagens  # noqa: E402

SAIDA = os.path.normpath(os.path.join(AQUI, "..", "..", "Assets", "StrikeKids", "Resources", "Malhas"))
ROUPAS = ["parque", "pirata", "dino", "astronauta"]


def tarefas():
    t = {}
    for nome, f in (("bob", personagens.bob), ("mia", personagens.mia)):
        t[nome] = (lambda f=f: f(""))
        for r in ROUPAS:
            t[nome + "_" + r] = (lambda f=f, r=r: f(r))
    t["pinos"] = objetos.pinos
    t["bola"] = objetos.bola
    try:
        import pista
        for nome, f in pista.MODELOS.items():
            t[nome] = f
    except ImportError:
        pass
    return t


def main(alvos):
    os.makedirs(SAIDA, exist_ok=True)
    todas = tarefas()
    nomes = alvos or list(todas.keys())
    for n in nomes:
        if n not in todas and not any(k.startswith(n + "_") for k in todas):
            print("não existe:", n)
            continue
        for k in [k for k in todas if k == n or (alvos and k.startswith(n + "_"))]:
            t0 = time.time()
            m = todas[k]()
            v, tri = gravar(m, os.path.join(SAIDA, k + ".bytes"))
            print("%-22s %6d vértices %6d triângulos  %.1fs" % (k, v, tri, time.time() - t0))


if __name__ == "__main__":
    main(sys.argv[1:])
