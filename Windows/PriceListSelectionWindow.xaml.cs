using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Fabrication.DB;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample
{
    /// <summary>
    /// Window for selecting a Supplier Group and Price List for import.
    /// Cascading selection: choosing a group populates its ProductId-type price lists.
    /// </summary>
    public partial class PriceListSelectionWindow : Window
    {
        /// <summary>
        /// The selected supplier group.
        /// </summary>
        public SupplierGroup SelectedSupplierGroup { get; private set; }

        /// <summary>
        /// The selected price list (ProductId type only).
        /// </summary>
        public PriceList SelectedPriceList { get; private set; }

        /// <summary>
        /// True if user clicked OK, false if cancelled.
        /// </summary>
        public bool DialogResultOk { get; private set; }

        public PriceListSelectionWindow()
        {
            InitializeComponent();
            LoadSupplierGroups();
        }

        private void LoadSupplierGroups()
        {
            var groups = FabDB.SupplierGroups.OrderBy(sg => sg.Name).ToList();
            cboSupplierGroup.ItemsSource = groups;
        }

        private void cboSupplierGroup_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            cboPriceList.ItemsSource = null;
            txtEntryCount.Text = "";
            btnOk.IsEnabled = false;

            var group = cboSupplierGroup.SelectedItem as SupplierGroup;
            if (group == null) return;

            // Filter to ProductId-type price lists only
            var productIdLists = group.PriceLists
                .OfType<PriceList>()
                .Where(pl => pl.Type == TableType.ProductId)
                .OrderBy(pl => pl.Name)
                .ToList();

            cboPriceList.ItemsSource = productIdLists;

            if (productIdLists.Count == 0)
            {
                txtEntryCount.Text = "(no Product Id lists)";
            }
        }

        private void cboPriceList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var priceList = cboPriceList.SelectedItem as PriceList;
            btnOk.IsEnabled = priceList != null;

            if (priceList != null)
            {
                int count = priceList.Products.Count;
                txtEntryCount.Text = $"{count} entries";
            }
            else
            {
                txtEntryCount.Text = "";
            }
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

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            SelectedSupplierGroup = cboSupplierGroup.SelectedItem as SupplierGroup;
            SelectedPriceList = cboPriceList.SelectedItem as PriceList;

            if (SelectedSupplierGroup == null || SelectedPriceList == null)
            {
                MessageBox.Show(
                    "Please select both a supplier group and a price list.",
                    "Selection Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
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
}
