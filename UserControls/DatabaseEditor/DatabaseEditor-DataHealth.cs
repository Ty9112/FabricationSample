using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using Microsoft.Win32;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Data Health / Validation Dashboard.
    /// Runs validation checks against the Fabrication database and displays results.
    /// </summary>
    public partial class DatabaseEditor : UserControl
    {
        #region Data Health

        private List<ValidationResult> _validationResults;
        private List<ValidationCategoryGroup> _categoryGroups;
        private bool _isRunningHealthChecks;

        private class ValidationResult
        {
            public string Category { get; set; }
            public string Severity { get; set; }
            public string Message { get; set; }
            public string Details { get; set; }
            public string SeverityIcon
            {
                get
                {
                    switch (Severity)
                    {
                        case "Error": return "!";
                        case "Warning": return "~";
                        case "Info": return "i";
                        default: return "";
                    }
                }
            }
        }

        private class ValidationCategoryGroup : INotifyPropertyChanged
        {
            public string CategoryName { get; set; }
            public int ErrorCount { get; set; }
            public int WarningCount { get; set; }
            public int InfoCount { get; set; }
            public List<ValidationResult> Items { get; set; }

            private bool _isExpanded;
            public bool IsExpanded
            {
                get => _isExpanded;
                set
                {
                    if (_isExpanded != value)
                    {
                        _isExpanded = value;
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                    }
                }
            }

            public string Summary
            {
                get
                {
                    var parts = new List<string>();
                    if (ErrorCount > 0) parts.Add($"{ErrorCount} error{(ErrorCount != 1 ? "s" : "")}");
                    if (WarningCount > 0) parts.Add($"{WarningCount} warning{(WarningCount != 1 ? "s" : "")}");
                    if (InfoCount > 0) parts.Add($"{InfoCount} info");
                    return $"({string.Join(", ", parts)})";
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private void tbiDataHealth_Loaded(object sender, RoutedEventArgs e)
        {
            // No initialization needed - groups are built after checks run
        }

        private void btnRunHealthChecks_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunningHealthChecks) return;
            _isRunningHealthChecks = true;
            btnRunHealthChecks.IsEnabled = false;
            btnExpandAllHealth.IsEnabled = false;
            btnCollapseAllHealth.IsEnabled = false;
            btnExportHealthReport.IsEnabled = false;

            _validationResults = new List<ValidationResult>();
            icDataHealthGroups.ItemsSource = null;
            txtDataHealthStatus.Text = "Running checks...";
            prgDataHealth.Value = 0;

            try
            {
                var checks = new List<Action<List<ValidationResult>>>
                {
                    CheckEmptyPriceLists,
                    CheckServicesWithNoTemplate,
                    CheckProductsWithNoSupplier,
                    CheckDuplicateProductDescriptions,
                    CheckZeroCostProducts,
                    CheckUnusedMaterials,
                    CheckUnusedSpecifications,
                    CheckDuplicateProductIds,
                    CheckServiceTypeIndexConflicts,
                };

                int total = checks.Count;
                for (int i = 0; i < checks.Count; i++)
                {
                    try
                    {
                        checks[i](_validationResults);
                    }
                    catch (Exception ex)
                    {
                        _validationResults.Add(new ValidationResult
                        {
                            Category = "System",
                            Severity = "Error",
                            Message = $"Check failed: {ex.Message}",
                            Details = ex.GetType().Name
                        });
                    }
                    prgDataHealth.Value = (double)(i + 1) / total * 100;
                }

                // Build grouped model and bind once
                _categoryGroups = _validationResults
                    .GroupBy(r => r.Category)
                    .OrderByDescending(g => g.Count(r => r.Severity == "Error"))
                    .ThenByDescending(g => g.Count(r => r.Severity == "Warning"))
                    .Select(g => new ValidationCategoryGroup
                    {
                        CategoryName = g.Key,
                        ErrorCount = g.Count(r => r.Severity == "Error"),
                        WarningCount = g.Count(r => r.Severity == "Warning"),
                        InfoCount = g.Count(r => r.Severity == "Info"),
                        Items = g.ToList(),
                        IsExpanded = false
                    })
                    .ToList();

                icDataHealthGroups.ItemsSource = _categoryGroups;

                int errors = _validationResults.Count(r => r.Severity == "Error");
                int warnings = _validationResults.Count(r => r.Severity == "Warning");
                int info = _validationResults.Count(r => r.Severity == "Info");

                txtDataHealthStatus.Text = $"Complete: {errors} error(s), {warnings} warning(s), {info} info. Total: {_validationResults.Count} issue(s) found.";
                prgDataHealth.Value = 100;

                btnExpandAllHealth.IsEnabled = _categoryGroups.Count > 0;
                btnCollapseAllHealth.IsEnabled = _categoryGroups.Count > 0;
                btnExportHealthReport.IsEnabled = _validationResults.Count > 0;
            }
            catch (Exception ex)
            {
                txtDataHealthStatus.Text = $"Error running checks: {ex.Message}";
            }
            finally
            {
                _isRunningHealthChecks = false;
                btnRunHealthChecks.IsEnabled = true;
            }
        }

        private void btnExpandAllHealth_Click(object sender, RoutedEventArgs e)
        {
            if (_categoryGroups == null) return;
            foreach (var group in _categoryGroups)
                group.IsExpanded = true;
        }

        private void btnCollapseAllHealth_Click(object sender, RoutedEventArgs e)
        {
            if (_categoryGroups == null) return;
            foreach (var group in _categoryGroups)
                group.IsExpanded = false;
        }

        private void btnExportHealthReport_Click(object sender, RoutedEventArgs e)
        {
            if (_validationResults == null || _validationResults.Count == 0) return;

            var dlg = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = "DataHealthReport"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Category,Severity,Message,Details");

                foreach (var r in _validationResults)
                {
                    sb.Append(CsvEscape(r.Category));
                    sb.Append(',');
                    sb.Append(CsvEscape(r.Severity));
                    sb.Append(',');
                    sb.Append(CsvEscape(r.Message));
                    sb.Append(',');
                    sb.Append(CsvEscape(r.Details));
                    sb.AppendLine();
                }

                System.IO.File.WriteAllText(dlg.FileName, sb.ToString());
                MessageBox.Show($"Report exported to:\n{dlg.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private void DataHealthCategory_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var panel = sender as FrameworkElement;
            if (panel?.DataContext is ValidationCategoryGroup group)
                group.IsExpanded = !group.IsExpanded;
        }

        private void CheckEmptyPriceLists(List<ValidationResult> results)
        {
            try
            {
                foreach (SupplierGroup group in Database.SupplierGroups)
                {
                    foreach (PriceListBase priceList in group.PriceLists)
                    {
                        bool isEmpty = false;

                        if (priceList is PriceListWithBreakPoints bpList)
                        {
                            var table = bpList.DefaultTable;
                            var vBp = table?.VerticalBreakPoints;
                            var hBp = table?.HorizontalBreakPoints;
                            isEmpty = (table == null || (vBp != null && vBp.Count() == 0) || (hBp != null && hBp.Count() == 0));
                        }
                        else if (priceList is PriceList idList)
                        {
                            isEmpty = (idList.Products == null || idList.Products.Count == 0);
                        }

                        if (isEmpty)
                        {
                            results.Add(new ValidationResult
                            {
                                Category = "Pricing",
                                Severity = "Warning",
                                Message = $"Empty price list: {priceList.Name}",
                                Details = $"Supplier Group: {group.Name}"
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private void CheckServicesWithNoTemplate(List<ValidationResult> results)
        {
            try
            {
                foreach (Service service in Database.Services)
                {
                    if (service.ServiceTemplate == null)
                    {
                        results.Add(new ValidationResult
                        {
                            Category = "Services",
                            Severity = "Error",
                            Message = $"Service has no template: {service.Name}",
                            Details = $"Group: {service.Group}"
                        });
                    }
                }
            }
            catch { }
        }

        private void CheckProductsWithNoSupplier(List<ValidationResult> results)
        {
            try
            {
                var products = ProductDatabase.ProductDefinitions;
                if (products == null) return;

                foreach (ProductDefinition def in products)
                {
                    try
                    {
                        if (def.Group == null)
                        {
                            results.Add(new ValidationResult
                            {
                                Category = "Products",
                                Severity = "Warning",
                                Message = $"Product has no group: {def.Description}",
                                Details = $"Product ID: {def.Id}"
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void CheckDuplicateProductDescriptions(List<ValidationResult> results)
        {
            try
            {
                var products = ProductDatabase.ProductDefinitions;
                if (products == null) return;

                var descriptions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (ProductDefinition def in products)
                {
                    try
                    {
                        string desc = def.Description ?? "";
                        if (descriptions.ContainsKey(desc))
                            descriptions[desc]++;
                        else
                            descriptions[desc] = 1;
                    }
                    catch { }
                }

                foreach (var kvp in descriptions.Where(d => d.Value > 1 && !string.IsNullOrEmpty(d.Key)))
                {
                    results.Add(new ValidationResult
                    {
                        Category = "Products",
                        Severity = "Warning",
                        Message = $"Duplicate product description: \"{kvp.Key}\"",
                        Details = $"Found {kvp.Value} entries with this description"
                    });
                }
            }
            catch { }
        }

        private void CheckZeroCostProducts(List<ValidationResult> results)
        {
            // Check for breakpoint price lists with all-zero values
            try
            {
                foreach (SupplierGroup group in Database.SupplierGroups)
                {
                    foreach (PriceListBase priceList in group.PriceLists)
                    {
                        if (!(priceList is PriceListWithBreakPoints bpList))
                            continue;

                        var table = bpList.DefaultTable;
                        if (table == null) continue;

                        var vBreakpoints = table.VerticalBreakPoints?.ToList();
                        var hBreakpoints = table.HorizontalBreakPoints?.ToList();
                        int rowCount = vBreakpoints?.Count ?? 0;
                        int colCount = hBreakpoints?.Count ?? 0;

                        if (rowCount == 0 || colCount == 0) continue;

                        bool allZero = true;
                        for (int row = 0; row < rowCount && allZero; row++)
                        {
                            for (int col = 0; col < colCount && allZero; col++)
                            {
                                var result = table.GetValue(col, row);
                                if (result.Status == ResultStatus.Succeeded)
                                {
                                    double val = (double)result.ReturnObject;
                                    if (val != 0) allZero = false;
                                }
                            }
                        }

                        if (allZero)
                        {
                            results.Add(new ValidationResult
                            {
                                Category = "Pricing",
                                Severity = "Warning",
                                Message = $"Price list has all zero values: {priceList.Name}",
                                Details = $"Supplier Group: {group.Name}, {rowCount} rows x {colCount} columns"
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private void CheckUnusedMaterials(List<ValidationResult> results)
        {
            try
            {
                var usedMaterialIds = new HashSet<int>();
                foreach (Service service in Database.Services)
                {
                    // We can't easily enumerate service items here without loading items,
                    // so this check reports materials not referenced by any job item
                    // when there are items in the current job.
                }

                var jobItems = Job.Items;
                if (jobItems != null && jobItems.Count > 0)
                {
                    foreach (Item item in jobItems)
                    {
                        try
                        {
                            if (item.Material != null)
                                usedMaterialIds.Add(item.Material.Id);
                        }
                        catch { }
                    }

                    int unusedCount = 0;
                    foreach (Material material in Database.Materials)
                    {
                        if (!usedMaterialIds.Contains(material.Id))
                            unusedCount++;
                    }

                    if (unusedCount > 0)
                    {
                        results.Add(new ValidationResult
                        {
                            Category = "Materials",
                            Severity = "Info",
                            Message = $"{unusedCount} material(s) not used by any item in current job",
                            Details = "Materials may be used in other jobs"
                        });
                    }
                }
            }
            catch { }
        }

        private void CheckUnusedSpecifications(List<ValidationResult> results)
        {
            try
            {
                var usedSpecIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Service service in Database.Services)
                {
                    try
                    {
                        if (service.Specification != null)
                            usedSpecIds.Add(service.Specification.Name);
                    }
                    catch { }
                }

                int unusedCount = 0;
                foreach (Specification spec in Database.Specifications)
                {
                    if (!usedSpecIds.Contains(spec.Name))
                        unusedCount++;
                }

                if (unusedCount > 0)
                {
                    results.Add(new ValidationResult
                    {
                        Category = "Specifications",
                        Severity = "Info",
                        Message = $"{unusedCount} specification(s) not assigned to any service",
                        Details = "Specifications may be used by items directly"
                    });
                }
            }
            catch { }
        }

        private void CheckDuplicateProductIds(List<ValidationResult> results)
        {
            try
            {
                var products = ProductDatabase.ProductDefinitions;
                if (products == null) return;

                var idCounts = new Dictionary<string, List<string>>();
                foreach (ProductDefinition def in products)
                {
                    try
                    {
                        string id = def.Id?.ToString() ?? "";
                        string desc = def.Description ?? "(no description)";
                        if (!idCounts.ContainsKey(id))
                            idCounts[id] = new List<string>();
                        idCounts[id].Add(desc);
                    }
                    catch { }
                }

                foreach (var kvp in idCounts.Where(k => k.Value.Count > 1 && !string.IsNullOrEmpty(k.Key)))
                {
                    results.Add(new ValidationResult
                    {
                        Category = "Products",
                        Severity = "Error",
                        Message = $"Duplicate Product ID: {kvp.Key}",
                        Details = $"{kvp.Value.Count} products share this ID: {string.Join(", ", kvp.Value.Take(5))}"
                    });
                }
            }
            catch { }
        }

        private void CheckServiceTypeIndexConflicts(List<ValidationResult> results)
        {
            try
            {
                var idMap = new Dictionary<int, List<string>>();
                foreach (ServiceType st in Database.ServiceTypes)
                {
                    try
                    {
                        int id = st.Id;
                        string name = st.Description ?? "(unnamed)";
                        if (!idMap.ContainsKey(id))
                            idMap[id] = new List<string>();
                        idMap[id].Add(name);
                    }
                    catch { }
                }

                foreach (var kvp in idMap.Where(k => k.Value.Count > 1))
                {
                    results.Add(new ValidationResult
                    {
                        Category = "Services",
                        Severity = "Warning",
                        Message = $"Service Type index conflict: ID {kvp.Key}",
                        Details = $"{kvp.Value.Count} service types share this ID: {string.Join(", ", kvp.Value)}"
                    });
                }
            }
            catch { }
        }

        #endregion
    }
}
