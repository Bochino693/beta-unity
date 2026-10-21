package com.lazersport.strikekids;

import android.app.Activity;
import android.app.Application;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.ImageFormat;
import android.graphics.SurfaceTexture;
import android.hardware.camera2.CameraAccessException;
import android.hardware.camera2.CameraCaptureSession;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraDevice;
import android.hardware.camera2.CameraManager;
import android.hardware.camera2.CaptureRequest;
import android.hardware.camera2.params.StreamConfigurationMap;
import android.hardware.usb.UsbConstants;
import android.hardware.usb.UsbDevice;
import android.hardware.usb.UsbManager;
import android.media.Image;
import android.media.ImageReader;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.SystemClock;
import android.util.Log;
import android.util.Size;
import android.view.Surface;

import com.unity3d.player.UnityPlayer;

import java.lang.reflect.Constructor;
import java.lang.reflect.InvocationHandler;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.nio.Buffer;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ThreadFactory;

/**
 * STRIKE CAMERA — a câmera da máquina (webcam USB, ex.: Sigma) na TV Box.
 *
 * O MESMO MÉTODO DO JOGO DE SOCO (Super Boxing), três caminhos, nesta ordem:
 *   1. CÂMERA DO SISTEMA pela API clássica (android.hardware.Camera): é por
 *      ela que as TV Box publicam a webcam USB (driver uvcvideo do kernel +
 *      HAL antigo, nível LEGACY). A webcam costuma ser a ÚLTIMA da lista.
 *   2. CAMERA2 (preferindo a câmera EXTERNA): TV Box com HAL novo.
 *   3. UVC DIRETA: a webcam aberta direto pelo USB com a libuvc (biblioteca
 *      AndroidUSBCamera, chamada por reflexão: se ela não estiver no APK,
 *      este caminho só avisa e o resto funciona igual). Para TV Box que não
 *      publicam a webcam no Android. Precisa da permissão USB (pedirUsb):
 *      no primeiro encaixe o Android oferece "Usar por padrão" e a
 *      permissão fica guardada (StrikeUsbAtivo).
 * VIGIA: se um caminho "abre" mas não manda imagem em poucos segundos, ele
 * é fechado e o próximo é tentado (webcam de TV Box que abre e fica muda).
 *
 * EDIÇÃO UNITY: o mesmo plugin da edição Godot, sem nada do Godot — uma
 * classe só com métodos estáticos, chamada pelo C# (CameraAndroid.cs). Nada
 * de sinal: o jogo pergunta (quadros(), estado(), listagens()...).
 *
 * O QUADRO PARA O JOGO: com alguém vendo (ver(true)), cada quadro da câmera
 * vira, nesta linha de execução da câmera, o QUADRADO DO MEIO já no lado da
 * foto (CameraUtil): Y (lado x lado) + U + V ((lado/2)², 4:2:0), a primeira
 * linha em cima. Dois vetores fixos (um sendo montado, outro pronto): nada
 * de lixo a cada quadro. O C# pega o pronto com copiarQuadro(destino, lado)
 * — o destino é memória do próprio jogo embrulhada num ByteBuffer direto (a
 * cópia sai sem criar nada no C#) — ou, na falta disso, com quadroBytes().
 * Sem ninguém vendo, a câmera continua ligada e só conta os quadros.
 *
 * C#:  StrikeCamera.iniciar(atividade); procurar(); listagens(); lista();
 *      abrir("auto", 640, 480, 384); quadros(); copiarQuadro(b, 384);
 *      fechar(); estado(); fonte(); ver(false); pedirUsb(); usbPendente();
 *      relatorio()
 * Tudo também vai para o logcat com a etiqueta "StrikeCamera".
 */
public final class StrikeCamera {

	private static final String TAG = "StrikeCamera";
	private static final long VIGIA_MS = 4500;          // sem quadro neste tempo: próximo caminho
	private static final long VIGIA_UVC_MS = 8000;      // a UVC direta demora mais para começar
	private static final String ACAO_USB = "com.lazersport.strikekids.PERMISSAO_USB_WEBCAM";
	private static final int FLAG_MUTABLE = 0x02000000; // PendingIntent.FLAG_MUTABLE (Android 12+)

	/** O quadro pronto (frente), os contadores e o tamanho. */
	private static final Object trava = new Object();
	/** O quadro sendo montado (trás): uma câmera escreve por vez. */
	private static final Object travaEscrita = new Object();
	private static volatile Activity atividade;
	private static boolean vigiandoAtividade = false;
	private static volatile HandlerThread linha;
	private static volatile Handler alca;
	private static CameraManager gerente;
	private static UsbManager usb;
	// caminho 2: Camera2
	private static volatile CameraDevice dispositivo;
	private static volatile CameraCaptureSession sessao;
	private static volatile ImageReader leitor;
	// caminho 1: câmera do sistema (API clássica)
	@SuppressWarnings("deprecation")
	private static volatile android.hardware.Camera camera1;
	private static volatile SurfaceTexture textura1;
	// caminho 3: UVC direta (libuvc por reflexão)
	private static volatile Object uvc;
	private static volatile Object uvcMonitor;
	private static volatile SurfaceTexture uvcTextura;
	private static volatile Surface uvcSuperficie;
	private static byte[] uvcBruto;

	// o quadro para o jogo (o quadrado da foto: Y + U + V)
	private static byte[] frente = new byte[0];
	private static byte[] tras = new byte[0];
	private static boolean temQuadro = false;
	private static int ladoFrente = 0;
	private static int numeroFrente = 0;
	private static int entregue = 0;
	private static int contador = 0;
	private static long publicadoEm = 0;
	private static int w = 0;
	private static int h = 0;
	private static volatile int ladoSaida = 384;
	// (só com travaEscrita) as tabelas do recorte e os planos da Camera2
	private static CameraUtil.Tabela tabelaY;
	private static CameraUtil.Tabela tabelaUV;
	private static byte[] planoY = new byte[0];
	private static byte[] planoU = new byte[0];
	private static byte[] planoV = new byte[0];
	private static boolean avisouPlano = false;

