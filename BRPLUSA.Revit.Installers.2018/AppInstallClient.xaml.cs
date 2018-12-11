﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using BRPLUSA.Core.Services;

namespace BRPLUSA.Revit.Installers._2018
{
    /// <summary>
    /// * DOCUMENTATION
    /// *
    /// * This app is two pieces, an installer and an updater
    /// *
    /// ** ############################ INSTALLER ########################################
    /// *
    /// * The installer's process is run via Squirrel and is documented on their site here:
    /// * https://github.com/Squirrel/Squirrel.Windows/blob/master/docs/using/install-process.md#install-process-overview
    /// *
    /// * This is the start point for the installer but I'm still trying to figure out how it's run exactly
    /// *
    /// * ############################ UPDATER ########################################
    /// *
    /// * The updater is primarily run via Revit and it's start point is "technically" in the Uninitialize method of RevitApplicationEnhancements
    /// * over in BRPLUSA.Revit.Client
    /// *
    /// * Basically, every time the user exits Revit, the app runs a check on the server to see if there's an updated version of the package available.
    /// *
    /// * #################################################################################
    /// *
    /// *
    /// </summary>
    public partial class AppInstallClient : Application
    {
        private bool _makeVisible;

        public AppInstallClient(bool startSilent = false) : base()
        {
            _makeVisible = !startSilent;
        }

        private InstallManager Manager { get; set; }

        public bool Revit2018Installed
        {
            get
            {
                return Manager.Revit2018Installed;
            }
        }

        public bool AppFor2018Installed
        {
            get
            {
                return Manager.AppFor2018Installed;
            }
        }

        public bool AppFor2018HasUpdate
        {
            get
            {
                return Manager.AppFor2018HasUpdateAvailable;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            InitializeServices();
        }

        private async Task InitializeServices()
        {
            LoggingService.LogInfo("Starting installation app client");

            Manager = new InstallManager();
            await Manager.InitializeAppStateAsync();

            MainWindow = new ProductSelectionView(Manager);

            if (_makeVisible)
                MainWindow.Show();
        }

        public void Reveal()
        {
            MainWindow.Show();
        }

        public async Task<bool> GetAppUpdateStatus()
        {
            await InitializeServices();

            return AppFor2018HasUpdate;
        }
    }
}
