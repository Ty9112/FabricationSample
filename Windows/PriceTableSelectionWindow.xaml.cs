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
    /// Window for selecting which price tables to export.
    /// </summary>
    public partial class PriceTableSelectionWindow : Window
    {
        private ObservableCollection<PriceTableItem> _priceTables;

        /// <summary>
        /// Get the list of selected price table items.
        /// </summary>
        public List<PriceTableItem> SelectedPriceTables
        {
            get
            {
                return _priceTables
                    .Where(t => t.IsSelected)
                    .ToList();
            }
        }

        /// <summary>
        /// True if user clicked OK, false if cancelled.
        /// </summary>
        public bool DialogResultOk { get; private set; }

        public PriceTableSelectionWindow()
        {
            InitializeComponent();
            LoadPriceTables();
        }

        private void LoadPriceTables()
        {
            _priceTables = new ObservableCollection<PriceTableItem>();

            try
            {
                foreach (var supplierGroup in FabDB.SupplierGroups.OrderBy(sg => sg.Name))
                {
                    foreach (PriceListBase priceList in supplierGroup.PriceLists)
                    {
                        _priceTables.Add(new PriceTableItem
                        {
                            DisplayName = $"{supplierGroup.Name} - {priceList.Name}",
                            SupplierGroupName = supplierGroup.Name,
                            PriceListName = priceList.Name,
                            IsSelected = true // Default to all selected
                        });
                    }
                }

                priceTablesListBox.ItemsSource = _priceTables;
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading price tables: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSelectedCount()
        {
            int count = _priceTables.Count(t => t.IsSelected);
            txtSelectedCount.Text = $"{count} price table(s) selected";
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
            foreach (var table in _priceTables)
            {
                table.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        private void btnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var table in _priceTables)
            {
                table.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        private void PriceTable_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            int selectedCount = _priceTables.Count(t => t.IsSelected);

            if (selectedCount == 0)
            {
                MessageBox.Show("Please select at least one price table to export.",
                    "No Price Tables Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
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
    /// ViewModel for a price table item in the selection list.
    /// </summary>
    public class PriceTableItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string DisplayName { get; set; }
        public string SupplierGroupName { get; set; }
        public string PriceListName { get; set; }

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
