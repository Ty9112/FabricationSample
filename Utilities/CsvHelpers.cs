using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FabricationSample.Utilities
{
    /// <summary>
    /// CSV formatting and parsing utilities.
    /// Ported from DiscordCADmep StringExtensions for consistent CSV export format.
    /// </summary>
    public static class CsvHelpers
    {
        /// <summary>
        /// Wrap a single value for CSV output with quotes and escape handling.
        /// Handles quotes, commas, and newlines per CSV RFC 4180 standard.
        /// </summary>
        /// <param name="value">The value to wrap</param>
        /// <returns>CSV-formatted string with quotes</returns>
        public static string WrapForCsv(this string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"N/A\"";

            // Escape quotes by doubling them (CSV standard)
            // Note: DiscordCADmep uses '' but we'll use standard "" for better compatibility
            string escaped = value.Replace("\"", "\"\"");

            // Always quote to handle commas, newlines, quotes
            return $"\"{escaped}\"";
        }

        /// <summary>
        /// Wrap multiple values for CSV output and join with commas.
        /// Convenience overload for creating complete CSV lines.
        /// </summary>
        /// <param name="values">Array of values to wrap and join</param>
        /// <returns>CSV-formatted string with all values quoted and comma-separated</returns>
        public static string WrapForCsv(params object[] values)
        {
            if (values == null || values.Length == 0)
                return "\"N/A\"";

            return string.Join(",", values.Select(v =>
                (v?.ToString() ?? "N/A").WrapForCsv()));
        }

        /// <summary>
        /// Wrap enumerable collection for CSV output.
        /// </summary>
        /// <param name="values">Collection of values to wrap and join</param>
        /// <returns>CSV-formatted string with all values quoted and comma-separated</returns>
        public static string WrapForCsv(this IEnumerable<string> values)
        {
            if (values == null || !values.Any())
                return "\"N/A\"";

            return string.Join(",", values.Select(v =>
                (v ?? "N/A").WrapForCsv()));
        }

        /// <summary>
        /// Parse a CSV line into fields, handling quoted values with embedded commas.
        /// Implements RFC 4180 CSV parsing.
        /// </summary>
        /// <param name="line">The CSV line to parse</param>
        /// <returns>List of field values (unquoted)</returns>
        public static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            if (string.IsNullOrEmpty(line))
                return fields;

            bool inQuotes = false;
            var currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote (doubled quote)
                        currentField.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        // Toggle quote state
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // Field separator
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            // Add final field
            fields.Add(currentField.ToString().Trim());

            return fields;
        }

        /// <summary>
        /// Unwrap CSV value by removing surrounding quotes and unescaping doubled quotes.
        /// </summary>
        /// <param name="value">The CSV field value to unwrap</param>
        /// <returns>Unquoted, unescaped string value</returns>
        public static string UnwrapCsvValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string trimmed = value.Trim();

            // Remove surrounding quotes
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\"") && trimmed.Length >= 2)
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            // Unescape doubled quotes
            return trimmed.Replace("\"\"", "\"");
        }

        /// <summary>
        /// Validate that CSV header matches expected columns.
        /// </summary>
        /// <param name="actualHeader">Actual header columns from CSV file</param>
        /// <param name="expectedHeader">Expected header columns</param>
        /// <returns>Validation result with success flag and error messages</returns>
        public static ValidationResult ValidateHeader(
            List<string> actualHeader,
            List<string> expectedHeader)
        {
            var result = new ValidationResult { IsValid = true };

            if (actualHeader == null || expectedHeader == null)
            {
                result.IsValid = false;
                result.Errors.Add("Header is null");
                return result;
            }

            // Check column count
            if (actualHeader.Count != expectedHeader.Count)
            {
                result.IsValid = false;
                result.Errors.Add($"Expected {expectedHeader.Count} columns, found {actualHeader.Count}");
            }

            // Check column names
            int columnCount = Math.Min(actualHeader.Count, expectedHeader.Count);
            for (int i = 0; i < columnCount; i++)
            {
                string actual = actualHeader[i]?.Trim() ?? "";
                string expected = expectedHeader[i]?.Trim() ?? "";

                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.Errors.Add($"Column {i + 1}: Expected '{expected}', found '{actual}'");
                }
            }

            return result;
        }

        /// <summary>
        /// Sanitize a filename by replacing invalid characters.
        /// </summary>
        /// <param name="fileName">The filename to sanitize</param>
        /// <returns>Sanitized filename safe for file system</returns>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "unnamed";

            string sanitized = fileName;

            // Replace invalid file name characters
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // Additional replacements for consistency
            sanitized = sanitized
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(":", "_")
                .Replace("*", "_")
                .Replace("?", "_")
                .Replace("\"", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("|", "_");

            return sanitized;
        }
    }

    /// <summary>
    /// Validation result for CSV operations.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public string GetErrorSummary()
        {
            if (IsValid)
                return "Validation passed.";

            var summary = new StringBuilder();
            summary.AppendLine("Validation failed:");

            if (Errors.Count > 0)
            {
                summary.AppendLine("\nErrors:");
                foreach (var error in Errors)
                    summary.AppendLine($"  - {error}");
            }

            if (Warnings.Count > 0)
            {
                summary.AppendLine("\nWarnings:");
                foreach (var warning in Warnings)
                    summary.AppendLine($"  - {warning}");
            }

            return summary.ToString();
        }
    }
}
