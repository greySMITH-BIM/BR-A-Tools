﻿using System;
using System.Threading.Tasks;
using BRPLUSA.Core.Services;
using BRPLUSA.Revit.Installers._2018.Entities;
using BRPLUSA.Revit.Installers._2018.Services;

namespace BRPLUSA.Revit.Installers._2018
{
    /// <summary>
    /// This holds the status of the installation and manages
    /// the process of installing and upgrading the product
    /// </summary>
    public class InstallManager
    {
        public bool Revit2018Installed { get; private set; }
        public bool AppFor2018HasUpdateAvailable { get; private set; }
        public bool AppFor2018Installed { get; private set; }
        public VersionData AppVersion { get; private set; }
        private InstallHandlingService InstallHandler { get; set; }

        public InstallManager()
        {
            Initialize();
        }

        private void Initialize()
        {
            LoggingService.LogInfo("Initializing InstallationManager");
            DoPreInstallStatusCheck();

            if(Revit2018Installed)
                InitializeHandlers();

            LoggingService.LogInfo("Initialized InstallationManager");
        }

        private void DoPreInstallStatusCheck()
        {
            Revit2018Installed = InstallStatusService.CheckIfRevit2018Installed();
        }

        private void InitializeHandlers()
        {
            InstallHandler = new InstallHandlingService();
        }

        public async Task InitializeAppStateAsync()
        {
            await InstallHandler.InitializeAppStateAsync().ConfigureAwait(false);
            SetInstallationStatuses();
        }

        private void SetInstallationStatuses()
        {
            AppFor2018Installed = InstallHandler.Revit2018AppInstalled;
            AppFor2018HasUpdateAvailable = InstallHandler.Revit2018AppUpdateAvailable;
            //AppVersion = InstallHandler.AppVersion
        }

        public async Task<bool> HandleRevit2018ApplicationInstallation()
        {
            var success = await InstallHandler.HandleRevit2018Installation();

            return success;
        }
    }
}
