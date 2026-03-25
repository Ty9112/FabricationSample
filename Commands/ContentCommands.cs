using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Fabrication.Content;
using Autodesk.Fabrication.DB;
using FabricationSample.Utilities;
using CADapp = Autodesk.AutoCAD.ApplicationServices.Application;
using FabDB = Autodesk.Fabrication.DB.Database;

namespace FabricationSample.Commands
{
    /// <summary>
    /// NETLOAD commands for Fabrication content management (clone, create, modify items).
    /// </summary>
    public class ContentCommands
    {
        #region Helper Methods

        private static void Princ(string message)
        {
            try
            {
                var doc = CADapp.DocumentManager.MdiActiveDocument;
                doc?.Editor?.WriteMessage("\n" + message);
            }
            catch { }
        }

        private static bool ValidateFabricationLoaded()
        {
            try
            {
                var services = FabDB.Services;
                return services != null;
            }
            catch
            {
                MessageBox.Show(
                    "Fabrication API is not loaded.\n\nPlease load CADmep and open a valid fabrication job.",
                    "Fabrication API Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        /// <summary>
        /// Simple input dialog using WinForms (no VisualBasic reference needed).
        /// </summary>
        private static string PromptForInput(string title, string prompt, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.Width = 450;
                form.Height = 200;

                var label = new Label { Left = 12, Top = 12, Width = 410, Height = 80, Text = prompt };
                var textBox = new TextBox { Left = 12, Top = 100, Width = 410, Text = defaultValue ?? "" };
                var okButton = new Button { Text = "OK", Left = 260, Top = 130, Width = 75, DialogResult = DialogResult.OK };
                var cancelButton = new Button { Text = "Cancel", Left = 345, Top = 130, Width = 75, DialogResult = DialogResult.Cancel };

                form.Controls.AddRange(new Control[] { label, textBox, okButton, cancelButton });
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                return form.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }

        #endregion

        #region CreateFromClone

        /// <summary>
        /// Clone an existing .itm file and its product list (.Txt) with new DatabaseIds.
        ///
        /// Workflow:
        /// 1. User selects source .itm file
        /// 2. User provides new item name (becomes filename and description)
        /// 3. User provides new DatabaseId base (e.g., MDSK_JOINT_000128)
        /// 4. Command copies the .itm via ContentManager.LoadItem + SaveItemAs
        /// 5. Command generates a new product list .Txt with updated DatabaseIds
        /// 6. Optionally copies the .png icon
        /// </summary>
        [CommandMethod("CreateFromClone")]
        public static void CreateFromClone()
        {
            try
            {
                if (!ValidateFabricationLoaded())
                    return;

                // Step 1: Select source .itm file
                string sourceItmPath;
                using (var openDialog = new OpenFileDialog())
                {
                    openDialog.Title = "Select Source ITM File to Clone";
                    openDialog.Filter = "Fabrication ITM files (*.itm)|*.itm";
                    openDialog.CheckFileExists = true;

                    // Default to item content path
                    try
                    {
                        string itemPath = Autodesk.Fabrication.ApplicationServices.Application.ItemContentPath;
                        if (!string.IsNullOrEmpty(itemPath) && Directory.Exists(itemPath))
                            openDialog.InitialDirectory = itemPath;
                    }
                    catch { }

                    if (openDialog.ShowDialog() != DialogResult.OK)
                    {
                        Princ("CreateFromClone: Cancelled by user.");
                        return;
                    }

                    sourceItmPath = openDialog.FileName;
                }

                string sourceDir = Path.GetDirectoryName(sourceItmPath);
                string sourceNameNoExt = Path.GetFileNameWithoutExtension(sourceItmPath);
                string sourceTxtPath = Path.Combine(sourceDir, sourceNameNoExt + ".Txt");
                string sourcePngPath = Path.Combine(sourceDir, sourceNameNoExt + ".png");

                // Verify source product list exists
                if (!File.Exists(sourceTxtPath))
                {
                    MessageBox.Show(
                        $"Product list file not found:\n{sourceTxtPath}\n\nThe .Txt file must exist alongside the .itm file.",
                        "Missing Product List",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Read source product list to get entry count
                var sourceEntries = ReadProductList(sourceTxtPath);
                if (sourceEntries.Count == 0)
                {
                    MessageBox.Show(
                        "Source product list is empty or could not be parsed.",
                        "Invalid Product List",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Step 2: Get new item name
                string defaultNewName = sourceNameNoExt.Replace("Standard", "Schedule 10S");
                string newItemName = PromptForInput(
                    "New Item Name",
                    $"Source: {sourceNameNoExt}\n" +
                    $"Product list entries: {sourceEntries.Count}\n\n" +
                    "Enter the new item name (becomes filename and description):",
                    defaultNewName);

                if (string.IsNullOrWhiteSpace(newItemName))
                {
                    Princ("CreateFromClone: Cancelled — no name provided.");
                    return;
                }

                // Step 3: Get new DatabaseId base
                // Parse source DatabaseId base from product list (e.g., MDSK_JOINT_000049)
                string sourceDbIdBase = "";
                if (sourceEntries.Count > 0)
                {
                    string firstId = sourceEntries[0].DatabaseId;
                    int lastDash = firstId.LastIndexOf('-');
                    if (lastDash > 0)
                        sourceDbIdBase = firstId.Substring(0, lastDash);
                }

                string newDbIdBase = PromptForInput(
                    "New DatabaseId Base",
                    $"Source DatabaseId base: {sourceDbIdBase}\n" +
                    $"(entries are {sourceDbIdBase}-0001 through -{sourceEntries.Count:D4})\n\n" +
                    "Enter the new DatabaseId base (e.g., MDSK_JOINT_000128):",
                    "");

                if (string.IsNullOrWhiteSpace(newDbIdBase))
                {
                    Princ("CreateFromClone: Cancelled — no DatabaseId base provided.");
                    return;
                }

                // Step 4: Select output directory (default to same folder as source)
                string outputDir = sourceDir;
                var result = MessageBox.Show(
                    $"Save cloned item to the same folder?\n\n{sourceDir}\n\nClick 'No' to choose a different folder.",
                    "Output Location",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                {
                    Princ("CreateFromClone: Cancelled.");
                    return;
                }

                if (result == DialogResult.No)
                {
                    string selectedFolder = FileHelpers.PromptForExportFolder("Select Output Folder for Cloned Item");
                    if (string.IsNullOrEmpty(selectedFolder))
                    {
                        Princ("CreateFromClone: Cancelled — no output folder selected.");
                        return;
                    }
                    outputDir = selectedFolder;
                }

                // Check if target files already exist
                string targetItmPath = Path.Combine(outputDir, newItemName + ".itm");
                string targetTxtPath = Path.Combine(outputDir, newItemName + ".Txt");
                if (File.Exists(targetItmPath) || File.Exists(targetTxtPath))
                {
                    var overwrite = MessageBox.Show(
                        $"Target files already exist:\n{targetItmPath}\n\nOverwrite?",
                        "File Exists",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);
                    if (overwrite != DialogResult.Yes)
                    {
                        Princ("CreateFromClone: Cancelled — target files exist.");
                        return;
                    }
                }

                // Step 5: Clone the .itm via ContentManager
                Princ($"Loading source item: {sourceItmPath}");
                Item sourceItem = ContentManager.LoadItem(sourceItmPath);
                if (sourceItem == null)
                {
                    MessageBox.Show(
                        $"Failed to load source item:\n{sourceItmPath}",
                        "Load Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                Princ($"Saving cloned item: {targetItmPath}");
                var saveResult = ContentManager.SaveItemAs(sourceItem, outputDir, newItemName, true);
                if (saveResult.Status != ResultStatus.Succeeded)
                {
                    MessageBox.Show(
                        $"Failed to save cloned item:\n{saveResult.Status}",
                        "Save Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // Step 6: Generate new product list .Txt with updated DatabaseIds
                Princ("Generating new product list...");
                var newEntries = new List<ProductListEntry>();
                for (int i = 0; i < sourceEntries.Count; i++)
                {
                    var entry = sourceEntries[i];
                    string newDbId = $"{newDbIdBase}-{(i + 1):D4}";
                    newEntries.Add(new ProductListEntry
                    {
                        Name = entry.Name,
                        Dim1 = entry.Dim1,
                        Order = entry.Order,
                        DatabaseId = newDbId
                    });
                }

                WriteProductList(targetTxtPath, newEntries);

                // Step 7: Copy .png icon if it exists
                if (File.Exists(sourcePngPath))
                {
                    string targetPngPath = Path.Combine(outputDir, newItemName + ".png");
                    File.Copy(sourcePngPath, targetPngPath, true);
                    Princ($"Copied icon: {targetPngPath}");
                }

                // Summary
                string summary =
                    $"Clone completed successfully!\n\n" +
                    $"Source: {sourceNameNoExt}\n" +
                    $"Target: {newItemName}\n" +
                    $"Location: {outputDir}\n\n" +
                    $"Files created:\n" +
                    $"  {newItemName}.itm\n" +
                    $"  {newItemName}.Txt ({newEntries.Count} entries)\n" +
                    (File.Exists(sourcePngPath) ? $"  {newItemName}.png\n" : "") +
                    $"\nDatabaseId range: {newDbIdBase}-0001 through -{newEntries.Count:D4}\n\n" +
                    "IMPORTANT: Open MAP and refresh the item folder to register\n" +
                    "the new item in the product database. Then set labor rates\n" +
                    "for each size entry in the product editor.";

                MessageBox.Show(summary, "CreateFromClone Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                Princ($"CreateFromClone: Created {newItemName} with {newEntries.Count} product list entries.");
                Princ($"DatabaseId range: {newDbIdBase}-0001 through -{newEntries.Count:D4}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"CreateFromClone error:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "CreateFromClone Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Princ($"CreateFromClone error: {ex.Message}");
            }
        }

        #endregion

        #region Product List Helpers

        /// <summary>
        /// Represents a single entry in a Fabrication product list .Txt file.
        /// Format: Name,DIM1,Order,ID
        /// </summary>
        private class ProductListEntry
        {
            public string Name { get; set; }
            public string Dim1 { get; set; }
            public string Order { get; set; }
            public string DatabaseId { get; set; }
        }

        /// <summary>
        /// Read a Fabrication product list .Txt file.
        /// Expected format: Name,DIM1,Order,ID (first row is header).
        /// </summary>
        private static List<ProductListEntry> ReadProductList(string txtPath)
        {
            var entries = new List<ProductListEntry>();

            try
            {
                var lines = File.ReadAllLines(txtPath);
                // Skip header row
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');
                    if (parts.Length >= 4)
                    {
                        entries.Add(new ProductListEntry
                        {
                            Name = parts[0].Trim(),
                            Dim1 = parts[1].Trim(),
                            Order = parts[2].Trim(),
                            DatabaseId = parts[3].Trim()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error reading product list:\n{ex.Message}",
                    "Read Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }

            return entries;
        }

        /// <summary>
        /// Write a Fabrication product list .Txt file.
        /// Format: Name,DIM1,Order,ID (with header row).
        /// </summary>
        private static void WriteProductList(string txtPath, List<ProductListEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,DIM1,Order,ID");

            foreach (var entry in entries)
            {
                sb.AppendLine($"{entry.Name},{entry.Dim1},{entry.Order},{entry.DatabaseId}");
            }

            File.WriteAllText(txtPath, sb.ToString());
        }

        #endregion
    }
}
