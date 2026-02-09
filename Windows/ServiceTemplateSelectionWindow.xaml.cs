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
    /// Window for selecting which service templates to export.
    /// </summary>
    public partial class ServiceTemplateSelectionWindow : Window
    {
        private ObservableCollection<TemplateItem> _templates;
        private IEnumerable<string> _preSelectedNames;

        /// <summary>
        /// Get the list of selected service template names.
        /// </summary>
        public List<string> SelectedTemplateNames
        {
            get
            {
                return _templates
                    .Where(t => t.IsSelected)
                    .Select(t => t.Name)
                    .ToList();
            }
        }

        /// <summary>
        /// True if user clicked OK, false if cancelled.
        /// </summary>
        public bool DialogResultOk { get; private set; }

        /// <summary>
        /// Default constructor: all templates selected.
        /// </summary>
        public ServiceTemplateSelectionWindow()
        {
            InitializeComponent();
            _preSelectedNames = null;
            LoadTemplates();
        }

        /// <summary>
        /// Constructor with pre-selected template names.
        /// Only the named templates will be initially selected.
        /// If null or empty, defaults to all selected.
        /// </summary>
        public ServiceTemplateSelectionWindow(IEnumerable<string> preSelectedNames)
        {
            InitializeComponent();
            _preSelectedNames = preSelectedNames;
            LoadTemplates();
        }

        private void LoadTemplates()
        {
            _templates = new ObservableCollection<TemplateItem>();

            try
            {
                var preSelectSet = _preSelectedNames != null
                    ? new HashSet<string>(_preSelectedNames, StringComparer.OrdinalIgnoreCase)
                    : null;

                foreach (var template in FabDB.ServiceTemplates)
                {
                    bool isSelected = preSelectSet == null || preSelectSet.Count == 0
                        ? true
                        : preSelectSet.Contains(template.Name);

                    _templates.Add(new TemplateItem
                    {
                        Name = template.Name,
                        IsSelected = isSelected
                    });
                }

                templatesListBox.ItemsSource = _templates;
                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading service templates: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSelectedCount()
        {
            int count = _templates.Count(t => t.IsSelected);
            txtSelectedCount.Text = $"{count} template(s) selected";
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
            foreach (var template in _templates)
            {
                template.IsSelected = true;
            }
            UpdateSelectedCount();
        }

        private void btnDeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var template in _templates)
            {
                template.IsSelected = false;
            }
            UpdateSelectedCount();
        }

        private void Template_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateSelectedCount();
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            int selectedCount = _templates.Count(t => t.IsSelected);

            if (selectedCount == 0)
            {
                MessageBox.Show("Please select at least one service template to export.",
                    "No Templates Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
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
    /// ViewModel for a service template item in the selection list.
    /// </summary>
    public class TemplateItem : INotifyPropertyChanged
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