	private static volatile int quadrosEtapa = 0;
	/** Alguém está vendo a imagem? Sem ninguém vendo, a câmera continua
	 * ligada mas só conta os quadros (sem copiar nada: não pesa na TV Box). */
	private static volatile boolean vendo = true;
	private static volatile String estado = "parada";
	private static volatile String fonte = "";
	private static int pedidoW = 640;
	private static int pedidoH = 480;
	private static int etapa = -1;
	private static volatile int geracao = 0;
	private static long pedidoUsbEm = 0;
	private static final StringBuilder diario = new StringBuilder();
	// a lista de câmeras, feita em segundo plano (numa TV Box fraca leva dezenas de ms)
	private static final ExecutorService listador = Executors.newSingleThreadExecutor(new ThreadFactory() {
		@Override
		public Thread newThread(Runnable r) {
			Thread t = new Thread(r, "StrikeCamera-lista");
			t.setDaemon(true);
			return t;
		}
	});
	private static volatile boolean listando = false;
	private static volatile String lista = "";
	private static volatile int listagens = 0;

	private StrikeCamera() {
	}

	/** O que a câmera está fazendo agora (vira o estado()) — e vai para o diário. */
	private static void anotar(String s) {
		estado = s;
		registrar(s);
	}

	/** Só para o diário e o logcat (o estado() não muda). */
	private static void registrar(String s) {
		try {
			Log.i(TAG, s);
		} catch (Throwable ignored) {
		}
		synchronized (diario) {
			if (diario.length() > 4000) {
				diario.delete(0, diario.length() - 2000);
			}
			diario.append(SystemClock.elapsedRealtime() / 1000).append("s ").append(s).append('\n');
		}
	}

	// ------------------------------------------------------------ atividade
	/**
	 * A atividade do jogo (o C# passa a da Unity; sem ela, vale a
	 * UnityPlayer.currentActivity). Quando a TV Box sai do jogo (pausa), a
	 * câmera é solta para outros apps — o jogo religa ao voltar (vê "parada").
	 */
	public static void iniciar(Activity a) {
		try {
			if (a == null) {
				return;
			}
			atividade = a;
			synchronized (StrikeCamera.class) {
				if (vigiandoAtividade) {
					return;
				}
				vigiandoAtividade = true;
			}
			a.getApplication().registerActivityLifecycleCallbacks(new Application.ActivityLifecycleCallbacks() {
				@Override
				public void onActivityCreated(Activity x, Bundle b) {
				}

				@Override
				public void onActivityStarted(Activity x) {
				}

				@Override
				public void onActivityResumed(Activity x) {
				}

				@Override
				public void onActivityPaused(Activity x) {
					if (x == atividade) {
						fechar();
					}
				}

				@Override
				public void onActivityStopped(Activity x) {
				}

				@Override
				public void onActivitySaveInstanceState(Activity x, Bundle b) {
				}

				@Override
				public void onActivityDestroyed(Activity x) {
					if (x == atividade) {
						fechar();
					}
				}
			});
		} catch (Throwable t) {
			anotar("iniciar: " + t.getMessage());
		}
	}

	private static Activity atividade() {
		Activity a = atividade;
		if (a == null) {
			try {
				a = UnityPlayer.currentActivity;
			} catch (Throwable ignored) {
				a = null;
			}
		}
		return a;
	}

	// ------------------------------------------------------------ consulta
	public static String estado() {
		return estado;
	}

	/** De onde vem a imagem agora: "sistema", "camera2", "uvc" ou "". */
	public static String fonte() {
		return fonte;
	}

	/** A câmera foi solta (fechar(), ou a TV Box pausou o jogo)? O jogo religa (sem criar texto). */
	public static boolean parada() {
		return "parada".equals(estado);
	}

