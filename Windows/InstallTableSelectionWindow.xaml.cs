using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample
{
    /// <summary>
    /// Window for selecting which installation tables to export.
    /// </summary>
    public partial class InstallTableSelectionWindow : Window
    {
        private ObservableCollection<InstallTableItem> _installTables;

        /// <summary>
        /// Get the list of selected installation table items.
        /// </summary>
        public List<InstallTableItem> SelectedInstallTables
        {
            get
            {
                return _installTables
                    .Where(t => t.IsSelected)
                    .ToList();
            }
        }

        /// <summary>
        /// True if user clicked OK, false if cancelled.
        /// </summary>
        public bool DialogResultOk { get; private set; }

        public InstallTableSelectionWindow()
        {
            InitializeComponent();
            LoadInstallTables();
        }

        private void LoadInstallTables()
        {
            _installTables = new ObservableCollection<InstallTableItem>();

            try
            {
                foreach (var table in FabDB.InstallationTimesTable.OrderBy(t => t.Group).ThenBy(t => t.Name))
                {
                    string displayName = string.IsNullOrEmpty(table.Group)
                        ? table.Name
                        : $"{table.Group} - {table.Name}";

                    _installTables.Add(new InstallTableItem
                    {
                        DisplayName = displayName,
                        TableName = table.Name,
                        TableGroup = table.Group ?? string.Empty,
                        IsSelected = true // Default to all selected
                    });
                }

                installTablesListBox.ItemsSource = _installTables;
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading installation tables: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSelectedCount()
        {
            int count = _installTables.Count(t => t.IsSelected);
            txtSelectedCount.Text = $"{count} installation table(s) selected";
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DialogResultOk = false;
            Close();
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var table in _installTables)
            {
                table.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        private void btnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var table in _installTables)
            {
                table.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        private void InstallTable_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            int selectedCount = _installTables.Count(t => t.IsSelected);

            if (selectedCount == 0)
            {
                MessageBox.Show("Please select at least one installation table to export.",
                    "No Installation Tables Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResultOk = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResultOk = false;
            Close();
        }
    }

    /// <summary>
    /// ViewModel for an installation table item in the selection list.
    /// </summary>
    public class InstallTableItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string DisplayName { get; set; }
        public string TableName { get; set; }
        public string TableGroup { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
