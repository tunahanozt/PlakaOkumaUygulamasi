using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PlakaUyg
{
    internal class EngineSettingsDialog : Form
    {
        public string DetectEngine { get; private set; }
        public string OcrEngine    { get; private set; }

        private TextBox txtDetect = null!;
        private TextBox txtOcr    = null!;

        private static readonly Color BgColor    = Color.FromArgb(20, 27, 44);
        private static readonly Color InputColor = Color.FromArgb(10, 14, 25);
        private static readonly Color CardColor  = Color.FromArgb(26, 35, 55);
        private static readonly Color AccentColor = Color.FromArgb(41, 128, 185);

        public EngineSettingsDialog(string detectEngine, string ocrEngine)
        {
            DetectEngine = detectEngine;
            OcrEngine    = ocrEngine;
            Build(detectEngine, ocrEngine);
        }

        private void Build(string det, string ocr)
        {
            Text = "Motor Dosyası Ayarları";
            Size = new Size(580, 255);
            BackColor = BgColor;
            ForeColor = Color.FromArgb(215, 225, 235);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f);

            MakeLabel("Tespit Motoru (.engine):",  16,  18);
            txtDetect = MakeTextBox(det,            16,  40, 490);
            var b1 = MakeBtn("...", 514, 39, 40);
            b1.Click += (_, _) => BrowseFor(txtDetect);

            MakeLabel("OCR Motoru (.engine):",      16,  92);
            txtOcr    = MakeTextBox(ocr,            16, 114, 490);
            var b2 = MakeBtn("...", 514, 113, 40);
            b2.Click += (_, _) => BrowseFor(txtOcr);

            var btnOk = MakeBtn("Kaydet", 356, 170, 100);
            btnOk.BackColor = AccentColor;
            btnOk.Click += (_, _) => {
                DetectEngine = txtDetect.Text.Trim();
                OcrEngine    = txtOcr.Text.Trim();
                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = MakeBtn("İptal", 466, 170, 80);
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] { txtDetect, b1, txtOcr, b2, btnOk, btnCancel });
        }

        private void MakeLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Color.FromArgb(100, 115, 135) });
        }

        private TextBox MakeTextBox(string text, int x, int y, int w)
        {
            var tb = new TextBox {
                Text = text, Location = new Point(x, y), Size = new Size(w, 28),
                BackColor = InputColor, ForeColor = Color.FromArgb(215, 225, 235),
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f)
            };
            return tb;
        }

        private Button MakeBtn(string text, int x, int y, int w)
        {
            var btn = new Button {
                Text = text, Location = new Point(x, y), Size = new Size(w, 28),
                BackColor = CardColor, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            Controls.Add(btn);
            return btn;
        }

        private static void BrowseFor(TextBox target)
        {
            using var dlg = new OpenFileDialog {
                Filter = "Engine Dosyaları|*.engine|Tüm Dosyalar|*.*",
                Title = "Engine Dosyası Seç"
            };
            var dir = Path.GetDirectoryName(Path.GetFullPath(target.Text));
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dlg.InitialDirectory = dir;
            if (dlg.ShowDialog() == DialogResult.OK) target.Text = dlg.FileName;
        }
    }
}
