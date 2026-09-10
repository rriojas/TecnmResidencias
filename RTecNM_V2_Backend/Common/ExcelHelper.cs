using System.Globalization;
using System.Text;
using MiniExcelLibs;

namespace TecNM.Residency.Common;

public static class ExcelHelper
{
    public static string NormalizeColumnName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var normalizedString = name.Trim().Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            {
                stringBuilder.Append(char.ToLowerInvariant(c));
            }
        }

        return stringBuilder.ToString();
    }

    public static (bool IsValid, string? ErrorMessage, List<Dictionary<string, string>> Rows) ParseExcelFile(
        Stream stream,
        List<string> expectedColumns)
    {
        return ParseExcelFile(stream, expectedColumns, null);
    }

    public static (bool IsValid, string? ErrorMessage, List<Dictionary<string, string>> Rows) ParseExcelFile(
        Stream stream,
        List<string> requiredColumns,
        List<string>? optionalColumns)
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
            var actualNormalizedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in actualColumns)
            {
                var norm = NormalizeColumnName(col);
                if (!string.IsNullOrEmpty(norm) && !actualNormalizedMap.ContainsKey(norm))
                {
                    actualNormalizedMap[norm] = col;
                }
            }

            var missing = new List<string>();
            foreach (var req in requiredColumns)
            {
                var normReq = NormalizeColumnName(req);
                if (!actualNormalizedMap.ContainsKey(normReq))
                {
                    missing.Add(req);
                }
            }

            if (missing.Count > 0)
            {
                var errorMsg = $"El archivo Excel no contiene las columnas requeridas. " +
                               $"Columnas esperadas: [{string.Join(", ", requiredColumns)}]. " +
                               $"Columnas recibidas: [{string.Join(", ", actualColumns)}]. " +
                               $"Faltantes: [{string.Join(", ", missing)}].";
                return (false, errorMsg, rows);
            }

            var allExpected = new List<string>(requiredColumns);
            if (optionalColumns != null)
            {
                allExpected.AddRange(optionalColumns);
            }

            // Parse data rows into normalized dictionary
            foreach (var item in rawRows)
            {
                var dict = item as IDictionary<string, object>;
                if (dict == null) continue;

                var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool hasData = false;

                // Also copy raw keys into rowDict for flexibility
                foreach (var kvp in dict)
                {
                    var rawVal = kvp.Value?.ToString()?.Replace("\u00A0", " ")?.Replace("\u200B", "")?.Trim() ?? string.Empty;
                    rowDict[kvp.Key.Trim()] = rawVal;
                }

                foreach (var col in allExpected)
                {
                    var normCol = NormalizeColumnName(col);
                    if (actualNormalizedMap.TryGetValue(normCol, out var actualKey) && dict.TryGetValue(actualKey, out var val))
                    {
                        var strVal = val?.ToString()?.Replace("\u00A0", " ")?.Replace("\u200B", "")?.Trim() ?? string.Empty;
                        rowDict[col.Trim()] = strVal;
                        if (!string.IsNullOrWhiteSpace(strVal))
                        {
                            hasData = true;
                        }
                    }
                    else if (!rowDict.ContainsKey(col.Trim()))
                    {
                        rowDict[col.Trim()] = string.Empty;
                    }
                }

                if (hasData || rowDict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
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
