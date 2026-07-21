using System;
using System.Drawing;
using System.Windows.Forms;

namespace PlakaUyg
{
    public partial class Form1
    {
        // ── Kontroller ────────────────────────────────────────────────────────
        private Label lblEngStatus = null!;
        private Label lblSourceInfo = null!;
        private Button btnStart = null!;
        private Button btnLiveCam = null!;
        private Button btnStop = null!;
        private Button btnEngines = null!;
        private PictureBox pbCam = null!;
        private Label lblNoCam = null!;
        private Label lblPlate = null!;
        private Panel pnlBadge = null!;
        private Label lblBadge = null!;
        private Label lblCount = null!;
        private FlowLayoutPanel flowLog = null!;
        private SplitContainer _split = null!;

        // Sekmeler
        private TabControl _dbTabs = null!;

        // "Kayıtlı Araçlar" sekmesi
        private TextBox txtSearch = null!;
        private ListBox lstPlates = null!;
        private TextBox txtNew = null!;
        private TextBox txtOwner = null!;
        private TextBox txtVehicleType = null!;
        private TextBox txtNotes = null!;
        private Button btnAdd = null!;
        private Button btnUpdate = null!;
        private Button btnRemove = null!;

        // "Kara Liste" sekmesi
        private TextBox txtBlacklistSearch = null!;
        private ListBox lstBlacklist = null!;
        private TextBox txtBlacklistPlate = null!;
        private TextBox txtBlacklistReason = null!;
        private Label lblBlacklistCount = null!;
        private Button btnAddBlacklist = null!;
        private Button btnRemoveBlacklist = null!;

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
            lblEngStatus = new Label { Parent = hdr, Text = "● Motor Yüklü Değil", ForeColor = T.Red, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Location = new Point(420, 15) };
            lblSourceInfo = new Label { Parent = hdr, Text = "", ForeColor = T.Dim, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), AutoSize = true, Location = new Point(420, 35) };

