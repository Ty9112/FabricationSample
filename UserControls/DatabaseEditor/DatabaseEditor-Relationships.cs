using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Partial class for DatabaseEditor - Relationship Manager / Dependency Viewer.
    /// Shows a tree/hierarchical view of dependencies between Services, Templates,
    /// Specifications, Materials, and other entities.
    /// </summary>
    public partial class DatabaseEditor : UserControl
    {
        #region Relationships

        private void tbiRelationships_Loaded(object sender, RoutedEventArgs e)
        {
            // Don't auto-load - wait for user to click Refresh
        }

        private void btnRefreshRelationships_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtRelationshipStatus.Text = "Building relationship tree...";
                trvRelationships.Items.Clear();

                BuildRelationshipTree();

                txtRelationshipStatus.Text = $"Relationship tree loaded.";
            }
            catch (Exception ex)
            {
                txtRelationshipStatus.Text = $"Error: {ex.Message}";
            }
        }

        private void BuildRelationshipTree()
        {
            // Services node
            var servicesNode = new TreeViewItem { Header = "Services", IsExpanded = true };
            BuildServicesTree(servicesNode);
            trvRelationships.Items.Add(servicesNode);

            // Materials node
            var materialsNode = new TreeViewItem { Header = "Materials" };
            BuildMaterialsTree(materialsNode);
            trvRelationships.Items.Add(materialsNode);

            // Specifications node
            var specificationsNode = new TreeViewItem { Header = "Specifications" };
            BuildSpecificationsTree(specificationsNode);
            trvRelationships.Items.Add(specificationsNode);

            // Service Templates node
            var templatesNode = new TreeViewItem { Header = "Service Templates" };
            BuildServiceTemplatesTree(templatesNode);
            trvRelationships.Items.Add(templatesNode);
        }

        private void BuildServicesTree(TreeViewItem parent)
        {
            try
            {
                // Group services by their Group property
                var groupedServices = Database.Services
                    .Cast<Service>()
                    .GroupBy(s => string.IsNullOrEmpty(s.Group) ? "(No Group)" : s.Group)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedServices)
                {
                    var groupNode = new TreeViewItem { Header = $"{group.Key} ({group.Count()})" };

                    foreach (var service in group.OrderBy(s => s.Name))
                    {
                        var serviceNode = new TreeViewItem { Header = service.Name };

                        // Template
                        if (service.ServiceTemplate != null)
                        {
                            var templateNode = new TreeViewItem
                            {
                                Header = $"Template: {service.ServiceTemplate.Name}",
                                Foreground = System.Windows.Media.Brushes.DarkBlue
                            };

                            // Buttons
                            try
                            {
                                foreach (ServiceTab tab in service.ServiceTemplate.ServiceTabs)
                                {
                                    var tabNode = new TreeViewItem { Header = $"Tab: {tab.Name}" };
                                    foreach (ServiceButton button in tab.ServiceButtons)
                                    {
                                        var buttonNode = new TreeViewItem { Header = $"Button: {button.Name}" };

                                        foreach (ServiceButtonItem item in button.ServiceButtonItems)
                                        {
                                            string itemPath = item.ItemPath ?? "(no path)";
                                            buttonNode.Items.Add(new TreeViewItem
                                            {
                                                Header = $"{item.ServiceTemplateCondition?.Description ?? "Default"}: {itemPath}",
                                                Foreground = System.Windows.Media.Brushes.Gray
                                            });
                                        }
                                        tabNode.Items.Add(buttonNode);
                                    }
                                    templateNode.Items.Add(tabNode);
                                }
                            }
                            catch { }

                            serviceNode.Items.Add(templateNode);
                        }

                        // Specification
                        if (service.Specification != null)
                        {
                            serviceNode.Items.Add(new TreeViewItem
                            {
                                Header = $"Specification: {service.Specification.Name}",
                                Foreground = System.Windows.Media.Brushes.DarkGreen
                            });
                        }

                        groupNode.Items.Add(serviceNode);
                    }

                    parent.Items.Add(groupNode);
                }
            }
            catch { }
        }

        private void BuildMaterialsTree(TreeViewItem parent)
        {
            try
            {
                // Track which services use each material
                var materialUsage = new Dictionary<int, List<string>>();
                foreach (Service service in Database.Services)
                {
                    // Can't easily get materials from service without loading items
                    // Show material list with gauge info instead
                }

                var groupedMaterials = Database.Materials
                    .Cast<Material>()
                    .GroupBy(m => string.IsNullOrEmpty(m.Group) ? "(No Group)" : m.Group)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedMaterials)
                {
                    var groupNode = new TreeViewItem { Header = $"{group.Key} ({group.Count()})" };

                    foreach (var material in group.OrderBy(m => m.Name))
                    {
                        var materialNode = new TreeViewItem { Header = material.Name };

                        // Show gauges
                        try
                        {
                            if (material.Gauges != null)
                            {
                                int gaugeCount = 0;
                                foreach (var gauge in material.Gauges)
                                    gaugeCount++;

                                if (gaugeCount > 0)
                                {
                                    materialNode.Items.Add(new TreeViewItem
                                    {
                                        Header = $"Gauges: {gaugeCount}",
                                        Foreground = System.Windows.Media.Brushes.Gray
                                    });
                                }
                            }
                        }
                        catch { }

                        groupNode.Items.Add(materialNode);
                    }

                    parent.Items.Add(groupNode);
                }
            }
            catch { }
        }

        private void BuildSpecificationsTree(TreeViewItem parent)
        {
            try
            {
                // Track which services use each specification
                var specUsage = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (Service service in Database.Services)
                {
                    try
                    {
                        if (service.Specification != null)
                        {
                            string specName = service.Specification.Name;
                            if (!specUsage.ContainsKey(specName))
                                specUsage[specName] = new List<string>();
                            specUsage[specName].Add(service.Name);
                        }
                    }
                    catch { }
                }

                var groupedSpecs = Database.Specifications
                    .Cast<Specification>()
                    .GroupBy(s => string.IsNullOrEmpty(s.Group) ? "(No Group)" : s.Group)
                    .OrderBy(g => g.Key);

                foreach (var group in groupedSpecs)
                {
                    var groupNode = new TreeViewItem { Header = $"{group.Key} ({group.Count()})" };

                    foreach (var spec in group.OrderBy(s => s.Name))
                    {
                        var specNode = new TreeViewItem { Header = spec.Name };

                        if (specUsage.ContainsKey(spec.Name))
                        {
                            var usedByNode = new TreeViewItem
                            {
                                Header = $"Used by {specUsage[spec.Name].Count} service(s)",
                                Foreground = System.Windows.Media.Brushes.DarkGreen
                            };
                            foreach (var serviceName in specUsage[spec.Name].OrderBy(n => n))
                            {
                                usedByNode.Items.Add(new TreeViewItem
                                {
                                    Header = serviceName,
                                    Foreground = System.Windows.Media.Brushes.Gray
                                });
                            }
                            specNode.Items.Add(usedByNode);
                        }
                        else
                        {
                            specNode.Items.Add(new TreeViewItem
                            {
                                Header = "Not used by any service",
                                Foreground = System.Windows.Media.Brushes.OrangeRed
                            });
                        }

                        groupNode.Items.Add(specNode);
                    }

                    parent.Items.Add(groupNode);
                }
            }
            catch { }
        }

        private void BuildServiceTemplatesTree(TreeViewItem parent)
        {
            try
            {
                // Track which services use each template
                var templateUsage = new Dictionary<int, List<string>>();
                foreach (Service service in Database.Services)
                {
                    try
                    {
                        if (service.ServiceTemplate != null)
                        {
                            int templateId = service.ServiceTemplate.Id;
                            if (!templateUsage.ContainsKey(templateId))
                                templateUsage[templateId] = new List<string>();
                            templateUsage[templateId].Add(service.Name);
                        }
                    }
                    catch { }
                }

                foreach (ServiceTemplate template in Database.ServiceTemplates.Cast<ServiceTemplate>().OrderBy(t => t.Name))
                {
                    var templateNode = new TreeViewItem { Header = template.Name };

                    // Show usage
                    if (templateUsage.ContainsKey(template.Id))
                    {
                        var usedByNode = new TreeViewItem
                        {
                            Header = $"Used by {templateUsage[template.Id].Count} service(s)",
                            Foreground = System.Windows.Media.Brushes.DarkGreen
                        };
                        foreach (var name in templateUsage[template.Id].OrderBy(n => n))
                        {
                            usedByNode.Items.Add(new TreeViewItem
                            {
                                Header = name,
                                Foreground = System.Windows.Media.Brushes.Gray
                            });
                        }
                        templateNode.Items.Add(usedByNode);
                    }
                    else
                    {
                        templateNode.Items.Add(new TreeViewItem
                        {
                            Header = "Not used by any service",
                            Foreground = System.Windows.Media.Brushes.OrangeRed
                        });
                    }

                    // Show conditions
                    try
                    {
                        int condCount = template.Conditions.Count();
                        if (condCount > 0)
                        {
                            var condNode = new TreeViewItem { Header = $"Conditions: {condCount}" };
                            foreach (var cond in template.Conditions)
                            {
                                condNode.Items.Add(new TreeViewItem
                                {
                                    Header = cond.Description,
                                    Foreground = System.Windows.Media.Brushes.Gray
                                });
                            }
                            templateNode.Items.Add(condNode);
                        }
                    }
                    catch { }

                    // Show tabs/buttons count
                    try
                    {
                        int tabCount = 0;
                        int buttonCount = 0;
                        foreach (ServiceTab tab in template.ServiceTabs)
                        {
                            tabCount++;
                            foreach (ServiceButton btn in tab.ServiceButtons)
                                buttonCount++;
                        }
                        templateNode.Items.Add(new TreeViewItem
                        {
                            Header = $"{tabCount} tab(s), {buttonCount} button(s)",
                            Foreground = System.Windows.Media.Brushes.Gray
                        });
                    }
                    catch { }

                    parent.Items.Add(templateNode);
                }
            }
            catch { }
        }

        #endregion
    }
}
