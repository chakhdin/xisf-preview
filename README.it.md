<p align="right">
  <a href="README.md">English</a> | 
  <b>Italiano</b> | 
  <a href="README.ru.md">Русский</a>
</p>

# XISF & FITS FastViewer ed Estensioni Shell

Estensione ad alte prestazioni per la Shell di Windows e visualizzatore rapido per formati di imaging astronomico: **XISF** (PixInsight Extensible Image Serialization Format) e **FITS** (`.fits`, `.fit`, `.fts`).

---

## Caratteristiche

- **Anteprime in Esplora File**: Estensione nativa `IThumbnailProvider` per la generazione rapida e ad alta risoluzione delle miniature direttamente nelle cartelle di sistema.
- **Riquadro di Anteprima**: Integrazione nativa `IPreviewHandler` per visualizzare i frame a piena risoluzione nel riquadro di anteprima di Esplora File (`Alt + P`).
- **AutoSTF conforme a PixInsight**: Auto-stretch Screen Transfer Function secondo la specifica PixInsight (sezione 8.5.7) con statistiche di mediana e MAD. Le immagini RGB sono stirate canale per canale (non collegato), il che neutralizza le dominanti di colore sui frame lineari grezzi; il pannello istogramma del viewer mostra i valori R/G/B ("Auto (per channel)") e passa a uno stretch collegato non appena si trascina un cursore.
- **Motore I/O ad Alte Prestazioni**: Lettura sequenziale in blocco direttamente in memoria e decodifica parallela per l'apertura immediata di immagini multi-megapixel da sensori CCD e CMOS.
- **Visualizzatore Standalone FastViewer**:
  - Adattamento automatico dell'immagine alla finestra all'avvio.
  - Pre-caricamento asincrono in background per la navigazione istantanea senza ritardi tra i file della cartella (**Successivo** / **Precedente**).
  - Pannello HUD interattivo con istogramma in tempo reale e regolazione manuale dello stretch STF.
  - Configurazione non invasiva: registra unicamente i componenti di anteprima e miniatura della Shell, senza mai modificare le associazioni dei programmi predefiniti esistenti.

---

## Formati Supportati

| Formato | Estensioni | Profondità Bit / Tipi di Dato |
|---|---|---|
| **XISF** | `.xisf` | UInt 8-bit, UInt 16-bit, Float 32-bit (Planar e Interleaved, non compresso) |
| **FITS** | `.fits`, `.fit`, `.fts` | UInt 8-bit, Int/UInt 16-bit (con BZERO/BSCALE), Int 32-bit, Float IEEE -32 |
| **FITS (compresso a tile)** | `.fits`, `.fit`, `.fts` | RICE_1, intero 32-bit, una riga per tile, senza perdita (es. output di HOPS) |

I frame FITS compressi a tile oltre i 16 megapixel non vengono decodificati: per questi file non viene generata alcuna anteprima né miniatura.

---

## Installazione

1. Scaricare l'eseguibile di installazione: `XisfFastViewerSetup.exe` dalla sezione [Releases](https://github.com/chakhdin/xisf-preview/releases).
2. Avviare `XisfFastViewerSetup.exe` con privilegi di amministratore.
3. Riavviare Esplora Risorse o effettuare una disconnessione se le miniature non si aggiornano immediatamente.

> **Nota**: L'installer registra unicamente i gestori di Anteprima e Miniatura di Esplora File. Non aggiunge FastViewer al menu «Apri con...» e non modifica in alcun modo il programma predefinito per l'apertura dei file `.xisf`/`.fits`/`.fit`/`.fts`: le associazioni esistenti (PixInsight, ASIFitsView, ecc.) restano completamente invariate.

---

## Scorciatoie da Tastiera (FastViewer)

| Tasto | Azione |
|---|---|
| `PageDown` / `Freccia Destra` | Immagine astronomica successiva nella cartella |
| `PageUp` / `Freccia Sinistra` | Immagine astronomica precedente nella cartella |
| `Spazio` | Riallinea e applica l'AutoSTF (per canale su RGB) |
| `F` | Adatta l'immagine alle dimensioni della finestra |
| `H` | Mostra / nasconde il pannello HUD dell'istogramma |
| `Rotella Mouse` | Zoom fluido centrato sulla posizione del puntatore |
| `Click Sinistro + Trascina` | Panoramica (spostamento) dell'immagine |
| `Esc` | Chiudi il visualizzatore |

---

## Compilazione dai Sorgenti

### Prerequisiti
- [.NET Framework 4.8 Developer Pack / SDK](https://dotnet.microsoft.com/download/dotnet-framework/net48)
- [Inno Setup 6](https://jrsoftware.org/isdl.php)
- Un file di chiave con nome sicuro `key.snk` nella cartella principale del repository. È in gitignore, quindi un clone appena creato non lo contiene e la compilazione fallisce con un errore di chiave mancante. Generane uno tuo con `sn -k key.snk` da un Developer Command Prompt (lo strumento del .NET Framework SDK); la chiave serve solo a dare un nome sicuro all'assembly e non è legata ad altro nel repository.

### Istruzioni di compilazione

```powershell
# 1. Compila i file binari in configurazione Release
dotnet build -c Release

# 2. Compila il pacchetto di installazione
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss

# 3. SHA-256 of the installer (publish it in the release notes)
Get-FileHash bin\Installer\XisfFastViewerSetup.exe -Algorithm SHA256
```

L'installer compilato sarà disponibile in `bin\Installer\XisfFastViewerSetup.exe`.