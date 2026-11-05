// WinAudioApo - apo-config.ini parser (starter skeleton).
// Copyright (c) WinAudioBooster contributors. License: BSD-2-Clause (see LICENSE-NOTE.md).

#include "ApoConfig.h"
#include <objbase.h>
#include <shlobj.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

// Defaults mirror DspProcessor: unity gain, flat EQ, -0.3 dB limiter ceiling.
ApoConfig::ApoConfig() :
    m_path(),
    m_lastWrite(),
    m_checkedOnce(false),
    m_values()
{
    m_values.GainDb = 0.0f;
    for (int i = 0; i < WINAPO_BAND_COUNT; ++i)
    {
        m_values.EqGainsDb[i] = 0.0f;
    }
    m_values.LimiterCeilingDb = WINAPO_DEFAULT_LIMITER_CEILING_DB;
}

void ApoConfig::ResolvePath()
{
    m_path[0] = L'\0';

    wchar_t appData[MAX_PATH] = {};
    // Use %APPDATA% (roaming profile), matching the WPF configurator's
    // %APPDATA%\WinAudioBooster folder.
    if (!ExpandEnvironmentStringsW(L"%APPDATA%", appData, MAX_PATH) || !appData[0])
    {
        return;
    }

    // TODO: switch to SHGetKnownFolderPath(FOLDERID_RoamingAppData) and give
    // the WPF app a command line switch to redirect the config for testing.
    _snwprintf_s(m_path, _TRUNCATE, L"%s\\WinAudioBooster\\apo-config.ini", appData);
}

bool ApoConfig::CheckStale()
{
    if (!m_path[0])
    {
        return false;
    }

    WIN32_FILE_ATTRIBUTE_DATA fad = {};
    if (!GetFileAttributesExW(m_path, GetFileExInfoStandard, &fad))
    {
        // File missing (configurator not installed yet): keep current values.
        m_checkedOnce = true;
        return false;
    }

    const ULONGLONG stamp =
        (static_cast<ULONGLONG>(fad.ftLastWriteTime.dwHighDateTime) << 32) |
        fad.ftLastWriteTime.dwLowDateTime;
    const ULONGLONG last =
        (static_cast<ULONGLONG>(m_lastWrite.dwHighDateTime) << 32) |
        m_lastWrite.dwLowDateTime;

    if (m_checkedOnce && stamp == last)
    {
        return false; // unchanged - steady-state path is a single stat call
    }

    m_lastWrite = fad.ftLastWriteTime;
    m_checkedOnce = true;

    Reload(); // on parse failure the previous values are kept
    return true;
}

bool ApoConfig::Reload()
{
    HANDLE hFile = CreateFileW(m_path, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (hFile == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    LARGE_INTEGER size = {};
    ApoConfigValues parsed = m_values; // unknown keys keep the current values
    bool ok = false;

    if (GetFileSizeEx(hFile, &size) && size.QuadPart > 0 && size.QuadPart < (1 << 20))
    {
        // TODO: replace the fixed-size read buffer with streaming parsing
        //       before large (macro-heavy) configs are supported.
        const DWORD cb = static_cast<DWORD>(size.QuadPart);
        char* text = static_cast<char*>(HeapAlloc(GetProcessHeap(), 0, cb + 1));
        if (text)
        {
            DWORD read = 0;
            if (ReadFile(hFile, text, cb, &read, nullptr) && read == cb)
            {
                text[read] = '\0';

                char* line = text;
                while (line)
                {
                    // Trim leading spaces.
                    while (*line == ' ' || *line == '\t' || *line == '\r') ++line;
                    if (*line == '\0') break;

                    char* eol = strchr(line, '\n');
                    if (eol) *eol = '\0';

                    // Skip comments and section headers; keys are matched
                    // case-insensitively, values are plain numbers or
                    // comma-separated number lists.
                    if (line[0] != ';' && line[0] != '#' && line[0] != '[')
                    {
                        char* eq = strchr(line, '=');
                        if (eq)
                        {
                            *eq = '\0';
                            char* key = line;
                            char* value = eq + 1;
                            // Trim trailing spaces off the key.
                            char* keyEnd = key + strlen(key);
                            while (keyEnd > key && (keyEnd[-1] == ' ' || keyEnd[-1] == '\t')) *(--keyEnd) = '\0';

                            if (!_stricmp(key, "GainDb"))
                            {
                                parsed.GainDb = strtof(value, nullptr);
                            }
                            else if (!_stricmp(key, "LimiterCeilingDb"))
                            {
                                parsed.LimiterCeilingDb = strtof(value, nullptr);
                            }
                            else if (!_stricmp(key, "EqGainsDb"))
                            {
                                // EqGainsDb=g1,g2,...,g10  (dB, band order 31 Hz..16 kHz)
                                char* cursor = value;
                                int band = 0;
                                while (band < WINAPO_BAND_COUNT)
                                {
                                    char* end = nullptr;
                                    float gain = strtof(cursor, &end);
                                    if (end == cursor) break;
                                    parsed.EqGainsDb[band++] =
                                        min(max(gain, WINAPO_MIN_BAND_GAIN_DB), WINAPO_MAX_BAND_GAIN_DB);
                                    if (!end) break;
                                    while (*end == ' ' || *end == ',') ++end;
                                    if (*end == '\0') break;
                                    cursor = end;
                                }
                                // Fewer than 10 entries: the rest keep their
                                // previous values (partial-update friendly).
                            }
                        }
                    }

                    line = eol ? eol + 1 : nullptr;
                }
                ok = true;
            }
            HeapFree(GetProcessHeap(), 0, text);
        }
    }

    CloseHandle(hFile);

    if (ok)
    {
        m_values = parsed;
    }
    return ok;
}
