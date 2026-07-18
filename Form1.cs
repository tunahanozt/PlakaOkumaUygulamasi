using System;
using System.Collections.Generic;
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
        [DllImport("main.cpp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool InitSystem(string det, string ocr);

        [DllImport("main.cpp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void ProcessFrame(IntPtr data, int w, int h, StringBuilder buf);

        [DllImport("main.cpp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void CleanupSystem();

        // ── Çalışma zamanı durumu ─────────────────────────────────────────────
        private readonly DatabaseService _db = new();
        private VideoCapture? _cap;
        private readonly Timer _tick = new() { Interval = 30 };
        private readonly StringBuilder _buf = new(1024);
        private bool _dllReady;
        private string _curPlate = "";
        private readonly List<LogEntry> _log = new();
        private readonly List<(string plate, int x, int y, int w, int h)> _dets = new();
        private string _videoPath = "";
        private string _detectEng = "plaka_tespit_v0.2.engine";
        private string _ocrEng = "plaka_okuma_v0.2.engine";

        public Form1()
        {
            InitializeComponent();
            BuildUi();
            WireEvents();
            RefreshList();
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
            if (_dllReady) OpenVideoSource(_videoPath);
        }

        private void InitDll()
        {
            try
            {
                _dllReady = InitSystem(_detectEng, _ocrEng);
                lblEngStatus.Text = _dllReady ? "● Motor Aktif" : "● Motor Yüklenemedi";
                lblEngStatus.ForeColor = _dllReady ? T.Green : T.Red;
                if (!_dllReady)
                    Msg("Engine dosyaları yüklenemedi.\nDosya yollarını ⚙ Motor Ayarları'ndan kontrol edin.", MessageBoxIcon.Error);
            }
            catch (DllNotFoundException)
            {
                Msg("PlateRecognition.dll bulunamadı.\nDLL'i uygulama klasörüne (bin\\x64\\Debug) kopyalayın.", MessageBoxIcon.Error);
            }
            catch (Exception ex) { Msg("Motor hatası: " + ex.Message, MessageBoxIcon.Error); }
        }

        private void OpenVideoSource(string path)
        {
            _cap = new VideoCapture(path);
            if (!_cap.IsOpened())
            {
                _cap.Dispose(); _cap = null;
                Msg($"Video açılamadı:\n{path}", MessageBoxIcon.Warning);
                return;
            }
            lblNoCam.Visible = false;
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            _tick.Start();
        }

        private void StopCamera()
        {
            _tick.Stop();
            _cap?.Release(); _cap?.Dispose(); _cap = null;
            pbCam.Image?.Dispose(); pbCam.Image = null;
            _videoPath = "";
            lblNoCam.Visible = true;
            btnStart.Enabled = true;
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
                    if (!string.IsNullOrEmpty(_videoPath))
                        _cap.Set(VideoCaptureProperties.PosFrames, 0);
                    return;
                }

                _buf.Clear();
                ProcessFrame(frame.Data, frame.Width, frame.Height, _buf);
                var raw = _buf.ToString();

                _dets.Clear();
                string? topPlate = null;

                if (!string.IsNullOrWhiteSpace(raw))
                {
                    foreach (var entry in raw.Split('|', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var ci = entry.IndexOf(':');
                        if (ci < 0) continue;
                        var plateName = entry[..ci];
                        var coords = entry[(ci + 1)..].Split(',');
                        if (coords.Length == 4
                            && int.TryParse(coords[0], out int rx)
                            && int.TryParse(coords[1], out int ry)
                            && int.TryParse(coords[2], out int rw)
                            && int.TryParse(coords[3], out int rh))
                        {
                            _dets.Add((plateName, rx, ry, rw, rh));
                            topPlate ??= plateName;
                        }
                    }
                }

                DrawOverlays(frame);

                var old = pbCam.Image;
                pbCam.Image = BitmapConverter.ToBitmap(frame);
                old?.Dispose();

                if (topPlate != null && topPlate != _curPlate)
                {
                    _curPlate = topPlate;

                    bool found = _db.PlateExists(topPlate);
                    bool blacklisted = _db.IsBlacklisted(topPlate);

                    _db.LogDetection(topPlate, found, blacklisted, "Kamera");

                    if (blacklisted)
                        SetState(topPlate, DetState.Blacklisted);
                    else
                        SetState(topPlate, found ? DetState.Found : DetState.NotFound);

                    AddLog(topPlate, found);
                    lstPlates.Invalidate();
                }
                else if (topPlate == null && !string.IsNullOrEmpty(_curPlate))
                {
                    _curPlate = "";
                    SetState("", DetState.Idle);
                    lstPlates.Invalidate();
                }
            }
            catch { /* kare hataları sessizce geç */ }
        }

        private void DrawOverlays(Mat frame)
        {
            foreach (var (plate, x, y, w, h) in _dets)
            {
                bool found = _db.PlateExists(plate);
                var boxColor = found ? new Scalar(60, 200, 60) : new Scalar(60, 60, 220);
                var textColor = new Scalar(255, 255, 255);

                Cv2.Rectangle(frame, new OpenCvSharp.Rect(x, y, w, h), boxColor, 2);

                int tx = x;
                int ty = Math.Max(y - 26, 0);
                int tw = Math.Min(plate.Length * 14 + 14, frame.Width - tx);
                Cv2.Rectangle(frame, new OpenCvSharp.Rect(tx, ty, tw, 26), boxColor, -1);

                Cv2.PutText(frame, plate, new OpenCvSharp.Point(tx + 6, ty + 18),
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