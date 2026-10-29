package com.lazersport.strikekids;

import android.content.Intent;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.WindowManager;

import com.unity3d.player.UnityPlayerActivity;

/**
 * A atividade principal do Strike Kids (no lugar da UnityPlayerActivity).
 *
 * Anota TODO aperto e soltura de tecla do Android (código cru do KeyEvent):
 * os botões da placa Zero Delay (como joystick ou, no modo teclado, como
 * teclas), o controle remoto da TV Box (menu do operador) e um teclado de
 * teste. Os sensores, START, ficha e configuração ligados no Arduino vêm pela
 * USB (StrikeSerial.java). O jogo lê a fila crua a cada quadro
 * (Controles.cs, AndroidPonte.cs).
 *
 * Também liga o "abrir ao ligar a TV": a volta da espera reinicia o jogo
 * (InicioDaTv.ouvirTela) e a janela do papel de tela inicial volta aqui.
 *
 * VOLTAR e MENU do controle remoto nunca fecham o jogo: o jogo trata (abre
 * o menu do operador, volta nos menus).
 */
public class StrikeAtividade extends UnityPlayerActivity {
    private static final Object TRAVA = new Object();
    private static final StringBuilder FILA = new StringBuilder();
    private static volatile boolean ativa = false;

    @Override
    protected void onCreate(Bundle estado) {
        super.onCreate(estado);
        ativa = true;
        // máquina de fliperama: a tela nunca apaga
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        // a TV voltando da espera (ligada pelo controle) reinicia o jogo (um erro aqui, numa TV
        // estranha, não pode impedir o jogo de abrir)
        try {
            InicioDaTv.ouvirTela(this);
        } catch (Throwable t) {
            android.util.Log.i("StrikeKids", "inicio: ouvir a tela: " + t);
        }
    }

    @Override
    protected void onActivityResult(int pedido, int resultado, Intent dados) {
        super.onActivityResult(pedido, resultado, dados);
        if (pedido == InicioDaTv.PEDIDO_PAPEL) {
            try {
                InicioDaTv.depoisDoPapel(this, resultado);
            } catch (Throwable ignored) {
            }
        }
    }

    @Override
    protected void onDestroy() {
        ativa = false;
        super.onDestroy();
    }

    @Override
    public boolean dispatchKeyEvent(KeyEvent e) {
        int k = e.getKeyCode();
        int a = e.getAction();
        // volume: do sistema, sem passar pelo jogo
        if (k == KeyEvent.KEYCODE_VOLUME_UP || k == KeyEvent.KEYCODE_VOLUME_DOWN || k == KeyEvent.KEYCODE_VOLUME_MUTE) {
            return super.dispatchKeyEvent(e);
        }
        if ((a == KeyEvent.ACTION_DOWN && e.getRepeatCount() == 0) || a == KeyEvent.ACTION_UP) {
            synchronized (TRAVA) {
                if (FILA.length() < 8000) {
                    FILA.append(k).append(':').append(a == KeyEvent.ACTION_DOWN ? '0' : '1').append(';');
                }
            }
        }
        if (k == KeyEvent.KEYCODE_BACK || k == KeyEvent.KEYCODE_MENU) {
            return true;
        }
        return super.dispatchKeyEvent(e);
    }

    /** "codigo:0;codigo:1;..." (0 = apertou, 1 = soltou) desde a última leitura, ou null. */
    public static String lerTeclas() {
        synchronized (TRAVA) {
            if (FILA.length() == 0) {
                return null;
            }
            String s = FILA.toString();
            FILA.setLength(0);
            return s;
        }
    }

    /** A atividade está rodando (as teclas vêm por ela). */
    public static boolean ativa() {
        return ativa;
    }
}
