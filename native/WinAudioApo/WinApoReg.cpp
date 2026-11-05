// WinAudioApo - COM / APO registration (starter skeleton).
// Copyright (c) WinAudioBooster contributors. License: BSD-2-Clause (see LICENSE-NOTE.md).
//
// Registry layout follows the recipe documented by the Equalizer APO project
// (SourceForge wiki, "Developer documentation") and the DDK helper functions
// RegisterAPO / UnregisterAPO declared in audioenginebaseapo.h.

#include "WinApo.h"
#include "ApoFact.h"

#include <objbase.h>
#include <string>

static HRESULT WriteStringValue(HKEY hRoot, LPCWSTR pwszSubKey, LPCWSTR pwszValueName, LPCWSTR pwszData)
{
    HKEY hKey = nullptr;
    DWORD dwDisposition = 0;
    HRESULT hr = HRESULT_FROM_WIN32(RegCreateKeyExW(hRoot, pwszSubKey, 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &hKey, &dwDisposition));
    if (SUCCEEDED(hr))
    {
        hr = HRESULT_FROM_WIN32(RegSetValueExW(hKey, pwszValueName, 0, REG_SZ,
            reinterpret_cast<const BYTE*>(pwszData),
            static_cast<DWORD>((wcslen(pwszData) + 1) * sizeof(WCHAR))));
        RegCloseKey(hKey);
    }
    return hr;
}

// Converts a GUID to its canonical "{XXXXXXXX-XXXX-...}" form.
static void GuidToString(REFGUID guid, wchar_t (&buf)[64])
{
    StringFromGUID2(guid, buf, 64);
}

STDAPI RegisterWinApo()
{
    wchar_t szClsid[64] = {};
    GuidToString(CLSID_WinAudioApo, szClsid);

    wchar_t szModule[MAX_PATH] = {};
    if (!GetModuleFileNameW(g_hModule, szModule, MAX_PATH))
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    // 1. COM class registration: HKLM\SOFTWARE\Classes\CLSID\{GUID}
    //    (default value = class name; InprocServer32 = DLL path, ThreadingModel = Both).
    HRESULT hr = WriteStringValue(HKEY_LOCAL_MACHINE,
        (std::wstring(L"SOFTWARE\\Classes\\CLSID\\") + szClsid).c_str(),
        nullptr, L"WinAudioApo Class");
    if (FAILED(hr)) return hr;

    hr = WriteStringValue(HKEY_LOCAL_MACHINE,
        (std::wstring(L"SOFTWARE\\Classes\\CLSID\\") + szClsid + L"\\InprocServer32").c_str(),
        nullptr, szModule);
    if (FAILED(hr)) return hr;

    hr = WriteStringValue(HKEY_LOCAL_MACHINE,
        (std::wstring(L"SOFTWARE\\Classes\\CLSID\\") + szClsid + L"\\InprocServer32").c_str(),
        L"ThreadingModel", L"Both");
    if (FAILED(hr)) return hr;

    // 2. APO registration: HKLM\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{GUID}.
    //    TODO (required before the audio engine will load this APO!): fill in
    //    the documented value set (friendly name, copyright, CreatedOn
    //    FILETIME, major/minor version, APO flags) - the supported way is the
    //    DDK helper RegisterAPO() declared in audioenginebaseapo.h (build the
    //    APO_REG_PROPERTIES for CWinApo and call it), or a RegApo-style
    //    registration script shipped by the installer (packaging\apo). The
    //    Equalizer APO project's Configurator does the same writes manually
    //    and is a good reference for the value layout.
    hr = WriteStringValue(HKEY_LOCAL_MACHINE,
        (std::wstring(L"SOFTWARE\\Classes\\AudioEngine\\AudioProcessingObjects\\") + szClsid).c_str(),
        nullptr, L"WinAudioApo (WinAudioBooster gain/EQ/limiter)");
    if (FAILED(hr)) return hr;

    // 3. Device attachment (FxProperties under the endpoint) and the
    //    DisableProtectedAudioDG developer switch are deliberately NOT done
    //    here: they must be reversible and auditable - see packaging\apo\README-uk.md.

    return S_OK;
}

STDAPI UnregisterWinApo()
{
    wchar_t szClsid[64] = {};
    GuidToString(CLSID_WinAudioApo, szClsid);

    // TODO: also remove the AudioProcessingObjects key once registration
    //       writes its full value set; RegDeleteTreeW handles the subtree.
    RegDeleteTreeW(HKEY_LOCAL_MACHINE,
        (std::wstring(L"SOFTWARE\\Classes\\CLSID\\") + szClsid).c_str());
    RegDeleteTreeW(HKEY_LOCAL_MACHINE,
        (std::wstring(L"SOFTWARE\\Classes\\AudioEngine\\AudioProcessingObjects\\") + szClsid).c_str());

    return S_OK;
}
