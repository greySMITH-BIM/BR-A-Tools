﻿using System;
using System.IO;
using BRPLUSA.Core.Services;

namespace BRPLUSA.Revit.Installers._2018.Providers
{
    public class RevitAddinLocationProvider
    {
        private static string ApplicationData
        {
            get
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }
        }

        private static string RevitAddinLocation
        {
            get
            {
                return ApplicationData + @"\Autodesk\Revit\Addins";
            }
        }

        private static string Revit2018AddinLocation
        {
            get
            {
                var location = RevitAddinLocation + @"\2018";
                LoggingService.LogInfo($"Revit 2018 found here: {location}");
                return location;
            }
        }

        private static string Revit2019AddinLocation
        {
            get
            {
                return RevitAddinLocation + @"\2019";
            }
        }

        private static string Revit2020AddinLocation
        {
            get
            {
                return RevitAddinLocation + @"\2020";
            }
        }

        public static string GetRevitAddinFolderLocation(RevitVersion version)
        {
            LoggingService.LogInfo("Searching for Revit addin locations...");
            switch (version)
            {
                case RevitVersion.V2018:
                    return Revit2018AddinLocation;
                case RevitVersion.V2019:
                    return Revit2019AddinLocation;
                case RevitVersion.V2020:
                    return Revit2020AddinLocation;

                default:
                case RevitVersion.Unknown:
                    var exception = new Exception("Unknown version");
                    LoggingService.LogError("Couldn't identify this version of Revit", exception);
                    throw exception;
            }
        }

        public static bool IsRevitVersionInstalled(RevitVersion revitVersion)
        {
            LoggingService.LogInfo("Checking for Revit installations");
            var addinFolder = GetRevitAddinFolderLocation(revitVersion);

            var exists = Directory.Exists(addinFolder);
            var status = exists ? "This version of Revit does exist" : "This version of Revit doesn't exist";
            LoggingService.LogInfo(status);

            return exists;
        }
    }
}
