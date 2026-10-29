package com.lazersport.strikekids;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.os.Bundle;
import android.os.Process;
import android.util.Log;

/**
 * O JOGO REINICIA DO ZERO (a TV voltou da espera: InicioDaTv.ouvirTela).
 *
 * Esta atividade invisível roda num processo à parte (":renasce" no
 * manifesto): aberta pelo jogo, ela fecha o processo dele, abre o jogo de
 * novo (processo novo, imagem nova da placa de vídeo) e some. Abrir a tela
 * antes de fechar o jogo é o que deixa o Android aceitar a volta (o mesmo
 * que o ProcessPhoenix da edição Godot faz).
 */
public class StrikeRenasce extends Activity {
	private static final String PID = "pid";

	/** Fecha o jogo e abre de novo, do zero. */
	static void reiniciar(Context c) {
		try {
			Intent i = new Intent(c, StrikeRenasce.class);
			i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			i.putExtra(PID, Process.myPid());
			c.startActivity(i);
		} catch (Throwable t) {
			Log.i("StrikeKids", "inicio: reiniciar: " + t);
		}
	}

	@Override
	protected void onCreate(Bundle estado) {
		super.onCreate(estado);
		int pid = getIntent().getIntExtra(PID, -1);
		if (pid > 0 && pid != Process.myPid()) {
			Process.killProcess(pid);
		}
		try {
			Intent jogo = getPackageManager().getLaunchIntentForPackage(getPackageName());
			if (jogo != null) {
				jogo.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_CLEAR_TASK);
				startActivity(jogo);
			}
		} catch (Throwable t) {
			Log.i("StrikeKids", "inicio: reabrir: " + t);
		}
		finish();
		Runtime.getRuntime().exit(0);
	}
}
