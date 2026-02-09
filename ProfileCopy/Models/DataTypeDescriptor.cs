using System.Collections.Generic;
using System.ComponentModel;

namespace FabricationSample.ProfileCopy.Models
{
    /// <summary>
    /// Enum of copyable database data types.
    /// Each maps to a specific .MAP file found in the profile's DATABASE folder.
    /// </summary>
    public enum DataType
    {
        Services,
        Costs,
        InstallationTimes,
        FabricationTimes,
        Materials,
        Specifications,
        Sections,
        Ancillaries,
        Suppliers,
        Connectors,
        Seams,
        Layers,
        Setup,
        Dampers,
        Airturn,
        Diameters,
        Cutouts,
        Notches,
        Stiffeners,
        Leads,
        Splitters,
        Silencers,
        Support,
        Takeoff,
        Facings,
        Notes,
        Resistance,
        ResistLink,
        StressLoad,
        PartNames,
        TextAttributes,
        HardwareSpecs,
        InsulationSpecs,
        DrawingDb,
        Nesting,
        ToolDefaults
    }

    /// <summary>
    /// Describes a copyable data type with its .MAP filename and UI state.
    /// Filenames match the actual files found in Fabrication profile DATABASE folders.
    /// </summary>
    public class DataTypeDescriptor : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isAvailable;
        private List<string> _selectedItems;
        private int? _manifestItemCount;

        public DataType DataType { get; set; }
        public string DisplayName { get; set; }
        public string FileName { get; set; }
        public string Group { get; set; }

        /// <summary>
        /// Whether this data type has an API collection that can be enumerated for preview.
        /// </summary>
        public bool IsEnumerable { get; set; }

        /// <summary>
        /// Whether this data type has Delete + Save API methods for post-restart cleanup.
        /// </summary>
        public bool SupportsSelectiveCleanup { get; set; }

        /// <summary>
        /// The manifest key used to look up items in ProfileManifest.DataTypes.
        /// Matches the key used in ProfileManifestService.GenerateManifest().
        /// </summary>
        public string ManifestKey { get; set; }

