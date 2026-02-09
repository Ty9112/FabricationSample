using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Autodesk.Fabrication;
using Autodesk.Fabrication.Results;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Units;
using Autodesk.Fabrication.LineWeights;

using FabricationSample.FunctionExamples;
using FabricationSample.Data;
using FabricationSample.Services.Export;
using FabricationSample.Services.Import;
using System.ComponentModel;
using System.IO;

using FabricationSample.Manager;

namespace FabricationSample.UserControls.ServiceEditor
{
    /// <summary>
    /// Interaction logic for ServiceEditor.xaml
    /// </summary>
    public partial class ServiceEditor : UserControl
    {
        #region Private Members

        ObservableCollection<ServiceType> _lstServiceTypes;

        #endregion

        #region ctor

        public ServiceEditor()
        {
            InitializeComponent();
        }

        #endregion


        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            txtServiceGroup.Text += FabricationManager.CurrentService.Group;
            txtServiceName.Text += FabricationManager.CurrentService.Name;

            // Load all services into the dropdown
            LoadServiceSelector();
        }

        private void LoadServiceSelector()
        {
            // Get all services sorted by group then name
            var services = Database.Services.OrderBy(s => s.Group).ThenBy(s => s.Name).ToList();

            // Populate the listbox
            lstServiceSelector.ItemsSource = services;

            // Select the current service by default
            if (FabricationManager.CurrentService != null)
            {
                var currentService = services.FirstOrDefault(s => s.Id == FabricationManager.CurrentService.Id);
                if (currentService != null)
                {
                    lstServiceSelector.SelectedItem = currentService;
                }
            }
        }


        private void dgServiceEntries_Loaded(object sender, RoutedEventArgs e)
        {
            LoadServiceEntries();
        }

        private void LoadServiceTypes()
        {
            _lstServiceTypes = new ObservableCollection<ServiceType>(Database.ServiceTypes.OrderBy(x => x.Description).ToList());
        }

        private void LoadServiceEntries()
        {
            if (_lstServiceTypes == null)
                LoadServiceTypes();

            ObservableCollection<ServiceEntryMapper> entries = new ObservableCollection<ServiceEntryMapper>();

            // Get selected services from the listbox
            if (lstServiceSelector.SelectedItems.Count > 0)
            {
                foreach (var selectedItem in lstServiceSelector.SelectedItems)
                {
                    var service = selectedItem as Service;
                    if (service != null && service.ServiceEntries != null)
                    {
                        foreach (ServiceEntry entry in service.ServiceEntries)
                        {
                            entries.Add(new ServiceEntryMapper(entry, service.Name));
                        }
                    }
                }
            }
            else if (FabricationManager.CurrentService != null)
            {
                // Fallback: if no services selected, show current service
                foreach (ServiceEntry entry in FabricationManager.CurrentService.ServiceEntries)
                {
                    entries.Add(new ServiceEntryMapper(entry, FabricationManager.CurrentService.Name));
                }
            }

            dgServiceEntries.ItemsSource = entries;
        }

