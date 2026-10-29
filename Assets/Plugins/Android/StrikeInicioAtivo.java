package com.lazersport.strikekids;

import android.app.Activity;
import android.os.Bundle;

/**
 * O JOGO ABRE SOZINHO AO LIGAR A TV.
 *
 * Esta atividade invisível é uma TELA INICIAL do Android (filtro HOME no
 * manifesto, StrikeUsb.androidlib). Escolhida como a tela inicial da TV
 * ("Sempre"), é ela que o Android abre ao ligar a TV, ao voltar do modo de
 * espera para o início e a cada HOME do controle: só traz o jogo para a
 * frente (ou abre) e fecha na hora. Se o jogo fechar ou cair, o Android volta
 * para a tela inicial - e o jogo abre de novo. Ao contrário de um aviso de
 * "ligou" (BOOT_COMPLETED), o Android nunca bloqueia a tela inicial de abrir
 * uma tela, em nenhuma versão.
 *
 * HOME apertado 5 vezes seguidas (ou o jogo caindo sem parar) abre a tela
 * inicial da própria TV (InicioDaTv.pediuParaSair): a saída do operador sem
 * o menu. Um HOME de novo volta ao jogo.
 */
public class StrikeInicioAtivo extends Activity {
	@Override
	protected void onCreate(Bundle estado) {
		super.onCreate(estado);
		try {
			if (InicioDaTv.pediuParaSair(this)) {
				InicioDaTv.sairParaTv(this);
			} else {
				InicioDaTv.abrirJogo(this);
			}
		} catch (Throwable ignored) {
		}
		finish();
	}
}
