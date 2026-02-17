using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using FabricationSample.Services.Import;

namespace FabricationSample
{
    /// <summary>
    /// Window for mapping CSV columns to expected import fields.
    /// Shows the CSV column names, allows user to map each expected field
    /// to an actual column, and previews the first few data rows.
    /// </summary>
    public partial class ColumnMappingWindow : Window
    {
        private const string SkipOption = "-- Skip --";

        private ObservableCollection<ColumnMappingField> _mappingFields;

        // Stored state for re-processing when toggling header checkbox
        private string[] _rawLines;
        private char _delimiter;
        private IEnumerable<string> _requiredFields;
        private IEnumerable<string> _optionalFields;
        private bool _isInitializing;

        /// <summary>
        /// CSV column names plus "-- Skip --" as options for ComboBoxes.
        /// </summary>
        public List<string> CsvColumnOptions { get; private set; }

        /// <summary>
        /// The resulting column mapping if user confirmed.
        /// </summary>
        public ColumnMappingConfig ResultMapping { get; private set; }

        /// <summary>
        /// True if user clicked Continue, false if cancelled.
        /// </summary>
        public bool DialogResultOk { get; private set; }

        /// <summary>
        /// Whether the user indicated their data has headers.
        /// Callers should use this to set ImportOptions.HasHeaderRow.
        /// </summary>
        public bool HasHeaders => chkHasHeaders.IsChecked == true;

        /// <summary>
        /// Create the column mapping window.
        /// </summary>
        /// <param name="csvFilePath">Path to the CSV file</param>
        /// <param name="requiredFields">List of required field names</param>
        /// <param name="optionalFields">List of optional field names</param>
        /// <param name="delimiter">CSV delimiter character</param>
        public ColumnMappingWindow(
            string csvFilePath,
            IEnumerable<string> requiredFields,
            IEnumerable<string> optionalFields,
            char delimiter = ',')
        {
            InitializeComponent();
            DataContext = this;

            _requiredFields = requiredFields;
            _optionalFields = optionalFields;
            _delimiter = delimiter;

            LoadCsvData(csvFilePath, requiredFields, optionalFields, delimiter);
        }