        private void newServiceEntry_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.MessageBox.Show(
                "Adding service entries is not available in multi-select view mode.\n\n" +
                "This view is for browsing entries across multiple services.\n" +
                "To add or edit entries, use the Database Editor > Services tab.",
                "Read-Only View",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void deleteServiceEntry_Click(object sender, RoutedEventArgs e)
        {
            if (dgServiceEntries.SelectedItem == null)
                return;

            var entry = dgServiceEntries.SelectedItem as ServiceEntryMapper;
            if (entry == null)
                return;

            if (MessageBox.Show("Confirm to Delete Service Entry", "Delete Service Entry",
        MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                if (FabricationAPIExamples.DeleteServiceEntry(FabricationManager.CurrentService, entry.ServiceEntry))
                {
                    LoadServiceEntries();
                }
            }

        }

        private void ServiceTypeComboChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;

            var selectedType = e.AddedItems[0] as ServiceType;
            if (selectedType == null)
                return;

            var selectedRow = dgServiceEntries.SelectedItem as ServiceEntryMapper;
            if (selectedRow == null)
                return;

            selectedRow.ServiceTypeId = selectedType.Id;
            selectedRow.ServiceTypeDescription = selectedType.Description;
        }

        private void btnUpdateServiceEntries_Click(object sender, RoutedEventArgs e)
        {
            if (dgServiceEntries == null || FabricationManager.CurrentService == null)
                return;

            foreach (ServiceEntryMapper mapper in dgServiceEntries.ItemsSource)
            {
                ServiceEntry entry = mapper.ServiceEntry;
                if (mapper.ServiceTypeId != entry.ServiceType.Id)
                {
                    // locate the ServiceType by id
                    ServiceType st = _lstServiceTypes.ToList().Find(x => x.Id == mapper.ServiceTypeId);
                    if (st != null)
                        entry.ServiceType = st;
                }

                if (!mapper.LayerTag1.Equals(entry.LayerTag1))
                    entry.LayerTag1 = mapper.LayerTag1;

                if (!mapper.LayerTag2.Equals(entry.LayerTag2))
                    entry.LayerTag2 = mapper.LayerTag2;

                if (mapper.LayerColor != entry.LayerColor)
                    entry.LayerColor = mapper.LayerColor;

                if (!mapper.LevelBlock.Equals(entry.LevelBlock))
                    entry.LevelBlock = mapper.LevelBlock;

                if (!mapper.SizeBlock.Equals(entry.SizeBlock))
                    entry.SizeBlock = mapper.SizeBlock;

                if (mapper.LineWeight != entry.LineWeight.LineWeightValue)
                    entry.SetLineWeightValue(mapper.LineWeight);

                if (mapper.IncludesInsulation != entry.IncludesInsulation)
                    entry.IncludesInsulation = mapper.IncludesInsulation;
            }

            dgServiceEntries.UpdateLayout();

            if (Database.SaveServices().Status == ResultStatus.Succeeded)
                MessageBox.Show("Service Entries Saved", "Service Entries", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Service Entries could not be Saved", "Service Entries", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        private Dictionary<LineWeight.LineWeightEnum, string> LineWeightValues()
        {
            var values = Enum.GetValues(typeof(LineWeight.LineWeightEnum)).Cast<LineWeight.LineWeightEnum>().ToList();
            var dictionary = new Dictionary<LineWeight.LineWeightEnum, string>();

            Autodesk.Fabrication.Units.MeasurementUnits units = Database.Units;

            foreach (LineWeight.LineWeightEnum lineweight in values)
            {
                string description = LineWeight.GetLineWeightDescription(units, lineweight);
                dictionary.Add(lineweight, description);
            }

            return dictionary;
        }

        private ObservableCollection<int> LayerColors()
        {
            var colors = new ObservableCollection<int>();
            for (int i = 0; i < 256; i++)
                colors.Add(i);

            return colors;
        }

        /// <summary>
        /// Export service entries for selected services to CSV.
        /// </summary>
        private void btnExportServiceEntries_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get currently selected service names from the multi-select ListBox
                var preSelectedNames = lstServiceSelector.SelectedItems
                    .Cast<Service>()
                    .Select(s => s.Name)
                    .ToList();

                // Show service selection dialog, pre-selecting current UI selection
                var selectionWindow = new ServiceSelectionWindow(
                    preSelectedNames.Count > 0 ? preSelectedNames : null);
                selectionWindow.ShowDialog();

                if (!selectionWindow.DialogResultOk)
                {
                    return; // User cancelled
                }

                var selectedServices = selectionWindow.SelectedServiceNames;

                if (selectedServices == null || selectedServices.Count == 0)
                {
                    System.Windows.MessageBox.Show("No services selected.", "Export Cancelled",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Prompt for file location
                using (var saveDialog = new System.Windows.Forms.SaveFileDialog())
                {
                    saveDialog.Title = "Export Service Entries";
                    saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    saveDialog.DefaultExt = "csv";
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                    // Create filename based on service selection
                    string filePrefix;
                    if (selectedServices.Count == 1)
                    {
                        // Single service: extract text from brackets [...]
                        string serviceName = selectedServices[0];
                        string bracketContent = ExtractBracketContent(serviceName);

                        if (!string.IsNullOrEmpty(bracketContent))
                        {
                            filePrefix = bracketContent.Replace(" ", "_");
                        }
                        else
                        {
                            // No brackets found, use full name
                            filePrefix = serviceName.Replace(" ", "_");
                        }
                    }
                    else
                    {
                        // Multiple services: use "MultipleServices"
                        filePrefix = "MultipleServices";
                    }

                    saveDialog.FileName = $"{filePrefix}_Entries_{timestamp}.csv";

                    if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string outputFile = saveDialog.FileName;

                        if (string.IsNullOrEmpty(outputFile))
                        {
                            System.Windows.MessageBox.Show("No file selected.", "Export Cancelled",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Initialize export service
                        var exportService = new ServiceEntriesExportService
                        {
                            SelectedServiceNames = selectedServices
                        };

                        // Export synchronously
                        var options = new ExportOptions { IncludeHeader = true };
                        var result = exportService.Export(outputFile, options);

                        if (result.IsSuccess)
                        {
                            var response = System.Windows.MessageBox.Show(
                                $"Service entries exported successfully!\n\n" +
                                $"File: {outputFile}\n" +
                                $"Rows: {result.RowCount}\n" +
                                $"Services: {selectedServices.Count}\n\n" +
                                $"Open file location?",
                                "Export Complete",
                                MessageBoxButton.YesNo, MessageBoxImage.Information);

                            if (response == MessageBoxResult.Yes)
                            {
                                System.Diagnostics.Process.Start("explorer.exe",
                                    $"/select,\"{outputFile}\"");
                            }
                        }
                        else
                        {
                            System.Windows.MessageBox.Show($"Export failed: {result.ErrorMessage}",
                                "Export Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error exporting service entries: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Extract text from within square brackets [].
        /// </summary>
        private string ExtractBracketContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            int startIndex = text.IndexOf('[');
            int endIndex = text.IndexOf(']');

            if (startIndex >= 0 && endIndex > startIndex)
            {
                return text.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
            }

            return string.Empty;
        }

        /// <summary>
        /// Handle service selection change from listbox (supports multi-select)
        /// </summary>
        private void lstServiceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Reload entries for all selected services
            LoadServiceEntries();

            // Update header to show selected count
            if (lstServiceSelector.SelectedItems.Count > 0)
            {
                if (lstServiceSelector.SelectedItems.Count == 1)
                {
                    var service = lstServiceSelector.SelectedItems[0] as Service;
                    if (service != null)
                    {
                        txtServiceGroup.Text = "Service Group: " + service.Group;
                        txtServiceName.Text = "Service Name: " + service.Name;
                    }
                }
                else
                {
                    txtServiceGroup.Text = "Service Group: Multiple";
                    txtServiceName.Text = $"Service Name: {lstServiceSelector.SelectedItems.Count} services selected";
                }
            }
        }

        /// <summary>
        /// Select all services in the listbox
        /// </summary>
        private void btnSelectAllServices_Click(object sender, RoutedEventArgs e)
        {
            lstServiceSelector.SelectAll();
        }

        /// <summary>
        /// Deselect all services in the listbox
        /// </summary>
        private void btnDeselectAllServices_Click(object sender, RoutedEventArgs e)
        {
            lstServiceSelector.UnselectAll();
        }

        /// <summary>
        /// Import service entries from CSV.
        /// </summary>
        private void btnImportServiceEntries_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var fileDialog = new System.Windows.Forms.OpenFileDialog())
                {
                    fileDialog.Title = "Select CSV file to import";
                    fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                    fileDialog.CheckFileExists = true;
                    fileDialog.CheckPathExists = true;

                    if (fileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    string importFile = fileDialog.FileName;
                    if (string.IsNullOrEmpty(importFile))
                        return;

                    // Show column mapping dialog
                    var requiredFields = new[] { "Service Name", "Service Type" };
                    var optionalFields = new[] { "Layer Tag 1", "Layer Tag 2", "Layer Color", "Level Block", "Size Block", "Includes Insulation", "Line Weight" };

                    var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    // Create import options with column mapping
                    var options = new ImportOptions
                    {
                        HasHeaderRow = true,
                        UpdateExisting = true,
                        StopOnFirstError = false
                    };

                    if (mappingWindow.ResultMapping != null)
                        options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

                    // Create import service
                    var importService = new ServiceEntriesImportService();

                    // Validate
                    var validation = importService.Validate(importFile, options);
                    if (!validation.IsValid)
                    {
                        string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
                        foreach (var error in validation.Errors.Take(5))
                            errorMsg += $"  {error}\n";
                        if (validation.Errors.Count > 5)
                            errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

                        System.Windows.MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Show warnings if any
                    if (validation.Warnings.Count > 0)
                    {
                        string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
                        foreach (var warning in validation.Warnings.Take(5))
                            warnMsg += $"  {warning}\n";
                        if (validation.Warnings.Count > 5)
                            warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
                        warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

                        if (System.Windows.MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }

                    // Preview
                    var preview = importService.Preview(importFile, options);
                    if (preview.IsSuccess)
                    {
                        string previewMsg = $"Ready to import service entries:\n\n" +
                                          $"Updated entries: {preview.UpdatedRecordCount}\n" +
                                          $"Skipped entries: {preview.SkippedRecordCount}\n" +
                                          $"Total changes: {preview.Changes.Count}\n\n" +
                                          $"Continue?";

                        if (System.Windows.MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
                            "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Import
                    var result = importService.Import(importFile, options);

                    if (result.IsSuccess)
                    {
                        string successMsg = $"Import complete.\n\n" +
                                          $"Updated: {result.ImportedCount} entries\n";
                        if (result.SkippedCount > 0)
                            successMsg += $"Skipped: {result.SkippedCount} entries\n";
                        successMsg += "\nSave services to persist changes.";

                        System.Windows.MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Refresh the grid
                        LoadServiceEntries();
                    }
                    else if (result.WasCancelled)
                    {
                        System.Windows.MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Import failed: {result.ErrorMessage}",
                            "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error during import: {ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            FabricationManager.CurrentService = null;
            FabricationManager.ParentWindow.LoadDBEditorControl("Services");
        }

        #region Service Information Tab

        /// <summary>
        /// Load the Service Information tab when selected.
        /// </summary>
        private void tbiServiceInfo_Loaded(object sender, RoutedEventArgs e)
        {
            LoadServiceInfo();
        }

        /// <summary>
        /// Load service information into the form fields.
        /// </summary>
        private void LoadServiceInfo()
        {
            if (FabricationManager.CurrentService == null)
                return;

            var service = FabricationManager.CurrentService;

            // Populate fields
            txtInfoServiceName.Text = service.Name;
            txtInfoServiceGroup.Text = service.Group;

            // Load service templates
            var templates = Database.ServiceTemplates.OrderBy(t => t.Name).ToList();
            cmbInfoServiceTemplate.ItemsSource = templates;
            if (service.ServiceTemplate != null)
            {
                cmbInfoServiceTemplate.SelectedItem = templates.FirstOrDefault(t => t.Name == service.ServiceTemplate.Name);
            }

            // Load specifications
            var specs = Database.Specifications.OrderBy(s => s.Group).ThenBy(s => s.Name).ToList();
            cmbInfoSpecification.ItemsSource = specs;

            if (service.Specification != null)
            {
                chkInfoSpecNotSet.IsChecked = false;
                cmbInfoSpecification.IsEnabled = true;
                cmbInfoSpecification.SelectedItem = specs.FirstOrDefault(s => s.Name == service.Specification.Name && s.Group == service.Specification.Group);
            }
            else
            {
                chkInfoSpecNotSet.IsChecked = true;
                cmbInfoSpecification.IsEnabled = false;
                cmbInfoSpecification.SelectedItem = null;
            }
        }

        /// <summary>
        /// Handle service template selection change.
        /// </summary>
        private void cmbInfoServiceTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Just track selection, changes are applied on save
        }

        /// <summary>
        /// Handle specification selection change.
        /// </summary>
        private void cmbInfoSpecification_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Just track selection, changes are applied on save
        }

        /// <summary>
        /// Handle "Not Set" checkbox for specification.
        /// </summary>
        private void chkInfoSpecNotSet_Click(object sender, RoutedEventArgs e)
        {
            if (chkInfoSpecNotSet.IsChecked == true)
            {
                cmbInfoSpecification.IsEnabled = false;
                cmbInfoSpecification.SelectedItem = null;
            }
            else
            {
                cmbInfoSpecification.IsEnabled = true;
            }
        }

        /// <summary>
        /// Save service information changes.
        /// </summary>
        private void btnSaveServiceInfo_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentService == null)
            {
                MessageBox.Show("No service selected.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var service = FabricationManager.CurrentService;

            try
            {
                // Update name if changed
                if (!string.IsNullOrWhiteSpace(txtInfoServiceName.Text) && txtInfoServiceName.Text != service.Name)
                {
                    service.Name = txtInfoServiceName.Text.Trim();
                }

                // Update group if changed
                if (txtInfoServiceGroup.Text != service.Group)
                {
                    service.Group = txtInfoServiceGroup.Text.Trim();
                }

                // Update service template if changed
                if (cmbInfoServiceTemplate.SelectedItem is ServiceTemplate selectedTemplate)
                {
                    if (service.ServiceTemplate == null || selectedTemplate.Name != service.ServiceTemplate.Name)
                    {
                        service.ServiceTemplate = selectedTemplate;
                    }
                }

                // Update specification
                if (chkInfoSpecNotSet.IsChecked == true)
                {
                    service.Specification = null;
                }
                else if (cmbInfoSpecification.SelectedItem is Specification selectedSpec)
                {
                    service.Specification = selectedSpec;
                }

                // Save to database
                var result = Database.SaveServices();
                if (result.Status == ResultStatus.Succeeded)
                {
                    // Update header text
                    txtServiceGroup.Text = "Service Group: " + service.Group;
                    txtServiceName.Text = "Service Name: " + service.Name;

                    MessageBox.Show("Service information saved successfully.", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to save service information.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving service information: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Button Mappings Tab

        /// <summary>
        /// Load the Button Mappings tab when selected.
        /// </summary>
        private void tbiButtonMappings_Loaded(object sender, RoutedEventArgs e)
        {
            LoadButtonMappings();
        }

        /// <summary>
        /// Load button mappings for all selected services.
        /// </summary>
        private void LoadButtonMappings()
        {
            var mappings = new ObservableCollection<ServiceButtonMappingGridItem>();

            // Get services to display
            List<Service> servicesToDisplay = new List<Service>();

            if (lstServiceSelector.SelectedItems.Count > 0)
            {
                foreach (var item in lstServiceSelector.SelectedItems)
                {
                    if (item is Service svc)
                        servicesToDisplay.Add(svc);
                }
            }
            else if (FabricationManager.CurrentService != null)
            {
                servicesToDisplay.Add(FabricationManager.CurrentService);
            }

            foreach (var service in servicesToDisplay)
            {
                var template = service.ServiceTemplate;
                if (template == null)
                    continue;

                foreach (ServiceTab tab in template.ServiceTabs)
                {
                    foreach (ServiceButton button in tab.ServiceButtons)
                    {
                        var itemPaths = new List<string>();
                        foreach (ServiceButtonItem buttonItem in button.ServiceButtonItems)
                        {
                            if (!string.IsNullOrEmpty(buttonItem.ItemPath))
                            {
                                itemPaths.Add(buttonItem.ItemPath);
                            }
                        }

                        var mappingItem = new ServiceButtonMappingGridItem
                        {
                            TabName = tab.Name,
                            ButtonName = button.Name,
                            ButtonCode = button.ButtonCode ?? "",
                            ItemCount = button.ServiceButtonItems.Count,
                            ItemPaths = string.Join("; ", itemPaths)
                        };

                        mappings.Add(mappingItem);
                    }
                }
            }

            dgButtonMappings.ItemsSource = mappings;
        }

        #endregion

        /// <summary>
        /// Handle mouse wheel scrolling for ScrollViewers
        /// </summary>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                // Calculate scroll amount (3 lines per wheel notch)
                double scrollAmount = -e.Delta / 3.0;

                // Scroll the viewer
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);

                // Mark event as handled to prevent parent controls from scrolling
                e.Handled = true;
            }
        }
    }
}
