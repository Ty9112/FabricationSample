using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace FabricationSample.Utilities
{
    /// <summary>
    /// Converts a ProductDefinition's SupplierIds collection to a display string.
    /// When ConverterParameter is set to a supplier name, returns just that supplier's ID.
    /// When no parameter is set, shows all "SupplierName: ExternalId" pairs.
    /// </summary>
    public class SupplierIdsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var supplierIds = value as System.Collections.IEnumerable;
            if (supplierIds == null) return string.Empty;

            string targetSupplier = parameter as string;

            try
            {
                if (targetSupplier != null)
                {
                    // Single supplier mode - return just that supplier's ID value
                    foreach (dynamic s in supplierIds)
                    {
                        try
                        {
                            string name = s.ProductSupplier?.Name;
                            if (name != null && name.Equals(targetSupplier, StringComparison.OrdinalIgnoreCase))
                                return s.Id ?? "";
                        }
                        catch { }
                    }
                    return "";
                }

                // All suppliers mode - concatenated string
                var parts = supplierIds.Cast<dynamic>()
                    .Where(s => s != null)
                    .Select(s =>
                    {
                        string supplierName = "Unknown";
                        string externalId = "N/A";
                        try { supplierName = s.ProductSupplier?.Name ?? "Unknown"; } catch { }
                        try { externalId = s.Id ?? "N/A"; } catch { }
                        return $"{supplierName}: {externalId}";
                    })
                    .ToList();

                return string.Join(", ", parts);
            }
            catch
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
