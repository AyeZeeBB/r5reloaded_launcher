﻿using DiscordRPC;
using launcher.Global;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using static launcher.Game.LaunchParameters;
using static launcher.Global.Logger;
using static launcher.Global.References;

namespace launcher.Game
{
    public static class GameUtils
    {
        public static void Launch()
        {
            Managers.App.EAAppCodes eaAppStatus = Managers.App.IsEAAppRunning();

            appDispatcher.Invoke(new Action(() =>
            {
                if (eaAppStatus == Managers.App.EAAppCodes.Not_Installed)
                {
                    LogError(LogSource.Launcher, "EA App is not installed. Please install the EA App and try again.");
                    MessageBox.Show(new Form { TopMost = true }, "EA App is not installed. Please install the EA App and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                if (eaAppStatus == Managers.App.EAAppCodes.Installed_And_Not_Running)
                {
                    LogError(LogSource.Launcher, "EA App is not running. Please launch the EA App and try again.");
                    MessageBox.Show(new Form { TopMost = true }, "EA App is not running. Please launch the EA App and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }));

            if (eaAppStatus != Managers.App.EAAppCodes.Installed_And_Running)
                return;

            appDispatcher.Invoke(new Action(() =>
            {
                Play_Button.IsEnabled = false;
                Play_Button.Content = "LAUNCHING...";
            }));

            eMode mode = (eMode)(int)Ini.Get(Ini.Vars.Mode);

            string exeName = mode switch
            {
                eMode.HOST => "r5apex.exe",
                eMode.SERVER => "r5apex_ds.exe",
                eMode.CLIENT => "r5apex.exe",
                _ => "r5apex.exe"
            };

            if (!File.Exists($"{GetBranch.Directory()}\\{exeName}"))
                return;

            string gameArguments = BuildParameters();

            var startInfo = new ProcessStartInfo
            {
                FileName = $"{GetBranch.Directory()}\\{exeName}",
                WorkingDirectory = GetBranch.Directory(),
                Arguments = gameArguments,
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process gameProcess = Process.Start(startInfo);

            gameProcess.WaitForInputIdle();

            if (gameProcess != null)
                SetProcessorAffinity(gameProcess);

            LogInfo(LogSource.Launcher, $"Launched game with arguments: {gameArguments}");

            appDispatcher.Invoke(new Action(() =>
            {
                Play_Button.IsEnabled = true;
                Play_Button.Content = "PLAY";
            }));
        }

        private static void SetProcessorAffinity(Process gameProcess)
        {
            try
            {
                int coreCount = int.Parse((string)Ini.Get(Ini.Vars.Processor_Affinity));
                int processorCount = Environment.ProcessorCount;

                if (coreCount == -1 || coreCount == 0)
                    return;

                if (coreCount > processorCount)
                    coreCount = processorCount;

                if (coreCount >= 1 && coreCount <= processorCount)
                {
                    int affinityMask = 0;

                    for (int i = 0; i < coreCount; i++)
                        affinityMask |= 1 << i;

                    gameProcess.ProcessorAffinity = affinityMask;

                    LogInfo(LogSource.Launcher, $"Processor affinity set to the first {coreCount} cores.");
                }
                else
                    LogError(LogSource.Launcher, $"Invalid core index: {coreCount}. Must be between -1 and {processorCount}.");
            }
            catch (Exception ex)
            {
                LogException($"Failed to set processor affinity", LogSource.Launcher, ex);
            }
        }
    }
}