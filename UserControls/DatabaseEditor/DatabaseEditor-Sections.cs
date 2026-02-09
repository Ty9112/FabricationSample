using Autodesk.Fabrication;
using Autodesk.Fabrication.DB;
using Autodesk.Fabrication.Results;
using FabricationSample.Data;
using FabricationSample.FunctionExamples;
using FabricationSample.Manager;
using FabricationSample.Services.Export;
using FabricationSample.Services.Import;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SaveFileDialog = System.Windows.Forms.SaveFileDialog;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace FabricationSample.UserControls.DatabaseEditor
{
  /// <summary>
  /// Interaction logic for DatabaseEditor.xaml
  /// </summary>
  public partial class DatabaseEditor : UserControl
  {
    #region Private Members

    Section _section;

    #endregion

    #region Sections

    private void tbiSections_Loaded(object sender, RoutedEventArgs e)
    {
      LoadSections(null, null);
    }

    private void LoadSections(string description, string group)
    {
      // setup materials
      ListCollectionView sections = new ListCollectionView(new ObservableCollection<Section>(Database.Sections));
      sections.GroupDescriptions.Add(new PropertyGroupDescription("Group"));
      sections.SortDescriptions.Add(new SortDescription("Group", ListSortDirection.Ascending));
      sections.SortDescriptions.Add(new SortDescription("Description", ListSortDirection.Ascending));
      cmbSelectSection.ItemsSource = sections;

      if (!String.IsNullOrWhiteSpace(description))
      {
        foreach (Section s in sections)
        {
          if (s.Description.Equals(description))
          {
            if (String.IsNullOrWhiteSpace(s.Group) && String.IsNullOrWhiteSpace(group))
            {
              cmbSelectSection.SelectedItem = s;
              break;
            }
            else if (!String.IsNullOrWhiteSpace(s.Group) && s.Group.Equals(group))
            {
              cmbSelectSection.SelectedItem = s;
              break;
            }
          }
        }
      }
    }

    private void editSection_Click(object sender, RoutedEventArgs e)
    {
      if (_section == null)
        return;

      EditNameWindow win = new EditNameWindow("Edit Section", _section.Description, _section.Group);
      win.ShowDialog();

      if (win.Completed)
      {
        _section.Description = win.NewName;
        _section.Group = win.NewGroup;

        LoadSections(_section.Description, _section.Group);
      }
    }

    private void addSection_Click(object sender, RoutedEventArgs e)
    {
      EditNameWindow win = new EditNameWindow("Add New Section", "", "");
      win.ShowDialog();

      if (win.Completed)
      {
        if (FabricationAPIExamples.AddNewSection(win.NewName, win.NewGroup) != null)
        {
          LoadSections(win.NewName, win.NewGroup);
        }
      }
    }

    private void deleteSection_Click(object sender, RoutedEventArgs e)
    {
      if (_section == null)
        return;

      if (FabricationAPIExamples.DeleteSection(_section))
        LoadSections(null, null);
    }

    private void cmbSelectSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if ((e.AddedItems != null) && e.AddedItems.Count > 0)
      {
        _section = e.AddedItems[0] as Section;
        if (_section != null)
        {
          LoadSectionData();
        }
      }
    }

    private void LoadSectionData()
    {
      if (_section == null)
        return;

      txtSectionIndex.Text = _section.Index.ToString();
      txtSectionDiffFactor.Text = _section.DifficultyFactor.ToString();
      chkSectionRetroFitted.IsChecked = _section.RetroFitted;
      txtSectionRetroPercent.Text = _section.RetroFittedPercentage.ToString();
      txtSectionLayerTag.Text = _section.LayerTag;
      txtSectionFloorLevel.Text = _section.FloorLevel.ToString();
      txtSectionSlabLevel.Text = _section.SlabLevel.ToString();

      // fill the rectangle
      System.Windows.Media.Color rectColor = System.Windows.Media.Color.FromArgb(_section.Color.A, _section.Color.R, _section.Color.G, _section.Color.B);
      rectSectionColor.Fill = new SolidColorBrush(rectColor);
    }

    private void chkSectionRetroFitted_Checked(object sender, RoutedEventArgs e)
    {
      if (_section == null)
        return;

      _section.RetroFitted = true;
    }

    private void chkSectionRetroFitted_Unchecked(object sender, RoutedEventArgs e)
    {
      if (_section == null)
        return;

      _section.RetroFitted = false;
    }

    private void txtSectionIndex_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      e.Handled = !IsIntegerNumber(e.Text);
    }

    private void txtSectionIndex_LostFocus(object sender, RoutedEventArgs e)
    {
      int value;
      if (Int32.TryParse(txtSectionIndex.Text, out value))
        _section.Index = value;
      else
        txtSectionIndex.Text = _section.Index.ToString();
    }

    private void txtSectionDiffFactor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      e.Handled = !IsPositiveRealNumber(e.Text);
    }

    private void txtSectionDiffFactor_LostFocus(object sender, RoutedEventArgs e)
    {
      double value;
      if (Double.TryParse(txtSectionDiffFactor.Text, out value))
        _section.DifficultyFactor = value;
      else
        txtSectionDiffFactor.Text = _section.DifficultyFactor.ToString();
    }

    private void txtSectionRetroPercent_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      e.Handled = !IsPositiveRealNumber(e.Text);
    }

    private void txtSectionRetroPercent_LostFocus(object sender, RoutedEventArgs e)
    {
      double value;
      if (Double.TryParse(txtSectionRetroPercent.Text, out value))
        _section.RetroFittedPercentage = value;
      else
        txtSectionRetroPercent.Text = _section.RetroFittedPercentage.ToString();
    }

    private void txtSectionFloorLevel_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      e.Handled = !IsRealNumber(e.Text);
    }

    private void txtSectionFloorLevel_LostFocus(object sender, RoutedEventArgs e)
    {
      double value;
      if (Double.TryParse(txtSectionFloorLevel.Text, out value))
        _section.FloorLevel = value;
      else
        txtSectionFloorLevel.Text = _section.FloorLevel.ToString();
    }

    private void txtSectionSlabLevel_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
      e.Handled = !IsRealNumber(e.Text);
    }

    private void txtSectionSlabLevel_LostFocus(object sender, RoutedEventArgs e)
    {
      double value;
      if (Double.TryParse(txtSectionSlabLevel.Text, out value))
        _section.SlabLevel = value;
      else
        txtSectionSlabLevel.Text = _section.SlabLevel.ToString(); 
    }

    private void txtSectionLayerTag_LostFocus(object sender, RoutedEventArgs e)
    {
      _section.LayerTag = txtSectionLayerTag.Text;
    }

    private void btnSectionColorPicker_Click(object sender, RoutedEventArgs e)
    {
      if (_section == null)
        return;

      System.Windows.Forms.ColorDialog win = new System.Windows.Forms.ColorDialog();
      win.Color = _section.Color;
      win.ShowDialog();

      _section.Color = win.Color;

      // fill the rectangle
      System.Windows.Media.Color rectColor = System.Windows.Media.Color.FromArgb(win.Color.A, win.Color.R, win.Color.G, win.Color.B);
      rectSectionColor.Fill = new SolidColorBrush(rectColor);
    }

    private void btnSaveSections_Click(object sender, RoutedEventArgs e)
    {
      DBOperationResult result = Database.SaveSections();
      if (result.Status == ResultStatus.Succeeded)
      {
        MessageBox.Show(result.Message, "Save Sections",
           MessageBoxButton.OK, MessageBoxImage.Information);
      }
      else
      {
        MessageBox.Show(result.Message, "Save Sections",
           MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    /// <summary>
    /// Export sections to CSV file.
    /// </summary>
    private void btnExportSections_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        var exportService = new SectionsExportService();

        using (var saveDialog = new SaveFileDialog())
        {
          saveDialog.Title = "Export Sections";
          saveDialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
          saveDialog.FileName = "Sections.csv";
          saveDialog.DefaultExt = ".csv";

          if (saveDialog.ShowDialog() == DialogResult.OK)
          {
            exportService.ProgressChanged += (s, args) =>
            {
              Dispatcher.Invoke(() => { prgSections.Value = args.Percentage; });
            };

            var result = exportService.Export(saveDialog.FileName);

            prgSections.Value = 0;

            if (result.IsSuccess)
            {
              MessageBox.Show($"Sections exported successfully!\n\nFile: {result.FilePath}\nRecords: {result.RecordCount}",
                "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
              MessageBox.Show($"Export failed: {result.ErrorMessage}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
          }
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Error exporting sections: {ex.Message}",
          "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    /// <summary>
    /// Import sections from CSV file.
    /// </summary>
    private void btnImportSections_Click(object sender, RoutedEventArgs e)
    {
      try
      {
        using (var fileDialog = new OpenFileDialog())
        {
          fileDialog.Title = "Select Sections CSV file to import";
          fileDialog.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
          fileDialog.CheckFileExists = true;
          fileDialog.CheckPathExists = true;

          if (fileDialog.ShowDialog() != DialogResult.OK)
            return;

          string importFile = fileDialog.FileName;
          if (string.IsNullOrEmpty(importFile))
            return;

          // Column mapping
          var requiredFields = new[] { "Description" };
          var optionalFields = new[] { "Group", "Index", "DifficultyFactor", "RetroFitted",
            "RetroFittedPercentage", "LayerTag", "FloorLevel", "SlabLevel",
            "ColorR", "ColorG", "ColorB" };

          var mappingWindow = new ColumnMappingWindow(importFile, requiredFields, optionalFields);
          mappingWindow.ShowDialog();

          if (!mappingWindow.DialogResultOk)
            return;

          var options = new ImportOptions
          {
            HasHeaderRow = true,
            UpdateExisting = true,
            StopOnFirstError = false
          };

          if (mappingWindow.ResultMapping != null)
            options.CustomSettings[ColumnMappingConfig.SettingsKey] = mappingWindow.ResultMapping;

          var importService = new SectionsImportService();

          // Validate
          var validation = importService.Validate(importFile, options);
          if (!validation.IsValid)
          {
            string errorMsg = $"Validation failed with {validation.Errors.Count} error(s):\n\n";
            foreach (var error in validation.Errors.Take(5))
              errorMsg += $"  {error}\n";
            if (validation.Errors.Count > 5)
              errorMsg += $"  ... and {validation.Errors.Count - 5} more errors";

            MessageBox.Show(errorMsg, "Validation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
          }

          // Warnings
          if (validation.Warnings.Count > 0)
          {
            string warnMsg = $"Validation found {validation.Warnings.Count} warning(s):\n\n";
            foreach (var warning in validation.Warnings.Take(5))
              warnMsg += $"  {warning}\n";
            if (validation.Warnings.Count > 5)
              warnMsg += $"  ... and {validation.Warnings.Count - 5} more warnings\n";
            warnMsg += $"\nFound {validation.DataRowCount} data rows.\n\nContinue with import?";

            if (MessageBox.Show(warnMsg, "Validation Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
              return;
          }

          // Preview
          var preview = importService.Preview(importFile, options);
          if (preview.IsSuccess)
          {
            string previewMsg = $"Ready to import sections:\n\n" +
                              $"New sections: {preview.NewRecordCount}\n" +
                              $"Updated sections: {preview.UpdatedRecordCount}\n" +
                              $"Skipped sections: {preview.SkippedRecordCount}\n" +
                              $"Total changes: {preview.Changes.Count}\n\n" +
                              $"Continue?";

            if (MessageBox.Show(previewMsg, "Import Preview", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
              return;
          }
          else
          {
            MessageBox.Show($"Failed to generate preview: {preview.ErrorMessage}",
              "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
          }

          // Import
          importService.ProgressChanged += (s, args) =>
          {
            Dispatcher.Invoke(() => { prgSections.Value = args.Percentage; });
          };

          var result = importService.Import(importFile, options);

          if (result.IsSuccess)
          {
            string successMsg = $"Import complete.\n\n" +
                              $"Imported (new/updated): {result.ImportedCount} sections\n";
            if (result.SkippedCount > 0)
              successMsg += $"Skipped: {result.SkippedCount} sections\n";
            if (result.ErrorCount > 0)
              successMsg += $"Errors: {result.ErrorCount} sections\n";
            successMsg += "\nClick 'Save Sections' to persist changes.";

            MessageBox.Show(successMsg, "Import Complete", MessageBoxButton.OK,
              result.ErrorCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

            // Reload the sections dropdown to show updated data
            LoadSections(null, null);
          }
          else if (result.WasCancelled)
          {
            MessageBox.Show("Import was cancelled.", "Import Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
          }
          else
          {
            MessageBox.Show($"Import failed: {result.ErrorMessage}",
              "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
          }

          prgSections.Value = 0;
        }
      }
      catch (Exception ex)
      {
        MessageBox.Show($"Unexpected error during import: {ex.Message}",
          "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private static bool IsRealNumber(string text)
    {
      Regex regex = new Regex("[^0-9.-]+"); 
      return !regex.IsMatch(text);
    }

    private static bool IsPositiveRealNumber(string text)
    {
      Regex regex = new Regex("[^0-9.]+");
      return !regex.IsMatch(text);
    }

    private static bool IsIntegerNumber(string text)
    {
      Regex regex = new Regex("[^0-9-]+");
      return !regex.IsMatch(text);
    }

    #endregion


  }
}


    