	public static boolean temPermissao() {
		try {
			Context c = atividade();
			if (c == null) {
				return false;
			}
			if (Build.VERSION.SDK_INT < 23) {
				return true;
			}
			return c.checkSelfPermission(android.Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED;
		} catch (Throwable t) {
			return false;
		}
	}

	/**
	 * Uma linha por câmera: "id|lado|api|permissao". api: 1 (sistema),
	 * 2 (Camera2) ou uvc (webcam no USB; permissao 1/0 = USB liberada).
	 * Linhas com # são só avisos. (Pode levar dezenas de ms: o jogo usa
	 * procurar() + listagens() + lista(), em segundo plano.)
	 */
	@SuppressWarnings("deprecation")
	public static String listar() {
		StringBuilder sb = new StringBuilder();
		try {
			int n = android.hardware.Camera.getNumberOfCameras();
			for (int i = 0; i < n; i++) {
				android.hardware.Camera.CameraInfo info = new android.hardware.Camera.CameraInfo();
				android.hardware.Camera.getCameraInfo(i, info);
				String lado = info.facing == android.hardware.Camera.CameraInfo.CAMERA_FACING_FRONT ? "frente" : "tras";
				// a última costuma ser a webcam USB
				if (n > 1 && i == n - 1) {
					lado = "externa";
				}
				sb.append("c1:").append(i).append('|').append(lado).append("|1|1\n");
			}
		} catch (Throwable e) {
			sb.append("#erro camera1: ").append(e.getMessage()).append('\n');
		}
		CameraManager m = gerente();
		if (m != null) {
			try {
				for (String id : m.getCameraIdList()) {
					sb.append(id).append('|').append(lado2(m, id)).append("|2|1\n");
				}
			} catch (Throwable e) {
				sb.append("#erro camera2: ").append(e.getMessage()).append('\n');
			}
		}
		try {
			// a webcam vista direto no USB só serve com a libuvc no APK: sem
			// ela, nada de pedir permissão USB à toa (vira só um aviso)
			boolean libuvc = temLibuvc();
			UsbManager u = usb();
			for (UsbDevice d : webcamsUsb()) {
				sb.append(libuvc ? "uvc:" : "#sem libuvc: uvc:").append(d.getDeviceId()).append("|externa|uvc|")
						.append(u != null && u.hasPermission(d) ? "1" : "0").append('\n');
			}
		} catch (Throwable e) {
			sb.append("#erro usb: ").append(e.getMessage()).append('\n');
		}
		return sb.toString();
	}

	/** Faz a lista numa linha de execução separada (o jogo não para); pronta quando listagens() mudar. */
	public static void procurar() {
		if (listando) {
			return;
		}
		listando = true;
		try {
			listador.execute(new Runnable() {
				@Override
				public void run() {
					String l;
					try {
						l = listar();
					} catch (Throwable t) {
						l = "#erro: " + t.getMessage() + "\n";
					}
					lista = l;
					listagens++;
					listando = false;
				}
			});
		} catch (Throwable t) {
			listando = false;
		}
	}

	/** Quantas listas já ficaram prontas (muda = lista() nova). */
	public static int listagens() {
		return listagens;
	}

	/** A última lista pronta (ver listar()). */
	public static String lista() {
		return lista;
	}

	/**
	 * Liga/desliga a cópia dos quadros. A câmera fica LIGADA o tempo todo
	 * (como no jogo de soco: abrir e fechar a webcam mexe no driver USB);
	 * com ver(false) ela só conta os quadros. ver(true) na hora da prévia e
	 * da foto: o próximo quadro já chega inteiro.
	 */
	public static void ver(boolean sim) {
		boolean antes = vendo;
		vendo = sim;
		if (!sim || !antes) {
			synchronized (trava) {
				// nada de quadro velho esperando a próxima foto
				temQuadro = false;
			}
		}
	}

	public static int quadros() {
		synchronized (trava) {
			return contador;
		}
	}

	/** Tamanho da imagem da câmera (não o da foto). */
	public static int largura() {
		synchronized (trava) {
			return w;
		}
	}

	public static int altura() {
		synchronized (trava) {
			return h;
		}
	}

	// ------------------------------------------------------------ o quadro para o jogo
	/** Bytes do quadro entregue (Y + U + V do quadrado de `lado`). */
	public static int tamanhoQuadro(int lado) {
		return CameraUtil.tamanho(lado);
	}

	/**
	 * Copia o quadro pronto (ainda não entregue) para `destino`: Y, U e V do
	 * quadrado de `lado`, a primeira linha em cima. Volta o número do quadro
	 * (> 0), 0 se não há quadro novo, ou -(bytes necessários) se o destino é
	 * pequeno. Cópia rápida (um memcpy), feita na hora, dentro da chamada.
	 */
	public static int copiarQuadro(ByteBuffer destino, int lado) {
		try {
			if (destino == null) {
				return 0;
			}
			synchronized (trava) {
				if (!temQuadro || numeroFrente == entregue || ladoFrente != lado) {
					return 0;
				}
				int n = CameraUtil.tamanho(lado);
				if (destino.capacity() < n || frente.length < n) {
					return -n;
				}
				// (Buffer: os métodos de ByteBuffer que devolvem ByteBuffer não existem nos Android antigos)
				((Buffer) destino).clear();
				destino.put(frente, 0, n);
				entregue = numeroFrente;
				return numeroFrente;
			}
		} catch (Throwable t) {
			return 0;
		}
	}

	/** Reserva (sem a memória do jogo): uma cópia do quadro pronto, ou null se não há quadro novo. */
	public static byte[] quadroBytes(int lado) {
		try {
			synchronized (trava) {
				if (!temQuadro || numeroFrente == entregue || ladoFrente != lado) {
					return null;
				}
				int n = CameraUtil.tamanho(lado);
				if (frente.length < n) {
					return null;
				}
				byte[] c = Arrays.copyOf(frente, n);
				entregue = numeroFrente;
				return c;
			}
		} catch (Throwable t) {
			return null;
		}
	}

	/**
	 * Embrulha memória do jogo (endereço vindo do C#) num ByteBuffer direto,
	 * para copiarQuadro escrever direto nela. É o mesmo construtor que o JNI
	 * NewDirectByteBuffer usa; se o Android não deixar, volta null (e o jogo
	 * usa outro caminho). O C# confere com marcar()/somar() antes de usar.
	 */
	public static ByteBuffer embrulhar(long endereco, int capacidade) {
		try {
			if (endereco == 0 || capacidade <= 0) {
				return null;
			}
			Class<?> c = Class.forName("java.nio.DirectByteBuffer");
			Constructor<?> k = c.getDeclaredConstructor(long.class, int.class);
			k.setAccessible(true);
			Object b = k.newInstance(endereco, capacidade);
			if (b instanceof ByteBuffer && ((ByteBuffer) b).isDirect() && ((ByteBuffer) b).capacity() >= capacidade) {
				return (ByteBuffer) b;
			}
		} catch (Throwable t) {
			registrar("memoria do jogo sem ByteBuffer: " + t.getClass().getSimpleName());
		}
		return null;
	}

	/** (conferência do buffer) Escreve (i * 7 + 3) nos primeiros n bytes. */
	public static boolean marcar(ByteBuffer b, int n) {
		try {
			for (int i = 0; i < n; i++) {
				b.put(i, (byte) (i * 7 + 3));
			}
			return true;
		} catch (Throwable t) {
			return false;
		}
	}

	/** (conferência do buffer) Soma dos primeiros n bytes (0..255 cada). */
	public static int somar(ByteBuffer b, int n) {
		try {
			int s = 0;
			for (int i = 0; i < n; i++) {
				s += b.get(i) & 0xFF;
			}
			return s;
		} catch (Throwable t) {
			return -1;
		}
	}

	// ------------------------------------------------------------ USB
	private static UsbManager usb() {
		if (usb == null) {
			Activity a = atividade();
			if (a != null) {
				usb = (UsbManager) a.getSystemService(Context.USB_SERVICE);
			}
		}
		return usb;
	}

	private static boolean ehWebcam(UsbDevice d) {
		if (d.getDeviceClass() == UsbConstants.USB_CLASS_VIDEO) {
			return true;
		}
		for (int i = 0; i < d.getInterfaceCount(); i++) {
			if (d.getInterface(i).getInterfaceClass() == UsbConstants.USB_CLASS_VIDEO) {
				return true;
			}
		}
		return false;
	}

	private static List<UsbDevice> webcamsUsb() {
		List<UsbDevice> l = new ArrayList<UsbDevice>();
		UsbManager u = usb();
		if (u == null) {
			return l;
		}
		for (UsbDevice d : u.getDeviceList().values()) {
			if (ehWebcam(d)) {
				l.add(d);
			}
		}
		return l;
	}

	/** A webcam USB (a que já tem permissão primeiro). */
	private static UsbDevice webcamUsb() {
		UsbManager u = usb();
		UsbDevice primeira = null;
		for (UsbDevice d : webcamsUsb()) {
			if (u != null && u.hasPermission(d)) {
				return d;
			}
			if (primeira == null) {
				primeira = d;
			}
		}
		return primeira;
	}

	/** Há webcam no USB ainda sem permissão? (o jogo pede na hora certa) */
	public static boolean usbPendente() {
		try {
			if (!temLibuvc()) {
				return false;
			}
			UsbDevice d = webcamUsb();
			return d != null && !usb().hasPermission(d);
		} catch (Throwable e) {
			return false;
		}
	}

	/**
	 * Pede ao Android a permissão da webcam USB (janela "Permitir que o
	 * Strike Kids acesse..." — marque "Usar por padrão"). Só para a UVC
	 * direta; a câmera do sistema não precisa disto.
	 */
	public static String pedirUsb() {
		final Activity a = atividade();
		if (a == null) {
			return "tela ainda nao disponivel";
		}
		try {
			if (!temLibuvc()) {
				return "sem libuvc neste apk: a permissao usb nao serviria";
			}
			UsbDevice d = webcamUsb();
			if (d == null) {
				return "nenhuma webcam no usb";
			}
			if (usb().hasPermission(d)) {
				return "ok";
			}
			long agora = SystemClock.elapsedRealtime();
			if (agora - pedidoUsbEm < 15000) {
				return "aguardando permissao usb";
			}
			pedidoUsbEm = agora;
			Intent it = new Intent(ACAO_USB);
			it.setPackage(a.getPackageName());
			int flags = PendingIntent.FLAG_UPDATE_CURRENT | (Build.VERSION.SDK_INT >= 31 ? FLAG_MUTABLE : 0);
			PendingIntent pi = PendingIntent.getBroadcast(a, 7300 + d.getDeviceId(), it, flags);
			usb().requestPermission(d, pi);
			anotar("pedindo permissao da webcam usb");
			return "pedido";
		} catch (Throwable e) {
			return "erro: " + e.getMessage();
		}
	}

	// ------------------------------------------------------------ ligar
	/**
	 * Abre a câmera. id: "auto" (sistema > Camera2 > UVC direta, com vigia),
	 * "c1:N" (sistema), um id da Camera2 ou "uvc". lado: o lado da foto
	 * quadrada que o jogo quer receber. Volta logo; o resultado aparece em
	 * estado()/fonte() e quadros() começa a subir.
	 */
	public static boolean abrir(final String id, int largura, int altura, int lado) {
		try {
			fechar();
			if (!temPermissao()) {
				anotar("sem permissao da camera");
				return false;
			}
			pedidoW = largura > 0 ? largura : 640;
			pedidoH = altura > 0 ? altura : 480;
			ladoSaida = lado >= 2 ? lado - (lado % 2) : 384;
			// nada da sessão anterior: a foto nunca sai com um quadro velho
			synchronized (trava) {
				temQuadro = false;
				numeroFrente = 0;
				entregue = 0;
				w = 0;
				h = 0;
				contador = 0;
			}
			HandlerThread lt = new HandlerThread("StrikeCamera");
			lt.start();
			final Handler hd = new Handler(lt.getLooper());
			linha = lt;
			alca = hd;
			final int g = geracao;
			final String qual = id != null ? id : "auto";
			anotar("ligando (" + qual + ")");
			hd.post(new Runnable() {
				@Override
				public void run() {
					try {
						if (g != geracao) {
							return;
						}
						if ("auto".equals(qual)) {
							etapa = -1;
							proxima(g);
						} else if (qual.startsWith("c1:")) {
							abrirSistema(Integer.parseInt(qual.substring(3)), g);
						} else if (qual.startsWith("uvc")) {
							UsbDevice d = webcamUsb();
							if (d != null) {
								abrirUvc(d, g);
							}
						} else {
							abrirCamera2(qual, g);
						}
					} catch (Throwable t) {
						anotar("erro ao ligar: " + t.getMessage());
					}
				}
			});
			return true;
		} catch (Throwable t) {
			anotar("erro ao ligar: " + t.getMessage());
			return false;
		}
	}

	/** Próximo caminho da sequência automática. */
	@SuppressWarnings("deprecation")
	private static void proxima(int g) {
		if (g != geracao) {
			return;
		}
		fecharDispositivos();
		etapa++;
		quadrosEtapa = 0;
		if (etapa == 0) {
			int n = 0;
			try {
				n = android.hardware.Camera.getNumberOfCameras();
			} catch (Throwable ignored) {
			}
			if (n > 0 && abrirSistema(-1, g)) {
				vigiar(g, VIGIA_MS);
				return;
			}
			proxima(g);
		} else if (etapa == 1) {
			String id2 = escolher2();
			if (id2 != null) {
				abrirCamera2(id2, g);
				vigiar(g, VIGIA_MS);
				return;
			}
			proxima(g);
		} else if (etapa == 2) {
			UsbDevice d = webcamUsb();
			if (d == null) {
				anotar("sem imagem: nenhuma camera respondeu e nao ha webcam no usb");
				return;
			}
			UsbManager u = usb();
			if (u == null || !u.hasPermission(d)) {
				anotar("webcam usb sem permissao (pedirUsb)");
				return;
			}
			if (abrirUvc(d, g)) {
				vigiar(g, VIGIA_UVC_MS);
			}
		} else {
			anotar("sem imagem: nenhum caminho mandou quadros");
		}
	}

	/** Se este caminho não mandar quadros a tempo, tenta o próximo. */
	private static void vigiar(final int g, long ms) {
		Handler hd = alca;
		if (hd == null) {
			return;
		}
		hd.postDelayed(new Runnable() {
			@Override
			public void run() {
				try {
					if (g != geracao || quadrosEtapa > 0) {
						return;
					}
					anotar("caminho " + fonte + " abriu mas nao mandou imagem: tentando o proximo");
					proxima(g);
				} catch (Throwable t) {
					anotar("vigia: " + t.getMessage());
				}
			}
		}, ms);
	}

	public static void fechar() {
		try {
			geracao++;
			fecharDispositivos();
			HandlerThread lt = linha;
			linha = null;
			alca = null;
			if (lt != null) {
				lt.quitSafely();
			}
		} catch (Throwable t) {
			Log.w(TAG, "fechar: " + t.getMessage());
		}
		fonte = "";
		estado = "parada";
		synchronized (trava) {
			temQuadro = false;
		}
	}

	@SuppressWarnings("deprecation")
	private static void fecharDispositivos() {
		try {
			if (sessao != null) {
				sessao.close();
			}
		} catch (Throwable ignored) {
		}
		sessao = null;
		try {
			if (dispositivo != null) {
				dispositivo.close();
			}
		} catch (Throwable ignored) {
		}
		dispositivo = null;
		try {
			if (leitor != null) {
				leitor.close();
			}
		} catch (Throwable ignored) {
		}
		leitor = null;
		try {
			if (camera1 != null) {
				camera1.setPreviewCallbackWithBuffer(null);
				camera1.stopPreview();
				camera1.release();
			}
		} catch (Throwable ignored) {
		}
		camera1 = null;
		try {
			if (textura1 != null) {
				textura1.release();
			}
		} catch (Throwable ignored) {
		}
		textura1 = null;
		fecharUvc();
	}

	// ------------------------------------------------- 1. câmera do sistema
	/** indice -1: da última para a primeira (a webcam USB costuma ser a última). */
	@SuppressWarnings("deprecation")
	private static boolean abrirSistema(int indice, int g) {
		if (g != geracao) {
			return false;
		}
		android.hardware.Camera cam = null;
		String erro = "";
		try {
			int n = android.hardware.Camera.getNumberOfCameras();
			if (indice >= 0) {
				cam = android.hardware.Camera.open(indice);
			} else {
				for (int i = n - 1; i >= 0 && cam == null; i--) {
					try {
						cam = android.hardware.Camera.open(i);
					} catch (Throwable t) {
						erro = t.getMessage();
					}
				}
			}
		} catch (Throwable t) {
			erro = t.getMessage();
		}
		if (cam == null) {
			anotar("camera do sistema nao abriu: " + erro);
			return false;
		}
		try {
			android.hardware.Camera.Parameters prm = cam.getParameters();
			android.hardware.Camera.Size melhor = null;
			long nota = Long.MAX_VALUE;
			List<android.hardware.Camera.Size> tams = prm.getSupportedPreviewSizes();
			if (tams != null) {
				for (android.hardware.Camera.Size s : tams) {
					long nn = Math.abs((long) s.width * s.height - (long) pedidoW * pedidoH);
					if (nn < nota) {
						nota = nn;
						melhor = s;
					}
				}
			}
			if (melhor != null) {
				prm.setPreviewSize(melhor.width, melhor.height);
			}
			prm.setPreviewFormat(ImageFormat.NV21);
			try {
				cam.setParameters(prm);
			} catch (Throwable ignored) {
				// alguns drivers recusam: segue com o que a câmera já usa
			}
			final android.hardware.Camera.Size usado = cam.getParameters().getPreviewSize();
			final int W = usado.width;
			final int H = usado.height;
			textura1 = new SurfaceTexture(10);
			cam.setPreviewTexture(textura1);
			int bytes = W * H * 3 / 2;
			cam.addCallbackBuffer(new byte[bytes]);
			cam.addCallbackBuffer(new byte[bytes]);
			cam.setPreviewCallbackWithBuffer(new android.hardware.Camera.PreviewCallback() {
				@Override
				public void onPreviewFrame(byte[] dados, android.hardware.Camera c) {
					try {
						guardarNv21(dados, W, H);
					} catch (Throwable ignored) {
					}
					try {
						c.addCallbackBuffer(dados);
					} catch (Throwable ignored) {
					}
				}
			});
			cam.setErrorCallback(new android.hardware.Camera.ErrorCallback() {
				@Override
				public void onError(int erro, android.hardware.Camera c) {
					anotar("camera do sistema caiu (erro " + erro + ")");
				}
			});
			cam.startPreview();
			camera1 = cam;
			fonte = "sistema";
			anotar("camera do sistema aberta " + W + "x" + H);
			return true;
		} catch (Throwable t) {
			anotar("camera do sistema falhou: " + t.getMessage());
			try {
				cam.release();
			} catch (Throwable ignored) {
			}
			return false;
		}
	}

	// ------------------------------------------------------------ 2. Camera2
	private static CameraManager gerente() {
		if (gerente == null) {
			Activity a = atividade();
			if (a != null) {
				gerente = (CameraManager) a.getSystemService(Context.CAMERA_SERVICE);
			}
		}
		return gerente;
	}

	private static String lado2(CameraManager m, String id) {
		try {
			Integer f = m.getCameraCharacteristics(id).get(CameraCharacteristics.LENS_FACING);
			if (f == null) {
				return "externa";
			}
			if (f == CameraCharacteristics.LENS_FACING_FRONT) {
				return "frente";
			}
			if (f == CameraCharacteristics.LENS_FACING_BACK) {
				return "tras";
			}
			return "externa";
		} catch (Throwable e) {
			return "externa";
		}
	}

	/** A webcam USB (externa) primeiro; senão a última que houver. */
	private static String escolher2() {
		CameraManager m = gerente();
		if (m == null) {
			return null;
		}
		try {
			String[] ids = m.getCameraIdList();
			for (String id : ids) {
				if ("externa".equals(lado2(m, id))) {
					return id;
				}
			}
			return ids.length > 0 ? ids[ids.length - 1] : null;
		} catch (Throwable e) {
			return null;
		}
	}

	private static Size tamanho2(String id) {
		try {
			StreamConfigurationMap mapa = gerente().getCameraCharacteristics(id)
					.get(CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP);
			Size[] tams = mapa != null ? mapa.getOutputSizes(ImageFormat.YUV_420_888) : null;
			if (tams == null || tams.length == 0) {
				return new Size(pedidoW, pedidoH);
			}
			Size melhor = tams[0];
			long nota = Long.MAX_VALUE;
			for (Size s : tams) {
				long n = Math.abs((long) s.getWidth() * s.getHeight() - (long) pedidoW * pedidoH);
				if (n < nota) {
					nota = n;
					melhor = s;
				}
			}
			return melhor;
		} catch (Throwable e) {
			return new Size(pedidoW, pedidoH);
		}
	}

	@SuppressWarnings("MissingPermission")
	private static void abrirCamera2(final String id, final int g) {
		CameraManager m = gerente();
		if (m == null) {
			proxima(g);
			return;
		}
		final Size tam = tamanho2(id);
		fonte = "camera2";
		try {
			m.openCamera(id, new CameraDevice.StateCallback() {
				@Override
				public void onOpened(CameraDevice d) {
					if (g != geracao) {
						d.close();
						return;
					}
					dispositivo = d;
					iniciarSessao(d, tam, g);
				}

				@Override
				public void onDisconnected(CameraDevice d) {
					d.close();
					if (dispositivo == d) {
						dispositivo = null;
					}
					anotar("camera2 desconectada");
				}

				@Override
				public void onError(CameraDevice d, int erro) {
					d.close();
					if (dispositivo == d) {
						dispositivo = null;
					}
					anotar("erro camera2 " + erro);
					proxima(g);
				}
			}, alca);
		} catch (Throwable e) {
			anotar("erro camera2: " + e.getMessage());
			proxima(g);
		}
	}

	private static void iniciarSessao(final CameraDevice d, Size tam, final int g) {
		try {
			final ImageReader r0 = ImageReader.newInstance(tam.getWidth(), tam.getHeight(), ImageFormat.YUV_420_888, 3);
			leitor = r0;
			r0.setOnImageAvailableListener(new ImageReader.OnImageAvailableListener() {
				@Override
				public void onImageAvailable(ImageReader r) {
					Image img = null;
					try {
						img = r.acquireLatestImage();
						if (img != null) {
							guardarYuv(img);
						}
					} catch (Throwable ignored) {
					} finally {
						if (img != null) {
							try {
								img.close();
							} catch (Throwable ignored) {
							}
						}
					}
				}
			}, alca);
			List<Surface> alvos = new ArrayList<Surface>(Arrays.asList(r0.getSurface()));
			d.createCaptureSession(alvos, new CameraCaptureSession.StateCallback() {
				@Override
				public void onConfigured(CameraCaptureSession s) {
					if (g != geracao) {
						s.close();
						return;
					}
					sessao = s;
					try {
						CaptureRequest.Builder b = d.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);
						b.addTarget(r0.getSurface());
						s.setRepeatingRequest(b.build(), null, alca);
						anotar("camera2 aberta");
					} catch (CameraAccessException e) {
						anotar("erro pedido camera2: " + e.getMessage());
						proxima(g);
					} catch (Throwable e) {
						anotar("erro pedido camera2: " + e.getMessage());
						proxima(g);
					}
				}

				@Override
				public void onConfigureFailed(CameraCaptureSession s) {
					anotar("erro sessao camera2");
					proxima(g);
				}
			}, alca);
		} catch (Throwable e) {
			anotar("erro sessao camera2: " + e.getMessage());
			proxima(g);
		}
	}

