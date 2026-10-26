// CsvRow.cs
//
// Fila de CSV para Excel en español: cada valor con su nombre de columna (así el
// encabezado y los valores nunca se desalinean), separador ';' y decimales con coma.
// No depende de UnityEngine.

using System.Collections.Generic;
using System.Globalization;

namespace PujJoycon
{
    /// <summary>Arma una fila de CSV con nombre de columna para cada valor (encabezado y valores siempre alineados).</summary>
    public sealed class CsvRow
    {
        private readonly List<string> _names = new List<string>();
        private readonly List<string> _values = new List<string>();

        public void Begin()
        {
            _names.Clear();
            _values.Clear();
        }

        public void Add(string name, string value)
        {
            _names.Add(name);
            _values.Add(Clean(value));
        }

        public void Add(string name, int value) => Add(name, value.ToString(CultureInfo.InvariantCulture));
        public void Add(string name, bool value) => Add(name, value ? "1" : "0");
        public void Add(string name, double value) => Add(name, Number(value));
        /// <summary>Valor vacío si no hay dato (ej. sin sensor en ese segmento).</summary>
        public void Add(string name, double value, bool has) => Add(name, has ? Number(value) : "");
        /// <summary>Con más decimales (p. ej. cuaterniones de crudos.csv, para poder recalcular después sin perder precisión).</summary>
        public void Add(string name, double value, bool has, int decimales) => Add(name, has ? Number(value, decimales) : "");

        public string Header(char sep) => string.Join(sep.ToString(), _names);
        public string Values(char sep) => string.Join(sep.ToString(), _values);

        /// <summary>Número con coma decimal (Excel en español). Vacío si no es un número.</summary>
        public static string Number(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>Número con coma decimal y hasta 'decimales' decimales.</summary>
        public static string Number(double v, int decimales)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "";
            return v.ToString("0." + new string('#', System.Math.Max(1, decimales)), CultureInfo.InvariantCulture).Replace('.', ',');
        }

        /// <summary>Quita lo que rompería la fila: el separador ';' y saltos de línea.</summary>
        public static string Clean(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
    }
}
