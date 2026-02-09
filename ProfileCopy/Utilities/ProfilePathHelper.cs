using System;
using System.IO;
using System.Linq;

namespace FabricationSample.ProfileCopy.Utilities
{
    /// <summary>
    /// Helper methods for resolving and validating Fabrication profile paths.
    ///
    /// Fabrication database structure (from MAP.ini [PROGRAM PATHS]):
    ///   {database root}/
    ///     MAP.INI          - Master configuration, defines all paths
    ///     DATABASE/        - Global profile .MAP files (same level as profiles/)
    ///     profiles/        - Profile folders, each with its own DATABASE subfolder
    ///       profiles.map   - Binary profile registry
    ///       {ProfileName}/
    ///         DATABASE/    - Profile-specific .MAP files
    ///
    /// When on a named profile: Application.DatabasePath = {root}/profiles/{Name}/DATABASE
    /// When on Global profile:  Application.DatabasePath = {root}/DATABASE
    /// </summary>
    public static class ProfilePathHelper
    {
        /// <summary>
        /// Gets the database root directory from the current profile's DATABASE path.
        ///
        /// Strategy: Look for a "profiles" path segment to determine if we're on a named profile.
        ///   Named profile path: {root}/profiles/{Name}/DATABASE → root = segment before "profiles"
        ///   Global profile path: {root}/database → root = parent of "database"
        ///
        /// Falls back to walking up looking for MAP.INI or directories named "database"+"profiles".
        /// </summary>
        public static string GetDatabaseRoot(string databasePath)
        {
            if (string.IsNullOrEmpty(databasePath))
                return null;

            string fullPath = Path.GetFullPath(databasePath);
            char sep = Path.DirectorySeparatorChar;

            // Strategy 1: Look for a "profiles" path segment
            // Path like: C:\...\Root\profiles\ProfileName\DATABASE
            // Split and find "profiles" to locate the root
            string[] parts = fullPath.Split(sep);
            for (int i = parts.Length - 1; i >= 1; i--)
            {
                if (parts[i].Equals("profiles", StringComparison.OrdinalIgnoreCase))
                {
                    // Root is everything before "profiles"
                    string root = string.Join(sep.ToString(), parts.Take(i));
                    if (Directory.Exists(root))
                        return root;
                }
            }

            // Strategy 2: Not on a named profile - current path IS the main database folder
            // Path like: C:\...\Root\database → root is parent
            var dbDir = new DirectoryInfo(fullPath);
            if (dbDir.Parent != null)
            {
                string parentPath = dbDir.Parent.FullName;

                // Verify this looks like a database root (has MAP.INI or a profiles folder)
                if (File.Exists(Path.Combine(parentPath, "MAP.INI")) ||
                    File.Exists(Path.Combine(parentPath, "map.ini")) ||
                    HasSubdirectoryNamed(parentPath, "profiles"))
                    return parentPath;

                // Last resort: just return the parent
                return parentPath;
            }

            return null;
        }

        /// <summary>
        /// Gets the Global profile's DATABASE folder path at the database root.
        /// The Global DATABASE folder sits at {root}\DATABASE - same naming convention
        /// as profile DATABASE subfolders but at the root level alongside profiles\.
        /// </summary>
        public static string FindMainDatabaseFolder(string databaseRoot)
        {
            if (string.IsNullOrEmpty(databaseRoot))
                return null;

            // Global profile DATABASE is always at {root}\DATABASE
            return Path.Combine(databaseRoot, "DATABASE");
        }

        /// <summary>
        /// Checks if a directory has a subdirectory with the given name (case-insensitive).
        /// </summary>
        private static bool HasSubdirectoryNamed(string parentPath, string name)
        {
            try
            {
                return Directory.GetDirectories(parentPath)
                    .Any(d => Path.GetFileName(d).Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the profiles directory path relative to the database root.
        /// As defined in MAP.ini: Profiles=./profiles
        /// </summary>
        public static string GetProfilesDirectory(string databaseRoot)
        {
            if (string.IsNullOrEmpty(databaseRoot))
                return null;

            return Path.Combine(databaseRoot, "profiles");
        }

        /// <summary>
        /// Gets the DATABASE subfolder path for a given profile path.
        /// </summary>
        public static string GetProfileDatabasePath(string profilePath)
        {
            return Path.Combine(profilePath, "DATABASE");
        }

        /// <summary>
        /// Checks whether a directory appears to be a valid Fabrication profile
        /// (has a DATABASE subfolder with .MAP files).
        /// </summary>
        public static bool IsValidProfile(string profilePath)
        {
            if (string.IsNullOrEmpty(profilePath) || !Directory.Exists(profilePath))
                return false;

            string dbPath = GetProfileDatabasePath(profilePath);
            if (!Directory.Exists(dbPath))
                return false;

            // Must have at least one .MAP file
            try
            {
                return Directory.GetFiles(dbPath, "*.MAP", SearchOption.TopDirectoryOnly).Length > 0 ||
                       Directory.GetFiles(dbPath, "*.map", SearchOption.TopDirectoryOnly).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the full path to a .MAP file within a DATABASE folder.
        /// </summary>
        public static string GetMapFilePath(string databasePath, string mapFileName)
        {
            return Path.Combine(databasePath, mapFileName);
        }

        /// <summary>
        /// Checks whether a specific .MAP file exists in the DATABASE folder.
        /// </summary>
        public static bool MapFileExists(string databasePath, string mapFileName)
        {
            string path = GetMapFilePath(databasePath, mapFileName);
            return File.Exists(path);
        }

        /// <summary>
        /// Gets the backup directory path for FabricationSample.
        /// </summary>
        public static string GetBackupDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FabricationSample", "Backups");
        }

        /// <summary>
        /// Ensures a directory exists, creating it if necessary.
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