	/** Copia um plano da Camera2 para um vetor reaproveitado; volta o vetor (o tamanho útil fica em `usado[0]`). */
	private static byte[] copiarPlano(ByteBuffer b, byte[] dst, int[] usado) {
		int n = b.remaining();
		if (dst.length < n) {
			dst = new byte[n];
		}
		b.get(dst, 0, n);
		usado[0] = n;
		return dst;
	}

	private static final int[] usadoY = new int[1];
	private static final int[] usadoU = new int[1];
	private static final int[] usadoV = new int[1];

	/** Camera2 (YUV_420_888): os três planos, com o passo que o aparelho der, viram o quadrado da foto. */
	private static void guardarYuv(Image img) {
		int W = img.getWidth();
		int H = img.getHeight();
		if (W < 2 || H < 2) {
			return;
		}
		if (!vendo || podePular()) {
			contar(W, H);
			return;
		}
		Image.Plane[] p = img.getPlanes();
		if (p == null || p.length < 3) {
			return;
		}
		synchronized (travaEscrita) {
			int lado = ladoSaida;
			int l2 = lado / 2;
			prepararTabelas(W, H, lado);
			planoY = copiarPlano(p[0].getBuffer(), planoY, usadoY);
			planoU = copiarPlano(p[1].getBuffer(), planoU, usadoU);
			planoV = copiarPlano(p[2].getBuffer(), planoV, usadoV);
			int rsY = p[0].getRowStride();
			int psY = Math.max(1, p[0].getPixelStride());
			int rsU = p[1].getRowStride();
			int psU = Math.max(1, p[1].getPixelStride());
			int rsV = p[2].getRowStride();
			int psV = Math.max(1, p[2].getPixelStride());
			byte[] t = tras(CameraUtil.tamanho(lado));
			reamostrarPlano(planoY, usadoY[0], rsY, psY, tabelaY, t, 0, 16);
			reamostrarPlano(planoU, usadoU[0], rsU, psU, tabelaUV, t, lado * lado, 128);
			reamostrarPlano(planoV, usadoV[0], rsV, psV, tabelaUV, t, lado * lado + l2 * l2, 128);
			publicar(W, H, lado);
		}
	}

