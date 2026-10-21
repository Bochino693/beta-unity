package com.unity3d.player;

import android.app.Activity;
import android.content.Context;
import android.widget.FrameLayout;

/**
 * SÓ PARA COMPILAR o Java de Assets/Plugins/Android sem a Unity
 * (tools/verificar/compilar_java.sh): o esqueleto da UnityPlayer da Unity
 * 2022.3, só com o que o nosso Java usa. NÃO vai para o APK — lá vale a
 * classe de verdade (unity-classes.jar).
 */
public class UnityPlayer extends FrameLayout {
	/** A atividade da Unity rodando agora. */
	public static Activity currentActivity;

	public UnityPlayer(Context contexto) {
		super(contexto);
	}

	/** Chama um método de um objeto do jogo (nome do GameObject, método, texto). */
	public static void UnitySendMessage(String objeto, String metodo, String texto) {
	}
}
