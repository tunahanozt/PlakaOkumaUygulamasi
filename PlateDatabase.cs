using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PlakaUyg
{
    internal class PlateDatabase
    {
        private readonly string _filePath;
        private HashSet<string> _plates;

        public PlateDatabase(string filePath = "plakadb.json")
        {
            _filePath = filePath;
            _plates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Load();
        }

        public IReadOnlyCollection<string> Plates => _plates;

        public bool Contains(string plate) =>
            !string.IsNullOrWhiteSpace(plate) && _plates.Contains(plate.Trim().ToUpperInvariant());

        public bool Add(string plate)
        {
            var p = plate.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(p)) return false;
            if (!_plates.Add(p)) return false;
            Save();
            return true;
        }

        public bool Remove(string plate)
        {
            if (!_plates.Remove(plate.Trim().ToUpperInvariant())) return false;
            Save();
            return true;
        }

        public List<string> Search(string query = "")
        {
            var all = _plates.OrderBy(p => p);
            if (string.IsNullOrWhiteSpace(query)) return all.ToList();
            var q = query.Trim().ToUpperInvariant();
            return all.Where(p => p.Contains(q)).ToList();
        }

        private void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_filePath));
                if (list != null)
                    _plates = new HashSet<string>(list.Select(p => p.ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);
            }
            catch { /* bozuk dosya — sıfırdan başla */ }
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(_filePath, JsonSerializer.Serialize(
                    _plates.OrderBy(p => p).ToList(),
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }
}
