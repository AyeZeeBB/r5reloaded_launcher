﻿using launcher.Core.Models;
using launcher.Configuration;
using static launcher.Core.UiReferences;

namespace launcher.Services
{
    public static class SetBranch
    {
        public static void UpdateAvailable(bool value, Branch branch = null)
        {
            if (branch != null)
                branch.update_available = value;
            else
                GetBranch.Branch().update_available = value;
        }

        public static void DownloadHDTextures(bool value, Branch branch = null)
        {
            if (branch != null)
                IniSettings.Set(branch.branch, "Download_HD_Textures", value);
            else
                IniSettings.Set(GetBranch.Name(false), "Download_HD_Textures", value);
        }

        public static void Installed(bool value, Branch branch = null)
        {
            if (branch != null)
                IniSettings.Set(branch.branch, "Is_Installed", value);
            else
                IniSettings.Set(GetBranch.Name(false), "Is_Installed", value);
        }

        public static void Version(string value, Branch branch = null)
        {
            if (branch != null)
                IniSettings.Set(branch.branch, "Version", value);
            else
                IniSettings.Set(GetBranch.Name(false), "Version", value);
        }

        public static void EULAAccepted(bool value, Branch branch = null)
        {
            if (branch != null)
                IniSettings.Set(branch.branch, "EULA_Accepted", value);
            else
                IniSettings.Set(GetBranch.Name(false), "EULA_Accepted", value);
        }
    }
}
