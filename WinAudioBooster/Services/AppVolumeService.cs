using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using WinAudioBooster.Models;

namespace WinAudioBooster.Services
{
    public class AppVolumeService
    {
        public Task<List<AppSessionInfo>> GetActiveAudioSessionsAsync(MMDevice? device)
        {
            return Task.Run(() =>
            {
                var sessionsMap = new Dictionary<int, (float Volume, bool IsMuted, string DisplayName)>();

                // 1. Збираємо активні аудіосесії з WASAPI (якщо програма зараз грає звук)
                try
                {
                    var sessionManager = device?.AudioSessionManager;
                    var sessions = sessionManager?.Sessions;
                    if (sessions != null)
                    {
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            var session = sessions[i];
                            int pid = (int)session.GetProcessID;
                            if (pid <= 0) continue;

                            float vol = session.SimpleAudioVolume.Volume * 100.0f;
                            bool muted = session.SimpleAudioVolume.Mute;
                            string name = session.DisplayName;

                            sessionsMap[pid] = (vol, muted, name);
                        }
                    }
                }
                catch { }

                var resultList = new List<AppSessionInfo>();
                var processedProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 2. Скануємо всі запущені програми з головними вікнами в Windows
                var runningProcesses = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && p.Id > 0);

                foreach (var proc in runningProcesses)
                {
                    try
                    {
                        string procName = proc.ProcessName;
                        if (processedProcessNames.Contains(procName)) continue;
                        processedProcessNames.Add(procName);

                        int pid = proc.Id;
                        float volumePercent = 100.0f;
                        bool isMuted = false;
                        string displayName = proc.MainWindowTitle;

                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            displayName = procName;
                        }

                        // Якщо у процеса є активна аудіосесія — беремо її реальні параметри гучності
                        if (sessionsMap.TryGetValue(pid, out var audioInfo))
                        {
                            volumePercent = audioInfo.Volume;
                            isMuted = audioInfo.IsMuted;
                            if (!string.IsNullOrWhiteSpace(audioInfo.DisplayName))
                            {
                                displayName = audioInfo.DisplayName;
                            }
                        }

                        resultList.Add(new AppSessionInfo
                        {
                            ProcessId = pid,
                            ProcessName = procName,
                            DisplayName = displayName,
                            VolumePercent = volumePercent,
                            IsMuted = isMuted
                        });
                    }
                    catch { }
                }

                return resultList.OrderBy(a => a.DisplayName).ToList();
            });
        }

        public Task SetAppVolumeAndBoostAsync(MMDevice? device, int processId, double volumePercent)
        {
            return Task.Run(() =>
            {
                if (device == null) return;
                try
                {
                    var sessions = device.AudioSessionManager?.Sessions;
                    if (sessions == null) return;

                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var session = sessions[i];
                        if ((int)session.GetProcessID == processId)
                        {
                            float targetVol = volumePercent <= 100.0 ? (float)(volumePercent / 100.0) : 1.0f;
                            if (Math.Abs(session.SimpleAudioVolume.Volume - targetVol) > 0.01f)
                            {
                                session.SimpleAudioVolume.Volume = targetVol;
                            }
                        }
                    }
                }
                catch { }
            });
        }
    }
}