	private static void reamostrarPlano(byte[] src, int n, int passoLinha, int passoPonto, CameraUtil.Tabela t, byte[] dst,
			int dstInicio, int fora) {
		if (CameraUtil.cabe(n, 0, passoLinha, passoPonto, t)) {
			CameraUtil.reamostrar(src, 0, passoLinha, passoPonto, t, dst, dstInicio);
		} else {
			if (!avisouPlano) {
				avisouPlano = true;
				registrar("plano da camera menor que o esperado (" + n + " bytes, passo " + passoLinha + "/" + passoPonto + ")");
			}
			CameraUtil.reamostrarSeguro(src, n, 0, Math.max(1, passoLinha), passoPonto, t, dst, dstInicio, fora);
		}
	}

	// --------------------------------------------------- 3. UVC direta
	/** A libuvc (AndroidUSBCamera) está dentro deste APK? */
	private static boolean temLibuvc() {
		try {
			Class.forName("com.serenegiant.usb.UVCCamera");
			return true;
		} catch (Throwable e) {
			return false;
		}
	}

	/**
	 * Abre a webcam direto pelo USB com a libuvc, SEM registrar o USBMonitor
	 * (o registro dele quebra no Android 12+): a permissão USB é deste
	 * plugin, e a libuvc é chamada por reflexão.
	 */
	private static boolean abrirUvc(UsbDevice device, final int g) {
		if (g != geracao) {
			return false;
		}
		if (!temLibuvc()) {
			anotar("uvc direta indisponivel neste apk (libuvc ausente)");
			return false;
		}
		Activity host = atividade();
		if (host == null) {
			return false;
		}
		fonte = "uvc";
		try {
			Class<?> cUvc = Class.forName("com.serenegiant.usb.UVCCamera");
			Class<?> cMonitor = Class.forName("com.serenegiant.usb.USBMonitor");
			Class<?> cOuvinte = Class.forName("com.serenegiant.usb.USBMonitor$OnDeviceConnectListener");
			Class<?> cBloco = Class.forName("com.serenegiant.usb.USBMonitor$UsbControlBlock");
			Object ouvinte = Proxy.newProxyInstance(cOuvinte.getClassLoader(), new Class<?>[] { cOuvinte },
					new InvocationHandler() {
						@Override
						public Object invoke(Object proxy, Method metodo, Object[] args) {
							return null;
						}
					});
			Object monitor = cMonitor.getConstructor(Context.class, cOuvinte).newInstance(host.getApplicationContext(), ouvinte);
			uvcMonitor = monitor;
			Constructor<?> ctorBloco = cBloco.getDeclaredConstructor(cMonitor, UsbDevice.class);
			ctorBloco.setAccessible(true);
			Object bloco = ctorBloco.newInstance(monitor, device);
			Object camera = cUvc.getDeclaredConstructor().newInstance();
			cUvc.getMethod("open", cBloco).invoke(camera, bloco);
			uvc = camera;
			Method setSize = cUvc.getMethod("setPreviewSize", int.class, int.class, int.class);
			int mjpeg = campo(cUvc, "FRAME_FORMAT_MJPEG", 1);
			int yuyv = campo(cUvc, "FRAME_FORMAT_YUYV", 0);
			int[][] tentativas = { { 640, 480, mjpeg }, { 640, 480, yuyv }, { 1280, 720, mjpeg }, { 320, 240, yuyv } };
			int largura = 0;
			int altura = 0;
			String ultimo = "";
			for (int[] t : tentativas) {
				try {
					setSize.invoke(camera, t[0], t[1], t[2]);
					largura = t[0];
					altura = t[1];
					break;
				} catch (Throwable e) {
					Throwable c = e instanceof InvocationTargetException ? ((InvocationTargetException) e).getTargetException() : e;
					ultimo = String.valueOf(c.getMessage());
				}
			}
			if (largura == 0) {
				throw new IllegalStateException("a webcam recusou os tamanhos: " + ultimo);
			}
			// superfície "de mentira" para o pipeline nativo andar
			uvcTextura = new SurfaceTexture(43);
			uvcTextura.setDefaultBufferSize(largura, altura);
			uvcSuperficie = new Surface(uvcTextura);
			try {
				cUvc.getMethod("setPreviewDisplay", Surface.class).invoke(camera, uvcSuperficie);
			} catch (NoSuchMethodException e) {
				cUvc.getMethod("setPreviewTexture", SurfaceTexture.class).invoke(camera, uvcTextura);
			}
			Class<?> cQuadro = Class.forName("com.serenegiant.usb.IFrameCallback");
			int nv21 = campo(cUvc, "PIXEL_FORMAT_NV21", 5);
			final int W = largura;
			final int H = altura;
			Object retorno = Proxy.newProxyInstance(cQuadro.getClassLoader(), new Class<?>[] { cQuadro },
					new InvocationHandler() {
						@Override
						public Object invoke(Object proxy, Method metodo, Object[] args) {
							try {
								if ("onFrame".equals(metodo.getName()) && args != null && args.length > 0 && args[0] instanceof ByteBuffer) {
									ByteBuffer buf = (ByteBuffer) args[0];
									int precisa = W * H * 3 / 2;
									if (!vendo) {
										contar(W, H);
									} else if (buf.remaining() >= precisa) {
										if (uvcBruto == null || uvcBruto.length != precisa) {
											uvcBruto = new byte[precisa];
										}
										buf.get(uvcBruto, 0, precisa);
										guardarNv21(uvcBruto, W, H);
									}
								}
							} catch (Throwable ignored) {
							}
							return null;
						}
					});
			cUvc.getMethod("setFrameCallback", cQuadro, int.class).invoke(camera, retorno, nv21);
			cUvc.getMethod("startPreview").invoke(camera);
			anotar("webcam usb aberta direto (uvc " + W + "x" + H + ")");
			return true;
		} catch (Throwable t) {
			Throwable c = t instanceof InvocationTargetException ? ((InvocationTargetException) t).getTargetException() : t;
			anotar("uvc direta falhou: " + c.getClass().getSimpleName() + " " + c.getMessage());
			fecharUvc();
			return false;
		}
	}