        /// <summary>
        /// null = copy all items (no selective filtering).
        /// Non-null list = only these named items should be kept after copy.
        /// </summary>
        public List<string> SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedItems)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionIndicator)));
            }
        }

        /// <summary>
        /// Total items from the manifest for this data type (for display).
        /// </summary>
        public int? ManifestItemCount
        {
            get => _manifestItemCount;
            set
            {
                _manifestItemCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ManifestItemCount)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionIndicator)));
            }
        }

        /// <summary>
        /// Display text like "(35/45)" when a selective filter is applied, empty otherwise.
        /// </summary>
        public string SelectionIndicator
        {
            get
            {
                if (SelectedItems != null && ManifestItemCount.HasValue)
                    return $"({SelectedItems.Count}/{ManifestItemCount.Value})";
                return "";
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public bool IsAvailable
        {
            get => _isAvailable;
            set
            {
                if (_isAvailable != value)
                {
                    _isAvailable = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAvailable)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the most commonly used data type descriptors (shown first in UI).
        /// These are the files users typically want to copy between profiles.
        /// </summary>
        public static DataTypeDescriptor[] GetAllDescriptors()
        {
            return new[]
            {
                // Price & Labor group - these files update together when pricing/labor changes
                new DataTypeDescriptor { DataType = DataType.Suppliers, DisplayName = "Suppliers", FileName = "SUPPLIER.MAP", Group = "Price & Labor",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Suppliers" },
                new DataTypeDescriptor { DataType = DataType.Setup, DisplayName = "Setup", FileName = "SETUP.MAP", Group = "Price & Labor",
                    IsEnumerable = false, SupportsSelectiveCleanup = false },
                new DataTypeDescriptor { DataType = DataType.Costs, DisplayName = "Costs / Price Lists", FileName = "Cost.MAP", Group = "Price & Labor",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Costs" },
                new DataTypeDescriptor { DataType = DataType.InstallationTimes, DisplayName = "Installation Times", FileName = "ETimes.MAP", Group = "Price & Labor",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "InstallationTimes" },

                // Primary data types
                new DataTypeDescriptor { DataType = DataType.Services, DisplayName = "Services", FileName = "service.map", Group = "Primary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Services" },
                new DataTypeDescriptor { DataType = DataType.FabricationTimes, DisplayName = "Fabrication Times", FileName = "FTimes.MAP", Group = "Primary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "FabricationTimes" },
                new DataTypeDescriptor { DataType = DataType.Materials, DisplayName = "Materials", FileName = "Material.MAP", Group = "Primary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Materials" },
                new DataTypeDescriptor { DataType = DataType.Specifications, DisplayName = "Specifications", FileName = "Specs.MAP", Group = "Primary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Specifications" },
                new DataTypeDescriptor { DataType = DataType.Sections, DisplayName = "Sections", FileName = "sections.map", Group = "Primary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Sections" },
                new DataTypeDescriptor { DataType = DataType.Ancillaries, DisplayName = "Ancillaries", FileName = "ANCILLRY.MAP", Group = "Primary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Ancillaries" },

                // Secondary data types
                new DataTypeDescriptor { DataType = DataType.Connectors, DisplayName = "Connectors", FileName = "Connectr.map", Group = "Secondary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Connectors" },
                new DataTypeDescriptor { DataType = DataType.Seams, DisplayName = "Seams", FileName = "seam.map", Group = "Secondary",
                    IsEnumerable = true, SupportsSelectiveCleanup = false, ManifestKey = "Seams" },
                new DataTypeDescriptor { DataType = DataType.Layers, DisplayName = "Layers", FileName = "layers.MAP", Group = "Secondary",
                    IsEnumerable = false, SupportsSelectiveCleanup = false },
                new DataTypeDescriptor { DataType = DataType.Dampers, DisplayName = "Dampers", FileName = "DAMPER.MAP", Group = "Secondary",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Dampers" },
                new DataTypeDescriptor { DataType = DataType.Diameters, DisplayName = "Diameters", FileName = "Diameter.MAP", Group = "Secondary",
                    IsEnumerable = false, SupportsSelectiveCleanup = false },

                // Additional data types
                new DataTypeDescriptor { DataType = DataType.Airturn, DisplayName = "Airturn", FileName = "Airturn.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Cutouts, DisplayName = "Cutouts", FileName = "Cutouts.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Notches, DisplayName = "Notches", FileName = "Notches.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Stiffeners, DisplayName = "Stiffeners", FileName = "STIFFNER.MAP", Group = "Other",
                    IsEnumerable = true, SupportsSelectiveCleanup = true, ManifestKey = "Stiffeners" },
                new DataTypeDescriptor { DataType = DataType.Leads, DisplayName = "Leads", FileName = "LEADS.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Splitters, DisplayName = "Splitters", FileName = "splitter.MAP", Group = "Other",
                    IsEnumerable = true, SupportsSelectiveCleanup = false, ManifestKey = "Splitters" },
                new DataTypeDescriptor { DataType = DataType.Silencers, DisplayName = "Silencers", FileName = "Silencer.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Support, DisplayName = "Support", FileName = "SUPPORT.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Takeoff, DisplayName = "Takeoff", FileName = "TAKEOFF.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Facings, DisplayName = "Facings", FileName = "FACINGS.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Notes, DisplayName = "Notes", FileName = "Notes.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Resistance, DisplayName = "Resistance", FileName = "RESISTANCE.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.ResistLink, DisplayName = "Resistance Links", FileName = "RESISTLINK.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.StressLoad, DisplayName = "Stress Load", FileName = "StressLd.map", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.PartNames, DisplayName = "Part Names", FileName = "PARTNAME.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.TextAttributes, DisplayName = "Text Attributes", FileName = "TEXTATTS.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.HardwareSpecs, DisplayName = "Hardware Specs", FileName = "HSpecs.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.InsulationSpecs, DisplayName = "Insulation Specs", FileName = "ISpecs.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.DrawingDb, DisplayName = "Drawing Database", FileName = "dwgdb.map", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.Nesting, DisplayName = "Nesting", FileName = "NESTING.MAP", Group = "Other" },
                new DataTypeDescriptor { DataType = DataType.ToolDefaults, DisplayName = "Tool Defaults", FileName = "TOOLDFLT.MAP", Group = "Other" },
            };
        }
    }
}
