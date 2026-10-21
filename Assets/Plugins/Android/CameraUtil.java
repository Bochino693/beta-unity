package com.lazersport.strikekids;

/**
 * As contas da imagem da câmera, sem nada do Android (dá para testar no PC).
 *
 * A foto é o QUADRADO DO MEIO da imagem da câmera. Em vez de mandar o quadro
 * inteiro (640x480) para o jogo, a câmera já entrega só esse quadrado,
 * reamostrado (bilinear, como a placa de vídeo faria) para o lado da foto:
 * Y com lado x lado pontos, U e V com (lado/2) x (lado/2) — 4:2:0, cada plano
 * separado, a primeira linha em cima. Espelhar e girar (a câmera montada de
 * lado) continua no shader do jogo (StrikeKids/Camera), como na edição Godot.
 *
 * Um plano é lido como src[inicio + y * passoLinha + x * passoPonto]: serve
 * para o Y e para o U/V intercalados do NV21 (passoPonto 2) e para os planos
 * da Camera2 (com o passo que cada aparelho der).
 */
public final class CameraUtil {

	private CameraUtil() {
	}

	/** Bytes do quadro entregue ao jogo: Y + U + V. */
	public static int tamanho(int lado) {
		int l2 = lado / 2;
		return lado * lado + 2 * l2 * l2;
	}

	/**
	 * Onde cada ponto da saída cai no plano (pontos vizinhos e peso, em 1/256)
	 * — refeita só quando o tamanho da câmera muda.
	 */
	public static final class Tabela {
		public final int largura;
		public final int altura;
		public final int lado;
		final int[] x0;
		final int[] x1;
		final int[] fx;
		final int[] y0;
		final int[] y1;
		final int[] fy;

		public Tabela(int largura, int altura, int lado) {
			this.largura = Math.max(1, largura);
			this.altura = Math.max(1, altura);
			this.lado = Math.max(1, lado);
			int q = Math.min(this.largura, this.altura);
			x0 = new int[this.lado];
			x1 = new int[this.lado];
			fx = new int[this.lado];
			y0 = new int[this.lado];
			y1 = new int[this.lado];
			fy = new int[this.lado];
			eixo((this.largura - q) / 2, q, this.largura, this.lado, x0, x1, fx);
			eixo((this.altura - q) / 2, q, this.altura, this.lado, y0, y1, fy);
		}

		public boolean serve(int largura, int altura, int lado) {
			return this.largura == largura && this.altura == altura && this.lado == lado;
		}
	}

	/** O centro de cada ponto da saída, no recorte [inicio, inicio + tamanho) de um eixo do plano. */
	private static void eixo(int inicio, int tamanho, int limite, int lado, int[] a0, int[] a1, int[] f) {
		long maximo = (long) (limite - 1) * 256;
		for (int i = 0; i < lado; i++) {
			long p = (long) inicio * 256 + ((2L * i + 1) * tamanho * 256) / (2L * lado) - 128;
			if (p < 0) {
				p = 0;
			}
			if (p > maximo) {
				p = maximo;
			}
			int i0 = (int) (p >> 8);
			a0[i] = i0;
			a1[i] = Math.min(i0 + 1, limite - 1);
			f[i] = (int) (p & 0xFF);
		}
	}

	/** O plano inteiro cabe no vetor? (senão, reamostrarSeguro) */
	public static boolean cabe(int tamanhoSrc, int inicio, int passoLinha, int passoPonto, Tabela t) {
		long ultimo = (long) inicio + (long) (t.altura - 1) * passoLinha + (long) (t.largura - 1) * passoPonto;
		return inicio >= 0 && passoLinha > 0 && passoPonto > 0 && ultimo < tamanhoSrc;
	}

	/** Recorta o quadrado do meio do plano e reamostra em lado x lado (bilinear). */
	public static void reamostrar(byte[] src, int inicio, int passoLinha, int passoPonto, Tabela t, byte[] dst, int dstInicio) {
		final int lado = t.lado;
		int o = dstInicio;
		for (int j = 0; j < lado; j++) {
			int r0 = inicio + t.y0[j] * passoLinha;
			int r1 = inicio + t.y1[j] * passoLinha;
			int fy = t.fy[j];
			int gy = 256 - fy;
			for (int i = 0; i < lado; i++) {
				int a0 = t.x0[i] * passoPonto;
				int a1 = t.x1[i] * passoPonto;
				int fx = t.fx[i];
				int gx = 256 - fx;
				int cima = (src[r0 + a0] & 0xFF) * gx + (src[r0 + a1] & 0xFF) * fx;
				int baixo = (src[r1 + a0] & 0xFF) * gx + (src[r1 + a1] & 0xFF) * fx;
				dst[o++] = (byte) ((cima * gy + baixo * fy + 32768) >> 16);
			}
		}
	}

	/** Igual, para plano mais curto que o esperado (driver esquisito): o que falta vale `fora`. */
	public static void reamostrarSeguro(byte[] src, int tamanhoSrc, int inicio, int passoLinha, int passoPonto, Tabela t,
			byte[] dst, int dstInicio, int fora) {
		final int lado = t.lado;
		int o = dstInicio;
		for (int j = 0; j < lado; j++) {
			long r0 = inicio + (long) t.y0[j] * passoLinha;
			long r1 = inicio + (long) t.y1[j] * passoLinha;
			int fy = t.fy[j];
			int gy = 256 - fy;
			for (int i = 0; i < lado; i++) {
				long a0 = (long) t.x0[i] * passoPonto;
				long a1 = (long) t.x1[i] * passoPonto;
				int fx = t.fx[i];
				int gx = 256 - fx;
				int cima = ponto(src, tamanhoSrc, r0 + a0, fora) * gx + ponto(src, tamanhoSrc, r0 + a1, fora) * fx;
				int baixo = ponto(src, tamanhoSrc, r1 + a0, fora) * gx + ponto(src, tamanhoSrc, r1 + a1, fora) * fx;
				dst[o++] = (byte) ((cima * gy + baixo * fy + 32768) >> 16);
			}
		}
	}

	private static int ponto(byte[] src, int tamanhoSrc, long i, int fora) {
		return i >= 0 && i < tamanhoSrc ? (src[(int) i] & 0xFF) : fora;
	}

	/**
	 * NV21 (câmera do sistema e UVC direta): o Y inteiro e depois V,U
	 * intercalados — vira o quadrado Y + U + V em `saida`.
	 * `ty` é a tabela de (largura, altura, lado); `tc` a de (largura/2, altura/2, lado/2).
	 */
	public static void nv21(byte[] d, int largura, int altura, Tabela ty, Tabela tc, byte[] saida) {
		int lado = ty.lado;
		int l2 = tc.lado;
		int base = largura * altura;
		reamostrar(d, 0, largura, 1, ty, saida, 0);
		reamostrar(d, base + 1, largura, 2, tc, saida, lado * lado);
		reamostrar(d, base, largura, 2, tc, saida, lado * lado + l2 * l2);
	}
}
