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
- **AutoSTF Collegato (Linked) conforme a PixInsight**: Calcolo conforme alla specifica PixInsight (Eq. 8.5.7) con statistiche congiunte di mediana e MAD su tutti i canali attivi per eliminare dominanti di colore sui frame lineari grezzi.
- **Motore I/O ad Alte Prestazioni**: Lettura sequenziale in blocco direttamente in memoria e decodifica parallela per l'apertura immediata di immagini multi-megapixel da sensori CCD e CMOS.
- **Visualizzatore Standalone FastViewer**:
  - Adattamento automatico dell'immagine alla finestra all'avvio.
  - Pre-caricamento asincrono in background per la navigazione istantanea senza ritardi tra i file della cartella (**Successivo** / **Precedente**).
  - Pannello HUD interattivo con istogramma in tempo reale e regolazione manuale dello stretch STF.
  - Configurazione predefinita non invasiva: registra i componenti di anteprima della Shell senza modificare le associazioni dei programmi predefiniti esistenti.

---

## Formati Supportati

| Formato | Estensioni | Profondità Bit / Tipi di Dato |
|---|---|---|
| **XISF** | `.xisf` | UInt 8-bit, UInt 16-bit, Float 32-bit (Planar e Interleaved, non compresso) |
| **FITS** | `.fits`, `.fit`, `.fts` | UInt 8-bit, Int/UInt 16-bit (con BZERO/BSCALE), Int 32-bit, Float IEEE -32 |

---

## Installazione

1. Scaricare l'eseguibile di installazione: `XisfFastViewerSetup.exe` dalla sezione [Releases](https://github.com/chakhdin/xisf-preview/releases).
2. Avviare `XisfFastViewerSetup.exe` con privilegi di amministratore.
3. Riavviare Esplora Risorse o effettuare una disconnessione se le miniature non si aggiornano immediatamente.

> **Nota**: Per impostazione predefinita, l'installer registra unicamente i gestori di anteprima della Shell e aggiunge FastViewer al menu contestuale «Apri con...». Le associazioni file predefinite (PixInsight, ASIFitsView, ecc.) rimangono invariate a meno che non venga selezionata l'apposita casella durante la procedura di setup.

---

## Scorciatoie da Tastiera (FastViewer)

| Tasto | Azione |
|---|---|
| `PageDown` / `Freccia Destra` | Immagine astronomica successiva nella cartella |
| `PageUp` / `Freccia Sinistra` | Immagine astronomica precedente nella cartella |
| `Spazio` | Riallinea e applica l'AutoSTF collegato |
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

### Istruzioni di compilazione

```powershell
# 1. Compila i file binari in configurazione Release
dotnet build -c Release

# 2. Compila il pacchetto di installazione
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

L'installer compilato sarà disponibile in `bin\Installer\XisfFastViewerSetup.exe`.