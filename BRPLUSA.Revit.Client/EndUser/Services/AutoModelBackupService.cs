﻿using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using BRPLUSA.Revit.Entities.Interfaces;
using BRPLUSA.Revit.Services.Updates;

namespace BRPLUSA.Revit.Client.EndUser.Services
{
    public class AutoModelBackupService : IRegisterableUpdater
    {
        private ModelBackupService BackupService { get; set; }

        public AutoModelBackupService()
        {
            BackupService = new ModelBackupService();
        }

        public void Register(Document doc)
        {
            BackupService.RegisterAutoBackup();
        }

        public void Deregister()
        {
            BackupService.DeregisterAutoBackup();
        }
    }
}
