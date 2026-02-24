using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Fabrication.DB;

namespace FabricationSample.Windows
{
    public partial class ConditionMappingWindow : Window
    {
        public const string UnrestrictedOption = "(Unrestricted)";
        public const string SkipOption = "(Skip - don't copy this item)";

        private List<ConditionMappingItem> _items;

        public bool DialogResultOk { get; private set; }

        /// <summary>
        /// The resolved condition mappings after user confirms.
        /// Key = compound source key (Description|GT|LTE), Value = the target ConditionMappingItem.
        /// </summary>
        public List<ConditionMappingItem> ResolvedMappings => _items;

        public ConditionMappingWindow(List<ConditionMappingItem> items)
        {
            InitializeComponent();
            _items = items;
            mappingsList.ItemsSource = _items;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            int conflicts = _items.Count(i => i.IsConflict);
            int autoMapped = _items.Count(i => i.IsAutoMapped);
            txtSummary.Text = $"{_items.Count} conditions ({autoMapped} auto-mapped, {conflicts} conflict{(conflicts == 1 ? "" : "s")})";
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
            // Validate all conflicts have been resolved
            var unresolved = _items.Where(i => i.IsConflict && string.IsNullOrEmpty(i.SelectedTarget)).ToList();
            if (unresolved.Any())
            {
                MessageBox.Show(
                    "Please resolve all condition conflicts before continuing.",
                    "Unresolved Conflicts",
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

    public class ConditionMappingItem : INotifyPropertyChanged
    {
        private string _selectedTarget;

        /// <summary>Display label: condition description.</summary>
        public string SourceLabel { get; set; }

        /// <summary>Range display: "GT: x, LTE: y" or "Unrestricted".</summary>
        public string SourceRange { get; set; }

        /// <summary>Which template this condition came from.</summary>
        public string SourceTemplateName { get; set; }

        /// <summary>The original condition object from the source template.</summary>
        public ServiceTemplateCondition SourceCondition { get; set; }

        /// <summary>Compound key: "Description|GT|LTE".</summary>
        public string CompoundKey { get; set; }

        /// <summary>The user's selected target from the dropdown.</summary>
        public string SelectedTarget
        {
            get => _selectedTarget;
            set
            {
                _selectedTarget = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTarget)));
            }
        }

        /// <summary>Available target options for the dropdown.</summary>
        public List<string> TargetOptions { get; set; } = new List<string>();

        /// <summary>True if this condition has the same description as another but different ranges.</summary>
        public bool IsConflict { get; set; }

        /// <summary>True if this was auto-mapped (exact match found).</summary>
        public bool IsAutoMapped { get; set; }

        /// <summary>
        /// The condition values to use when creating this condition in the new template.
        /// Set during resolution. Null means Unrestricted.
        /// </summary>
        public double ResolvedGreaterThan { get; set; }
        public double ResolvedLessThanEqualTo { get; set; }
        public string ResolvedDescription { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public static string MakeCompoundKey(string description, double greaterThan, double lessThanEqualTo)
        {
            return $"{description ?? ""}|{greaterThan}|{lessThanEqualTo}";
        }

        public static string FormatRange(double greaterThan, double lessThanEqualTo)
        {
            if (greaterThan < 0 && lessThanEqualTo < 0)
                return "Unrestricted";
            return $"GT: {greaterThan}, LTE: {lessThanEqualTo}";
        }

        public static string FormatOptionLabel(string description, double greaterThan, double lessThanEqualTo)
        {
            string range = FormatRange(greaterThan, lessThanEqualTo);
            return $"{description} ({range})";
        }
    }
}
