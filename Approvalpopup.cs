using System;
using System.Drawing;
using System.Windows.Forms;

namespace PlakaUyg
{
    // Kayıtlı bir araç tespit edilip onaylandığında ekranın sağ üst köşesinde
    // birkaç saniyeliğine beliren, yeşil temalı "toast" tarzı bildirim penceresi.
    // Modal DEĞİLDİR (Show ile açılır) — arka plandaki kamera işleme döngüsünü kilitlemez.
    internal sealed class ApprovalPopup : Form
    {
        private readonly System.Windows.Forms.Timer _closeTimer = new() { Interval = 3800 };

        public ApprovalPopup(string plate, string? owner)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Size = new Size(380, 150);
            BackColor = Color.FromArgb(9, 33, 25);
            Cursor = Cursors.Hand;

            var accent = Color.FromArgb(34, 197, 94);

            var accentBar = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = accent };

            var icon = new Label
            {
                Text = "✓",
                Font = new Font("Segoe UI", 30f, FontStyle.Bold),
                ForeColor = accent,
                AutoSize = true,
                Location = new Point(22, 26)
            };

            var title = new Label
            {
                Text = "GEÇİŞ ONAYLANDI",
                Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(85, 24)
            };

            var plateLbl = new Label
            {
                Text = plate,
                Font = new Font("Consolas", 20f, FontStyle.Bold),
                ForeColor = accent,
                AutoSize = true,
                Location = new Point(85, 52)
            };

            var ownerText = string.IsNullOrWhiteSpace(owner) ? "Sahip bilgisi kayıtlı değil" : $"Araç Sahibi: {owner}";
            var ownerLbl = new Label
            {
                Text = ownerText,
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(190, 220, 200),
                AutoSize = true,
                Location = new Point(85, 96)
            };

            Controls.AddRange(new Control[] { accentBar, icon, title, plateLbl, ownerLbl });

            Paint += (_, e) =>
            {
                using var p = new System.Drawing.Pen(accent, 1.5f);
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            };

            // Pencereye veya herhangi bir alt kontrole tıklanınca hemen kapansın.
            Click += (_, _) => Close();
            foreach (Control c in Controls) c.Click += (_, _) => Close();

            _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); Close(); };
            _closeTimer.Start();

            var wa = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
            Location = new Point(wa.Right - Width - 24, wa.Top + 24);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closeTimer.Stop();
            _closeTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}