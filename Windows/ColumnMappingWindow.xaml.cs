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
                var lines = File.ReadAllLines(csvFilePath, Encoding.UTF8);
                if (lines.Length == 0) return;

                // Parse header
                var csvHeaders = ParseCsvLine(lines[0], delimiter);

                // Build ComboBox options: "-- Skip --" first, then actual columns
                CsvColumnOptions = new List<string> { SkipOption };
                CsvColumnOptions.AddRange(csvHeaders);

                // Build mapping fields
                _mappingFields = new ObservableCollection<ColumnMappingField>();

                foreach (var field in requiredFields)
                {
                    _mappingFields.Add(new ColumnMappingField
                    {
                        FieldName = field,
                        IsRequired = true,
                        SelectedCsvColumn = SkipOption
                    });
                }

                foreach (var field in optionalFields)
                {
                    _mappingFields.Add(new ColumnMappingField
                    {
                        FieldName = field,
                        IsRequired = false,
                        SelectedCsvColumn = SkipOption
                    });
                }

                mappingsList.ItemsSource = _mappingFields;

                // Auto-map on initial load
                AutoMapColumns(csvHeaders);

                // Load preview data
                LoadPreview(lines, csvHeaders, delimiter);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading CSV file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void LoadPreview(string[] lines, List<string> headers, char delimiter)
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

                // Load up to 10 data rows
                int maxPreviewRows = Math.Min(11, lines.Length); // line 0 is header
                for (int i = 1; i < maxPreviewRows; i++)
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading preview: {ex.Message}");
            }
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
