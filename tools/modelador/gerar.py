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

import malha  # noqa: E402
from malha import gravar  # noqa: E402
import objetos  # noqa: E402
import personagens  # noqa: E402

SAIDA = os.path.normpath(os.path.join(AQUI, "..", "..", "Assets", "StrikeKids", "Resources", "Malhas"))
ROUPAS = ["parque", "pirata", "dino", "astronauta"]
# resolução (gomos e anéis a mais nas formas curvas) de cada tipo de modelo:
# os bichinhos aparecem de pertinho (mais), os pinos são 10 (menos)
RESOLUCAO = {"bob": 2.0, "mia": 2.0, "pinos": 1.35, "bola": 1.0, "pista": 1.4}


def resolucao_de(nome):
    for prefixo, r in RESOLUCAO.items():
        if nome == prefixo or nome.startswith(prefixo + "_"):
            return r
    return 1.75


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
            malha.RESOLUCAO = resolucao_de(k)
            m = todas[k]()
            v, tri = gravar(m, os.path.join(SAIDA, k + ".bytes"))
            print("%-22s %6d vértices %6d triângulos  %.1fs" % (k, v, tri, time.time() - t0))


if __name__ == "__main__":
    main(sys.argv[1:])
