package com.lazersport.strikekids;

import android.app.Activity;
import android.app.role.RoleManager;
import android.content.ActivityNotFoundException;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.SharedPreferences;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.provider.Settings;
import android.util.Log;

import java.util.List;

/**
 * O JOGO ABRE SOZINHO AO LIGAR A TV - de qualquer jeito que ela seja ligada
 * (o mesmo da edição Godot: tools/android/StrikeSerial/.../InicioDaTv.java).
 *
 * 1. TELA INICIAL: o jogo é uma tela inicial do Android (StrikeInicioAtivo).
 *    Escolhido como a tela inicial da TV, é ele que o Android abre ao ligar
 *    na tomada, ao voltar para o início e a cada HOME. estado() diz se já é;
 *    escolher() abre a escolha do Android (o papel HOME do Android 10+, os
 *    ajustes da tela inicial ou a janela "Sempre / Só uma vez") - o jogo
 *    pede sozinho na tela de espera enquanto não for (sozinho = true).
 * 2. VOLTA DA ESPERA: desligada pelo CONTROLE, a TV não desliga de verdade
 *    (fica em espera) e nada "liga" de novo - um app de "abrir ao ligar" não
 *    percebe nada. Com o processo do jogo vivo (mesmo fechado ou atrás de
 *    outro app), ouvirTela() percebe a tela apagar e acender: depois de 10 s
 *    ou mais apagada, o jogo reinicia do zero (como ao ligar na tomada) e
 *    vem para a frente, sem a imagem travada da placa de vídeo depois da
 *    espera (StrikeRenasce). Ligada na tomada, StrikeLigou também abre o jogo.
 * 3. SAÍDA DO OPERADOR: sairParaTv() abre a tela inicial da própria TV (o
 *    menu do operador, ou HOME 5 vezes seguidas - pediuParaSair()).
 *
 * O jogo (C#, AndroidPonte.cs) chama estado / escolher / sairParaTv com a
 * atividade; o resto o Android chama sozinho.
 */
public final class InicioDaTv {

	private static final String TAG = "StrikeKids";
	static final int PEDIDO_PAPEL = 7501;
	private static final String PREFS = "strike_inicio";
	private static final int VEZES_PARA_SAIR = 5;
	private static final long JANELA_SAIR_MS = 10000;
	private static final long APAGADA_PARA_REINICIAR_MS = 10000;
	private static final long ESPERA_AO_ACENDER_MS = 1500;

	private static long pedidoEm = 0;
	private static boolean pedidoSozinho = false;
	private static long apagouEm = 0;
	private static BroadcastReceiver tela;

	private InicioDaTv() {
	}

