using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabricationSample.Data;
using FabricationSample.FunctionExamples;
using FabricationSample.Manager;
using FabricationSample.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Interaction logic for DatabaseEditor.xaml
    /// </summary>
    public partial class DatabaseEditor : UserControl
    {
        #region Private Members

        ObservableCollection<MapProdGridItem> _lstMapProdItems;
        bool isProductDataLoaded;
        int noOfProductDefinitions;
        int _staticColumnCount;

        #endregion

        #region MapProd

        private async Task BindMapProdDataAsync()
        {
            if (_lstMapProdItems == null)
            {
                _lstMapProdItems = new ObservableCollection<MapProdGridItem>();

                noOfProductDefinitions = ProductDatabase.ProductDefinitions.Count;

                Task getMapProdData = Task.Run(() =>
                {
                    int step = 0;
                    foreach (ProductDefinition def in ProductDatabase.ProductDefinitions)
                    {
                        _lstMapProdItems.Add(new MapProdGridItem(def));
                        step++;
                    }
                });

                await getMapProdData;

                MapProdGridItem createItem = _lstMapProdItems[0];

                for (int i = 0; i < createItem.Count; i++)
                {
                    AddMapProdColumn(createItem[i].Name, "[" + i + "].Value");
                }

                dgMapprod.ItemsSource = _lstMapProdItems;
            }
        }

        private void AddMapProdColumn(string headerName, string bindingName)
        {
            DataGridTextColumn textColumn = new DataGridTextColumn();
            textColumn.Header = headerName;
            Binding binding = new Binding(bindingName);
            binding.Mode = BindingMode.TwoWay;
            binding.UpdateSourceTrigger = UpdateSourceTrigger.LostFocus;
            textColumn.Binding = binding;
            dgMapprod.Columns.Add(textColumn);
        }

        private void btnLoadMapProd_Click(object sender, RoutedEventArgs e)
        {
            ReloadProductData();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            DBOperationResult result = ProductDatabase.Save();
            MessageBoxImage messageImage = MessageBoxImage.Error;

            if (result.Status == ResultStatus.Succeeded)
                messageImage = MessageBoxImage.Information;

            MessageBox.Show(result.Message, "Save Product Database", MessageBoxButton.OK, messageImage);

        }

        private void dgMapprod_Loaded(object sender, RoutedEventArgs e)
        {
            if (!isProductDataLoaded)
            {
                dgMapprod.ItemsSource = new ObservableCollection<ProductDefinition>(ProductDatabase.ProductDefinitions);
                _staticColumnCount = dgMapprod.Columns.Count;
                AddSupplierColumns();
                isProductDataLoaded = true;
            }
            CreateProductGroupFilters();
            CreateSupplierFilters();
            //New Product Definition Combo
            cmbNewProductDefinitionGroup.ItemsSource = new ObservableCollection<ProductGroup>(ProductDatabase.ProductGroups);
            cmbNewProductDefinitionGroup.DisplayMemberPath = "Name";
        }

        /// <summary>
        /// Add dynamic columns for each product supplier.
        /// Each supplier gets its own column showing the supplier ID for each product.
        /// </summary>
        private void AddSupplierColumns()
        {
            var converter = (IValueConverter)FindResource("SupplierIdsConverter");

            foreach (var supplier in ProductDatabase.Suppliers)
            {
                var col = new DataGridTextColumn
                {
                    Header = supplier.Name,
                    IsReadOnly = true,
                    Binding = new Binding("SupplierIds")
                    {
                        Converter = converter,
                        ConverterParameter = supplier.Name,
                        Mode = BindingMode.OneWay
                    }
                };
                dgMapprod.Columns.Add(col);
            }
        }

        /// <summary>
        /// Remove dynamic supplier columns (keeps static XAML-defined columns).
        /// </summary>
        private void RemoveSupplierColumns()
        {
            while (dgMapprod.Columns.Count > _staticColumnCount)
                dgMapprod.Columns.RemoveAt(dgMapprod.Columns.Count - 1);
        }

        /// <summary>
        /// Reload product data and rebuild dynamic supplier columns.
        /// </summary>
        private void ReloadProductData()
        {
            RemoveSupplierColumns();
            dgMapprod.ItemsSource = new ObservableCollection<ProductDefinition>(ProductDatabase.ProductDefinitions);
            AddSupplierColumns();
            CreateProductGroupFilters();
            CreateSupplierFilters();
        }

        private void CreateProductGroupFilters()
        {
            //Filter Combo
            List<string> lstProductGroups = new List<string>();
            lstProductGroups.Add("None");
            lstProductGroups.AddRange(ProductDatabase.ProductGroups.Select(x => x.Name));
            cmbMapProdFilterGroup.ItemsSource = new ObservableCollection<string>(lstProductGroups);
            cmbMapProdFilterGroup.SelectedIndex = 0;
        }

        /// <summary>
        /// Populate the supplier filter dropdown.
        /// </summary>
        private void CreateSupplierFilters()
        {
            var supplierNames = new List<string> { "None" };
            supplierNames.AddRange(ProductDatabase.Suppliers.Select(s => s.Name));
            cmbMapProdFilterSupplier.ItemsSource = new ObservableCollection<string>(supplierNames);
            cmbMapProdFilterSupplier.SelectedIndex = 0;
        }

        private void cmbMapProdFilterGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbMapProdFilterGroup.SelectedItem != null)
            {
                string filterGroup = cmbMapProdFilterGroup.SelectedItem as string;
                if (filterGroup == "None")
                    dgMapprod.ItemsSource = new ObservableCollection<ProductDefinition>(ProductDatabase.ProductDefinitions);
                else
                    dgMapprod.ItemsSource = new ObservableCollection<ProductDefinition>(ProductDatabase.ProductDefinitions.Where(x => x.Group != null && x.Group.Name == filterGroup));
            }
        }

        /// <summary>
        /// Filter products by supplier - shows only products that have a non-empty supplier ID for the selected supplier.
        /// </summary>
        private void cmbMapProdFilterSupplier_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbMapProdFilterSupplier.SelectedItem == null) return;

            string filterSupplier = cmbMapProdFilterSupplier.SelectedItem as string;
            if (filterSupplier == "None")
            {
                dgMapprod.ItemsSource = new ObservableCollection<ProductDefinition>(ProductDatabase.ProductDefinitions);
            }
            else
            {
                var filtered = ProductDatabase.ProductDefinitions.Where(def =>
                {
                    if (def.SupplierIds == null) return false;
                    return def.SupplierIds.Any(s =>
                        s.ProductSupplier != null &&
                        s.ProductSupplier.Name == filterSupplier &&
                        !string.IsNullOrWhiteSpace(s.Id));
                });
                dgMapprod.ItemsSource = new ObservableCollection<ProductDefinition>(filtered);
            }
        }

        private void btnFilterMapProdById_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtFilterMapProdById.Text))
            {
                dgMapprod.ItemsSource = new ObservableCollection<ProductDefinition>(ProductDatabase.ProductDefinitions.Where(x => x.Id == txtFilterMapProdById.Text.Trim()));
            }
        }

        private void dgMapprod_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender != null)
            {
                DataGrid grid = sender as DataGrid;
                if (grid != null && grid.SelectedItems != null && grid.SelectedItems.Count == 1)
                {
                    try
                    {
                        AddEditProductDBWindow win = new AddEditProductDBWindow((ProductDefinition)dgMapprod.Items[grid.SelectedIndex], true);
                        win.ShowDialog();
                        dgMapprod.Items.Refresh();
                    }
                    catch (Exception)
                    {
                        System.Windows.MessageBox.Show("Error Loading Product Entry", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    }
                }
            }
        }

        private void btnCreateProductDefinition_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtNewProductDefinition.Text) && cmbNewProductDefinitionGroup.SelectedItem != null)
            {
                ProductGroup newGroup = cmbNewProductDefinitionGroup.SelectedItem as ProductGroup;

                if (newGroup != null)
                {
                    ProductDefinition def = ProductDatabase.CreateProductDefinition(txtNewProductDefinition.Text, newGroup);

                    if (def != null)
                    {
                        AddEditProductDBWindow win = new AddEditProductDBWindow(def, false);
                        win.ShowDialog();
                        dgMapprod.Items.Refresh();
                    }
                    else
                        MessageBox.Show("Error creating Product Definition\nThe Id may already exist", "New Product Definition", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        private void btnCreateProductGroup_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtNewProductGroup.Text))
            {
                DBOperationResult result = ProductDatabase.CreateProductGroup(txtNewProductGroup.Text.Trim());

                MessageBoxImage messageImage = MessageBoxImage.Error;

                if (result.Status == ResultStatus.Succeeded)
                {
                    CreateProductGroupFilters();
                    cmbNewProductDefinitionGroup.Items.Refresh();
                    messageImage = MessageBoxImage.Information;
                }

                MessageBox.Show(result.Message, "Create Product Group", MessageBoxButton.OK, messageImage);
            }
        }

        private void btnCreateProductSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtNewProductSupplier.Text))
            {
                DBOperationResult result = ProductDatabase.CreateProductSupplier(txtNewProductSupplier.Text.Trim());

                MessageBoxImage messageImage = MessageBoxImage.Error;

                if (result.Status == ResultStatus.Succeeded)
                {
                    messageImage = MessageBoxImage.Information;
                    // Reload to show the new supplier column
                    ReloadProductData();
                }

                MessageBox.Show(result.Message, "Create Product Supplier", MessageBoxButton.OK, messageImage);
            }
        }

        #endregion
    }
}