        private void LoadCsvData(
            string csvFilePath,
            IEnumerable<string> requiredFields,
            IEnumerable<string> optionalFields,
            char delimiter)
        {
            try
            {
                _isInitializing = true;
                _rawLines = File.ReadAllLines(csvFilePath, Encoding.UTF8);
                if (_rawLines.Length == 0) return;

                // Parse first row to check if it looks like headers
                var firstRowValues = ParseCsvLine(_rawLines[0], delimiter);
                bool looksLikeHeaders = DetectHeaders(firstRowValues, requiredFields, optionalFields);

                chkHasHeaders.IsChecked = looksLikeHeaders;
                _isInitializing = false;

                // Build the UI based on header detection
                RebuildMappingUI(looksLikeHeaders);
            }
            catch (Exception ex)
            {
                _isInitializing = false;
                MessageBox.Show($"Error reading CSV file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Detect whether the first row looks like column headers by checking
        /// if any values match or partially match the expected field names.
        /// Also checks if values look like data (numbers, dates) rather than labels.
        /// </summary>
        private bool DetectHeaders(List<string> firstRowValues,
            IEnumerable<string> requiredFields, IEnumerable<string> optionalFields)
        {
            var allFields = requiredFields.Concat(optionalFields).ToList();

            // Count how many first-row values match expected field names
            int matchCount = 0;
            foreach (var value in firstRowValues)
            {
                var trimmed = value.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                foreach (var field in allFields)
                {
                    if (trimmed.Equals(field, StringComparison.OrdinalIgnoreCase) ||
                        trimmed.IndexOf(field, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        field.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchCount++;
                        break;
                    }
                }
            }

            // If at least 2 values match field names, it's likely a header row
            if (matchCount >= 2)
                return true;

            // If most values are numeric, it's probably data, not headers
            int numericCount = firstRowValues.Count(v =>
                double.TryParse(v.Trim(), out _) || DateTime.TryParse(v.Trim(), out _));
            if (numericCount > firstRowValues.Count / 2)
                return false;

            // Default: if we have at least 1 match, treat as headers
            return matchCount >= 1;
        }

        /// <summary>
        /// Rebuild the entire mapping UI and preview based on the current header setting.
        /// </summary>
        private void RebuildMappingUI(bool hasHeaders)
        {
            if (_rawLines == null || _rawLines.Length == 0) return;

            var firstRowValues = ParseCsvLine(_rawLines[0], _delimiter);
            int columnCount = firstRowValues.Count;

            List<string> csvHeaders;

            if (hasHeaders)
            {
                // Use actual first-row values as headers
                csvHeaders = firstRowValues;
            }
            else
            {
                // Generate numbered column headers: "Col 1", "Col 2", etc.
                csvHeaders = new List<string>();
                for (int i = 0; i < columnCount; i++)
                    csvHeaders.Add($"Col {i + 1}");
            }

            // Build ComboBox options
            CsvColumnOptions = new List<string> { SkipOption };
            CsvColumnOptions.AddRange(csvHeaders);

            // Build mapping fields
            _mappingFields = new ObservableCollection<ColumnMappingField>();

            foreach (var field in _requiredFields)
            {
                _mappingFields.Add(new ColumnMappingField
                {
                    FieldName = field,
                    IsRequired = true,
                    SelectedCsvColumn = SkipOption
                });
            }

            foreach (var field in _optionalFields)
            {
                _mappingFields.Add(new ColumnMappingField
                {
                    FieldName = field,
                    IsRequired = false,
                    SelectedCsvColumn = SkipOption
                });
            }

            mappingsList.ItemsSource = _mappingFields;

            // Notify bindings that CsvColumnOptions changed
            // (ItemsControl ComboBoxes bind to this via RelativeSource)
            // Force rebind by resetting DataContext
            DataContext = null;
            DataContext = this;
            mappingsList.ItemsSource = _mappingFields;

            // Auto-map columns
            AutoMapColumns(csvHeaders);

            // Load preview
            LoadPreview(_rawLines, csvHeaders, _delimiter, hasHeaders);
        }

        private void AutoMapColumns(List<string> csvHeaders)
        {
            foreach (var field in _mappingFields)
            {
                // Try exact match (case-insensitive)
                var exactMatch = csvHeaders.FirstOrDefault(h =>
                    h.Trim().Equals(field.FieldName, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    field.SelectedCsvColumn = exactMatch;
                    continue;
                }

                // Try contains match (field name in header or header in field name)
                var containsMatch = csvHeaders.FirstOrDefault(h =>
                    h.Trim().IndexOf(field.FieldName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    field.FieldName.IndexOf(h.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

                if (containsMatch != null)
                {
                    field.SelectedCsvColumn = containsMatch;
                }
            }
        }

        private void LoadPreview(string[] lines, List<string> headers, char delimiter, bool hasHeaders)
        {
            try
            {
                var table = new DataTable();
                foreach (var header in headers)
                {
                    // Deduplicate column names for DataTable
                    string uniqueName = header;
                    int suffix = 2;
                    while (table.Columns.Contains(uniqueName))
                        uniqueName = $"{header}_{suffix++}";
                    table.Columns.Add(uniqueName);
                }

                // Determine start row: skip first row if it's headers, otherwise include it
                int startRow = hasHeaders ? 1 : 0;
                int maxPreviewRows = lines.Length;

                for (int i = startRow; i < maxPreviewRows; i++)
                {
                    var fields = ParseCsvLine(lines[i], delimiter);
                    var row = table.NewRow();
                    for (int j = 0; j < Math.Min(fields.Count, headers.Count); j++)
                    {
                        row[j] = fields[j];
                    }
                    table.Rows.Add(row);
                }

                dgPreview.ItemsSource = table.DefaultView;
                lblPreview.Content = $"Data Preview ({table.Rows.Count} row{(table.Rows.Count == 1 ? "" : "s")})";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading preview: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle "My data has headers" checkbox toggle.
        /// Rebuilds column options, mapping, and preview.
        /// </summary>
        private void chkHasHeaders_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            RebuildMappingUI(chkHasHeaders.IsChecked == true);
        }

        private List<string> ParseCsvLine(string line, char delimiter)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            fields.Add(currentField.ToString().Trim());
            return fields;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DialogResultOk = false;
            Close();
        }

        private void btnAutoMap_Click(object sender, RoutedEventArgs e)
        {
            var csvHeaders = CsvColumnOptions.Where(o => o != SkipOption).ToList();
            AutoMapColumns(csvHeaders);
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            // Validate that all required fields are mapped
            var unmappedRequired = _mappingFields
                .Where(f => f.IsRequired &&
                       (f.SelectedCsvColumn == SkipOption || string.IsNullOrEmpty(f.SelectedCsvColumn)))
                .ToList();

            if (unmappedRequired.Any())
            {
                string fields = string.Join(", ", unmappedRequired.Select(f => f.FieldName));
                MessageBox.Show(
                    $"The following required fields are not mapped:\n\n{fields}\n\nPlease map all required fields before continuing.",
                    "Required Fields Missing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Build mapping config
            ResultMapping = new ColumnMappingConfig();
            foreach (var field in _mappingFields)
            {
                if (field.SelectedCsvColumn != SkipOption && !string.IsNullOrEmpty(field.SelectedCsvColumn))
                {
                    ResultMapping.Mappings[field.FieldName] = field.SelectedCsvColumn;
                }
            }

            DialogResultOk = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResultOk = false;
            Close();
        }

        /// <summary>
        /// Handler for skip checkbox changes - sets the combo to "-- Skip --" when checked.
        /// </summary>
        private void SkipCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as System.Windows.Controls.CheckBox;
            if (checkBox?.DataContext is ColumnMappingField field)
            {
                if (field.IsSkipped)
                {
                    field.SelectedCsvColumn = SkipOption;
                }
            }
        }
    }
}
