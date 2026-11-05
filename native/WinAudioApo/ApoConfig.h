// WinAudioApo - configuration loader (apo-config.ini) for the WinAudioBooster APO.
// Copyright (c) WinAudioBooster contributors. License: BSD-2-Clause (see LICENSE-NOTE.md).
//
// The APO is loaded inside audiodg.exe, so configuration is exchanged through a
// plain INI file written by the WPF configurator (same parameter names and value
// semantics as WinAudioBooster/Audio/DspProcessor.cs). The APO polls the file's
// LastWriteTime once per processed chunk - a FileSystemWatcher-style refresh
// without a second thread.

#pragma once

#include <windows.h>

// Must match WinAudioBooster/Audio/DspProcessor.cs
#define WINAPO_BAND_COUNT 10
// ISO 1/3-octave centers (DspProcessor.BandFrequencies)
#define WINAPO_EQ_Q 1.2f
#define WINAPO_MIN_BAND_GAIN_DB (-12.0f)
#define WINAPO_MAX_BAND_GAIN_DB 12.0f
#define WINAPO_GAIN_SLEW_DB_PER_SECOND 60.0f
#define WINAPO_LIMITER_KNEE_FACTOR 0.9f

// Default limiter ceiling mirrors DspProcessor.DefaultLimiterCeilingDb (-0.3 dB).
#define WINAPO_DEFAULT_LIMITER_CEILING_DB (-0.3f)

// Parameter values loaded from apo-config.ini.
// Names match Models.AppSettings / Models.Preset fields used by the WPF app.
struct ApoConfigValues
{
    float GainDb;                          // target gain, dB (can be large - this is a booster)
    float EqGainsDb[WINAPO_BAND_COUNT];    // per-band peaking EQ gain, dB
    float LimiterCeilingDb;                // soft limiter ceiling, dB (typically <= 0)
};

// Loads and hot-reloads apo-config.ini from %APPDATA%\WinAudioBooster.
class ApoConfig
{
public:
    ApoConfig();

    // Resolves %APPDATA%\WinAudioBooster\apo-config.ini. Called from the APO
    // constructor; falls back to a disabled (defaults-only) state when the
    // profile folder cannot be resolved.
    void ResolvePath();

    // FileSystemWatcher-style check, called once per processed chunk.
    // Returns true when the file changed (or was loaded for the first time) and
    // a filter rebuild is needed. Reparses the file internally; on parse errors
    // the previous valid values are kept.
    bool CheckStale();

    // Last successfully loaded values (defaults until the first successful load).
    const ApoConfigValues& Current() const { return m_values; }

private:
    bool Reload();

    wchar_t m_path[MAX_PATH];
    FILETIME m_lastWrite;
    bool m_checkedOnce;
    ApoConfigValues m_values;
};
