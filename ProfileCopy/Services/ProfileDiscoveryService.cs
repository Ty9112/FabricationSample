using System;
using System.Collections.Generic;
using System.IO;
using FabricationSample.ProfileCopy.Models;
using FabricationSample.ProfileCopy.Utilities;

namespace FabricationSample.ProfileCopy.Services
{
    /// <summary>
    /// Discovers available Fabrication profiles for data copy operations.
    ///
    /// Database hierarchy:
    ///   {root}/
    ///     DATABASE/                  ← "Global" profile (master .MAP files)
    ///     profiles/{Name}/DATABASE/  ← Named profiles
    ///     MAP.INI
    ///
    /// "Global" is always listed first - it's the root-level DATABASE folder
    /// containing the master set of .MAP files shared across all profiles.
    /// </summary>
    public class ProfileDiscoveryService
    {
        /// <summary>
        /// Discovers all available profiles. Global is always first.
        /// Uses profile name matching (not path comparison) to mark the current profile.
        /// </summary>
        public List<ProfileInfo> GetAvailableProfiles(string currentDatabasePath)
        {
            var profiles = new List<ProfileInfo>();

            if (string.IsNullOrEmpty(currentDatabasePath))
                return profiles;

            string databaseRoot = ProfilePathHelper.GetDatabaseRoot(currentDatabasePath);
            if (string.IsNullOrEmpty(databaseRoot) || !Directory.Exists(databaseRoot))
                return profiles;

            // Use profile NAME to determine current (path comparison is unreliable
            // because Application.DatabasePath may return the root DATABASE path
            // regardless of which named profile is active).
            string currentProfileName = GetCurrentProfileName();
            bool onGlobal = string.IsNullOrEmpty(currentProfileName)
                || currentProfileName.Equals("Global", StringComparison.OrdinalIgnoreCase);

            // Always add Global first - it's {root}\DATABASE
            string globalDbPath = ProfilePathHelper.FindMainDatabaseFolder(databaseRoot);
            profiles.Add(new ProfileInfo
            {
                Name = "Global",
                Path = databaseRoot,
                DatabasePath = globalDbPath,
                Version = Path.GetFileName(databaseRoot),
                IsCurrent = onGlobal
            });

            // Scan named profiles from {root}\profiles\
            string profilesDir = ProfilePathHelper.GetProfilesDirectory(databaseRoot);
            if (!Directory.Exists(profilesDir))
                return profiles;

            try
            {
                foreach (var profileDir in Directory.GetDirectories(profilesDir))
                {
                    if (!ProfilePathHelper.IsValidProfile(profileDir))
                        continue;

                    string profileName = Path.GetFileName(profileDir);
                    string profileDbPath = ProfilePathHelper.GetProfileDatabasePath(profileDir);

                    bool isCurrent = !onGlobal &&
                        profileName.Equals(currentProfileName, StringComparison.OrdinalIgnoreCase);

                    profiles.Add(new ProfileInfo
                    {
                        Name = profileName,
                        Path = profileDir,
                        DatabasePath = profileDbPath,
                        Version = Path.GetFileName(databaseRoot),
                        IsCurrent = isCurrent
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (IOException)
            {
                // Skip on I/O errors
            }

            return profiles;
        }

        /// <summary>
        /// Gets the current profile's database path using the Fabrication API.
        /// </summary>
        public string GetCurrentDatabasePath()
        {
            try
            {
                return Autodesk.Fabrication.ApplicationServices.Application.DatabasePath;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the current profile name using the Fabrication API.
        /// </summary>
        public string GetCurrentProfileName()
        {
            try
            {
                return Autodesk.Fabrication.ApplicationServices.Application.CurrentProfile;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks which .MAP files exist in the given profile's DATABASE folder.
        /// Updates the IsAvailable property on each descriptor.
        /// </summary>
        public void CheckAvailableDataTypes(ProfileInfo profile, DataTypeDescriptor[] descriptors)
        {
            if (profile == null || !profile.IsValid())
                return;

            foreach (var desc in descriptors)
            {
                desc.IsAvailable = ProfilePathHelper.MapFileExists(profile.DatabasePath, desc.FileName);
            }
        }
    }
}
