using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Universal search/filter functionality for all DataGrid tabs.
    /// </summary>
    public partial class DatabaseEditor : UserControl
    {
        #region Search/Filter

        /// <summary>
        /// Applies a text filter to the specified DataGrid's ItemsSource using ICollectionView.
        /// Checks all visible column values via ToString().
        /// </summary>
        private void ApplySearchFilter(DataGrid dataGrid, string searchText)
        {
            if (dataGrid == null || dataGrid.ItemsSource == null)
                return;

            var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
            if (view == null)
                return;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                view.Filter = null;
                return;
            }

            string lowerSearch = searchText.ToLowerInvariant();

            view.Filter = item =>
            {
                if (item == null) return false;

                // Check all public properties via reflection
                var properties = item.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    try
                    {
                        var value = prop.GetValue(item);
                        if (value != null)
                        {
                            string strValue = value.ToString();
                            if (!string.IsNullOrEmpty(strValue) && strValue.ToLowerInvariant().Contains(lowerSearch))
                                return true;
                        }
                    }
                    catch
                    {
                        // Skip properties that throw
                    }
                }
                return false;
            };
        }

        /// <summary>
        /// Universal TextChanged handler for search boxes.
        /// The TextBox.Tag must contain the target DataGrid name (set via x:Reference in XAML).
        /// </summary>
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            var dataGrid = textBox.Tag as DataGrid;
            if (dataGrid != null)
            {
                ApplySearchFilter(dataGrid, textBox.Text);
                return;
            }

            // Fallback: resolve by name convention
            string name = textBox.Name;
            if (string.IsNullOrEmpty(name)) return;

            // Map search box names to DataGrids
            var mapping = GetSearchDataGridMapping();
            if (mapping.ContainsKey(name))
            {
                var dg = FindName(mapping[name]) as DataGrid;
                if (dg != null)
                    ApplySearchFilter(dg, textBox.Text);
            }
        }

        /// <summary>
        /// Universal Clear button handler for search boxes.
        /// The Button.Tag must contain the target search TextBox (set via x:Reference in XAML).
        /// </summary>
        private void btnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;

            var textBox = button.Tag as TextBox;
            if (textBox != null)
            {
                textBox.Text = string.Empty;
                return;
            }
        }

        private Dictionary<string, string> GetSearchDataGridMapping()
        {
            return new Dictionary<string, string>
            {
                { "txtSearchJobItems", "dgJobItems" },
                { "txtSearchProductDb", "dgMapprod" },
                { "txtSearchSupplierDiscounts", "dgSupplierDiscounts" },
                { "txtSearchJobCustomData", "dgJobCustomData" },
                { "txtSearchItemCustomData", "dgCustomData" },
                { "txtSearchItemStatuses", "dgItemStatuses" },
                { "txtSearchJobStatuses", "dgJobStatuses" },
                { "txtSearchSpecifications", "dgSpecifications" },
            };
        }

        #endregion
    }
}
