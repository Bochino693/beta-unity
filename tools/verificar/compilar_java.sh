#!/bin/bash
# CONFERE O JAVA DO PROJETO SEM A UNITY: compila Assets/Plugins/Android/*.java
# (a StrikeAtividade, o Arduino e a câmera) do jeito que a Unity 2022.3 compila
# no build do APK — Java 8 — contra as classes do Android e um esqueleto da
# Unity (tools/verificar/stubs: UnityPlayer e UnityPlayerActivity, só para o
# javac enxergar; não vão para o APK).
#
#   ANDROID_JAR=/caminho/android.jar bash tools/verificar/compilar_java.sh
#
# ANDROID_JAR: o android.jar de uma plataforma do SDK do Android
# (platforms/android-33/android.jar) ou um android-all.jar (Robolectric).
# Precisa do JDK (javac 9 ou mais novo, por causa do --release 8).
# SAIDA=pasta guarda as classes compiladas (senão, uma pasta temporária).
# Depois de compilar, roda no PC o teste das contas que não usam o Android
# (SerialUtil: divisores dos chips e montagem das linhas — os mesmos valores
# do teste da edição Godot; CameraUtil: o recorte do quadrado da foto).
# SEM_TESTE=1 pula o teste.
set -e
AQUI=$(cd "$(dirname "$0")" && pwd)
RAIZ=$(cd "$AQUI/../.." && pwd)
JAR=${ANDROID_JAR:-}
if [ -z "$JAR" ]; then
  for c in "$ANDROID_HOME" "$ANDROID_SDK_ROOT" "$HOME/Android/Sdk"; do
    [ -n "$c" ] || continue
    j=$(ls -d "$c"/platforms/android-*/android.jar 2>/dev/null | sort -V | tail -1)
    if [ -n "$j" ]; then JAR=$j; break; fi
  done
fi
[ -n "$JAR" ] && [ -f "$JAR" ] || { echo "defina ANDROID_JAR (android.jar de uma plataforma do SDK do Android)" >&2; exit 1; }
command -v javac >/dev/null || { echo "não achei o javac (instale um JDK)" >&2; exit 1; }

if [ -n "$SAIDA" ]; then
  T=$SAIDA
  rm -rf "$T/esqueleto" "$T/classes"
else
  T=$(mktemp -d)
  trap 'rm -rf "$T"' EXIT
fi
mkdir -p "$T/esqueleto" "$T/classes"

