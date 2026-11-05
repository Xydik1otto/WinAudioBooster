namespace WinAudioBooster.Services;

public class AppVolumeService
{
    public Task<List<AppSessionInfo>> GetActiveAudioSessionsAsync(MMDevice? device)
    {
        return Task.Run(() =>
        {
            var resultList = new List<AppSessionInfo>();
            if (device == null) return resultList;

            try
            {
                var sessionManager = device.AudioSessionManager;
                var sessions = sessionManager?.Sessions;
                if (sessions == null) return resultList;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    int pid = (int)session.GetProcessID;
                    if (pid <= 0) continue;

                    string name = session.DisplayName;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        try
                        {
                            var proc = Process.GetProcessById(pid);
                            name = !string.IsNullOrEmpty(proc.MainWindowTitle) ? proc.MainWindowTitle : proc.ProcessName;
                        }
                        catch { name = $"Process ({pid})"; }
                    }

                    float currentVol = session.SimpleAudioVolume.Volume * 100.0f;
                    bool isMuted = session.SimpleAudioVolume.Mute;

                    resultList.Add(new AppSessionInfo
                    {
                        ProcessId = pid,
                        ProcessName = name,
                        DisplayName = name,
                        VolumePercent = currentVol,
                        IsMuted = isMuted
                    });
                }
            }
            catch { }

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
                        // Якщо гучність <= 100% -> змінюємо системну гучність WASAPI
                        // Якщо > 100% -> тримаємо системну гучність на 100% (1.0f), а решту компенсує загальний Boost DSP
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