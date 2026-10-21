package com.unity3d.player;

import android.app.Activity;
import android.os.Bundle;
import android.view.KeyEvent;

/**
 * SÓ PARA COMPILAR o Java de Assets/Plugins/Android sem a Unity
 * (tools/verificar/compilar_java.sh): o esqueleto da UnityPlayerActivity da
 * Unity 2022.3 (a StrikeAtividade herda dela). NÃO vai para o APK.
 */
public class UnityPlayerActivity extends Activity {
	/** O jogo (a vista da Unity), criado no onCreate. */
	protected UnityPlayer mUnityPlayer;

	@Override
	protected void onCreate(Bundle estado) {
		super.onCreate(estado);
	}

	@Override
	protected void onDestroy() {
		super.onDestroy();
	}

	@Override
	public boolean dispatchKeyEvent(KeyEvent e) {
		return super.dispatchKeyEvent(e);
	}
}