	private static int campo(Class<?> c, String nome, int padrao) {
		try {
			return c.getField(nome).getInt(null);
		} catch (Throwable e) {
			return padrao;
		}
	}

	private static void fecharUvc() {
		Object camera = uvc;
		uvc = null;
		if (camera != null) {
			try {
				camera.getClass().getMethod("stopPreview").invoke(camera);
			} catch (Throwable ignored) {
			}
			try {
				camera.getClass().getMethod("destroy").invoke(camera);
			} catch (Throwable ignored) {
			}
		}
		uvcMonitor = null;
		try {
			if (uvcSuperficie != null) {
				uvcSuperficie.release();
			}
		} catch (Throwable ignored) {
		}
		try {
			if (uvcTextura != null) {
				uvcTextura.release();
			}
		} catch (Throwable ignored) {
		}
		uvcSuperficie = null;
		uvcTextura = null;
	}

	// ------------------------------------------------------------ quadros
	/** (com travaEscrita) As tabelas do recorte para este tamanho de câmera. */
	private static void prepararTabelas(int W, int H, int lado) {
		if (tabelaY == null || !tabelaY.serve(W, H, lado)) {
			tabelaY = new CameraUtil.Tabela(W, H, lado);
		}
		if (tabelaUV == null || !tabelaUV.serve(W / 2, H / 2, lado / 2)) {
			tabelaUV = new CameraUtil.Tabela(W / 2, H / 2, lado / 2);
		}
	}