mapfile -t ESQUELETO < <(find "$AQUI/stubs" -name '*.java' | sort)
mapfile -t FONTES < <(find "$RAIZ/Assets/Plugins/Android" -maxdepth 1 -name '*.java' | sort)
[ ${#FONTES[@]} -gt 0 ] || { echo "nenhum .java em Assets/Plugins/Android" >&2; exit 1; }

echo "== esqueleto da Unity (${#ESQUELETO[@]} arquivos)"
javac --release 8 -nowarn -encoding UTF-8 -cp "$JAR" -d "$T/esqueleto" "${ESQUELETO[@]}"
echo "== Assets/Plugins/Android (${#FONTES[@]} arquivos)"
# Java 8 (o nível da Unity 2022.3); avisos aparecem, mas só erro para
javac --release 8 -Xlint:all -Xlint:-options -encoding UTF-8 -cp "$JAR:$T/esqueleto" -d "$T/classes" "${FONTES[@]}"
echo "ok: o Java compila ($(find "$T/classes" -name '*.class' | wc -l) classes)"

[ "$SEM_TESTE" = "1" ] && exit 0
command -v java >/dev/null || { echo "(sem o java: teste das contas pulado)"; exit 0; }
mkdir -p "$T/teste"
cat > "$T/teste/TesteContas.java" <<'JAVA'
import com.lazersport.strikekids.CameraUtil;
import com.lazersport.strikekids.SerialUtil;
import java.nio.charset.StandardCharsets;

/** As contas do Arduino e da câmera, sem Android (gerado por tools/verificar/compilar_java.sh). */
public class TesteContas {
	static int falhas = 0;

	static void checar(boolean ok, String t) {
		System.out.println((ok ? "  ok     " : "  FALHA  ") + t);
		if (!ok) falhas++;
	}

	public static void main(String[] a) {
		// Arduino (valores do driver ch341.c do Linux e da libftdi)
		checar(SerialUtil.ch34xDivisor(9600) == 0xB282, "CH340 9600 = 0xB282");
		checar(SerialUtil.ch34xDivisor(115200) == 0xCC83, "CH340 115200 = 0xCC83");
		int[] f = SerialUtil.ftdiDivisor(9600);
		checar(f[0] == 0x4138 && f[1] == 0, "FTDI 9600 = 0x4138/0");
		byte[] l = SerialUtil.cdcLinha(9600);
		checar(l.length == 7 && (l[0] & 0xFF) == 0x80 && (l[1] & 0xFF) == 0x25 && l[6] == 8, "CDC 9600 8N1");
		SerialUtil.Linhas li = new SerialUtil.Linhas(200);
		byte[] b = "\u0000OLA:STRIKE_KIDS:1\r\nS:".getBytes(StandardCharsets.ISO_8859_1);
		li.receber(b, 0, b.length);
		checar(li.temLinhas() && li.tirar().equals("OLA:STRIKE_KIDS:1") && !li.temLinhas(), "linha com lixo do boot e \\r");
		b = "3\nCFG\n\nSENS:00100\n".getBytes(StandardCharsets.US_ASCII);
		li.receber(b, 0, b.length);
		checar(li.tirar().equals("S:3\nCFG\nSENS:00100"), "linha partida em dois pedaços");
		byte[] ft = new byte[130];
		for (int i = 0; i < 130; i++) ft[i] = (byte) ('a' + (i % 26));
		checar(SerialUtil.ftdiTirarEstado(ft, 130, 64) == 124 && ft[0] == (byte) ('a' + 2), "FTDI tira 2 bytes de estado por pacote");
		// câmera: o quadrado do meio de um 640x480 NV21 (valor = coluna / 4; U = linha; V = 200)
		int W = 640, H = 480;
		byte[] q = new byte[W * H * 3 / 2];
		for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) q[y * W + x] = (byte) (x / 4);
		for (int y = 0; y < H / 2; y++) for (int x = 0; x < W / 2; x++) { q[W * H + y * W + x * 2] = (byte) 200; q[W * H + y * W + x * 2 + 1] = (byte) y; }
		byte[] s = new byte[CameraUtil.tamanho(480)];
		CameraUtil.nv21(q, W, H, new CameraUtil.Tabela(W, H, 480), new CameraUtil.Tabela(W / 2, H / 2, 240), s);
		int u0 = 480 * 480, v0 = u0 + 240 * 240;
		checar((s[0] & 0xFF) == 20 && (s[479] & 0xFF) == 139, "foto: recorte do quadrado do meio (colunas 80..559)");
		checar((s[u0] & 0xFF) == 0 && (s[u0 + 240 * 239] & 0xFF) == 239 && (s[v0] & 0xFF) == 200, "foto: U e V do NV21 (V vem antes)");
		byte[] s3 = new byte[CameraUtil.tamanho(384)];
		CameraUtil.nv21(q, W, H, new CameraUtil.Tabela(W, H, 384), new CameraUtil.Tabela(W / 2, H / 2, 192), s3);
		checar(CameraUtil.tamanho(384) == 221184 && (s3[0] & 0xFF) == 20, "foto 384: tamanho e recorte");
		System.out.println(falhas == 0 ? "ok: as contas conferem" : "ERRO: " + falhas + " conta(s) errada(s)");
		System.exit(falhas == 0 ? 0 : 1);
	}
}
JAVA
echo "== teste das contas (PC)"
javac -nowarn -encoding UTF-8 -cp "$T/classes" -d "$T/teste" "$T/teste/TesteContas.java"
java -Dfile.encoding=UTF-8 -Dstdout.encoding=UTF-8 -cp "$T/classes:$T/teste" TesteContas
