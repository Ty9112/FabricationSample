using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using FabricationSample.ProfileCopy.Models;

namespace FabricationSample.ProfileCopy.Windows
{
    public partial class DataTypePreviewWindow : Window
    {
        private List<PreviewItem> _items;

        /// <summary>
        /// After the window closes with DialogResult=true, this contains
        /// the names the user selected. Null means cancelled.
        /// </summary>
        public List<string> SelectedItemNames { get; private set; }

        /// <param name="dataTypeName">Display name, e.g. "Services"</param>
        /// <param name="profileName">Source profile name, e.g. "Global"</param>
        /// <param name="manifestItems">Items from the manifest for this data type</param>
        /// <param name="supportsCleanup">If false, shows the Tier 2 warning banner</param>
        /// <param name="previousSelection">Previously selected names (null = all selected)</param>
        public DataTypePreviewWindow(
            string dataTypeName,
            string profileName,
            List<ManifestItem> manifestItems,
            bool supportsCleanup,
            List<string> previousSelection)
        {
            InitializeComponent();

            txtHeader.Text = $"{dataTypeName} in {profileName} ({manifestItems.Count} items)";
            Title = $"{dataTypeName} - {profileName}";

            if (!supportsCleanup)
                pnlTier2Warning.Visibility = Visibility.Visible;

            // Build PreviewItem list
            var previousSet = previousSelection != null
                ? new HashSet<string>(previousSelection)
                : null;

            _items = manifestItems.Select(m => new PreviewItem
            {
                Name = m.Name,
                GroupName = m.Group ?? "(ungrouped)",
                IsSelected = previousSet == null || previousSet.Contains(m.Name)
            }).ToList();

            lstItems.ItemsSource = _items;

            // Group by GroupName
            var view = CollectionViewSource.GetDefaultView(_items);
            view.GroupDescriptions.Add(new PropertyGroupDescription("GroupName"));
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items)
                item.IsSelected = true;
            lstItems.Items.Refresh();
        }

        private void btnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items)
                item.IsSelected = false;
            lstItems.Items.Refresh();
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            SelectedItemNames = _items.Where(i => i.IsSelected).Select(i => i.Name).ToList();
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    public class PreviewItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; }
        public string GroupName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