	/** (com travaEscrita) O vetor de trás, com pelo menos n bytes. */
	private static byte[] tras(int n) {
		if (tras.length < n) {
			tras = new byte[n];
		}
		return tras;
	}

	/** NV21 (câmera do sistema e UVC): Y inteiro e depois V,U intercalados. */
	private static void guardarNv21(byte[] d, int W, int H) {
		if (d == null || W < 2 || H < 2 || d.length < W * H * 3 / 2) {
			return;
		}
		if (!vendo || podePular()) {
			contar(W, H);
			return;
		}
		synchronized (travaEscrita) {
			int lado = ladoSaida;
			prepararTabelas(W, H, lado);
			byte[] t = tras(CameraUtil.tamanho(lado));
			CameraUtil.nv21(d, W, H, tabelaY, tabelaUV, t);
			publicar(W, H, lado);
		}
	}

	/** (com travaEscrita) O quadro de trás vira o pronto (se ainda tem alguém vendo). */
	private static void publicar(int W, int H, int lado) {
		synchronized (trava) {
			w = W;
			h = H;
			contador++;
			if (vendo) {
				byte[] x = frente;
				frente = tras;
				tras = x;
				ladoFrente = lado;
				numeroFrente = contador;
				temQuadro = true;
				publicadoEm = SystemClock.uptimeMillis();
			}
		}
		chegou(W, H);
	}

