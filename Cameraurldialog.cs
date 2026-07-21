using System;
using System.Drawing;
using System.Windows.Forms;

namespace PlakaUyg
{
    // Sunucudan/IP kameradan canlı yayın almak için RTSP ya da HTTP adresi
    // girilmesini sağlayan basit bir pencere. OpenCvSharp'ın VideoCapture'ı
    // FFmpeg tabanlı olduğu için rtsp://, http:// ve hatta bazı özel akış
    // adreslerini doğrudan kabul edebilir.
    internal sealed class CameraUrlDialog : Form
    {
        public string CameraUrl { get; private set; } = "";

        private readonly TextBox _txtUrl;

        public CameraUrlDialog(string initialUrl)
        {
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Text = "Canlı Kamera Bağlantısı";
            BackColor = T.Card;
            ForeColor = T.Text;
            Size = new Size(480, 220);
            Font = new Font("Segoe UI", 9.5f);

            var lblTitle = new Label
            {
                Text = "📡 Sunucu / IP Kamera Adresi",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = T.Text,
                AutoSize = true,
                Location = new Point(24, 20)
            };

            var lblHint = new Label
            {
                Text = "Örnek: rtsp://kullanici:sifre@192.168.1.50:554/stream1\nya da: http://192.168.1.50:8080/video",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = T.Dim,
                AutoSize = true,
                Location = new Point(24, 50)
            };

            _txtUrl = new TextBox
            {
                Location = new Point(24, 96),
                Width = 430,
                BackColor = T.Input,
                ForeColor = T.Text,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                Text = initialUrl
            };

            var btnCancel = new Button
            {
                Text = "İptal",
                DialogResult = DialogResult.Cancel,
                BackColor = T.Dim,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(90, 34),
                Location = new Point(364, 140)
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            var btnConnect = new Button
            {
                Text = "Bağlan",
                DialogResult = DialogResult.OK,
                BackColor = T.Accent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 34),
                Location = new Point(258, 140)
            };
            btnConnect.FlatAppearance.BorderSize = 0;
            btnConnect.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_txtUrl.Text))
                {
                    MessageBox.Show("Lütfen bir kamera adresi girin.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                CameraUrl = _txtUrl.Text.Trim();
            };

            AcceptButton = btnConnect;
            CancelButton = btnCancel;

            Controls.AddRange(new Control[] { lblTitle, lblHint, _txtUrl, btnConnect, btnCancel });
        }
    }
}