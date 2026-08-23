using MiniExcelLibs;

namespace TecNM.Residency.Common;

public static class ExcelHelper
{
    public static (bool IsValid, string? ErrorMessage, List<Dictionary<string, string>> Rows) ParseExcelFile(
        Stream stream,
        List<string> expectedColumns)
    {
        var rows = new List<Dictionary<string, string>>();
        try
        {
            var rawRows = MiniExcel.Query(stream, useHeaderRow: true).ToList();
            if (rawRows.Count == 0)
            {
                return (false, "El archivo Excel está vacío.", rows);
            }

            // Inspect headers from the first dictionary object
            var firstRow = rawRows.First() as IDictionary<string, object>;
            if (firstRow == null)
            {
                return (false, "No se pudieron leer las columnas del archivo Excel.", rows);
            }

            var actualColumns = firstRow.Keys.Select(k => k.Trim()).ToList();

            // Strict column matching (case-insensitive and trimmed)
            var expectedSet = expectedColumns.Select(c => c.Trim().ToLowerInvariant()).ToHashSet();
            var actualSet = actualColumns.Select(c => c.Trim().ToLowerInvariant()).ToHashSet();

            var missing = expectedColumns
                .Where(c => !actualSet.Contains(c.Trim().ToLowerInvariant()))
                .ToList();

            if (missing.Count > 0)
            {
                var errorMsg = $"El archivo Excel no contiene las columnas requeridas. " +
                               $"Columnas esperadas: [{string.Join(", ", expectedColumns)}]. " +
                               $"Columnas recibidas: [{string.Join(", ", actualColumns)}]. " +
                               $"Faltantes: [{string.Join(", ", missing)}].";
                return (false, errorMsg, rows);
            }

            // Parse data rows into normalized dictionary
            foreach (var item in rawRows)
            {
                var dict = item as IDictionary<string, object>;
                if (dict == null) continue;

                var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool hasData = false;

                foreach (var key in expectedColumns)
                {
                    var val = dict.FirstOrDefault(k => string.Equals(k.Key.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase)).Value;
                    var strVal = val?.ToString()?.Trim() ?? string.Empty;
                    rowDict[key.Trim()] = strVal;
                    if (!string.IsNullOrWhiteSpace(strVal))
                    {
                        hasData = true;
                    }
                }

                if (hasData)
                {
                    rows.Add(rowDict);
                }
            }

            return (true, null, rows);
        }
        catch (Exception ex)
        {
            return (false, $"Error al procesar el archivo Excel: {ex.Message}", rows);
        }
    }
}
