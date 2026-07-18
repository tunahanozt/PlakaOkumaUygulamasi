using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace PlakaUyg
{
    internal record PlateRecord(int Id, string Plate, string? OwnerName, string? VehicleType, string? Notes, bool IsActive, string CreatedAt);
    internal record BlacklistRecord(int Id, string Plate, string? Reason, string AddedAt);
    internal record DetectionRecord(int Id, string Plate, bool IsFound, bool IsBlacklisted, string? Source, string DetectedAt);
    internal record DbStats(int TotalPlates, int BlacklistCount, int TodayDetections, int TotalDetections);

    internal class DatabaseService
    {
        private readonly string _conn;

        public DatabaseService(string dbPath = "PlakaOkumaDB.db")
        {
            _conn = $"Data Source={dbPath}";
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            using var con = Open();
            Exec(con, @"
                CREATE TABLE IF NOT EXISTS plates (
                    id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    plate        TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    owner_name   TEXT,
                    vehicle_type TEXT,
                    notes        TEXT,
                    is_active    INTEGER NOT NULL DEFAULT 1,
                    created_at   TEXT NOT NULL DEFAULT (datetime('now','localtime')),
                    updated_at   TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );
                CREATE TABLE IF NOT EXISTS blacklist (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    plate     TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    reason    TEXT,
                    added_at  TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );
                CREATE TABLE IF NOT EXISTS detections (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    plate          TEXT NOT NULL,
                    is_found       INTEGER NOT NULL,
                    is_blacklisted INTEGER NOT NULL DEFAULT 0,
                    source         TEXT,
                    detected_at    TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                );
                CREATE INDEX IF NOT EXISTS idx_det_plate ON detections(plate);
                CREATE INDEX IF NOT EXISTS idx_det_date  ON detections(detected_at);
            ");
        }

        // ── Plakalar ──────────────────────────────────────────────────────────
        public void ClearDetections()
        {
            using var con = Open();
            Exec(con, "DELETE FROM detections");
        }
        public bool AddPlate(string plate, string? owner, string? type, string? notes)
        {
            var p = plate.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(p)) return false;
            using var con = Open();
            try
            {
                Exec(con, "INSERT INTO plates(plate,owner_name,vehicle_type,notes) VALUES(@p,@o,@t,@n)",
                    ("@p", p), ("@o", owner), ("@t", type), ("@n", notes));
                return true;
            }
            catch { return false; }
        }

        public bool RemovePlate(string plate)
        {
            using var con = Open();
            Exec(con, "DELETE FROM plates WHERE plate=@p COLLATE NOCASE", ("@p", plate));
            return true;
        }

        public bool PlateExists(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return false;
            using var con = Open();
            using var cmd = new SqliteCommand("SELECT 1 FROM plates WHERE plate=@p COLLATE NOCASE AND is_active=1", con);
            cmd.Parameters.AddWithValue("@p", plate.Trim());
            return cmd.ExecuteScalar() != null;
        }

        public List<PlateRecord> SearchPlates(string query = "")
        {
            using var con = Open();
            var sql = string.IsNullOrWhiteSpace(query)
                ? "SELECT id,plate,owner_name,vehicle_type,notes,is_active,created_at FROM plates ORDER BY plate"
                : "SELECT id,plate,owner_name,vehicle_type,notes,is_active,created_at FROM plates WHERE plate LIKE @q OR owner_name LIKE @q ORDER BY plate";
            using var cmd = new SqliteCommand(sql, con);
            if (!string.IsNullOrWhiteSpace(query)) cmd.Parameters.AddWithValue("@q", $"%{query.Trim()}%");
            var list = new List<PlateRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new PlateRecord(
                    r.GetInt32(0), r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2),
                    r.IsDBNull(3) ? null : r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    r.GetInt32(5) == 1, r.GetString(6)));
            return list;
        }

        // ── Kara Liste ────────────────────────────────────────────────────────

        public bool AddToBlacklist(string plate, string? reason)
        {
            var p = plate.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(p)) return false;
            using var con = Open();
            try
            {
                Exec(con, "INSERT OR REPLACE INTO blacklist(plate,reason) VALUES(@p,@r)",
                    ("@p", p), ("@r", reason));
                return true;
            }
            catch { return false; }
        }

        public bool RemoveFromBlacklist(string plate)
        {
            using var con = Open();
            Exec(con, "DELETE FROM blacklist WHERE plate=@p COLLATE NOCASE", ("@p", plate.Trim()));
            return true;
        }

        public bool IsBlacklisted(string plate)
        {
            if (string.IsNullOrWhiteSpace(plate)) return false;
            using var con = Open();
            using var cmd = new SqliteCommand("SELECT 1 FROM blacklist WHERE plate=@p COLLATE NOCASE", con);
            cmd.Parameters.AddWithValue("@p", plate.Trim());
            return cmd.ExecuteScalar() != null;
        }

        public List<BlacklistRecord> GetBlacklist(string query = "")
        {
            using var con = Open();
            var sql = string.IsNullOrWhiteSpace(query)
                ? "SELECT id,plate,reason,added_at FROM blacklist ORDER BY added_at DESC"
                : "SELECT id,plate,reason,added_at FROM blacklist WHERE plate LIKE @q OR reason LIKE @q ORDER BY added_at DESC";
            using var cmd = new SqliteCommand(sql, con);
            if (!string.IsNullOrWhiteSpace(query)) cmd.Parameters.AddWithValue("@q", $"%{query.Trim()}%");
            var list = new List<BlacklistRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new BlacklistRecord(r.GetInt32(0), r.GetString(1),
                    r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3)));
            return list;
        }

        // ── Tespitler ─────────────────────────────────────────────────────────

        public void LogDetection(string plate, bool found, bool blacklisted, string? source)
        {
            using var con = Open();
            Exec(con, "INSERT INTO detections(plate,is_found,is_blacklisted,source) VALUES(@p,@f,@b,@s)",
                ("@p", plate), ("@f", found ? 1 : 0), ("@b", blacklisted ? 1 : 0), ("@s", source));
        }

        public List<DetectionRecord> GetDetections(int limit = 300)
        {
            using var con = Open();
            using var cmd = new SqliteCommand(
                "SELECT id,plate,is_found,is_blacklisted,source,detected_at FROM detections ORDER BY detected_at DESC LIMIT @l", con);
            cmd.Parameters.AddWithValue("@l", limit);
            var list = new List<DetectionRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new DetectionRecord(r.GetInt32(0), r.GetString(1),
                    r.GetInt32(2) == 1, r.GetInt32(3) == 1,
                    r.IsDBNull(4) ? null : r.GetString(4), r.GetString(5)));
            return list;
        }

        public DbStats GetStats()
        {
            using var con = Open();
            int plates = ScalarInt(con, "SELECT COUNT(*) FROM plates WHERE is_active=1");
            int black = ScalarInt(con, "SELECT COUNT(*) FROM blacklist");
            int today = ScalarInt(con, "SELECT COUNT(*) FROM detections WHERE date(detected_at)=date('now','localtime')");
            int total = ScalarInt(con, "SELECT COUNT(*) FROM detections");
            return new DbStats(plates, black, today, total);
        }

        // ── Yardımcılar ───────────────────────────────────────────────────────

        private SqliteConnection Open()
        {
            var c = new SqliteConnection(_conn);
            c.Open();
            return c;
        }

        private static void Exec(SqliteConnection con, string sql, params (string Key, object? Val)[] parms)
        {
            using var cmd = new SqliteCommand(sql, con);
            foreach (var (k, v) in parms)
                cmd.Parameters.AddWithValue(k, v ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static int ScalarInt(SqliteConnection con, string sql)
        {
            using var cmd = new SqliteCommand(sql, con);
            var r = cmd.ExecuteScalar();
            return r == null || r == DBNull.Value ? 0 : Convert.ToInt32(r);
        }
    }
}
