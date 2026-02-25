using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabricationSample.Data;
using FabricationSample.FunctionExamples;
using FabricationSample.Manager;
using FabricationSample.Windows;
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
        #region Service Templates

        private void tbiServiceTemplates_Loaded(object sender, RoutedEventArgs e)
        {
            LoadServiceTemplates();

            FabricationManager.ItemFoldersView = new ItemFoldersView();

            // Populate bulk spec combo once
            var bulkSpecs = new ListCollectionView(new ObservableCollection<Specification>(Database.Specifications));
            bulkSpecs.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
            bulkSpecs.SortDescriptions.Add(new SortDescription("Group", ListSortDirection.Ascending));
            bulkSpecs.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            cmbBulkSpecTmpl.ItemsSource = bulkSpecs;
        }

        private void tbiServiceTemplates_UnLoaded(object sender, RoutedEventArgs e)
        {
            cmbSelectServiceTemplate.ItemsSource = null;
            dgTemplateConditions.ItemsSource = null;
        }


        private void LoadServiceTemplates()
        {
            ListCollectionView lcv = new ListCollectionView(new ObservableCollection<ServiceTemplate>(Database.ServiceTemplates.ToList()));
            lcv.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

            cmbSelectServiceTemplate.ItemsSource = null;
            cmbSelectServiceTemplate.ItemsSource = lcv;
            cmbSelectServiceTemplate.Items.Refresh();
        }

        private void cmbSelectServiceTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((e.AddedItems != null) && e.AddedItems.Count > 0)
            {
                FabricationManager.CurrentServiceTemplate = e.AddedItems[0] as ServiceTemplate;
                if (FabricationManager.CurrentServiceTemplate != null)
                {
                    cmbSelectServiceTemplate.Text = FabricationManager.CurrentServiceTemplate.Name;
                    cmbSelectServiceTemplate.UpdateLayout();
                    ButtonsTabControl_Templates.Content = new ServiceButtonsView(ServiceButtonsViewType.ServiceTemplates);
                    LoadServiceTemplateConditions();
                    RefreshBulkConditionCombo();
                }
            }
        }

        private void addServiceTemplate_Click(object sender, RoutedEventArgs e)
        {
            AddServiceTemplateWindow win = new AddServiceTemplateWindow();
            win.ShowDialog();
        }

        private void deleteServiceTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentServiceTemplate == null)
                return;

            if (MessageBox.Show("Confirm to Delete Service Template: " + FabricationManager.CurrentServiceTemplate.Name, "Delete Service Template",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                if (FabricationAPIExamples.DeleteServiceTemplate(FabricationManager.CurrentServiceTemplate))
                {
                    cmbSelectServiceTemplate.SelectedItem = null;
                    FabricationManager.CurrentServiceTemplate = null;
                    FabricationManager.CurrentService = null;

                    var view = ButtonsTabControl_Templates.Content as ServiceButtonsView;
                    if (view != null)
                    {
                        view.CurrentServiceTab = null;
                        view.CurrentServiceButton = null;
                    }

                    LoadServiceTemplates();

                    view = ButtonsTabControl_Services.Content as ServiceButtonsView;
                    if (view != null)
                    {
                        view.CurrentServiceTab = null;
                        view.CurrentServiceButton = null;
                        LoadServices(null);
                    }
                }
            }
        }

        private void editServiceTemplateName_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentServiceTemplate == null)
                return;

            EditNameWindow win = new EditNameWindow(null, FabricationManager.CurrentServiceTemplate.Name, null);
            win.ShowDialog();
            if (win.Completed)
            {
                FabricationManager.CurrentServiceTemplate.Name = win.NewName;
                LoadServiceTemplates();
                cmbSelectServiceTemplate.SelectedItem = FabricationManager.CurrentServiceTemplate;
            }
        }

        public void addServiceTemplate(string serviceTemplateName)
        {
            ServiceTemplate newServiceTemplate = FabricationAPIExamples.AddNewServiceTemplate(serviceTemplateName);
            if (newServiceTemplate == null)
                return;

            // reload the service templates and switch to the new one
            LoadServiceTemplates();
            //Refresh services also
            LoadServices(null);
            ListCollectionView lcv = cmbSelectServiceTemplate.ItemsSource as ListCollectionView;
            if (lcv == null)
                return;

            foreach (ServiceTemplate st in lcv)
            {
                if (newServiceTemplate.Id == st.Id)
                {
                    cmbSelectServiceTemplate.SelectedItem = newServiceTemplate;
                    break;
                }
            }

            var view = ButtonsTabControl_Templates.Content as ServiceButtonsView;
            if (view != null)
            {
                view.CurrentServiceTab = null;
                view.CurrentServiceButton = null;
            }

        }

        private void addServiceTab_Click(object sender, RoutedEventArgs e)
        {
            AddServiceTabWindow win = new AddServiceTabWindow();
            win.ShowDialog();
        }

        public void addServiceTab(string tabName)
        {
            if (FabricationManager.CurrentServiceTemplate == null)
                return;

            ServiceTab newServiceTab = FabricationAPIExamples.AddNewServiceTab(FabricationManager.CurrentServiceTemplate, tabName);

            if (newServiceTab == null)
                return;

            var serviceButtonsViewer = ButtonsTabControl_Templates.Content as ServiceButtonsView;
            if (serviceButtonsViewer != null)
                serviceButtonsViewer.LoadServiceTabs(newServiceTab.Id);

            var view = ButtonsTabControl_Services.Content as ServiceButtonsView;
            if (view == null)
                return;

            Service service = FabricationManager.CurrentService;
            if (service == null || service.ServiceTemplate == null)
                return;
            if (service.ServiceTemplate.Id != FabricationManager.CurrentServiceTemplate.Id)
                return;

            view.LoadServiceTabs(-1);
        }

        private void addServiceButton_Click(object sender, RoutedEventArgs e)
        {
            AddServiceButtonWindow win = new AddServiceButtonWindow();
            win.ShowDialog();
        }

        public void addServiceButton(string buttonName)
        {
            var view = ButtonsTabControl_Templates.Content as ServiceButtonsView;
            if (view == null || view.CurrentServiceTab == null)
                return;

            ServiceButton newButton = FabricationAPIExamples.AddNewServiceButton(view.CurrentServiceTab, buttonName);

            if (newButton == null)
                return;

            var buttonsViewer = ButtonsTabControl_Templates.Content as ServiceButtonsView;
            if (buttonsViewer != null)
                buttonsViewer.LoadServiceButtons();

            view = ButtonsTabControl_Services.Content as ServiceButtonsView;
            if (view == null)
                return;

            Service service = FabricationManager.CurrentService;
            if (service == null || service.ServiceTemplate == null)
                return;
            if (service.ServiceTemplate.Id != FabricationManager.CurrentServiceTemplate.Id)
                return;

            view.LoadServiceTabs(-1);
        }

        private void dgTemplateConditions_OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadServiceTemplateConditions();
        }

        private void LoadServiceTemplateConditions()
        {
            if (FabricationManager.CurrentServiceTemplate == null)
                return;

            var conditions = new ObservableCollection<FabServiceTemplateCondition>();

            int condIndex = 0;
            FabricationManager.CurrentServiceTemplate.Conditions.ToList().ForEach((x) =>
            {
                conditions.Add(new FabServiceTemplateCondition(x, condIndex++));
            });

            dgTemplateConditions.ItemsSource = conditions;
        }

        private void dgTemplateConditions_rowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            var item = e.Row.Item as FabServiceTemplateCondition;
            if (item == null)
                return;

            if (!item.Description.Equals(item.Condition.Description))
                item.Condition.Description = item.Description;

            double lessThan = 0;
            if (item.LessThanOrEqual.Equals("Unrestricted"))
                lessThan = -1;
            else if (!double.TryParse(item.LessThanOrEqual, out lessThan))
                return;

            double greaterThan = 0;
            if (item.GreaterThan.Equals("Unrestricted"))
                greaterThan = -1;
            else if (!double.TryParse(item.GreaterThan, out greaterThan))
                return;

            if (lessThan != item.Condition.LessThanEqualTo || greaterThan != item.Condition.GreaterThan)
            {
                bool modified = FabricationAPIExamples.SetServiceTemplateConditionValues(item.Condition, greaterThan, lessThan);
                if (!modified)
                {
                    // reset the values
                    string lessThanString, greaterThanString;
                    if (item.Condition.LessThanEqualTo == -1)
                        lessThanString = "Unrestricted";
                    else
                        lessThanString = item.Condition.LessThanEqualTo.ToString();

                    if (item.Condition.GreaterThan == -1)
                        greaterThanString = "Unrestricted";
                    else
                        greaterThanString = item.Condition.GreaterThan.ToString();

                    item.LessThanOrEqual = lessThanString;
                    item.GreaterThan = greaterThanString;
                }
            }
        }

        private void addServiceTemplateCondition_Click(object sender, RoutedEventArgs e)
        {
            AddServiceTemplateConditionWindow win = new AddServiceTemplateConditionWindow();
            win.ShowDialog();
        }

        public void addServiceTemplateCondition(string description, string greaterThan, string lessThan)
        {
            if (FabricationManager.CurrentServiceTemplate == null)
                return;

            if (!double.TryParse(greaterThan, out double greaterThanValue) ||
                !double.TryParse(lessThan, out double lessThanValue))
                return;

            var condition = FabricationAPIExamples.AddNewServiceTemplateCondition(FabricationManager.CurrentServiceTemplate, description, greaterThanValue, lessThanValue);
            if (condition != null)
            {
                LoadServiceTemplateConditions();
            }
        }

        private void dgTemplateConditions_KeyDown(object sender, KeyEventArgs e)
        {
            // Shift+D: fill-down — apply topmost selected row's values to all selected rows below it
            if (e.Key == Key.D && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                if (dgTemplateConditions.SelectedItems.Count < 2) return;

                dgTemplateConditions.CommitEdit(DataGridEditingUnit.Cell, true);

                var selectedSet = new HashSet<FabServiceTemplateCondition>(
                    dgTemplateConditions.SelectedItems.OfType<FabServiceTemplateCondition>());
                var ordered = dgTemplateConditions.Items.OfType<FabServiceTemplateCondition>()
                    .Where(i => selectedSet.Contains(i)).ToList();

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

                    // Sync to underlying Fabrication API object
                    if (ordered[i].Condition != null)
                    {
                        ordered[i].Condition.Description = source.Description;
                        FabricationAPIExamples.SetServiceTemplateConditionValues(ordered[i].Condition, srcGt, srcLt);
                    }
                }

                dgTemplateConditions.Items.Refresh();
                e.Handled = true;
            }
        }

        private void deleteTemplateCondition_Click(object sender, RoutedEventArgs e)
        {
            var condition = dgTemplateConditions.SelectedItem as FabServiceTemplateCondition;
            if (condition == null)
                return;

            if (MessageBox.Show("Confirm to Delete Service Template Condition: " + condition.Description, "Delete Service Template Condition",
          MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
            {
                if (FabricationAPIExamples.DeleteServiceTemplateCondition(FabricationManager.CurrentServiceTemplate, condition.Condition))
                    LoadServiceTemplateConditions();
            }
        }

        private void btnSaveServiceTemplates_Click(object sender, RoutedEventArgs e)
        {
            DBOperationResult result = Database.SaveServices();
            MessageBoxImage image = MessageBoxImage.Information;

            if (result.Status == ResultStatus.Failed)
                image = MessageBoxImage.Exclamation;

            MessageBox.Show(result.Message, "Service Templates",
              MessageBoxButton.OK, image);

        }

        private void textbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!char.IsDigit(e.Text, e.Text.Length - 1))
            {
                e.Handled = true;
            }
        }

        private void DuplicateTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentServiceTemplate == null)
            {
                MessageBox.Show("Please select a service template first.", "No Template Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sourceTemplate = FabricationManager.CurrentServiceTemplate;
            string newName = sourceTemplate.Name + " (Copy)";

            // Prompt for new name
            var win = new EditNameWindow(null, newName, null);
            win.ShowDialog();
            if (!win.Completed)
                return;

            newName = win.NewName;

            ServiceTemplate newTemplate = FabricationAPIExamples.AddNewServiceTemplate(newName);
            if (newTemplate != null)
            {
                // Copy conditions from source template
                try
                {
                    foreach (var condition in sourceTemplate.Conditions)
                    {
                        FabricationAPIExamples.AddNewServiceTemplateCondition(
                            newTemplate,
                            condition.Description,
                            condition.GreaterThan,
                            condition.LessThanEqualTo);
                    }
                }
                catch { }

                // Copy tabs and buttons from source template
                try
                {
                    foreach (ServiceTab sourceTab in sourceTemplate.ServiceTabs)
                    {
                        var newTab = FabricationAPIExamples.AddNewServiceTab(newTemplate, sourceTab.Name);
                        if (newTab != null)
                        {
                            foreach (ServiceButton sourceButton in sourceTab.ServiceButtons)
                            {
                                var newButton = FabricationAPIExamples.AddNewServiceButton(newTab, sourceButton.Name);
                                if (newButton != null)
                                {
                                    // Copy button items
                                    foreach (ServiceButtonItem sourceItem in sourceButton.ServiceButtonItems)
                                    {
                                        try
                                        {
                                            // Find matching condition in new template
                                            ServiceTemplateCondition matchingCondition = null;
                                            if (sourceItem.ServiceTemplateCondition != null)
                                            {
                                                foreach (var newCond in newTemplate.Conditions)
                                                {
                                                    if (newCond.Description == sourceItem.ServiceTemplateCondition.Description)
                                                    {
                                                        matchingCondition = newCond;
                                                        break;
                                                    }
                                                }
                                            }

                                            // Always add item, even with null condition (Unrestricted)
                                            FabricationAPIExamples.AddNewServiceButtonItem(
                                                newButton, sourceItem.ItemPath, matchingCondition);
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                LoadServiceTemplates();

                // Select the new template
                ListCollectionView lcv = cmbSelectServiceTemplate.ItemsSource as ListCollectionView;
                if (lcv != null)
                {
                    foreach (ServiceTemplate st in lcv)
                    {
                        if (st.Id == newTemplate.Id)
                        {
                            cmbSelectServiceTemplate.SelectedItem = st;
                            break;
                        }
                    }
                }
            }
        }

        private void BulkAddItemToButtons_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentServiceTemplate == null)
            {
                MessageBox.Show("Please select a service template first.", "No Template Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var view = ButtonsTabControl_Templates.Content as ServiceButtonsView;
            if (view == null) return;

            var selectedButtons = view.GetSelectedButtons();
            if (selectedButtons.Count < 2)
            {
                MessageBox.Show("Please select 2 or more buttons (Ctrl+Click or Shift+Click) in the tab above, then click this button to assign the same item to all of them.",
                    "Select Multiple Buttons", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var template = FabricationManager.CurrentServiceTemplate;
            var conditions = template.Conditions.ToList();

            // Build picker window in code
            var win = new Window
            {
                Title = "Bulk Add Item to Buttons",
                Width = 500,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            try { win.Owner = Window.GetWindow(this); } catch { }

            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock
            {
                Text = $"Assign item to {selectedButtons.Count} selected button(s):",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Condition picker
            panel.Children.Add(new TextBlock { Text = "Condition:", Margin = new Thickness(0, 0, 0, 4) });
            var condCombo = new ComboBox
            {
                DisplayMemberPath = "Description",
                Margin = new Thickness(0, 0, 0, 10)
            };
            var condList = new List<ServiceTemplateCondition>(conditions);
            condCombo.ItemsSource = condList;
            if (condList.Count > 0) condCombo.SelectedIndex = 0;
            panel.Children.Add(condCombo);

            // Item path
            panel.Children.Add(new TextBlock { Text = "Item Path (from content tree):", Margin = new Thickness(0, 0, 0, 4) });
            string currentPath = FabricationManager.CurrentLoadedItemPath ?? "";
            var pathBox = new TextBox
            {
                Text = currentPath,
                Margin = new Thickness(0, 0, 0, 4),
                IsReadOnly = true,
                Background = System.Windows.Media.Brushes.WhiteSmoke
            };
            panel.Children.Add(pathBox);
            panel.Children.Add(new TextBlock
            {
                Text = "Tip: Select an item in the Manage Content tree first to set the path.",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Add to All Selected", Width = 140, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnOk.Click += (s, args) => { win.DialogResult = true; win.Close(); };
            btnCancel.Click += (s, args) => { win.DialogResult = false; win.Close(); };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(btnPanel);

            win.Content = panel;
            if (win.ShowDialog() != true) return;

            var selectedCondition = condCombo.SelectedItem as ServiceTemplateCondition;
            string itemPath = pathBox.Text;

            if (string.IsNullOrWhiteSpace(itemPath))
            {
                MessageBox.Show("No item path specified. Select an item in Manage Content first.",
                    "No Item Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int successCount = 0;
            foreach (var btn in selectedButtons)
            {
                try
                {
                    FabricationAPIExamples.AddNewServiceButtonItem(btn.Button, itemPath, selectedCondition);
                    successCount++;
                }
                catch { }
            }

            // Refresh the views
            view.LoadServiceButtons();

            // Also refresh Services view if same template
            if (FabricationManager.CurrentService != null &&
                FabricationManager.CurrentService.ServiceTemplate?.Id == template.Id)
            {
                var svcView = ButtonsTabControl_Services.Content as ServiceButtonsView;
                if (svcView != null) svcView.LoadServiceTabs(-1);
            }

            MessageBox.Show($"Added item to {successCount} of {selectedButtons.Count} button(s).",
                "Bulk Add Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyConditions_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentServiceTemplate == null)
            {
                MessageBox.Show("Please select a target service template first.", "No Template Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var targetTemplate = FabricationManager.CurrentServiceTemplate;
            var allTemplates = Database.ServiceTemplates.ToList()
                .Where(t => t.Id != targetTemplate.Id)
                .OrderBy(t => t.Name)
                .ToList();

            if (allTemplates.Count == 0)
            {
                MessageBox.Show("No other service templates available to copy from.", "No Templates",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Build picker window in code
            var win = new Window
            {
                Title = "Copy Conditions From...",
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };

            try { win.Owner = Window.GetWindow(this); } catch { }

            var panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock
            {
                Text = "Select source template to copy conditions from:",
                Margin = new Thickness(0, 0, 0, 8)
            });

            var combo = new ComboBox
            {
                ItemsSource = allTemplates,
                DisplayMemberPath = "Name",
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(combo);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Copy", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            var btnCancel = new Button { Content = "Cancel", Width = 80 };
            btnOk.Click += (s, args) => { win.DialogResult = true; win.Close(); };
            btnCancel.Click += (s, args) => { win.DialogResult = false; win.Close(); };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            panel.Children.Add(btnPanel);

            win.Content = panel;
            if (win.ShowDialog() != true) return;

            var sourceTemplate = combo.SelectedItem as ServiceTemplate;
            if (sourceTemplate == null) return;

            // Get existing condition descriptions for duplicate check
            var existingDescriptions = new HashSet<string>(
                targetTemplate.Conditions.Select(c => c.Description),
                StringComparer.OrdinalIgnoreCase);

            int copied = 0;
            int skipped = 0;
            foreach (var condition in sourceTemplate.Conditions)
            {
                if (existingDescriptions.Contains(condition.Description))
                {
                    skipped++;
                    continue;
                }

                var newCondition = FabricationAPIExamples.AddNewServiceTemplateCondition(
                    targetTemplate, condition.Description, condition.GreaterThan, condition.LessThanEqualTo);
                if (newCondition != null)
                    copied++;
            }

            LoadServiceTemplateConditions();

            string msg = $"Copied {copied} condition(s) from '{sourceTemplate.Name}'.";
            if (skipped > 0)
                msg += $"\nSkipped {skipped} duplicate(s).";
            MessageBox.Show(msg, "Copy Conditions", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnComposeTemplate_Click(object sender, RoutedEventArgs e)
        {
            var win = new TemplateComposerWindow();
            win.ShowDialog();

            if (win.Completed && win.NewTemplate != null)
            {
                LoadServiceTemplates();
                LoadServices(null);

                // Select the new template
                ListCollectionView lcv = cmbSelectServiceTemplate.ItemsSource as ListCollectionView;
                if (lcv != null)
                {
                    foreach (ServiceTemplate st in lcv)
                    {
                        if (st.Id == win.NewTemplate.Id)
                        {
                            cmbSelectServiceTemplate.SelectedItem = st;
                            break;
                        }
                    }
                }
            }
        }

        #region Bulk Assignment — Service Templates

        private void RefreshBulkConditionCombo()
        {
            var conditions = FabricationManager.CurrentServiceTemplate?.Conditions?.ToList();
            cmbBulkConditionTmpl.ItemsSource = conditions;
            cmbBulkConditionTmpl.SelectedIndex = conditions != null && conditions.Count > 0 ? 0 : -1;
        }

        private void btnNewConditionTmpl_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentServiceTemplate == null) return;

            var win = new EditNameWindow("New Condition", "New Condition", null);
            win.ShowDialog();
            if (!win.Completed || string.IsNullOrWhiteSpace(win.NewName)) return;

            var cond = FabricationAPIExamples.AddNewServiceTemplateCondition(
                FabricationManager.CurrentServiceTemplate, win.NewName, -1, -1);
            if (cond != null)
            {
                LoadServiceTemplateConditions();
                RefreshBulkConditionCombo();
                cmbBulkConditionTmpl.SelectedItem = FabricationManager.CurrentServiceTemplate.Conditions
                    .FirstOrDefault(c => c.Description == cond.Description);
            }
        }

        private void btnBulkAssignConditionTmpl_Click(object sender, RoutedEventArgs e)
        {
            if (FabricationManager.CurrentServiceTemplate == null) return;

            var condition = cmbBulkConditionTmpl.SelectedItem as ServiceTemplateCondition;
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

            var view = ButtonsTabControl_Templates.Content as ServiceButtonsView;
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

        private void btnBulkApplySpecTmpl_Click(object sender, RoutedEventArgs e)
        {
            var spec = cmbBulkSpecTmpl.SelectedItem as Specification;
            if (spec == null)
            {
                MessageBox.Show("Select a specification first.", "Bulk Apply Specification",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var view = ButtonsTabControl_Templates.Content as ServiceButtonsView;
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

        #endregion
    }
}


