using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace FabricationSample
{
    /// <summary>
    /// Window for previewing CSV export data before saving.
    /// </summary>
    public partial class ExportPreviewWindow : Window
    {
        private readonly string _tempFilePath;
        private readonly string _defaultFileName;

        /// <summary>
        /// True if user clicked Save As, false if cancelled.
        /// </summary>
        public bool DialogResultOk { get; private set; }

        /// <summary>
        /// The user-chosen save path after clicking Save As.
        /// </summary>
        public string SavePath { get; private set; }

        /// <summary>
        /// Create preview window for a single CSV file.
        /// </summary>
        /// <param name="tempFilePath">Path to the temp CSV file to preview.</param>
        /// <param name="commandName">Display name of the export command.</param>
        /// <param name="defaultFileName">Default file name for Save As dialog.</param>
        public ExportPreviewWindow(string tempFilePath, string commandName, string defaultFileName)
        {
            InitializeComponent();
            _tempFilePath = tempFilePath;
            _defaultFileName = defaultFileName;
            WindowTitle.Content = $"Export Preview - {commandName}";
            LoadCsvPreview();
        }

        private void LoadCsvPreview()
        {
            try
            {
                var lines = File.ReadAllLines(_tempFilePath);
                if (lines.Length == 0)
                {
                    txtExportInfo.Text = "No data to preview.";
                    return;
                }

                var dataTable = new DataTable();
                // Parse header
                var headers = ParseCsvLine(lines[0]);
                foreach (var header in headers)
                {
                    string colName = header.Trim();
                    // DataTable requires unique column names
                    string uniqueName = colName;
                    int suffix = 1;
                    while (dataTable.Columns.Contains(uniqueName))
                    {
                        uniqueName = $"{colName}_{suffix++}";
                    }
                    dataTable.Columns.Add(uniqueName);
                }

                // Parse data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                        continue;

                    var fields = ParseCsvLine(lines[i]);
                    var row = dataTable.NewRow();
                    for (int j = 0; j < Math.Min(fields.Length, dataTable.Columns.Count); j++)
                    {
                        row[j] = fields[j].Trim();
                    }
                    dataTable.Rows.Add(row);
                }

                dgPreview.ItemsSource = dataTable.DefaultView;
                txtExportInfo.Text = $"{dataTable.Rows.Count} rows, {dataTable.Columns.Count} columns";
            }
            catch (Exception ex)
            {
                txtExportInfo.Text = $"Error loading preview: {ex.Message}";
            }
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new System.Collections.Generic.List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++; // skip escaped quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DialogResultOk = false;
            CleanupTempFile();
            Close();
        }

        private void btnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            using (var saveDialog = new System.Windows.Forms.SaveFileDialog())
            {
                saveDialog.Title = "Save Export As";
                saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                saveDialog.DefaultExt = "csv";
                saveDialog.FileName = _defaultFileName;

                if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SavePath = saveDialog.FileName;
                    DialogResultOk = true;
                    Close();
                }
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResultOk = false;
            CleanupTempFile();
            Close();
        }

        private void CleanupTempFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tempFilePath) && File.Exists(_tempFilePath))
                    File.Delete(_tempFilePath);
            }
            catch { /* temp file cleanup is best-effort */ }
        }
    }
}
