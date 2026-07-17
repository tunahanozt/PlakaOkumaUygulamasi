using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Timer = System.Windows.Forms.Timer;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace PlakaUyg
{
    // ── Renk paleti ───────────────────────────────────────────────────────────
    internal static class T
    {
        internal static readonly Color Bg = Color.FromArgb(13, 18, 30);
        internal static readonly Color Panel = Color.FromArgb(20, 27, 44);
        internal static readonly Color Card = Color.FromArgb(26, 35, 55);
        internal static readonly Color Border = Color.FromArgb(42, 55, 82);
        internal static readonly Color Accent = Color.FromArgb(41, 128, 185);
        internal static readonly Color Green = Color.FromArgb(39, 174, 96);
        internal static readonly Color Red = Color.FromArgb(192, 57, 43);
        internal static readonly Color Dim = Color.FromArgb(80, 100, 130);
        internal static readonly Color Text = Color.FromArgb(215, 225, 235);
        internal static readonly Color Input = Color.FromArgb(10, 14, 25);
    }

    // ── Log kaydı ─────────────────────────────────────────────────────────────
    internal sealed record LogEntry(string Plate, bool Found, DateTime At);

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
        private readonly PlateDatabase _db = new();
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

        // ── Kontroller ────────────────────────────────────────────────────────
        private Label lblEngStatus = null!;
        private Button btnStart = null!;
        private Button btnStop = null!;
        private Button btnEngines = null!;
        private PictureBox pbCam = null!;
        private Label lblNoCam = null!;
        private Label lblPlate = null!;
        private Panel pnlBadge = null!;
        private Label lblBadge = null!;
        private Label lblCount = null!;
        private TextBox txtSearch = null!;
        private ListBox lstPlates = null!;
        private TextBox txtNew = null!;
        private Button btnAdd = null!;
        private Button btnRemove = null!;
        private FlowLayoutPanel flowLog = null!;
        private SplitContainer _split = null!;

        // ── Kurucu ────────────────────────────────────────────────────────────
        public Form1()
        {
            InitializeComponent();
            BuildUi();
            WireEvents();
            RefreshList();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ARAYÜZ KURULUM
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void BuildUi()
        {
            SuspendLayout();
            BackColor = T.Bg;
            ForeColor = T.Text;
            Text = "Plaka Tanıma Sistemi";
            Size = new Size(1300, 840);
            MinimumSize = new Size(1060, 720);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);

            BuildHeader();
            BuildLogStrip();
            BuildSplitView();
            ResumeLayout(true);
        }

        // ── Üst çubuk ─────────────────────────────────────────────────────────
        private void BuildHeader()
        {
            var hdr = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(8, 12, 22) };
            hdr.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 55, hdr.Width, 55);
            };

            new Label { Parent = hdr, Text = "◉", ForeColor = T.Accent, Font = new Font("Segoe UI", 17f, FontStyle.Bold), AutoSize = true, Location = new Point(14, 14) };
            new Label { Parent = hdr, Text = "PLAKA TANIMA SİSTEMİ", ForeColor = T.Text, Font = new Font("Segoe UI", 13f, FontStyle.Bold), AutoSize = true, Location = new Point(48, 18) };

            lblEngStatus = new Label { Parent = hdr, Text = "● Motor Yüklü Değil", ForeColor = T.Red, Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(380, 21) };

            btnEngines = Btn("⚙  Motor Ayarları", T.Card, 150, 30);
            btnStart = Btn("📁  Video Seç", T.Accent, 145, 30);
            btnStop = Btn("■  Durdur", T.Red, 105, 30);
            btnStop.Enabled = false;

            hdr.Controls.AddRange(new Control[] { btnEngines, btnStart, btnStop });
            PositionHeaderButtons(hdr);
            hdr.Resize += (_, _) => PositionHeaderButtons(hdr);

            Controls.Add(hdr);
        }

        private void PositionHeaderButtons(Panel hdr)
        {
            btnStop.Location = new Point(hdr.Width - 118, 13);
            btnStart.Location = new Point(hdr.Width - 277, 13);
            btnEngines.Location = new Point(hdr.Width - 441, 13);
        }

        // ── Alt log şeridi ────────────────────────────────────────────────────
        private void BuildLogStrip()
        {
            var log = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Color.FromArgb(8, 12, 22), Padding = new Padding(10, 0, 10, 6) };
            log.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 0, log.Width, 0);
            };

            new Label { Parent = log, Text = "SON OKUMALAR", ForeColor = T.Dim, Font = new Font("Segoe UI", 7.5f), AutoSize = true, Location = new Point(10, 7) };

            flowLog = new FlowLayoutPanel
            {
                Parent = log,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Location = new Point(10, 26),
                Height = 36,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            log.Resize += (_, _) => flowLog.Width = log.Width - 20;

            Controls.Add(log);
        }

        // ── Ana bölünmüş görünüm ──────────────────────────────────────────────
        private void BuildSplitView()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = T.Bg,
                SplitterWidth = 5
            };

            // Sol: Kamera
            var pnlCam = new Panel { Dock = DockStyle.Fill, BackColor = T.Panel, Padding = new Padding(8, 6, 4, 8) };
            new Label { Parent = pnlCam, Text = "KAMERA GÖRÜNTÜSÜ", ForeColor = T.Dim, Font = new Font("Segoe UI", 7.5f), Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0) };
            pbCam = new PictureBox { Dock = DockStyle.Fill, BackColor = T.Input, SizeMode = PictureBoxSizeMode.StretchImage };
            lblNoCam = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = T.Input,
                ForeColor = T.Dim,
                Font = new Font("Segoe UI", 11f),
                Text = "📷\n\nKamera Bekleniyor\n\nSağ üstten  'Kamerayı Başlat'  düğmesine tıklayın",
                TextAlign = ContentAlignment.MiddleCenter
            };
            pbCam.Controls.Add(lblNoCam);
            pnlCam.Controls.Add(pbCam);
            _split.Panel1.Controls.Add(pnlCam);

            // Sağ: Tespit + Veritabanı
            var pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = T.Bg, Padding = new Padding(4, 4, 4, 4) };
            BuildDetCard(pnlRight);
            BuildDbPanel(pnlRight);
            _split.Panel2.Controls.Add(pnlRight);

            Controls.Add(_split);
        }

        // ── Tespit kartı ──────────────────────────────────────────────────────
        private void BuildDetCard(Panel parent)
        {
            var card = new Panel { Dock = DockStyle.Top, Height = 200, BackColor = T.Card };
            card.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };

            new Label { Parent = card, Text = "ALGILANAN PLAKA", ForeColor = T.Dim, Font = new Font("Segoe UI", 7.5f), AutoSize = true, Location = new Point(14, 10) };

            lblPlate = new Label
            {
                Parent = card,
                Text = "—",
                ForeColor = T.Dim,
                Font = new Font("Consolas", 40f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 28),
                Height = 80,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            pnlBadge = new Panel
            {
                Parent = card,
                BackColor = T.Border,
                Location = new Point(40, 120),
                Height = 54,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblBadge = new Label
            {
                Parent = pnlBadge,
                Dock = DockStyle.Fill,
                Text = "Kamera Bekleniyor...",
                ForeColor = T.Dim,
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            card.Resize += (_, _) =>
            {
                lblPlate.Width = card.Width;
                pnlBadge.Width = card.Width - 80;
            };

            parent.Controls.Add(card);
        }

        // ── Veritabanı paneli ─────────────────────────────────────────────────
        private void BuildDbPanel(Panel parent)
        {
            var db = new Panel { Dock = DockStyle.Fill, BackColor = T.Panel };
            db.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawRectangle(p, 0, 0, db.Width - 1, db.Height - 1);
            };

            // Başlık satırı
            var hdr = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = T.Card };
            hdr.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 43, hdr.Width, 43);
            };
            new Label { Parent = hdr, Text = "PLAKA VERİTABANI", ForeColor = T.Dim, Font = new Font("Segoe UI", 7.5f), AutoSize = true, Location = new Point(14, 14) };
            lblCount = new Label { Parent = hdr, Text = "0 kayıt", ForeColor = T.Accent, Font = new Font("Segoe UI", 8f, FontStyle.Bold), AutoSize = true };
            hdr.Resize += (_, _) => lblCount.Location = new Point(hdr.Width - 75, 14);

            // Arama kutusu
            var searchWrap = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = T.Panel, Padding = new Padding(10, 8, 10, 0) };
            txtSearch = new TextBox { Dock = DockStyle.Fill, BackColor = T.Input, ForeColor = T.Dim, Font = new Font("Segoe UI", 10f), BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "🔍  Plaka ara..." };
            searchWrap.Controls.Add(txtSearch);

            // Ekleme/silme satırı (Dock.Bottom — listeden önce ekle)
            var addRow = new Panel { Dock = DockStyle.Bottom, Height = 58, BackColor = T.Card, Padding = new Padding(10, 0, 10, 0) };
            addRow.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 0, addRow.Width, 0);
            };

            txtNew = new TextBox
            {
                BackColor = T.Input,
                ForeColor = T.Text,
                Font = new Font("Consolas", 10.5f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(10, 14),
                Size = new Size(195, 28),
                PlaceholderText = "ör: 34ABC123",
                CharacterCasing = CharacterCasing.Upper,
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            btnAdd = Btn("+ Ekle", T.Green, 88, 28); btnAdd.Location = new Point(214, 14);
            btnRemove = Btn("✕ Sil", T.Red, 88, 28); btnRemove.Location = new Point(310, 14);
            btnAdd.Anchor = btnRemove.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            addRow.Controls.AddRange(new Control[] { txtNew, btnAdd, btnRemove });

            // Plaka listesi
            lstPlates = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = T.Input,
                ForeColor = T.Text,
                Font = new Font("Consolas", 10.5f),
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 30,
                SelectionMode = SelectionMode.One,
                ScrollAlwaysVisible = false,
                IntegralHeight = false
            };
            lstPlates.DrawItem += LstPlates_DrawItem;

            // Sıraya göre ekle (Bottom docked olanları önce ekle)
            db.Controls.Add(lstPlates);
            db.Controls.Add(addRow);
            db.Controls.Add(searchWrap);
            db.Controls.Add(hdr);

            parent.Controls.Add(db);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // OLAYLAR
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void WireEvents()
        {
            Load += (_, _) => {
                _split.Panel1MinSize = 380;
                _split.Panel2MinSize = 300;
                _split.SplitterDistance = (int)(_split.Width * 0.58);
            };
            btnStart.Click += (_, _) => StartPressed();
            btnStop.Click += (_, _) => StopCamera();
            btnAdd.Click += (_, _) => AddPlate();
            btnRemove.Click += (_, _) => RemovePlate();
            btnEngines.Click += (_, _) => ShowEngineSettings();
            txtSearch.TextChanged += (_, _) => RefreshList(txtSearch.Text);
            txtNew.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddPlate(); };
            _tick.Tick += Timer_Tick;
            FormClosing += (_, _) => Cleanup();
        }

        // ── Düğme işlevleri ───────────────────────────────────────────────────
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

        private void StartCamera()
        {
            _cap = new VideoCapture(0);
            if (!_cap.IsOpened())
            {
                _cap.Dispose(); _cap = null;
                Msg("Kamera açılamadı.\nKameranın bağlı olduğundan emin olun.", MessageBoxIcon.Warning);
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

        private void AddPlate()
        {
            var plate = txtNew.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(plate)) return;
            if (_db.Add(plate))
            {
                txtNew.Clear();
                RefreshList(txtSearch.Text);
                var idx = lstPlates.FindString(plate);
                if (idx >= 0) lstPlates.SelectedIndex = idx;
            }
            else Msg($"{plate} zaten veritabanında kayıtlı.", MessageBoxIcon.Information);
        }

        private void RemovePlate()
        {
            if (lstPlates.SelectedItem is not string sel) return;
            if (MessageBox.Show($"'{sel}' silinsin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _db.Remove(sel);
                RefreshList(txtSearch.Text);
            }
        }

        private void ShowEngineSettings()
        {
            using var dlg = new EngineSettingsDialog(_detectEng, _ocrEng);
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _detectEng = dlg.DetectEngine;
            _ocrEng = dlg.OcrEngine;
            if (_dllReady) { CleanupSystem(); _dllReady = false; }
            lblEngStatus.Text = "● Motor Yüklü Değil"; lblEngStatus.ForeColor = T.Red;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // KARE İŞLEME
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_cap == null) return;
            try
            {
                using var frame = new Mat();
                if (!_cap.Read(frame) || frame.Empty())
                {
                    // Video bitti → başa sar (loop)
                    if (!string.IsNullOrEmpty(_videoPath))
                        _cap.Set(VideoCaptureProperties.PosFrames, 0);
                    return;
                }

                // DLL çağrısı
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

                // Overlay çiz
                DrawOverlays(frame);

                // Görüntüyü güncelle
                var old = pbCam.Image;
                pbCam.Image = BitmapConverter.ToBitmap(frame);
                old?.Dispose();

                // Plaka değişti mi?
                if (topPlate != null && topPlate != _curPlate)
                {
                    _curPlate = topPlate;
                    bool found = _db.Contains(topPlate);
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
                bool found = _db.Contains(plate);
                // Yeşil = kayıtlı, kırmızı = kayıtsız (BGR sırası)
                var boxColor = found ? new Scalar(60, 200, 60) : new Scalar(60, 60, 220);
                var textColor = new Scalar(255, 255, 255);

                // Kutu
                Cv2.Rectangle(frame, new OpenCvSharp.Rect(x, y, w, h), boxColor, 2);

                // Metin arka planı
                int tx = x;
                int ty = Math.Max(y - 26, 0);
                int tw = Math.Min(plate.Length * 14 + 14, frame.Width - tx);
                Cv2.Rectangle(frame, new OpenCvSharp.Rect(tx, ty, tw, 26), boxColor, -1);

                // Plaka metni
                Cv2.PutText(frame, plate, new OpenCvSharp.Point(tx + 6, ty + 18),
                    HersheyFonts.HersheySimplex, 0.72, textColor, 2);

                // Köşe süsü (ince iç köşe)
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

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ARAYÜZ GÜNCELLEME
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private enum DetState { Idle, Found, NotFound }

        private void SetState(string plate, DetState state)
        {
            if (InvokeRequired) { Invoke(() => SetState(plate, state)); return; }

            lblPlate.Text = string.IsNullOrEmpty(plate) ? "—" : plate;

            switch (state)
            {
                case DetState.Found:
                    lblPlate.ForeColor = T.Green;
                    pnlBadge.BackColor = T.Green;
                    lblBadge.Text = "✓   KAYITLI ARAÇ";
                    lblBadge.ForeColor = Color.White;
                    break;
                case DetState.NotFound:
                    lblPlate.ForeColor = T.Red;
                    pnlBadge.BackColor = T.Red;
                    lblBadge.Text = "✕   KAYITSIZ ARAÇ";
                    lblBadge.ForeColor = Color.White;
                    break;
                default:
                    lblPlate.ForeColor = T.Dim;
                    pnlBadge.BackColor = T.Border;
                    lblBadge.Text = "Kamera Bekleniyor...";
                    lblBadge.ForeColor = T.Dim;
                    break;
            }
        }

        private void AddLog(string plate, bool found)
        {
            _log.Insert(0, new LogEntry(plate, found, DateTime.Now));
            if (_log.Count > 50) _log.RemoveAt(_log.Count - 1);

            flowLog.SuspendLayout();
            flowLog.Controls.Clear();
            foreach (var e in _log.Take(25))
            {
                var bg = e.Found ? Color.FromArgb(35, 39, 174, 96) : Color.FromArgb(45, 192, 57, 43);
                var fg = e.Found ? T.Green : Color.FromArgb(220, 90, 80);
                var chip = new Label
                {
                    Text = $"  {e.Plate}  {e.At:HH:mm:ss}  ",
                    BackColor = bg,
                    ForeColor = fg,
                    Font = new Font("Consolas", 8.5f, FontStyle.Bold),
                    AutoSize = true,
                    Padding = new Padding(2, 4, 2, 4),
                    Margin = new Padding(0, 3, 6, 0)
                };
                flowLog.Controls.Add(chip);
            }
            flowLog.ResumeLayout();
        }

        private void RefreshList(string filter = "")
        {
            var plates = _db.Search(filter);
            lstPlates.BeginUpdate();
            lstPlates.Items.Clear();
            foreach (var p in plates) lstPlates.Items.Add(p);
            lstPlates.EndUpdate();
            lblCount.Text = $"{_db.Plates.Count} kayıt";
        }

        // ── ListBox özel çizim ────────────────────────────────────────────────
        private void LstPlates_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var plate = lstPlates.Items[e.Index]?.ToString() ?? "";
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool active = plate.Equals(_curPlate, StringComparison.OrdinalIgnoreCase);
            bool even = e.Index % 2 == 0;

            Color bg = sel ? T.Accent
                     : active ? Color.FromArgb(20, 39, 174, 96)
                     : even ? T.Input
                              : Color.FromArgb(14, 18, 32);

            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

            // Sol renkli şerit (aktif plaka için)
            if (active)
                e.Graphics.FillRectangle(new SolidBrush(T.Green), e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height);

            var fg = sel ? Color.White : active ? T.Green : T.Text;
            var icon = active ? "  ✓ " : "     ";
            e.Graphics.DrawString(icon + plate, lstPlates.Font, new SolidBrush(fg),
                new RectangleF(e.Bounds.X + 4, e.Bounds.Y + 5, e.Bounds.Width - 8, e.Bounds.Height - 5));

            // İnce ayırıcı çizgi
            using var pen = new System.Drawing.Pen(T.Border, 1);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // YARDIMCILAR
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static Button Btn(string text, Color back, int w, int h)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(w, h),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(back, 0.25f);
            return btn;
        }

        private static void Msg(string text, MessageBoxIcon icon) =>
            MessageBox.Show(text, "Plaka Tanıma Sistemi", MessageBoxButtons.OK, icon);

        private void Cleanup()
        {
            _tick.Stop();
            _cap?.Release(); _cap?.Dispose();
            pbCam?.Image?.Dispose();
            if (_dllReady) try { CleanupSystem(); } catch { }
        }
    }
}