	private static Intent home() {
		return new Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME);
	}

	/** Abre uma tela de outro app; false se a TV não tem essa tela. */
	private static boolean abrir(Context c, Intent i) {
		try {
			i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			c.startActivity(i);
			return true;
		} catch (ActivityNotFoundException e) {
			return false;
		} catch (Throwable t) {
			Log.i(TAG, "inicio: " + t);
			return false;
		}
	}

	/** O jogo, como o lançador o abre (só traz para a frente se já está aberto). */
	static void abrirJogo(Context c) {
		Intent jogo = c.getPackageManager().getLaunchIntentForPackage(c.getPackageName());
		if (jogo != null) {
			jogo.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED);
			c.startActivity(jogo);
		}
	}

	// ------------------------------------------------------------ tela inicial
	/** A tela inicial que o Android abre hoje (null, ou a janela de escolha). */
	private static ResolveInfo padrao(Context c) {
		return c.getPackageManager().resolveActivity(home(), PackageManager.MATCH_DEFAULT_ONLY);
	}

	private static boolean ehCandidata(Context c, ResolveInfo ri) {
		List<ResolveInfo> todas = c.getPackageManager().queryIntentActivities(home(), PackageManager.MATCH_DEFAULT_ONLY);
		for (ResolveInfo r : todas) {
			if (r.activityInfo.packageName.equals(ri.activityInfo.packageName) && r.activityInfo.name.equals(ri.activityInfo.name)) {
				return true;
			}
		}
		return false;
	}

	/** "jogo" (a TV já abre o jogo), "escolher" (o Android ainda pergunta) ou "outra|Nome". */
	public static String estado(Context c) {
		try {
			ResolveInfo ri = padrao(c);
			if (ri == null || ri.activityInfo == null) {
				return "escolher";
			}
			if (ri.activityInfo.packageName.equals(c.getPackageName())) {
				return "jogo";
			}
			// sem uma escolhida, o Android devolve a própria janela de escolha
			if (!ehCandidata(c, ri)) {
				return "escolher";
			}
			return "outra|" + ri.loadLabel(c.getPackageManager());
		} catch (Throwable t) {
			Log.i(TAG, "inicio: estado: " + t);
			return "";
		}
	}

	/**
	 * Abre a escolha da tela inicial: "ja" (já é o jogo), "papel" (a janela do
	 * Android 10+), "ajustes", "escolha" (a janela "Sempre / Só uma vez"),
	 * "app" (a página da tela inicial da TV: "Limpar padrões"), "config" ou ""
	 * (nada abriu). sozinho (o jogo pedindo na tela de espera): só as janelas
	 * de escolha, nunca a página do app nem os ajustes gerais.
	 */
	public static String escolher(Activity a, boolean sozinho) {
		try {
			String agora = estado(a);
			if (agora.equals("jogo")) {
				return "ja";
			}
			pedidoSozinho = sozinho;
			if (Build.VERSION.SDK_INT >= 29 && pedirPapel(a)) {
				return "papel";
			}
			return semPapel(a, agora, sozinho);
		} catch (Throwable t) {
			Log.i(TAG, "inicio: escolher: " + t);
			return "";
		}
	}

	private static boolean pedirPapel(Activity a) {
		try {
			RoleManager rm = a.getSystemService(RoleManager.class);
			if (rm == null || !rm.isRoleAvailable(RoleManager.ROLE_HOME) || rm.isRoleHeld(RoleManager.ROLE_HOME)) {
				return false;
			}
			pedidoEm = SystemClock.elapsedRealtime();
			a.startActivityForResult(rm.createRequestRoleIntent(RoleManager.ROLE_HOME), PEDIDO_PAPEL);
			return true;
		} catch (Throwable t) {
			Log.i(TAG, "inicio: papel: " + t);
			return false;
		}
	}

	/** A janela do papel voltou: se ela nem apareceu (TV sem essa janela), segue pelos ajustes. */
	static void depoisDoPapel(Activity a, int resultado) {
		if (resultado == Activity.RESULT_OK || SystemClock.elapsedRealtime() - pedidoEm > 1500) {
			return;
		}
		String agora = estado(a);
		if (!agora.equals("jogo")) {
			semPapel(a, agora, pedidoSozinho);
		}
	}

	private static String semPapel(Activity a, String agora, boolean sozinho) {
		if (abrir(a, new Intent(Settings.ACTION_HOME_SETTINGS))) {
			return "ajustes";
		}
		if (agora.equals("escolher") && abrir(a, home())) {
			return "escolha";
		}
		if (sozinho) {
			return "";
		}
		ResolveInfo ri = padrao(a);
		if (ri != null && ri.activityInfo != null && !ri.activityInfo.packageName.equals(a.getPackageName())
				&& abrir(a, new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS, Uri.parse("package:" + ri.activityInfo.packageName)))) {
			return "app";
		}
		if (abrir(a, new Intent(Settings.ACTION_SETTINGS))) {
			return "config";
		}
		return "";
	}

	// ------------------------------------------------------------ saída
	/** A tela inicial da própria TV (a de maior prioridade que não é o jogo), ou null. */
	private static ResolveInfo outraTela(Context c) {
		PackageManager pm = c.getPackageManager();
		ResolveInfo melhor = null;
		for (ResolveInfo r : pm.queryIntentActivities(home(), PackageManager.MATCH_DEFAULT_ONLY)) {
			if (r.activityInfo == null || r.activityInfo.packageName.equals(c.getPackageName())) {
				continue;
			}
			// a "FallbackHome" dos Ajustes (só a do começo do boot) tem prioridade -1000
			if (melhor == null || r.priority > melhor.priority
					|| (r.priority == melhor.priority && sistema(r) && !sistema(melhor))) {
				melhor = r;
			}
		}
		return melhor;
	}

	private static boolean sistema(ResolveInfo r) {
		return (r.activityInfo.applicationInfo.flags & ApplicationInfo.FLAG_SYSTEM) != 0;
	}

	/** Abre a tela inicial da própria TV (ou os ajustes): o nome dela, "config" ou "". */
	public static String sairParaTv(Context c) {
		try {
			ResolveInfo outra = outraTela(c);
			if (outra != null) {
				Intent i = home().setClassName(outra.activityInfo.packageName, outra.activityInfo.name)
						.addFlags(Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED);
				if (abrir(c, i)) {
					return outra.loadLabel(c.getPackageManager()).toString();
				}
			}
			return abrir(c, new Intent(Settings.ACTION_SETTINGS)) ? "config" : "";
		} catch (Throwable t) {
			Log.i(TAG, "inicio: sair: " + t);
			return "";
		}
	}

	/** HOME 5 vezes em 10 s (ou o jogo caindo sem parar): true = sair para a tela da TV. */
	static boolean pediuParaSair(Context c) {
		SharedPreferences p = c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
		long agora = SystemClock.elapsedRealtime();
		long desde = p.getLong("desde", 0);
		int vezes = p.getInt("vezes", 0);
		if (agora < desde || agora - desde > JANELA_SAIR_MS) {
			desde = agora;
			vezes = 0;
		}
		vezes++;
		boolean sair = vezes >= VEZES_PARA_SAIR;
		p.edit().putLong("desde", sair ? 0 : desde).putInt("vezes", sair ? 0 : vezes).commit();
		return sair;
	}

	// ------------------------------------------------------------ volta da espera
	/**
	 * Tela apagada 10 s ou mais e acesa de novo = o jogo reinicia do zero.
	 * Fica ouvindo enquanto o processo do jogo viver (mesmo com o jogo
	 * fechado ou atrás de outro app).
	 */
	static void ouvirTela(Activity atividade) {
		if (tela != null) {
			return;
		}
		final Context app = atividade.getApplicationContext();
		tela = new BroadcastReceiver() {
			@Override
			public void onReceive(Context c, Intent i) {
				if (Intent.ACTION_SCREEN_OFF.equals(i.getAction())) {
					apagouEm = SystemClock.elapsedRealtime();
					return;
				}
				long apagada = apagouEm > 0 ? SystemClock.elapsedRealtime() - apagouEm : 0;
				apagouEm = 0;
				if (apagada < APAGADA_PARA_REINICIAR_MS) {
					return;
				}
				Log.i(TAG, "inicio: a TV voltou da espera (" + apagada / 1000 + " s): o jogo reinicia");
				new Handler(Looper.getMainLooper()).postDelayed(new Runnable() {
					@Override
					public void run() {
						StrikeRenasce.reiniciar(app);
					}
				}, ESPERA_AO_ACENDER_MS);
			}
		};
		IntentFilter f = new IntentFilter(Intent.ACTION_SCREEN_OFF);
		f.addAction(Intent.ACTION_SCREEN_ON);
		try {
			app.registerReceiver(tela, f);
		} catch (Throwable t) {
			tela = null;
			Log.i(TAG, "inicio: tela: " + t);
		}
	}
}
