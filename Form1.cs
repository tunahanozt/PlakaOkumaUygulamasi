using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Timer = System.Windows.Forms.Timer;

namespace PlakaUyg
{
    public partial class Form1 : Form
    {
        // ── DLL bağlantısı ────────────────────────────────────────────────────
        // NOT: Bu isim, derlediğiniz .dll dosyasının GERÇEK adıyla birebir
        // aynı olmalı (Properties -> General -> Target Name). Farklıysa
        // DllNotFoundException alırsınız.
        [DllImport("PlakaDLL.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool InitSystem(string det, string ocr, StringBuilder outError, int outErrorSize);
        [DllImport("PlakaDLL.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void ProcessFrame(IntPtr data, int w, int h, StringBuilder buf);
        [DllImport("PlakaDLL.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void CleanupSystem();

        // ── Çalışma zamanı durumu ─────────────────────────────────────────────
        private readonly DatabaseService _db = new();
        private VideoCapture? _cap;
        private readonly Timer _tick = new() { Interval = 30 };
        private readonly StringBuilder _buf = new(1024);
        private bool _dllReady;
        private string _curPlate = "";
        private readonly List<LogEntry> _log = new();

        // DLL artık her tespit için "onaylandı mı?" (confirmed) bilgisini de gönderiyor.
        // confirmed=true  -> DLL'in kendi araç-takip/oylama sistemi bu plakayı kesinleştirdi.
        // confirmed=false -> henüz deneniyor; bu SADECE ekranda "taranıyor" göstergesi için kullanılır,
        //                     veritabanına işlenmez, loglanmaz, pop-up açtırmaz.
        private readonly List<(string plate, int x, int y, int w, int h, bool confirmed)> _dets = new();
        private string _videoPath = "";

        // ── Giriş kaynağı (Video Dosyası / Canlı Kamera) ────────────────────────
        private bool _isLiveSource = false;
        private string _lastCameraUrl = "";
        private int _liveReadFailStreak = 0;
        private const int LiveReconnectAfterFails = 60; // ~60 karelik okuma hatasından sonra yeniden bağlan

        private const int MissingGrace = 10; // araç/plaka görünmeden kaç kare beklenip sıfırlanacak
        private static readonly TimeSpan ReconfirmCooldown = TimeSpan.FromSeconds(4); // aynı plaka için tekrar bildirim aralığı
        private int _missingStreak = 0;
        private DateTime _lastConfirmAt = DateTime.MinValue;

        // Motor (.engine) dosyalarının bulunduğu klasör. ÇALIŞTIRILAN .exe'nin
        // KENDİ BULUNDUĞU KLASÖR olarak otomatik belirlenir (AppContext.BaseDirectory) —
        // böylece proje başka bir bilgisayara/klasöre taşındığında da elle yol
        // değiştirmenize gerek kalmaz. .engine dosyalarını, DLL'i ve diğer
        // bağımlılıkları HER ZAMAN .exe ile aynı klasöre koyun.
        private static readonly string EngineDir = AppContext.BaseDirectory;

        private string _detectEng = Path.Combine(EngineDir, "plaka_tespit_v0.2.engine");
        private string _ocrEng = Path.Combine(EngineDir, "plaka_okuma_v0.3.engine");

        public Form1()
        {
            InitializeComponent();
            BuildUi();
            WireEvents();
            RefreshList();
            RefreshBlacklist();

            // Açılışta motorları sessizce (hata penceresi göstermeden) yüklemeyi dene.
            // Dosyalar sabit klasörde bulunamazsa üstteki durum etiketi kırmızı kalır,
            // kullanıcı isterse "⚙ Motor Ayarları"ndan farklı bir yol seçebilir.
            InitDll(silent: true);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // KAMERA VE DLL YÖNETİMİ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void StartPressed()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Video Dosyası Seç",
                Filter = "Video Dosyaları|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.ts|Tüm Dosyalar|*.*"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _videoPath = dlg.FileName;
            if (!_dllReady) InitDll();
            if (_dllReady) OpenSource(_videoPath, live: false);
        }

        private void StartLivePressed()
        {
            using var dlg = new CameraUrlDialog(_lastCameraUrl);
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _lastCameraUrl = dlg.CameraUrl;
            if (!_dllReady) InitDll();
            if (_dllReady) OpenSource(_lastCameraUrl, live: true);
        }

        // silent=true  -> hata olsa bile pop-up göstermez (sadece üstteki durum etiketini günceller).
        //                 Uygulama açılışında otomatik deneme için kullanılır.
        // silent=false -> hata olursa, DLL'den gelen GERÇEK sebebi pop-up'ta gösterir.
        private void InitDll(bool silent = false)
        {
            // DLL, başarısız olursa gerçek hata mesajını bu buffer'a yazacak.
            // Böylece "Motor Yüklenemedi" gibi belirsiz bir mesaj yerine,
            // örneğin bir CUDA/TensorRT hata metnini doğrudan görebiliyoruz.
            var errBuf = new StringBuilder(512);

            try
            {
                _dllReady = InitSystem(_detectEng, _ocrEng, errBuf, errBuf.Capacity);
                lblEngStatus.Text = _dllReady ? "● Motor Aktif" : "● Motor Yüklenemedi";
                lblEngStatus.ForeColor = _dllReady ? T.Green : T.Red;

                if (!_dllReady && !silent)
                {
                    var reason = errBuf.Length > 0 ? errBuf.ToString() : "(DLL bir sebep bildirmedi)";
                    Msg($"Engine dosyaları yüklenemedi.\n\nGerçek sebep:\n{reason}\n\nDosya yollarını ⚙ Motor Ayarları'ndan kontrol edin.", MessageBoxIcon.Error);
                }
            }
            catch (DllNotFoundException)
            {
                lblEngStatus.Text = "● Motor Yüklenemedi";
                lblEngStatus.ForeColor = T.Red;
                if (!silent)
                    Msg("DLL bulunamadı.\nDLL dosya adının [DllImport(\"...\")] içindeki isimle BİREBİR aynı olduğundan ve .exe ile aynı klasörde olduğundan emin olun.", MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                lblEngStatus.Text = "● Motor Yüklenemedi";
                lblEngStatus.ForeColor = T.Red;
                if (!silent) Msg("Motor hatası: " + ex.Message, MessageBoxIcon.Error);
            }
        }

        // path: video dosyası yolu YA DA rtsp://.../http://... canlı yayın adresi.
        // live: true ise "dosya bitti, başa sar" mantığı yerine "yayın koptu, yeniden bağlan" mantığı çalışır.
        private void OpenSource(string path, bool live)
        {
            _cap = new VideoCapture(path);
            if (!_cap.IsOpened())
            {
                _cap.Dispose(); _cap = null;
                Msg(live ? $"Kameraya bağlanılamadı:\n{path}" : $"Video açılamadı:\n{path}", MessageBoxIcon.Warning);
                return;
            }

            _isLiveSource = live;
            _liveReadFailStreak = 0;
            lblSourceInfo.Text = live ? "📡 CANLI KAMERA" : "📼 VİDEO DOSYASI";
            lblSourceInfo.ForeColor = live ? Color.FromArgb(220, 90, 80) : T.Dim;

            lblNoCam.Visible = false;
            btnStart.Enabled = false;
            btnLiveCam.Enabled = false;
            btnStop.Enabled = true;
            _tick.Start();
        }

        private void StopCamera()
        {
            _tick.Stop();
            _cap?.Release(); _cap?.Dispose(); _cap = null;
            pbCam.Image?.Dispose(); pbCam.Image = null;
            _videoPath = "";
            _isLiveSource = false;
            _liveReadFailStreak = 0;
            lblSourceInfo.Text = "";
            lblNoCam.Visible = true;
            btnStart.Enabled = true;
            btnLiveCam.Enabled = true;
            btnStop.Enabled = false;
            SetState("", DetState.Idle);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // KARE (FRAME) İŞLEME DÖNGÜSÜ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_cap == null) return;
            try
            {
                using var frame = new Mat();
                if (!_cap.Read(frame) || frame.Empty())
                {
                    if (!_isLiveSource)
                    {
                        // Video dosyası: sona geldi, başa sar ve devam et.
                        if (!string.IsNullOrEmpty(_videoPath))
                            _cap.Set(VideoCaptureProperties.PosFrames, 0);
                    }
                    else
                    {
                        // Canlı yayın: ağ/sunucu kaynaklı geçici bir kare kaybı olabilir.
                        // Art arda çok fazla hata olursa bağlantıyı koparıp yeniden kurmayı dene.
                        _liveReadFailStreak++;
                        if (_liveReadFailStreak >= LiveReconnectAfterFails)
                        {
                            _liveReadFailStreak = 0;
                            var url = _lastCameraUrl;
                            _cap.Release(); _cap.Dispose(); _cap = null;

                            var reconnected = new VideoCapture(url);
                            if (reconnected.IsOpened())
                            {
                                _cap = reconnected;
                            }
                            else
                            {
                                reconnected.Dispose();
                                StopCamera();
                                Msg($"Canlı kamera bağlantısı koptu ve yeniden kurulamadı:\n{url}", MessageBoxIcon.Warning);
                            }
                        }
                    }
                    return;
                }
                _liveReadFailStreak = 0;

                _buf.Clear();
                ProcessFrame(frame.Data, frame.Width, frame.Height, _buf);
                var raw = _buf.ToString();

                _dets.Clear();
                string? topConfirmed = null;
                string? topPending = null;

                // DLL formatı: "PLAKA:x,y,w,h,C|..." — C = 1 (onaylandı) / 0 (deneniyor).
                // Metin "..." ise DLL o araç için henüz hiç OCR sonucu üretmedi
                // (kutu görünür olsun diye yer tutucu gönderiliyor).
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    foreach (var entry in raw.Split('|', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var ci = entry.IndexOf(':');
                        if (ci < 0) continue;
                        var plateName = entry[..ci];
                        var parts = entry[(ci + 1)..].Split(',');
                        if (parts.Length == 5
                            && int.TryParse(parts[0], out int rx)
                            && int.TryParse(parts[1], out int ry)
                            && int.TryParse(parts[2], out int rw)
                            && int.TryParse(parts[3], out int rh)
                            && int.TryParse(parts[4], out int rc))
                        {
                            bool confirmed = rc == 1;
                            bool hasText = plateName != "...";
                            _dets.Add((plateName, rx, ry, rw, rh, confirmed));

                            if (confirmed) topConfirmed ??= plateName;
                            else if (hasText) topPending ??= plateName;
                        }
                    }
                }

                DrawOverlays(frame);
                var old = pbCam.Image;
                pbCam.Image = BitmapConverter.ToBitmap(frame);
                old?.Dispose();

                if (topConfirmed != null)
                {
                    _missingStreak = 0;

                    bool isNewPlate = topConfirmed != _curPlate;
                    bool cooldownPassed = DateTime.Now - _lastConfirmAt > ReconfirmCooldown;

                    if (isNewPlate || cooldownPassed)
                    {
                        _lastConfirmAt = DateTime.Now;
                        _curPlate = topConfirmed;

                        bool found = _db.PlateExists(topConfirmed);
                        bool blacklisted = _db.IsBlacklisted(topConfirmed);
                        _db.LogDetection(topConfirmed, found, blacklisted, "Kamera");

                        if (blacklisted)
                            SetState(topConfirmed, DetState.Blacklisted);
                        else
                            SetState(topConfirmed, found ? DetState.Found : DetState.NotFound);

                        AddLog(topConfirmed, found);
                        lstPlates.Invalidate();

                        if (found && !blacklisted)
                        {
                            var owner = _db.GetPlate(topConfirmed)?.OwnerName;
                            ShowApprovalPopup(topConfirmed, owner);
                        }
                    }
                    // else: bu plaka zaten işlendi ve hâlâ kadrajda — tekrar loglama/pop-up yok.
                }
                else if (_dets.Count > 0 && string.IsNullOrEmpty(_curPlate))
                {
                    // Henüz kesinleşmemiş ama kadrajda en az bir araç/kutu var: kullanıcıya
                    // "sistem bu aracı fark etti, çalışıyor" hissi vermek için canlı bir
                    // gösterge. Bu durum veritabanına işlenmez, loglanmaz.
                    _missingStreak = 0;
                    var displayText = string.IsNullOrEmpty(topPending) ? "Analiz ediliyor" : topPending;
                    SetState(displayText, DetState.Scanning);
                }
                else
                {
                    // Kare içinde hiçbir tespit yok. Birkaç kare (grace period) boyunca
                    // hiç görünmezse her şeyi sıfırla; araç kadraja tekrar girdiğinde
                    // (aynısı da olsa) temiz bir doğrulama süreci baştan başlasın.
                    _missingStreak++;
                    if (_missingStreak >= MissingGrace)
                    {
                        if (!string.IsNullOrEmpty(_curPlate))
                        {
                            _curPlate = "";
                            SetState("", DetState.Idle);
                            lstPlates.Invalidate();
                        }
                    }
                }
            }
            catch { /* kare hataları sessizce geç */ }
        }

        private void DrawOverlays(Mat frame)
        {
            foreach (var (plate, x, y, w, h, confirmed) in _dets)
            {
                Scalar boxColor;
                if (!confirmed)
                {
                    // Henüz onaylanmadı: turuncu/nötr renk, "deneniyor" hissi verir.
                    boxColor = new Scalar(0, 165, 255);
                }
                else
                {
                    bool found = _db.PlateExists(plate);
                    boxColor = found ? new Scalar(60, 200, 60) : new Scalar(60, 60, 220);
                }
                var textColor = new Scalar(255, 255, 255);
                string label;
                if (plate == "...") label = "TARANIYOR...";
                else if (confirmed) label = plate;
                else label = plate + " ...";

                Cv2.Rectangle(frame, new OpenCvSharp.Rect(x, y, w, h), boxColor, 2);

                int tx = x;
                int ty = Math.Max(y - 26, 0);
                int tw = Math.Min(label.Length * 14 + 14, frame.Width - tx);
                Cv2.Rectangle(frame, new OpenCvSharp.Rect(tx, ty, tw, 26), boxColor, -1);
                Cv2.PutText(frame, label, new OpenCvSharp.Point(tx + 6, ty + 18),
                    HersheyFonts.HersheySimplex, 0.72, textColor, 2);

                int cs = 12;
                Cv2.Line(frame, new OpenCvSharp.Point(x, y), new OpenCvSharp.Point(x + cs, y), boxColor, 3);
                Cv2.Line(frame, new OpenCvSharp.Point(x, y), new OpenCvSharp.Point(x, y + cs), boxColor, 3);
                Cv2.Line(frame, new OpenCvSharp.Point(x + w, y), new OpenCvSharp.Point(x + w - cs, y), boxColor, 3);
                Cv2.Line(frame, new OpenCvSharp.Point(x + w, y), new OpenCvSharp.Point(x + w, y + cs), boxColor, 3);
                Cv2.Line(frame, new OpenCvSharp.Point(x, y + h), new OpenCvSharp.Point(x + cs, y + h), boxColor, 3);
                Cv2.Line(frame, new OpenCvSharp.Point(x, y + h), new OpenCvSharp.Point(x, y + h - cs), boxColor, 3);
                Cv2.Line(frame, new OpenCvSharp.Point(x + w, y + h), new OpenCvSharp.Point(x + w - cs, y + h), boxColor, 3);
                Cv2.Line(frame, new OpenCvSharp.Point(x + w, y + h), new OpenCvSharp.Point(x + w, y + h - cs), boxColor, 3);
            }
        }
    }
}