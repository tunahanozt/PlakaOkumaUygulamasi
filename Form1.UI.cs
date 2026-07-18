using System;
using System.Drawing;
using System.Windows.Forms;

namespace PlakaUyg
{
    public partial class Form1
    {
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
        private Button btnAddBlacklist = null!;
        private Button btnRemoveBlacklist = null!;
        private FlowLayoutPanel flowLog = null!;
        private SplitContainer _split = null!;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ARAYÜZ KURULUM METOTLARI
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
            Font = new Font("Segoe UI", 9.5f);

            BuildHeader();
            BuildLogStrip();
            BuildSplitView();

            // ÇÖZÜM 1: _split panelini en öne getirerek ana alanın üst ve alt panellerin altında kalmasını önlüyoruz!
            _split.BringToFront();

            ResumeLayout(true);
        }

        private void BuildHeader()
        {
            var hdr = new Panel { Dock = DockStyle.Top, Height = 65, BackColor = T.Panel };
            hdr.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border, 1.5f);
                e.Graphics.DrawLine(p, 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            };

            new Label { Parent = hdr, Text = "◉", ForeColor = T.Accent, Font = new Font("Segoe UI", 20f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 14) };
            new Label { Parent = hdr, Text = "PLAKA TANIMA SİSTEMİ", ForeColor = T.Text, Font = new Font("Segoe UI", 14f, FontStyle.Bold), AutoSize = true, Location = new Point(60, 18) };

            lblEngStatus = new Label { Parent = hdr, Text = "● Motor Yüklü Değil", ForeColor = T.Red, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Location = new Point(420, 24) };

