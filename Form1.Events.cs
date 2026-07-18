using System;
using System.Drawing;
using System.Linq;
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
			btnStop.Click += (_, _) => StopCamera();
			btnAdd.Click += (_, _) => AddPlate();
			btnRemove.Click += (_, _) => RemovePlate();
			btnEngines.Click += (_, _) => ShowEngineSettings();
			txtSearch.TextChanged += (_, _) => RefreshList(txtSearch.Text);
			txtNew.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) AddPlate(); };
			_tick.Tick += Timer_Tick;
			FormClosing += (_, _) => Cleanup();

			btnAddBlacklist.Click += (_, _) => {
				var plate = txtNew.Text.Trim().ToUpperInvariant();
				if (string.IsNullOrWhiteSpace(plate)) return;
				_db.AddToBlacklist(plate, "Kullanıcı tarafından arayüzden eklendi.");
				Msg($"{plate} karalisteye eklendi!", MessageBoxIcon.Warning);
				txtNew.Clear();
			};

			btnRemoveBlacklist.Click += (_, _) => {
				var plate = txtNew.Text.Trim().ToUpperInvariant();
				if (string.IsNullOrWhiteSpace(plate)) return;
				_db.RemoveFromBlacklist(plate);
				Msg($"{plate} karalisteden başarıyla çıkarıldı.", MessageBoxIcon.Information);
				txtNew.Clear();
			};
		}

		private void AddPlate()
		{
			var plate = txtNew.Text.Trim().ToUpperInvariant();
			if (string.IsNullOrWhiteSpace(plate)) return;

			if (_db.AddPlate(plate, null, null, null))
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
				_db.RemovePlate(sel);
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

		// ── Arayüz Durum Güncelleyicileri ─────────────────────────────────────

		private enum DetState { Idle, Found, NotFound, Blacklisted }

		private void SetState(string plate, DetState state)
		{
			if (InvokeRequired) { Invoke(() => SetState(plate, state)); return; }

			lblPlate.Text = string.IsNullOrEmpty(plate) ? "—" : plate;

			switch (state)
			{
				case DetState.Found:
					lblPlate.ForeColor = T.Green;
					pnlBadge.BackColor = Color.FromArgb(6, 78, 59);
					lblBadge.Text = "✓   KAYITLI ARAÇ";
					lblBadge.ForeColor = T.Green;
					break;
				case DetState.NotFound:
					lblPlate.ForeColor = T.Red;
					pnlBadge.BackColor = Color.FromArgb(127, 29, 29);
					lblBadge.Text = "✕   KAYITSIZ ARAÇ";
					lblBadge.ForeColor = T.Red;
					break;
				case DetState.Blacklisted:
					lblPlate.ForeColor = Color.Yellow;
					pnlBadge.BackColor = Color.DarkRed;
					lblBadge.Text = "☠ DİKKAT: KARALİSTEDEKİ ARAÇ!";
					lblBadge.ForeColor = Color.White;
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
			var plates = _db.SearchPlates(filter);
			lstPlates.BeginUpdate();
			lstPlates.Items.Clear();
			foreach (var p in plates) lstPlates.Items.Add(p.Plate);
			lstPlates.EndUpdate();
			lblCount.Text = $"{plates.Count} kayıt";
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