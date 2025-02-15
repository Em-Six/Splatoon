﻿using Dalamud.Configuration;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface.Internal.Notifications;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Splatoon
{
    [Serializable]
    class Configuration : IPluginConfiguration
    {
        [NonSerialized] Splatoon plugin;
        [NonSerialized] SemaphoreSlim ZipSemaphore;

        public int Version { get; set; } = 1;

        public Dictionary<string, Layout> Layouts = new Dictionary<string, Layout>();
        public bool dumplog = false;
        public bool verboselog = false;
        public int segments = 100;
        public float maxdistance = 100;
        //public float maxcamY = 0.05f;
        public int ChlogReadVer = ChlogGui.ChlogVersion;
        public int lineSegments = 10;
        public bool UseHttpServer = false;
        public int port = 47774;
        public bool TetherOnFind = true;
        public bool LimitTriggerMessages = false;
        public bool DirectNameComparison = false;
        public bool NoMemory = false;
        public bool ShowOnUiHide = false;

        public void Initialize(Splatoon plugin)
        {
            this.plugin = plugin;
            ZipSemaphore = new SemaphoreSlim(1);
            Svc.PluginInterface.UiBuilder.OpenConfigUi += delegate
            {
                plugin.ConfigGui.Open = true;
            };
        }

        public void Save()
        {
            Svc.PluginInterface.SavePluginConfig(this);
        }

        public bool Backup()
        {
            if (!ZipSemaphore.Wait(0))
            {
                LogErrorAndNotify("Failed to create backup: previous backup did not completed yet. ");
                return false;
            }
            string tempDir = null;
            string bkpFile = null;
            string tempFile = null;
            try
            {
                var cFile = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "..", "Splatoon.json");
                var configStr = File.ReadAllText(cFile);
                var bkpFPath = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "Backups");
                Directory.CreateDirectory(bkpFPath);
                tempDir = Path.Combine(bkpFPath, "temp");
                Directory.CreateDirectory(tempDir);
                tempFile = Path.Combine(tempDir, "Splatoon.json");
                bkpFile = Path.Combine(bkpFPath, "Backup." + DateTimeOffset.Now.ToString("yyyy-MM-dd HH-mm-ss-fffffff") + ".zip");
                File.Copy(cFile, tempFile, true);
            }
            catch(FileNotFoundException e)
            {
                ZipSemaphore.Release();
                LogErrorAndNotify(e, "Could not find configuration to backup.");
            }
            catch(Exception e)
            {
                ZipSemaphore.Release();
                LogErrorAndNotify(e, "Failed to create a backup:\n" + e.Message);
            }
            Task.Run(new Action(delegate { 
                try
                {
                    ZipFile.CreateFromDirectory(tempDir, bkpFile, CompressionLevel.Optimal, false);
                    File.Delete(tempFile);
                    plugin.tickScheduler.Enqueue(delegate
                    {
                        plugin.Log("Backup created: " + bkpFile);
                        Notify("A backup of your current configuration has been created.", NotificationType.Info);
                    });
                }
                catch (Exception e)
                {
                    plugin.tickScheduler.Enqueue(delegate
                    {
                        plugin.Log("Failed to create backup: " + e.Message, true);
                        plugin.Log(e.StackTrace, true);
                    });
                }
                ZipSemaphore.Release();
            }));
            return true;
        }
    }
}
