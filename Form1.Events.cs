using System;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace PlakaUyg
{
    public partial class Form1
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // OLAYLAR VE KULLANICI ETKİLEŞİMİ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        private void WireEvents()
        {
            Load += (_, _) => {
                _split.Panel1MinSize = 380;
                _split.Panel2MinSize = 450;
                _split.SplitterDistance = (int)(_split.Width * 0.55);
            };

            btnStart.Click += (_, _) => StartPressed();
            btnLiveCam.Click += (_, _) => StartLivePressed();
            btnStop.Click += (_, _) => StopCamera();
            btnEngines.Click += (_, _) => ShowEngineSettings();

            // Kayıtlı Araçlar sekmesi
            btnAdd.Click += (_, _) => AddPlate();
            btnUpdate.Click += (_, _) => UpdateSelectedPlate();
            btnRemove.Click += (_, _) => RemovePlate();
            txtSearch.TextChanged += (_, _) => RefreshList(txtSearch.Text);
            txtNew.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddPlate(); };

            // Kara Liste sekmesi
            txtBlacklistSearch.TextChanged += (_, _) => RefreshBlacklist(txtBlacklistSearch.Text);

            btnAddBlacklist.Click += (_, _) =>
            {
                var plate = txtBlacklistPlate.Text.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(plate)) return;
                var reason = string.IsNullOrWhiteSpace(txtBlacklistReason.Text) ? "Belirtilmedi" : txtBlacklistReason.Text.Trim();

                _db.AddToBlacklist(plate, reason);
                RefreshBlacklist(txtBlacklistSearch.Text);
                txtBlacklistPlate.Clear();
                txtBlacklistReason.Clear();
                Msg($"{plate} karalisteye eklendi.", MessageBoxIcon.Warning);
            };

            btnRemoveBlacklist.Click += (_, _) =>
            {
                string? plate = lstBlacklist.SelectedItem is BlacklistRecord b
                    ? b.Plate
                    : txtBlacklistPlate.Text.Trim().ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(plate)) return;
                if (MessageBox.Show($"'{plate}' kara listeden çıkarılsın mı?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                _db.RemoveFromBlacklist(plate);
                RefreshBlacklist(txtBlacklistSearch.Text);
                txtBlacklistPlate.Clear();
                txtBlacklistReason.Clear();
                Msg($"{plate} karalisteden başarıyla çıkarıldı.", MessageBoxIcon.Information);
            };

            _tick.Tick += Timer_Tick;
            FormClosing += (_, _) => Cleanup();
        }

        // ── Kayıtlı Araçlar: ekleme / güncelleme / silme ───────────────────────
        private void AddPlate()
        {
            var plate = txtNew.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(plate)) return;

            var owner = NullIfEmpty(txtOwner.Text);
            var type = NullIfEmpty(txtVehicleType.Text);
            var notes = NullIfEmpty(txtNotes.Text);

            if (_db.AddPlate(plate, owner, type, notes))
            {
                RefreshList(txtSearch.Text);
                var idx = FindPlateIndex(plate);
                if (idx >= 0) lstPlates.SelectedIndex = idx;
                ClearPlateForm();
            }
            else
            {
                Msg($"{plate} zaten veritabanında kayıtlı. Bilgilerini değiştirmek için listeden seçip 'Güncelle' kullanabilirsiniz.", MessageBoxIcon.Information);
            }
        }

        private void UpdateSelectedPlate()
        {
            var plate = txtNew.Text.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(plate)) return;

            if (_db.GetPlate(plate) == null)
            {
                Msg($"{plate} kayıtlı değil. Önce '+ Ekle' ile ekleyin.", MessageBoxIcon.Warning);
                return;
            }

            var owner = NullIfEmpty(txtOwner.Text);
            var type = NullIfEmpty(txtVehicleType.Text);
            var notes = NullIfEmpty(txtNotes.Text);

            _db.UpdatePlate(plate, owner, type, notes);
            RefreshList(txtSearch.Text);
            var idx = FindPlateIndex(plate);
            if (idx >= 0) lstPlates.SelectedIndex = idx;
            Msg($"{plate} bilgileri güncellendi.", MessageBoxIcon.Information);
        }

        private void RemovePlate()
        {
            if (lstPlates.SelectedItem is not PlateRecord sel) return;
            if (MessageBox.Show($"'{sel.Plate}' silinsin mi?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _db.RemovePlate(sel.Plate);
                RefreshList(txtSearch.Text);
                ClearPlateForm();
            }
        }

        private void LoadSelectedPlateIntoForm()
        {
            if (lstPlates.SelectedItem is not PlateRecord p) return;
            txtNew.Text = p.Plate;
            txtOwner.Text = p.OwnerName ?? "";
            txtVehicleType.Text = p.VehicleType ?? "";
            txtNotes.Text = p.Notes ?? "";
        }

        private void ClearPlateForm()
        {
            txtNew.Clear();
            txtOwner.Clear();
            txtVehicleType.Clear();
            txtNotes.Clear();
        }

        private int FindPlateIndex(string plate)
        {
            for (int i = 0; i < lstPlates.Items.Count; i++)
                if (lstPlates.Items[i] is PlateRecord p && p.Plate.Equals(plate, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        // ── Motor Ayarları ──────────────────────────────────────────────────
        private void ShowEngineSettings()
        {
            using var dlg = new EngineSettingsDialog(_detectEng, _ocrEng);
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _detectEng = dlg.DetectEngine;
            _ocrEng = dlg.OcrEngine;

            if (_dllReady) { CleanupSystem(); _dllReady = false; }

            // Yeni yolları hemen dene; başarısız olursa kullanıcıya haber ver.
            InitDll();
        }

        // ── Onay Bildirimi (ses + pop-up) ──────────────────────────────────────
        // Timer_Tick içinde bir plaka art arda yeterli kare boyunca doğrulanıp
        // veritabanında kayıtlı (ve kara listede olmayan) bulunduğunda çağrılır.
        private void ShowApprovalPopup(string plate, string? owner)
        {
            try { SystemSounds.Asterisk.Play(); } catch { /* ses aygıtı yoksa sessizce geç */ }

            var popup = new ApprovalPopup(plate, owner);
            popup.Show();
        }

        // ── Arayüz Durum Güncelleyicileri ─────────────────────────────────────
        private enum DetState { Idle, Found, NotFound, Blacklisted, Scanning }

        private void SetState(string plate, DetState state)
        {
            if (InvokeRequired) { Invoke(() => SetState(plate, state)); return; }

            lblPlate.Text = string.IsNullOrEmpty(plate) ? "—" : plate;

            switch (state)
            {
                case DetState.Found:
                    lblPlate.ForeColor = T.Green;
                    pnlBadge.BackColor = Color.FromArgb(6, 78, 59);
                    lblBadge.Text = "✓ KAYITLI ARAÇ";
                    lblBadge.ForeColor = T.Green;
                    break;
                case DetState.NotFound:
                    lblPlate.ForeColor = T.Red;
                    pnlBadge.BackColor = Color.FromArgb(127, 29, 29);
                    lblBadge.Text = "✕ KAYITSIZ ARAÇ";
                    lblBadge.ForeColor = T.Red;
                    break;
                case DetState.Blacklisted:
                    lblPlate.ForeColor = Color.Yellow;
                    pnlBadge.BackColor = Color.DarkRed;
                    lblBadge.Text = "☠ DİKKAT: KARALİSTEDEKİ ARAÇ!";
                    lblBadge.ForeColor = Color.White;
                    break;
                case DetState.Scanning:
                    // Henüz kesinleşmedi: veritabanına işlenmiyor, sadece kullanıcıya
                    // "sistem bu aracı fark etti ve okumaya çalışıyor" hissini veriyor.
                    lblPlate.ForeColor = Color.Orange;
                    pnlBadge.BackColor = Color.FromArgb(120, 53, 15);
                    lblBadge.Text = "🔍 Taranıyor...";
                    lblBadge.ForeColor = Color.Orange;
                    break;
                default:
                    lblPlate.ForeColor = T.Dim;
                    pnlBadge.BackColor = T.Input;
                    lblBadge.Text = "Sistem Hazır";
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
                    Text = $" {e.Plate}  {e.At:HH:mm:ss} ",
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
            var plates = _db.SearchPlates(filter);
            lstPlates.BeginUpdate();
            lstPlates.Items.Clear();
            foreach (var p in plates) lstPlates.Items.Add(p);
            lstPlates.EndUpdate();
            lblCount.Text = $"{plates.Count} kayıt";
        }

        private void RefreshBlacklist(string filter = "")
        {
            var list = _db.GetBlacklist(filter);
            lstBlacklist.BeginUpdate();
            lstBlacklist.Items.Clear();
            foreach (var b in list) lstBlacklist.Items.Add(b);
            lstBlacklist.EndUpdate();
            lblBlacklistCount.Text = $"{list.Count} kayıt";
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