using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ExcelDataReader;

namespace KontrolSage.Services
{
    /// Resultado crudo del parsing de un archivo Excel.
    public class ExcelParseResult
    {
        /// Nombres de columna detectados (primera fila del Excel).
        public List<string> Headers { get; set; } = new();

        /// Todas las filas de datos (excluyendo el header), cada celda como string.
        public List<string[]> Rows { get; set; } = new();

        /// Número total de filas de datos.
        public int TotalRows => Rows.Count;
    }

    public class ExcelImportService
    {
        public Task<ExcelParseResult> ParseExcelAsync(string filePath)
        {
            return Task.Run(() =>
            {
                // Necesario para .NET Core / .NET 5+ en sistema no-Windows
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                var result = new ExcelParseResult();

                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                bool isFirstRow = true;
                int columnCount = 0;

                while (reader.Read())
                {
                    if (isFirstRow)
                    {
                        // Primera fila = headers
                        columnCount = reader.FieldCount;
                        for (int i = 0; i < columnCount; i++)
                        {
                            var header = reader.GetValue(i)?.ToString()?.Trim() ?? $"Columna {i + 1}";
                            result.Headers.Add(string.IsNullOrWhiteSpace(header) ? $"Columna {i + 1}" : header);
                        }
                        isFirstRow = false;
                    }
                    else
                    {
                        // Verificar si la fila está completamente vacía
                        bool rowIsEmpty = true;
                        var row = new string[columnCount];
                        for (int i = 0; i < columnCount; i++)
                        {
                            var val = reader.GetValue(i)?.ToString()?.Trim() ?? string.Empty;
                            row[i] = val;
                            if (!string.IsNullOrWhiteSpace(val)) rowIsEmpty = false;
                        }
                        if (!rowIsEmpty)
                            result.Rows.Add(row);
                    }
                }

                return result;
            });
        }
    }
}
