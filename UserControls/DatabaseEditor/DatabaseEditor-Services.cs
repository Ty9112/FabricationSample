using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabricationSample.Data;
using FabricationSample.FunctionExamples;
using FabricationSample.Manager;
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

namespace FabricationSample.UserControls.DatabaseEditor
{
    /// <summary>
    /// Interaction logic for DatabaseEditor.xaml
    /// </summary>
    public partial class DatabaseEditor : UserControl
    {
        #region Private Members

        Service _updateService;
        ServiceButtonItem _selectedButtonItem;
        bool _specsInitialised;

        #endregion

        #region Services

        private void tbiServices_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSpecs();

            LoadServices(null);
        }

        public void LoadSpecs()
        {
            _specsInitialised = false;

            // setup specs
            ListCollectionView specs = new ListCollectionView(new ObservableCollection<Specification>(Database.Specifications));
            specs.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            specs.SortDescriptions.Add(new SortDescription("Group", ListSortDirection.Ascending));
            specs.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            cmbServiceSpecification.ItemsSource = specs;

            // Bulk spec combo uses same grouped view
            var bulkSpecs = new ListCollectionView(new ObservableCollection<Specification>(Database.Specifications));
            bulkSpecs.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            bulkSpecs.SortDescriptions.Add(new SortDescription("Group", ListSortDirection.Ascending));
            bulkSpecs.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            cmbBulkSpecSvc.ItemsSource = bulkSpecs;

            _specsInitialised = true;
        }

        public void LoadServices(Service service)
        {
            // setup service templates
            cmbUseServiceTemplate.ItemsSource = new ObservableCollection<ServiceTemplate>(Database.ServiceTemplates);

            // setup services
            ListCollectionView services = new ListCollectionView(new ObservableCollection<Service>(Database.Services));
            services.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            services.SortDescriptions.Add(new SortDescription("Group", ListSortDirection.Ascending));
            services.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            cmbSelectService.ItemsSource = services;

            if (service != null)
            {
                foreach (Service s in services)
                {
                    if (service.Id == s.Id)
                    {
                        cmbSelectService.SelectedItem = s;
                        break;
                    }
                }
            }
        }

        private void serviceProperties_Click(object sender, RoutedEventArgs e)
        {
            if (sender != null && FabricationManager.CurrentService != null)
            {
                try
                {
                    FabricationManager.ParentWindow.LoadServiceEditorControl();
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Error Loading Service Properties", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        private void cmbUseServiceTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                ServiceTemplate serviceTemplate = e.AddedItems[0] as ServiceTemplate;
                FabricationManager.CurrentService.ServiceTemplate = serviceTemplate;

                ButtonsTabControl_Services.Content = new ServiceButtonsView(ServiceButtonsViewType.Services);

                cmbSelectButtonItem.ItemsSource = null;
                cmbSelectButtonItem.SelectedIndex = -1;
                btnAddItem.IsEnabled = false;
                _selectedButtonItem = null;

                var view = ButtonsTabControl_Services.Content as ServiceButtonsView;
                if (view != null)
                {
                    view.CurrentServiceButton = null;
                }
            }
        }

        private void chkServiceSpecification_Click(object sender, RoutedEventArgs e)
        {
            if ((bool)chkServiceSpecification.IsChecked)
            {
                FabricationManager.CurrentService.Specification = null;
                cmbServiceSpecification.IsEnabled = false;
            }
            else
            {
                cmbServiceSpecification.IsEnabled = true;
                if (cmbServiceSpecification.SelectedItem != null)
                    FabricationManager.CurrentService.Specification = cmbServiceSpecification.SelectedItem as Specification;
            }

        }

        private void cmbServiceSpecification_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_specsInitialised)
                return;

            if (e.AddedItems == null || e.AddedItems.Count == 0)
                return;

            if (FabricationManager.CurrentService == null)
                return;

            Specification spec = e.AddedItems[0] as Specification;
            if (spec == null)
                return;