            var pnlHdrButtons = new FlowLayoutPanel
            {
                Parent = hdr,
                Dock = DockStyle.Right,
                Width = 450,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 15, 20, 0),
                BackColor = Color.Transparent
            };

            btnStop = Btn("■  Durdur", T.Red, 110, 36);
            btnStart = Btn("📁  Video Seç", T.Accent, 140, 36);
            btnEngines = Btn("⚙  Motor Ayarları", T.Dim, 160, 36);

            btnStop.Enabled = false;

            pnlHdrButtons.Controls.AddRange(new Control[] { btnStop, btnStart, btnEngines });
            Controls.Add(hdr);
        }

        private void BuildLogStrip()
        {
            var log = new Panel { Dock = DockStyle.Bottom, Height = 75, BackColor = T.Panel, Padding = new Padding(20, 0, 20, 10) };
            log.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border, 1.5f);
                e.Graphics.DrawLine(p, 0, 0, log.Width, 0);
            };

            new Label { Parent = log, Text = "SON OKUMALAR", ForeColor = T.Dim, Font = new Font("Segoe UI", 8f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 10) };

            flowLog = new FlowLayoutPanel
            {
                Parent = log,
                BackColor = Color.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Location = new Point(20, 32),
                Height = 36,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            log.Resize += (_, _) => flowLog.Width = log.Width - 40;

            Controls.Add(log);
        }

        private void BuildSplitView()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                BackColor = T.Bg,
                SplitterWidth = 8
            };

            var pnlCamContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
            var pnlCamCard = new Panel { Dock = DockStyle.Fill, BackColor = T.Panel };
            pnlCamCard.Paint += DrawCardBorder;

            new Label { Parent = pnlCamCard, Text = "KAMERA GÖRÜNTÜSÜ", ForeColor = T.Dim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), Dock = DockStyle.Top, Height = 35, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(15, 0, 0, 8) };
            pbCam = new PictureBox { Dock = DockStyle.Fill, BackColor = T.Input, SizeMode = PictureBoxSizeMode.StretchImage };
            lblNoCam = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = T.Input,
                ForeColor = T.Dim,
                Font = new Font("Segoe UI", 12f),
                Text = "📷\n\nKamera Bekleniyor\n\nSağ üstten  'Video Seç'  düğmesine tıklayın",
                TextAlign = ContentAlignment.MiddleCenter
            };
            pbCam.Controls.Add(lblNoCam);
            pnlCamCard.Controls.Add(pbCam);
            pnlCamContainer.Controls.Add(pnlCamCard);
            _split.Panel1.Controls.Add(pnlCamContainer);

            var pnlRightContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 20, 20, 20) };
            BuildDetCard(pnlRightContainer);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 20, BackColor = Color.Transparent };
            pnlRightContainer.Controls.Add(spacer);

            BuildDbPanel(pnlRightContainer);
            _split.Panel2.Controls.Add(pnlRightContainer);

            Controls.Add(_split);
        }

        private void BuildDetCard(Panel parent)
        {
            var card = new Panel { Dock = DockStyle.Top, Height = 180, BackColor = T.Card };
            card.Paint += DrawCardBorder;

            new Label { Parent = card, Text = "ALGILANAN PLAKA", ForeColor = T.Dim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 15) };

            lblPlate = new Label
            {
                Parent = card,
                Text = "—",
                ForeColor = T.Dim,
                Font = new Font("Consolas", 48f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 35),
                Height = 80,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            pnlBadge = new Panel
            {
                Parent = card,
                BackColor = T.Input,
                Location = new Point(40, 115),
                Height = 45,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblBadge = new Label
            {
                Parent = pnlBadge,
                Dock = DockStyle.Fill,
                Text = "Sistem Hazır",
                ForeColor = T.Dim,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            card.Resize += (_, _) =>
            {
                lblPlate.Width = card.Width;
                pnlBadge.Width = card.Width - 80;
            };

            parent.Controls.Add(card);
            card.BringToFront();
        }

        private void BuildDbPanel(Panel parent)
        {
            var db = new Panel { Dock = DockStyle.Fill, BackColor = T.Card };
            db.Paint += DrawCardBorder;

            var hdr = new Panel { Dock = DockStyle.Top, Height = 55, BackColor = Color.Transparent };
            hdr.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            };
            new Label { Parent = hdr, Text = "PLAKA YÖNETİMİ", ForeColor = T.Dim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 20) };
            lblCount = new Label { Parent = hdr, Text = "0 kayıt", ForeColor = T.Accent, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true };
            hdr.Resize += (_, _) => lblCount.Location = new Point(hdr.Width - 80, 18);

            var searchWrap = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Color.Transparent, Padding = new Padding(20, 15, 20, 5) };
            txtSearch = new TextBox { Dock = DockStyle.Fill, BackColor = T.Input, ForeColor = T.Text, Font = new Font("Segoe UI", 11.5f), BorderStyle = BorderStyle.FixedSingle, PlaceholderText = "🔍 Listede plaka ara..." };
            searchWrap.Controls.Add(txtSearch);

            // ÇÖZÜM 2: İşlem panelini tam Dock sistemine bağlayıp esnek hale getirdik. Padding ekleyerek butonların sıkışmasını önledik.
            var actionWrap = new Panel { Dock = DockStyle.Bottom, Height = 135, BackColor = T.Input, Padding = new Padding(20, 20, 20, 20) };
            actionWrap.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 0, actionWrap.Width, 0);
            };

            txtNew = new TextBox
            {
                Dock = DockStyle.Top,
                BackColor = T.Card,
                ForeColor = T.Text,
                Font = new Font("Consolas", 14f, FontStyle.Bold),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Plaka girin...",
                CharacterCasing = CharacterCasing.Upper
            };

            var flowActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            btnAdd = Btn("+ Ekle", T.Green, 100, 36);
            btnAddBlacklist = Btn("☠ Karaliste", Color.FromArgb(220, 38, 38), 120, 36);
            btnRemoveBlacklist = Btn("✓ Temizle", Color.FromArgb(234, 88, 12), 110, 36);
            btnRemove = Btn("✕ Sil", T.Dim, 90, 36);

            foreach (Control c in new Control[] { btnAdd, btnAddBlacklist, btnRemoveBlacklist, btnRemove })
                c.Margin = new Padding(0, 0, 10, 0);

            flowActions.Controls.AddRange(new Control[] { btnAdd, btnAddBlacklist, btnRemoveBlacklist, btnRemove });

            actionWrap.Controls.Add(txtNew);
            actionWrap.Controls.Add(flowActions);

            lstPlates = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = T.Card,
                ForeColor = T.Text,
                Font = new Font("Consolas", 12f, FontStyle.Bold),
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 45,
                SelectionMode = SelectionMode.One
            };
            lstPlates.DrawItem += LstPlates_DrawItem;

            lstPlates.SelectedIndexChanged += (_, _) => {
                if (lstPlates.SelectedItem != null) txtNew.Text = lstPlates.SelectedItem.ToString();
            };

            // Nesneleri ekleme sırası Z-Order'ı belirler. 
            db.Controls.Add(hdr);
            db.Controls.Add(searchWrap);
            db.Controls.Add(actionWrap);
            db.Controls.Add(lstPlates);

            lstPlates.BringToFront(); // ListBox, yukarıdaki panellerden arta kalan alanı tam olarak doldurur.

            parent.Controls.Add(db);
            db.BringToFront();
        }

        // ── Arayüz Çizim Yardımcıları ─────────────────────────────────────────

        private void LstPlates_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var plate = lstPlates.Items[e.Index]?.ToString() ?? "";
            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool active = plate.Equals(_curPlate, StringComparison.OrdinalIgnoreCase);

            Color bg = sel ? T.Border : active ? Color.FromArgb(6, 78, 59) : T.Card;

            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

            if (active)
                e.Graphics.FillRectangle(new SolidBrush(T.Green), e.Bounds.X, e.Bounds.Y, 5, e.Bounds.Height);
            else if (sel)
                e.Graphics.FillRectangle(new SolidBrush(T.Accent), e.Bounds.X, e.Bounds.Y, 5, e.Bounds.Height);

            var fg = active ? T.Green : T.Text;
            var icon = active ? "  ✓ " : "     ";

            // ÇÖZÜM 3: ListBox metni X=20 koordinatından başlatılarak altındaki TextBox ile aynı hizaya getirildi
            e.Graphics.DrawString(icon + plate, lstPlates.Font, new SolidBrush(fg),
                new RectangleF(e.Bounds.X + 20, e.Bounds.Y + 12, e.Bounds.Width - 40, e.Bounds.Height - 12));

            using var pen = new System.Drawing.Pen(T.Border, 1);
            e.Graphics.DrawLine(pen, e.Bounds.Left + 20, e.Bounds.Bottom - 1, e.Bounds.Right - 20, e.Bounds.Bottom - 1);
        }

        private void DrawCardBorder(object? sender, PaintEventArgs e)
        {
            if (sender is Control c)
            {
                using var p = new System.Drawing.Pen(T.Border, 1);
                e.Graphics.DrawRectangle(p, 0, 0, c.Width - 1, c.Height - 1);
            }
        }

        private static Button Btn(string text, Color back, int w, int h)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(w, h),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;

            var hoverColor = Color.FromArgb(
                Math.Min(255, back.R + 25),
                Math.Min(255, back.G + 25),
                Math.Min(255, back.B + 25)
            );

            btn.FlatAppearance.MouseOverBackColor = hoverColor;

            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(
                Math.Max(0, back.R - 15),
                Math.Max(0, back.G - 15),
                Math.Max(0, back.B - 15)
            );
            return btn;
        }
    }
}