            var pnlHdrButtons = new FlowLayoutPanel
            {
                Parent = hdr,
                Dock = DockStyle.Right,
                Width = 600,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 15, 20, 0),
                BackColor = Color.Transparent
            };

            btnStop = Btn("■ Durdur", T.Red, 110, 36);
            btnLiveCam = Btn("📡 Canlı Kamera", T.Green, 160, 36);
            btnStart = Btn("📁 Video Seç", T.Accent, 140, 36);
            btnEngines = Btn("⚙ Motor Ayarları", T.Dim, 160, 36);
            btnStop.Enabled = false;

            pnlHdrButtons.Controls.AddRange(new Control[] { btnStop, btnLiveCam, btnStart, btnEngines });
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
                Text = "📷\n\nKamera Bekleniyor\n\nSağ üstten 'Video Seç' düğmesine tıklayın",
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

        // ── Plaka Yönetimi (sekmeli: Kayıtlı Araçlar / Kara Liste) ─────────────
        private void BuildDbPanel(Panel parent)
        {
            var db = new Panel { Dock = DockStyle.Fill, BackColor = T.Card };
            db.Paint += DrawCardBorder;

            var hdr = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Transparent };
            hdr.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, hdr.Height - 1, hdr.Width, hdr.Height - 1);
            };
            new Label { Parent = hdr, Text = "PLAKA YÖNETİMİ", ForeColor = T.Dim, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 13) };

            lblCount = new Label { Parent = hdr, Text = "0 kayıt", ForeColor = T.Accent, Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            hdr.Resize += (_, _) => lblCount.Location = new Point(Math.Max(0, hdr.Width - 90), 13);
            lblCount.Location = new Point(Math.Max(0, hdr.Width - 90), 13);

            _dbTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };

            var tabPlates = new TabPage("Kayıtlı Araçlar") { BackColor = T.Card, Padding = new Padding(0) };
            var tabBlacklist = new TabPage("Kara Liste") { BackColor = T.Card, Padding = new Padding(0) };
            _dbTabs.TabPages.Add(tabPlates);
            _dbTabs.TabPages.Add(tabBlacklist);

            BuildPlatesTab(tabPlates);
            BuildBlacklistTab(tabBlacklist);

            db.Controls.Add(_dbTabs);
            db.Controls.Add(hdr);
            _dbTabs.BringToFront();

            parent.Controls.Add(db);
            db.BringToFront();
        }

        private void BuildPlatesTab(Panel parent)
        {
            var searchWrap = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(15, 12, 15, 5), BackColor = Color.Transparent };
            txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = T.Input,
                ForeColor = T.Text,
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "🔍 Plaka veya isimle ara..."
            };
            searchWrap.Controls.Add(txtSearch);

            // Ekleme / düzenleme formu: Plaka, Sahip Adı, Araç Tipi, Not
            var actionWrap = new Panel { Dock = DockStyle.Bottom, Height = 220, BackColor = T.Input };
            actionWrap.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 0, actionWrap.Width, 0);
            };

            txtNew = LabeledBox(actionWrap, "Plaka", 0, upper: true);
            txtOwner = LabeledBox(actionWrap, "Sahip Adı", 1);
            txtVehicleType = LabeledBox(actionWrap, "Araç Tipi", 2);
            txtNotes = LabeledBox(actionWrap, "Not", 3);

            var flowActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(15, 0, 0, 8),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            btnAdd = Btn("+ Ekle", T.Green, 85, 34);
            btnUpdate = Btn("✎ Güncelle", T.Accent, 100, 34);
            btnRemove = Btn("✕ Sil", T.Dim, 75, 34);
            foreach (Control c in new Control[] { btnAdd, btnUpdate, btnRemove }) c.Margin = new Padding(0, 0, 8, 0);
            flowActions.Controls.AddRange(new Control[] { btnAdd, btnUpdate, btnRemove });
            actionWrap.Controls.Add(flowActions);

            lstPlates = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = T.Card,
                ForeColor = T.Text,
                Font = new Font("Consolas", 11f, FontStyle.Bold),
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 45,
                SelectionMode = SelectionMode.One
            };
            lstPlates.DrawItem += LstPlates_DrawItem;
            lstPlates.SelectedIndexChanged += (_, _) => LoadSelectedPlateIntoForm();

            parent.Controls.Add(lstPlates);
            parent.Controls.Add(actionWrap);
            parent.Controls.Add(searchWrap);
            lstPlates.BringToFront();
        }

        private void BuildBlacklistTab(Panel parent)
        {
            var searchWrap = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(15, 12, 15, 5), BackColor = Color.Transparent };
            txtBlacklistSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = T.Input,
                ForeColor = T.Text,
                Font = new Font("Segoe UI", 11f),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "🔍 Kara listede ara..."
            };
            searchWrap.Controls.Add(txtBlacklistSearch);

            lblBlacklistCount = new Label
            {
                Parent = searchWrap,
                Text = "0 kayıt",
                ForeColor = T.Accent,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(searchWrap.Width - 80, 0)
            };
            searchWrap.Resize += (_, _) => lblBlacklistCount.Location = new Point(Math.Max(0, searchWrap.Width - 80), 0);

            var actionWrap = new Panel { Dock = DockStyle.Bottom, Height = 150, BackColor = T.Input };
            actionWrap.Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(T.Border);
                e.Graphics.DrawLine(p, 0, 0, actionWrap.Width, 0);
            };

            txtBlacklistPlate = LabeledBox(actionWrap, "Plaka", 0, upper: true);
            txtBlacklistReason = LabeledBox(actionWrap, "Sebep", 1);

            var flowActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(15, 0, 0, 8),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            btnAddBlacklist = Btn("☠ Karalisteye Ekle", Color.FromArgb(220, 38, 38), 150, 34);
            btnRemoveBlacklist = Btn("✓ Listeden Çıkar", Color.FromArgb(234, 88, 12), 140, 34);
            foreach (Control c in new Control[] { btnAddBlacklist, btnRemoveBlacklist }) c.Margin = new Padding(0, 0, 8, 0);
            flowActions.Controls.AddRange(new Control[] { btnAddBlacklist, btnRemoveBlacklist });
            actionWrap.Controls.Add(flowActions);

            lstBlacklist = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = T.Card,
                ForeColor = T.Text,
                Font = new Font("Consolas", 10.5f, FontStyle.Bold),
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 45,
                SelectionMode = SelectionMode.One
            };
            lstBlacklist.DrawItem += LstBlacklist_DrawItem;
            lstBlacklist.SelectedIndexChanged += (_, _) =>
            {
                if (lstBlacklist.SelectedItem is BlacklistRecord b)
                {
                    txtBlacklistPlate.Text = b.Plate;
                    txtBlacklistReason.Text = b.Reason ?? "";
                }
            };

            parent.Controls.Add(lstBlacklist);
            parent.Controls.Add(actionWrap);
            parent.Controls.Add(searchWrap);
            lstBlacklist.BringToFront();
        }

        // Küçük "etiket + tek satır kutu" bloğu üreten yardımcı. Ekleme/düzenleme
        // formlarındaki (Plaka, Sahip Adı, Araç Tipi, Not, Sebep vb.) tüm alanlar
        // bunun üzerinden aynı görünümle oluşturulur.
        private TextBox LabeledBox(Panel parent, string label, int row, bool upper = false)
        {
            const int margin = 15;
            const int rowH = 40;
            int y = row * rowH + 8;

            new Label
            {
                Parent = parent,
                Text = label,
                ForeColor = T.Dim,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(margin, y)
            };

            var tb = new TextBox
            {
                Parent = parent,
                Location = new Point(margin, y + 16),
                Height = 24,
                Width = Math.Max(50, parent.ClientSize.Width - margin * 2),
                BackColor = T.Card,
                ForeColor = T.Text,
                Font = new Font("Consolas", 10.5f, upper ? FontStyle.Bold : FontStyle.Regular),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CharacterCasing = upper ? CharacterCasing.Upper : CharacterCasing.Normal
            };
            parent.Resize += (_, _) => tb.Width = Math.Max(50, parent.ClientSize.Width - margin * 2);
            return tb;
        }

        // ── Arayüz Çizim Yardımcıları ─────────────────────────────────────────
        private void LstPlates_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            if (lstPlates.Items[e.Index] is not PlateRecord rec) return;
            var plate = rec.Plate;

            bool sel = (e.State & DrawItemState.Selected) != 0;
            bool active = plate.Equals(_curPlate, StringComparison.OrdinalIgnoreCase);

            Color bg = sel ? T.Border : active ? Color.FromArgb(6, 78, 59) : T.Card;
            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

            if (active)
                e.Graphics.FillRectangle(new SolidBrush(T.Green), e.Bounds.X, e.Bounds.Y, 5, e.Bounds.Height);
            else if (sel)
                e.Graphics.FillRectangle(new SolidBrush(T.Accent), e.Bounds.X, e.Bounds.Y, 5, e.Bounds.Height);

            var fg = active ? T.Green : T.Text;
            var icon = active ? " ✓ " : " ";
            var sub = string.IsNullOrWhiteSpace(rec.OwnerName) ? "" : $"  ·  {rec.OwnerName}";

            e.Graphics.DrawString(icon + plate + sub, lstPlates.Font, new SolidBrush(fg),
                new RectangleF(e.Bounds.X + 20, e.Bounds.Y + 12, e.Bounds.Width - 40, e.Bounds.Height - 12));

            using var pen = new System.Drawing.Pen(T.Border, 1);
            e.Graphics.DrawLine(pen, e.Bounds.Left + 20, e.Bounds.Bottom - 1, e.Bounds.Right - 20, e.Bounds.Bottom - 1);
        }

        private void LstBlacklist_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            if (lstBlacklist.Items[e.Index] is not BlacklistRecord b) return;

            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color bg = sel ? T.Border : T.Card;
            e.Graphics.FillRectangle(new SolidBrush(bg), e.Bounds);

            if (sel)
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(220, 38, 38)), e.Bounds.X, e.Bounds.Y, 5, e.Bounds.Height);

            using var titleFont = new Font("Consolas", 11f, FontStyle.Bold);
            using var subFont = new Font("Segoe UI", 8.5f);

            e.Graphics.DrawString($" ☠ {b.Plate}", titleFont, new SolidBrush(Color.FromArgb(220, 90, 80)),
                new RectangleF(e.Bounds.X + 20, e.Bounds.Y + 4, e.Bounds.Width - 40, 20));
            e.Graphics.DrawString($"   {(string.IsNullOrWhiteSpace(b.Reason) ? "Sebep belirtilmemiş" : b.Reason)} · {b.AddedAt}",
                subFont, new SolidBrush(T.Dim), new RectangleF(e.Bounds.X + 20, e.Bounds.Y + 24, e.Bounds.Width - 40, 18));

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