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
    /// Window for selecting which services to export.
    /// </summary>
    public partial class ServiceSelectionWindow : Window
    {
        private ObservableCollection<ServiceItem> _services;
        private IEnumerable<string> _preSelectedNames;

        /// <summary>
        /// Get the list of selected service names.
        /// </summary>
        public List<string> SelectedServiceNames
        {
            get
            {
                return _services
                    .Where(s => s.IsSelected)
                    .Select(s => s.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// True if user clicked OK, false if cancelled.
        /// </summary>
        public bool DialogResultOk { get; private set; }

        /// <summary>
        /// Default constructor: all services selected.
        /// </summary>
        public ServiceSelectionWindow()
        {
            InitializeComponent();
            _preSelectedNames = null;
            LoadServices();
        }

        /// <summary>
        /// Constructor with pre-selected service names.
        /// Only the named services will be initially selected.
        /// If null or empty, defaults to all selected.
        /// </summary>
        public ServiceSelectionWindow(IEnumerable<string> preSelectedNames)
        {
            InitializeComponent();
            _preSelectedNames = preSelectedNames;
            LoadServices();
        }

        private void LoadServices()
        {
            _services = new ObservableCollection<ServiceItem>();

            try
            {
                var preSelectSet = _preSelectedNames != null
                    ? new HashSet<string>(_preSelectedNames, StringComparer.OrdinalIgnoreCase)
                    : null;

                foreach (var service in FabDB.Services)
                {
                    bool isSelected = preSelectSet == null || preSelectSet.Count == 0
                        ? true
                        : preSelectSet.Contains(service.Name);

                    _services.Add(new ServiceItem
                    {
                        Name = service.Name,
                        IsSelected = isSelected
                    });
                }

                servicesListBox.ItemsSource = _services;
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading services: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSelectedCount()
        {
            int count = _services.Count(s => s.IsSelected);
            txtSelectedCount.Text = $"{count} service(s) selected";
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
            foreach (var service in _services)
            {
                service.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        private void btnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var service in _services)
            {
                service.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        private void Service_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            int selectedCount = _services.Count(s => s.IsSelected);

            if (selectedCount == 0)
            {
                MessageBox.Show("Please select at least one service to export.",
                    "No Services Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
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
    /// ViewModel for a service item in the selection list.
    /// </summary>
    public class ServiceItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; }

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