	/**
	 * O jogo ainda não pegou o último quadro montado e ele é bem recente: este
	 * só é contado, sem montar (o jogo pega no máximo ~16 por segundo; a
	 * câmera manda até 30 — a TV Box faz metade da conta).
	 */
	private static boolean podePular() {
		synchronized (trava) {
			return temQuadro && numeroFrente != entregue && SystemClock.uptimeMillis() - publicadoEm < 45;
		}
	}

	/** Ninguém vendo: só conta o quadro (a câmera segue viva). */
	private static void contar(int W, int H) {
		synchronized (trava) {
			w = W;
			h = H;
			contador++;
		}
		chegou(W, H);
	}

	private static void chegou(int W, int H) {
		if (quadrosEtapa == 0) {
			quadrosEtapa = 1;
			anotar("imagem chegando (" + fonte + " " + W + "x" + H + ")");
		}
	}

	// ------------------------------------------------------------ relatório
	/**
	 * O que o Android enxerga, uma linha por fato: câmeras do sistema, da
	 * Camera2 (com o nível), aparelhos USB (classe e permissão), se a libuvc
	 * está no APK e o que esta ponte tentou. Aparece no menu do operador.
	 */
	@SuppressWarnings("deprecation")
	public static String relatorio() {
		StringBuilder sb = new StringBuilder();
		try {
			sb.append("Android ").append(Build.VERSION.RELEASE).append(" (API ").append(Build.VERSION.SDK_INT).append(") ")
					.append(Build.MANUFACTURER).append(' ').append(Build.MODEL).append('\n');
			sb.append("permissao da camera: ").append(temPermissao() ? "SIM" : "NAO").append('\n');
			try {
				sb.append("cameras do sistema (API classica): ").append(android.hardware.Camera.getNumberOfCameras()).append('\n');
			} catch (Throwable e) {
				sb.append("cameras do sistema: erro ").append(e.getMessage()).append('\n');
			}
			CameraManager m = gerente();
			if (m != null) {
				try {
					String[] ids = m.getCameraIdList();
					sb.append("camera2: ").append(ids.length).append('\n');
					for (String id : ids) {
						Integer nivel = m.getCameraCharacteristics(id).get(CameraCharacteristics.INFO_SUPPORTED_HARDWARE_LEVEL);
						sb.append("  ").append(id).append(" ").append(lado2(m, id)).append(" nivel ").append(nivel).append('\n');
					}
				} catch (Throwable e) {
					sb.append("camera2: erro ").append(e.getMessage()).append('\n');
				}
			}
			try {
				UsbManager u = usb();
				if (u != null) {
					sb.append("usb: ").append(u.getDeviceList().size()).append(" aparelho(s)\n");
					for (UsbDevice d : u.getDeviceList().values()) {
						sb.append("  ").append(String.format("%04x:%04x", d.getVendorId(), d.getProductId()))
								.append(" classe ").append(d.getDeviceClass())
								.append(ehWebcam(d) ? " WEBCAM" : "")
								.append(u.hasPermission(d) ? " permitido" : " sem-permissao").append('\n');
					}
				}
			} catch (Throwable e) {
				sb.append("usb: erro ").append(e.getMessage()).append('\n');
			}
			sb.append("libuvc no apk: ").append(temLibuvc() ? "SIM" : "NAO").append('\n');
			int cw;
			int ch;
			synchronized (trava) {
				cw = w;
				ch = h;
			}
			sb.append("agora: ").append(estado).append(" | fonte: ").append(fonte.isEmpty() ? "-" : fonte)
					.append(" | quadros: ").append(quadros()).append(" | imagem ").append(cw).append('x').append(ch)
					.append(" -> foto ").append(ladoSaida).append('\n');
		} catch (Throwable t) {
			sb.append("relatorio: ").append(t.getMessage()).append('\n');
		}
		synchronized (diario) {
			sb.append(diario);
		}
		return sb.toString();
	}
}
