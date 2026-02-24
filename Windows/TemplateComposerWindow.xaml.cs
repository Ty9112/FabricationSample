using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Windows
{
    public partial class TemplateComposerWindow : Window
    {
        private List<TemplateViewModel> _templates;
        private List<ComposeReportEntry> _reportEntries;

        public bool Completed { get; private set; }
        public ServiceTemplate NewTemplate { get; private set; }

        public TemplateComposerWindow()
        {
            InitializeComponent();
            LoadTemplates();
            txtNewTemplateName.Text = "New Composed Template";
        }

        private void LoadTemplates()
        {
            _templates = new List<TemplateViewModel>();

            foreach (ServiceTemplate template in FabDB.ServiceTemplates)
            {
                var vm = new TemplateViewModel { TemplateName = template.Name, Template = template };

                if (template.ServiceTabs != null)
                {
                    foreach (ServiceTab tab in template.ServiceTabs)
                    {
                        vm.Tabs.Add(new TabSelectionItem
                        {
                            DisplayName = $"{tab.Name} ({CountButtons(tab)} buttons)",
                            TabName = tab.Name,
                            SourceTemplate = template,
                            SourceTab = tab,
                            IsSelected = false
                        });
                    }
                }

                if (vm.Tabs.Count > 0)
                    _templates.Add(vm);
            }

            _templates = _templates.OrderBy(t => t.TemplateName).ToList();
            tvTemplateTabs.ItemsSource = _templates;
        }

        private int CountButtons(ServiceTab tab)
        {
            try { return tab.ServiceButtons?.Count ?? 0; } catch { return 0; }
        }

        private void btnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in _templates)
                foreach (var tab in t.Tabs)
                    tab.IsSelected = true;
        }

        private void btnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in _templates)
                foreach (var tab in t.Tabs)
                    tab.IsSelected = false;
        }

        private void btnCompose_Click(object sender, RoutedEventArgs e)
        {
            string newName = txtNewTemplateName.Text?.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Please enter a name for the new template.", "Name Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedTabs = _templates.SelectMany(t => t.Tabs).Where(t => t.IsSelected).ToList();
            if (selectedTabs.Count == 0)
            {
                MessageBox.Show("Please select at least one tab.", "No Tabs Selected",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pre-compose analysis: collect and resolve conditions
            var conditionAnalysis = AnalyzeConditions(selectedTabs);
            List<ConditionMappingItem> resolvedMappings = null;

            if (conditionAnalysis.Count > 0)
            {
                bool hasConflicts = conditionAnalysis.Any(c => c.IsConflict);

                if (hasConflicts)
                {
                    // Show condition mapping window for user to resolve conflicts
                    var mappingWindow = new ConditionMappingWindow(conditionAnalysis);
                    mappingWindow.ShowDialog();

                    if (!mappingWindow.DialogResultOk)
                        return;

                    resolvedMappings = mappingWindow.ResolvedMappings;
                }
                else
                {
                    // All conditions auto-mapped, no conflicts
                    resolvedMappings = conditionAnalysis;
                }
            }

            btnCompose.IsEnabled = false;
            btnSelectAll.IsEnabled = false;
            btnSelectNone.IsEnabled = false;
            txtNewTemplateName.IsEnabled = false;
            txtStatus.Text = "Composing template...";

            try
            {
                _reportEntries = new List<ComposeReportEntry>();
                NewTemplate = ComposeTemplate(newName, selectedTabs, resolvedMappings);

                // Show the report
                ShowReport();

                if (NewTemplate != null)
                {
                    Completed = true;
                    int okCount = _reportEntries.Count(r => r.Status == "OK");
                    int failedCount = _reportEntries.Count(r => r.Status == "FAILED");
                    int skippedCount = _reportEntries.Count(r => r.Status == "SKIPPED");
                    txtStatus.Text = $"Created '{newName}': {okCount} succeeded, {failedCount} failed, {skippedCount} skipped.";
                }
                else
                {
                    txtStatus.Text = "Failed to create template. See report for details.";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Error: {ex.Message}";
                ShowReport();
            }
        }

        #region Pre-Compose Condition Analysis

        /// <summary>
        /// Analyze all conditions referenced by button items in the selected tabs.
        /// Detects exact matches (auto-map) and conflicts (same description, different ranges).
        /// </summary>
        private List<ConditionMappingItem> AnalyzeConditions(List<TabSelectionItem> selectedTabs)
        {
            // Collect all unique conditions referenced by button items
            // Key = compound key (Description|GT|LTE), Value = (condition, templateName)
            var conditionsByKey = new Dictionary<string, (ServiceTemplateCondition cond, string templateName)>();
            // Track all conditions by description to detect conflicts
            var conditionsByDesc = new Dictionary<string, List<(ServiceTemplateCondition cond, string templateName, string compoundKey)>>();

            foreach (var tabItem in selectedTabs)
            {
                try
                {
                    // Gather conditions from the template's condition list
                    foreach (var condition in tabItem.SourceTemplate.Conditions)
                    {
                        string key = ConditionMappingItem.MakeCompoundKey(
                            condition.Description, condition.GreaterThan, condition.LessThanEqualTo);
                        string desc = condition.Description ?? "";

                        if (!conditionsByKey.ContainsKey(key))
                            conditionsByKey[key] = (condition, tabItem.SourceTemplate.Name);

                        if (!conditionsByDesc.ContainsKey(desc))
                            conditionsByDesc[desc] = new List<(ServiceTemplateCondition, string, string)>();

                        // Only add if this exact compound key isn't already in the list for this description
                        if (!conditionsByDesc[desc].Any(c => c.compoundKey == key))
                            conditionsByDesc[desc].Add((condition, tabItem.SourceTemplate.Name, key));
                    }

                    // Also check button items for condition references
                    foreach (ServiceButton button in tabItem.SourceTab.ServiceButtons)
                    {
                        foreach (ServiceButtonItem item in button.ServiceButtonItems)
                        {
                            try
                            {
                                var cond = item.ServiceTemplateCondition;
                                if (cond == null) continue;

                                string key = ConditionMappingItem.MakeCompoundKey(
                                    cond.Description, cond.GreaterThan, cond.LessThanEqualTo);
                                string desc = cond.Description ?? "";

                                if (!conditionsByKey.ContainsKey(key))
                                    conditionsByKey[key] = (cond, tabItem.SourceTemplate.Name);

                                if (!conditionsByDesc.ContainsKey(desc))
                                    conditionsByDesc[desc] = new List<(ServiceTemplateCondition, string, string)>();

                                if (!conditionsByDesc[desc].Any(c => c.compoundKey == key))
                                    conditionsByDesc[desc].Add((cond, tabItem.SourceTemplate.Name, key));
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            // Build mapping items
            var mappingItems = new List<ConditionMappingItem>();

            // Build the full list of target options (all unique conditions found)
            var allOptions = new List<string> { ConditionMappingWindow.UnrestrictedOption };
            foreach (var kvp in conditionsByKey)
            {
                var c = kvp.Value.cond;
                allOptions.Add(ConditionMappingItem.FormatOptionLabel(
                    c.Description, c.GreaterThan, c.LessThanEqualTo));
            }

            foreach (var kvp in conditionsByKey)
            {
                var cond = kvp.Value.cond;
                string desc = cond.Description ?? "";
                bool isConflict = conditionsByDesc.ContainsKey(desc) && conditionsByDesc[desc].Count > 1;
                string optionLabel = ConditionMappingItem.FormatOptionLabel(
                    cond.Description, cond.GreaterThan, cond.LessThanEqualTo);

                var item = new ConditionMappingItem
                {
                    SourceLabel = cond.Description ?? "(No description)",
                    SourceRange = ConditionMappingItem.FormatRange(cond.GreaterThan, cond.LessThanEqualTo),
                    SourceTemplateName = $"From: {kvp.Value.templateName}",
                    SourceCondition = cond,
                    CompoundKey = kvp.Key,
                    IsConflict = isConflict,
                    IsAutoMapped = !isConflict,
                    SelectedTarget = optionLabel,
                    TargetOptions = new List<string>(allOptions) { ConditionMappingWindow.SkipOption },
                    ResolvedDescription = cond.Description,
                    ResolvedGreaterThan = cond.GreaterThan,
                    ResolvedLessThanEqualTo = cond.LessThanEqualTo
                };

                mappingItems.Add(item);
            }

            return mappingItems;
        }

        #endregion

        #region Silent API Helpers

        private ServiceTemplate SilentAddTemplate(string name)
        {
            DBOperationResult result = FabDB.AddServiceTemplate(name);
            if (result.Status == ResultStatus.Succeeded)
            {
                var template = result.ReturnObject as ServiceTemplate;
                _reportEntries.Add(new ComposeReportEntry("Template", name, "OK", "Created"));
                return template;
            }
            _reportEntries.Add(new ComposeReportEntry("Template", name, "FAILED", result.Message));
            return null;
        }

        private ServiceTab SilentAddTab(ServiceTemplate template, string name)
        {
            DBOperationResult result = template.AddServiceTab(name);
            if (result.Status == ResultStatus.Succeeded)
            {
                var tab = result.ReturnObject as ServiceTab;
                _reportEntries.Add(new ComposeReportEntry("Tab", name, "OK", "Created"));
                return tab;
            }
            _reportEntries.Add(new ComposeReportEntry("Tab", name, "FAILED", result.Message));
            return null;
        }

        private ServiceButton SilentAddButton(ServiceTab tab, string name)
        {
            DBOperationResult result = tab.AddServiceButton(name);
            if (result.Status == ResultStatus.Succeeded)
            {
                var button = result.ReturnObject as ServiceButton;
                _reportEntries.Add(new ComposeReportEntry("Button", name, "OK", "Created"));
                return button;
            }
            _reportEntries.Add(new ComposeReportEntry("Button", name, "FAILED", result.Message));
            return null;
        }

        private ServiceTemplateCondition SilentAddCondition(ServiceTemplate template, string description,
            double greaterThan, double lessThanEqualTo)
        {
            DBOperationResult result = template.AddServiceTemplateCondition(description, greaterThan, lessThanEqualTo);
            if (result.Status == ResultStatus.Succeeded)
            {
                var condition = result.ReturnObject as ServiceTemplateCondition;
                string range = ConditionMappingItem.FormatRange(greaterThan, lessThanEqualTo);
                _reportEntries.Add(new ComposeReportEntry("Condition", $"{description} ({range})", "OK", "Created"));
                return condition;
            }
            _reportEntries.Add(new ComposeReportEntry("Condition", description, "FAILED", result.Message));
            return null;
        }

        private ServiceButtonItem SilentAddButtonItem(ServiceButton button, string path,
            ServiceTemplateCondition condition)
        {
            DBOperationResult result = button.AddServiceButtonItem(path, condition);
            if (result.Status == ResultStatus.Succeeded)
            {
                var item = result.ReturnObject as ServiceButtonItem;
                string condDesc = condition?.Description ?? "Unrestricted";
                _reportEntries.Add(new ComposeReportEntry("Button Item", path, "OK", condDesc));
                return item;
            }
            _reportEntries.Add(new ComposeReportEntry("Button Item", path, "FAILED", result.Message));
            return null;
        }

        #endregion

        #region Compose

        private ServiceTemplate ComposeTemplate(string name, List<TabSelectionItem> selectedTabs,
            List<ConditionMappingItem> resolvedMappings)
        {
            // 1. Create the new template
            ServiceTemplate newTemplate = SilentAddTemplate(name);
            if (newTemplate == null) return null;

            // 2. Create conditions from resolved mappings and build lookup
            // Key = compound source key, Value = new condition on the target template
            var conditionMap = new Dictionary<string, ServiceTemplateCondition>();
            // Track which compound keys map to skip
            var skipKeys = new HashSet<string>();

            if (resolvedMappings != null)
            {
                // Group by selected target to avoid creating duplicate conditions
                var targetGroups = new Dictionary<string, ServiceTemplateCondition>();

                foreach (var mapping in resolvedMappings)
                {
                    if (mapping.SelectedTarget == ConditionMappingWindow.SkipOption)
                    {
                        skipKeys.Add(mapping.CompoundKey);
                        _reportEntries.Add(new ComposeReportEntry("Condition",
                            mapping.SourceLabel, "SKIPPED", "User chose to skip"));
                        continue;
                    }

                    if (mapping.SelectedTarget == ConditionMappingWindow.UnrestrictedOption)
                    {
                        // Map to null (unrestricted)
                        conditionMap[mapping.CompoundKey] = null;
                        continue;
                    }

                    // Parse the selected target to find which condition values to use
                    // Find the mapping item that matches the selected target label
                    var targetMapping = resolvedMappings.FirstOrDefault(m =>
                        ConditionMappingItem.FormatOptionLabel(
                            m.ResolvedDescription, m.ResolvedGreaterThan, m.ResolvedLessThanEqualTo)
                        == mapping.SelectedTarget);

                    string desc = targetMapping?.ResolvedDescription ?? mapping.ResolvedDescription;
                    double gt = targetMapping?.ResolvedGreaterThan ?? mapping.ResolvedGreaterThan;
                    double lte = targetMapping?.ResolvedLessThanEqualTo ?? mapping.ResolvedLessThanEqualTo;
                    string targetKey = ConditionMappingItem.MakeCompoundKey(desc, gt, lte);

                    if (!targetGroups.ContainsKey(targetKey))
                    {
                        // Create this condition on the new template
                        var newCond = SilentAddCondition(newTemplate, desc, gt, lte);
                        targetGroups[targetKey] = newCond;
                    }

                    conditionMap[mapping.CompoundKey] = targetGroups[targetKey];
                }
            }

            // 3. Copy selected tabs
            foreach (var tabItem in selectedTabs)
            {
                try
                {
                    ServiceTab sourceTab = tabItem.SourceTab;
                    var newTab = SilentAddTab(newTemplate, sourceTab.Name);
                    if (newTab == null) continue;

                    foreach (ServiceButton sourceButton in sourceTab.ServiceButtons)
                    {
                        var newButton = SilentAddButton(newTab, sourceButton.Name);
                        if (newButton == null) continue;

                        // Copy button code
                        try { newButton.ButtonCode = sourceButton.ButtonCode; } catch { }

                        // Copy button items
                        foreach (ServiceButtonItem sourceItem in sourceButton.ServiceButtonItems)
                        {
                            try
                            {
                                ServiceTemplateCondition matchingCondition = null;
                                bool shouldSkip = false;

                                if (sourceItem.ServiceTemplateCondition != null)
                                {
                                    var srcCond = sourceItem.ServiceTemplateCondition;
                                    string srcKey = ConditionMappingItem.MakeCompoundKey(
                                        srcCond.Description, srcCond.GreaterThan, srcCond.LessThanEqualTo);

                                    if (skipKeys.Contains(srcKey))
                                    {
                                        _reportEntries.Add(new ComposeReportEntry("Button Item",
                                            sourceItem.ItemPath, "SKIPPED", "Condition skipped by user"));
                                        shouldSkip = true;
                                    }
                                    else if (conditionMap.TryGetValue(srcKey, out matchingCondition))
                                    {
                                        // matchingCondition may be null (Unrestricted) — that's valid
                                    }
                                    else
                                    {
                                        // Condition not in resolved mappings — try description-only fallback
                                        string desc = srcCond.Description ?? "";
                                        var fallback = conditionMap.FirstOrDefault(kvp =>
                                            kvp.Key.StartsWith(desc + "|"));
                                        if (fallback.Key != null)
                                        {
                                            matchingCondition = fallback.Value;
                                        }
                                        // else: matchingCondition stays null (Unrestricted)
                                    }
                                }

                                if (!shouldSkip)
                                {
                                    var newItem = SilentAddButtonItem(newButton, sourceItem.ItemPath, matchingCondition);

                                    // Copy item-level condition range overrides if present
                                    if (newItem != null && sourceItem.ServiceTemplateCondition != null)
                                    {
                                        try
                                        {
                                            newItem.SetConditionOverride(
                                                sourceItem.GreaterThan, sourceItem.LessThanEqualTo);
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _reportEntries.Add(new ComposeReportEntry("Button Item",
                                    sourceItem.ItemPath, "FAILED", ex.Message));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _reportEntries.Add(new ComposeReportEntry("Tab",
                        tabItem.TabName, "FAILED", ex.Message));
                }
            }

            return newTemplate;
        }

        #endregion

        #region Report

        private void ShowReport()
        {
            tvTemplateTabs.Visibility = Visibility.Collapsed;
            dgReport.Visibility = Visibility.Visible;
            dgReport.ItemsSource = _reportEntries;

            txtInstructions.Text = "Compose Report:";
            btnCompose.Visibility = Visibility.Collapsed;
            btnSelectAll.Visibility = Visibility.Collapsed;
            btnSelectNone.Visibility = Visibility.Collapsed;
            btnClose.Content = "Close";
        }

        #endregion

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (Completed)
                DialogResult = true;
            else
                DialogResult = false;
            Close();
        }
    }

    public class ComposeReportEntry
    {
        public string Operation { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public string Details { get; set; }

        public ComposeReportEntry(string operation, string name, string status, string details)
        {
            Operation = operation;
            Name = name;
            Status = status;
            Details = details;
        }
    }

    public class TemplateViewModel
    {
        public string TemplateName { get; set; }
        public ServiceTemplate Template { get; set; }
        public List<TabSelectionItem> Tabs { get; set; } = new List<TabSelectionItem>();
    }

    public class TabSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string DisplayName { get; set; }
        public string TabName { get; set; }
        public ServiceTemplate SourceTemplate { get; set; }
        public ServiceTab SourceTab { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
