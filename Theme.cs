using System;
using System.Drawing;

namespace PlakaUyg
{
    // ── Modern, Derin ve Profesyonel Karanlık Tema Paleti ─────────────────────
    internal static class T
    {
        internal static readonly Color Bg = Color.FromArgb(15, 23, 42);
        internal static readonly Color Panel = Color.FromArgb(30, 41, 59);
        internal static readonly Color Card = Color.FromArgb(30, 41, 59);
        internal static readonly Color Border = Color.FromArgb(51, 65, 85);
        internal static readonly Color Accent = Color.FromArgb(59, 130, 246);
        internal static readonly Color Green = Color.FromArgb(34, 197, 94);
        internal static readonly Color Red = Color.FromArgb(239, 68, 68);
        internal static readonly Color Dim = Color.FromArgb(148, 163, 184);
        internal static readonly Color Text = Color.FromArgb(248, 250, 252);
        internal static readonly Color Input = Color.FromArgb(11, 15, 25);
    }

    // ── Log kaydı ─────────────────────────────────────────────────────────────
    internal sealed record LogEntry(string Plate, bool Found, DateTime At);
}