            FabricationManager.CurrentService.Specification = spec;
        }
        private void cmbSelectService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((e.AddedItems != null) && e.AddedItems.Count > 0)
            {
                FabricationManager.CurrentService = e.AddedItems[0] as Service;
                if (FabricationManager.CurrentService != null)
                {
                    FabricationManager.CurrentServiceTemplate = FabricationManager.CurrentService.ServiceTemplate;

                    // set the service template
                    if (cmbUseServiceTemplate.ItemsSource != null)
                    {
                        foreach (ServiceTemplate t in cmbUseServiceTemplate.ItemsSource)
                        {
                            if (t.Id == FabricationManager.CurrentServiceTemplate.Id)
                            {
                                cmbUseServiceTemplate.SelectedItem = t;
                                break;
                            }
                        }
                    }

                    if (cmbServiceSpecification.ItemsSource != null)
                    {
                        ListCollectionView specs = cmbServiceSpecification.ItemsSource as ListCollectionView;
                        Specification thisSpec = FabricationManager.CurrentService.Specification;
                        if (thisSpec != null)
                        {
                            cmbServiceSpecification.IsEnabled = true;
                            chkServiceSpecification.IsChecked = false;
                            string specGroup = thisSpec.Group;
                            string specName = thisSpec.Name;
                            bool sameGroup = false;
                            bool sameName = false;
                            foreach (Specification spec in specs)
                            {
                                sameGroup = false;
                                sameName = false;
                                if (String.IsNullOrWhiteSpace(specGroup) && String.IsNullOrWhiteSpace(spec.Group))
                                    sameGroup = true;
                                else if (FabricationManager.CurrentService.Specification.Group.Equals(spec.Group))
                                    sameGroup = true;

                                if (sameGroup)
                                {
                                    if (String.IsNullOrWhiteSpace(specName) && String.IsNullOrWhiteSpace(spec.Name))
                                        sameName = true;

                                    if (specName.Equals(spec.Name))
                                        sameName = true;
                                }

                                if (sameName)
                                {
                                    cmbServiceSpecification.SelectedItem = spec;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            cmbServiceSpecification.IsEnabled = false;
                            chkServiceSpecification.IsChecked = true;
                        }

                    }


                    ButtonsTabControl_Services.Content = new ServiceButtonsView(ServiceButtonsViewType.Services);

                    cmbSelectButtonItem.ItemsSource = null;
                    cmbSelectButtonItem.SelectedIndex = -1;
                    btnAddItem.IsEnabled = false;
                    _selectedButtonItem = null;

                    // Populate bulk condition combo from the service's template
                    var conditions = FabricationManager.CurrentService.ServiceTemplate?.Conditions?.ToList();
                    cmbBulkConditionSvc.ItemsSource = conditions;
                    cmbBulkConditionSvc.SelectedIndex = conditions != null && conditions.Count > 0 ? 0 : -1;

                    // Refresh Service Conditions DataGrid
                    LoadServiceConditions();

                    var view = ButtonsTabControl_Services.Content as ServiceButtonsView;
                    if (view != null)
                    {
                        view.CurrentServiceButton = null;
                    }
                }
            }
        }

        public void ServiceButtonSelected(FabServiceButton button)
        {
            if (cmbSelectButtonItem != null)
            {
                cmbSelectButtonItem.ItemsSource = new ObservableCollection<ServiceButtonItem>(button.Button.ServiceButtonItems);
                cmbSelectButtonItem.SelectedIndex = 0;
            }
        }

        private void cmbSelectButtonItem_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                ServiceButtonItem buttonItem = e.AddedItems[0] as ServiceButtonItem;
                if (buttonItem != null)
                {
                    string itemPath = buttonItem.ItemPath;
                    if (!String.IsNullOrWhiteSpace(itemPath))
                    {
                        string itemContentPath = Autodesk.Fabrication.ApplicationServices.Application.ItemContentPath;
                        bool contains = itemPath.Contains(itemContentPath);
                        if (contains)
                            itemPath = itemPath.Replace(itemContentPath, "");
                    }

                    labItemPath.Content = itemPath;
                    labLessThan.Content = buttonItem.LessThanEqualTo.ToString();
                    labGreaterThan.Content = buttonItem.GreaterThan.ToString();

                    btnAddItem.IsEnabled = true;
                    _selectedButtonItem = buttonItem;
                }
            }
        }

        private void btnAddItem_Click(object sender, RoutedEventArgs e)
        {
            var view = ButtonsTabControl_Services.Content as ServiceButtonsView;
            if (view == null)
                return;

            Item item = FabricationAPIExamples.LoadServiceItem(FabricationManager.CurrentService, view.CurrentServiceButton, _selectedButtonItem, true);
            if (item != null)
                FabricationAPIExamples.AddItemToJob(item);
        }

        public void addService(string name, string group, ServiceTemplate serviceTemplate)
        {
            if (serviceTemplate == null)
                return;

            Service newService = FabricationAPIExamples.AddNewService(name, group, serviceTemplate);
            if (newService == null)
                return;

            // reload the services and switch to the new one
            LoadServices(newService);
        }

        private void EditServiceName_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentService == null)
                return;

            EditNameWindow win = new EditNameWindow("Edit Service", FabricationManager.CurrentService.Name, FabricationManager.CurrentService.Group);
            win.ShowDialog();
            if (win.Completed)
            {
                FabricationManager.CurrentService.Name = win.NewName;
                FabricationManager.CurrentService.Group = win.NewGroup;

                LoadServices(FabricationManager.CurrentService);
            }
        }

        private void AddService_Click(object sender, RoutedEventArgs e)
        {
            AddServiceWindow win = new AddServiceWindow();
            win.ShowDialog();
        }

        private void DeleteService_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentService != null)
            {
                if (MessageBox.Show("Confirm to Delete Service", "Delete Service",
                  MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                {
                    if (FabricationAPIExamples.DeleteService(FabricationManager.CurrentService))
                    {
                        FabricationManager.CurrentService = null;
                        LoadServices(null);
                    }
                }
            }
        }

        private void SaveServices_Click(object sender, RoutedEventArgs e)
        {
            DBOperationResult result = Database.SaveServices();
            MessageBoxImage image = MessageBoxImage.Information;

            if (result.Status != ResultStatus.Succeeded)
                image = MessageBoxImage.Exclamation;

            MessageBox.Show(result.Message, "Save Services", MessageBoxButton.OK, image);

        }

        private void DuplicateService_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentService == null)
            {
                MessageBox.Show("Please select a service first.", "No Service Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sourceService = FabricationManager.CurrentService;
            string newName = sourceService.Name + " (Copy)";

            // Prompt for new name
            var win = new EditNameWindow("Duplicate Service", newName, sourceService.Group);
            win.ShowDialog();
            if (!win.Completed)
                return;

            newName = win.NewName;
            string newGroup = win.NewGroup;

            // Create the new service using the same template
            ServiceTemplate template = sourceService.ServiceTemplate;
            if (template == null)
            {
                MessageBox.Show("Source service has no template. Cannot duplicate.",
                    "No Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Service newService = FabricationAPIExamples.AddNewService(newName, newGroup, template);
            if (newService != null)
            {
                // Copy specification if set
                if (sourceService.Specification != null)
                {
                    try { newService.Specification = sourceService.Specification; }
                    catch { }
                }

                LoadServices(newService);
            }
        }

        #region Bulk Assignment — Services

        private void btnNewConditionSvc_Click(object sender, RoutedEventArgs e)
        {
            var service = FabricationManager.CurrentService;
            if (service?.ServiceTemplate == null) return;

            var win = new EditNameWindow("New Condition", "New Condition", null);
            win.ShowDialog();
            if (!win.Completed || string.IsNullOrWhiteSpace(win.NewName)) return;

            var cond = FabricationAPIExamples.AddNewServiceTemplateCondition(service.ServiceTemplate, win.NewName, -1, -1);
            if (cond != null)
            {
                var conditions = service.ServiceTemplate.Conditions?.ToList();
                cmbBulkConditionSvc.ItemsSource = conditions;
                cmbBulkConditionSvc.SelectedItem = cond;
            }
        }

        private void btnBulkAssignConditionSvc_Click(object sender, RoutedEventArgs e)
        {
            var service = FabricationManager.CurrentService;
            if (service == null) return;

            var condition = cmbBulkConditionSvc.SelectedItem as ServiceTemplateCondition;
            if (condition == null)
            {
                MessageBox.Show("Select a condition first.", "Bulk Assign Condition",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string itemPath = FabricationManager.CurrentLoadedItemPath;
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                MessageBox.Show("Select an item from the item folders first to provide the item path.",
                    "Bulk Assign Condition", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var view = ButtonsTabControl_Services.Content as ServiceButtonsView;
            var selectedButtons = view?.GetSelectedButtons();
            if (selectedButtons == null || selectedButtons.Count == 0)
            {
                MessageBox.Show("Select one or more buttons first.", "Bulk Assign Condition",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int added = 0;
            foreach (var fabButton in selectedButtons)
            {
                DBOperationResult result = fabButton.Button.AddServiceButtonItem(itemPath, condition);
                if (result.Status == ResultStatus.Succeeded) added++;
            }

            MessageBox.Show($"Added condition '{condition.Description}' to {added} button(s).",
                "Bulk Assign Condition", MessageBoxButton.OK, MessageBoxImage.Information);
            view?.LoadServiceButtons();
        }

        private void btnBulkApplySpecSvc_Click(object sender, RoutedEventArgs e)
        {
            var spec = cmbBulkSpecSvc.SelectedItem as Specification;
            if (spec == null)
            {
                MessageBox.Show("Select a specification first.", "Bulk Apply Specification",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var view = ButtonsTabControl_Services.Content as ServiceButtonsView;
            var selectedButtons = view?.GetSelectedButtons();
            if (selectedButtons == null || selectedButtons.Count == 0)
            {
                MessageBox.Show("Select one or more buttons first.", "Bulk Apply Specification",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int updated = 0, failed = 0;
            foreach (var fabButton in selectedButtons)
            {
                foreach (ServiceButtonItem bi in fabButton.Button.ServiceButtonItems)
                {
                    if (string.IsNullOrWhiteSpace(bi.ItemPath)) continue;
                    try
                    {
                        Item itm = Autodesk.Fabrication.Content.ContentManager.LoadItem(bi.ItemPath);
                        if (itm == null) { failed++; continue; }
                        var result = itm.ChangeSpecification(spec, false);
                        if (result.Status == ResultStatus.Succeeded)
                        {
                            Autodesk.Fabrication.Content.ContentManager.SaveItem(itm);
                            updated++;
                        }
                        else
                            failed++;
                    }
                    catch { failed++; }
                }
            }

            string msg = $"Applied specification '{spec.Name}' to {updated} item(s).";
            if (failed > 0) msg += $"\n{failed} item(s) could not be updated.";
            MessageBox.Show(msg, "Bulk Apply Specification", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Service Conditions

        void LoadServiceConditions()
        {
            var template = FabricationManager.CurrentService?.ServiceTemplate;
            if (template == null)
            {
                dgServiceConditions.ItemsSource = null;
                return;
            }

            int idx = 0;
            var items = new ObservableCollection<FabServiceTemplateCondition>(
                template.Conditions.Select(c => new FabServiceTemplateCondition(c, idx++)));
            dgServiceConditions.ItemsSource = items;
        }

        private void btnAddServiceCondition_Click(object sender, RoutedEventArgs e)
        {
            var template = FabricationManager.CurrentService?.ServiceTemplate;
            if (template == null) return;

            var win = new AddServiceTemplateConditionWindow();
            win.ShowDialog();
            // The window calls back through FabricationManager; just reload
            LoadServiceConditions();
        }

        private void dgServiceConditions_rowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            var item = e.Row.Item as FabServiceTemplateCondition;
            if (item == null) return;

            if (!item.Description.Equals(item.Condition.Description))
                item.Condition.Description = item.Description;

            double lessThan = 0;
            if (item.LessThanOrEqual.Equals("Unrestricted"))
                lessThan = -1;
            else
                double.TryParse(item.LessThanOrEqual, out lessThan);

            double greaterThan = 0;
            if (item.GreaterThan.Equals("Unrestricted"))
                greaterThan = -1;
            else
                double.TryParse(item.GreaterThan, out greaterThan);

            if (lessThan != item.Condition.LessThanEqualTo || greaterThan != item.Condition.GreaterThan)
                FabricationAPIExamples.SetServiceTemplateConditionValues(item.Condition, greaterThan, lessThan);
        }

        private void dgServiceConditions_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.D ||
                (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == 0)
                return;

            if (dgServiceConditions.SelectedItems.Count < 2) return;
            dgServiceConditions.CommitEdit(DataGridEditingUnit.Cell, true);

            var selected = new HashSet<FabServiceTemplateCondition>(
                dgServiceConditions.SelectedItems.OfType<FabServiceTemplateCondition>());
            var ordered = dgServiceConditions.Items.OfType<FabServiceTemplateCondition>()
                .Where(i => selected.Contains(i)).ToList();

            if (ordered.Count < 2) return;
            var source = ordered[0];
            double srcGt = source.GreaterThan.ToLower() == "unrestricted" ? -1 : 0;
            double srcLt = source.LessThanOrEqual.ToLower() == "unrestricted" ? -1 : 0;
            if (srcGt == 0) double.TryParse(source.GreaterThan, out srcGt);
            if (srcLt == 0) double.TryParse(source.LessThanOrEqual, out srcLt);

            for (int i = 1; i < ordered.Count; i++)
            {
                ordered[i].Description = source.Description;
                ordered[i].GreaterThan = source.GreaterThan;
                ordered[i].LessThanOrEqual = source.LessThanOrEqual;
                if (ordered[i].Condition != null)
                {
                    ordered[i].Condition.Description = source.Description;
                    FabricationAPIExamples.SetServiceTemplateConditionValues(ordered[i].Condition, srcGt, srcLt);
                }
            }
            dgServiceConditions.Items.Refresh();
            e.Handled = true;
        }

        private void deleteServiceCondition_Click(object sender, RoutedEventArgs e)
        {
            var item = dgServiceConditions.SelectedItem as FabServiceTemplateCondition;
            if (item == null) return;

            var template = FabricationManager.CurrentService?.ServiceTemplate;
            if (template == null) return;

            if (MessageBox.Show($"Delete condition '{item.Description}'?", "Delete Service Condition",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                if (FabricationAPIExamples.DeleteServiceTemplateCondition(template, item.Condition))
                    LoadServiceConditions();
            }
        }

        #endregion

        #endregion
    }
}


