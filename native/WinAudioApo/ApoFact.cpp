// WinAudioApo - class factory and DLL entry points (starter skeleton).
// Copyright (c) WinAudioBooster contributors. License: BSD-2-Clause (see LICENSE-NOTE.md).

#include "WinApo.h"
#include "ApoFact.h"

#include <new>
#include <objbase.h>

volatile LONG g_lServerLock = 0;
volatile LONG g_lObjectCount = 0;
HINSTANCE g_hModule = nullptr;

const GUID CLSID_WinAudioApo = __uuidof(CWinApo); // {FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}

//-------------------------------------------------------------------------
// Class factory
//-------------------------------------------------------------------------

STDMETHODIMP CWinApoClassFactory::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }

    *ppv = nullptr;
    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppv = static_cast<IClassFactory*>(this);
        AddRef();
        return S_OK;
    }
    return E_NOINTERFACE;
}

STDMETHODIMP CWinApoClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }
    *ppv = nullptr;

    // APOs are created by the audio engine as stand-alone objects.
    if (pUnkOuter)
    {
        return CLASS_E_NOAGGREGATION;
    }

    // ATL creates the reference-counted instance; the first reference is
    // returned to the caller after QueryInterface below.
    CComObject<CWinApo>* pNew = nullptr;
    HRESULT hr = CComObject<CWinApo>::CreateInstance(&pNew);
    if (FAILED(hr))
    {
        return hr;
    }

    hr = pNew->QueryInterface(riid, ppv);
    if (FAILED(hr))
    {
        delete pNew; // nobody holds a reference yet
        return hr;
    }
    return S_OK;
}

//-------------------------------------------------------------------------
// DLL exports
//-------------------------------------------------------------------------

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }
    *ppv = nullptr;

    if (rclsid != CLSID_WinAudioApo)
    {
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    CWinApoClassFactory* pFactory = new (std::nothrow) CWinApoClassFactory();
    if (!pFactory)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = pFactory->QueryInterface(riid, ppv);
    pFactory->Release(); // QI holds the returned reference
    return hr;
}

STDAPI DllCanUnloadNow(void)
{
    return (0 == g_lServerLock && 0 == g_lObjectCount) ? S_OK : S_FALSE;
}

// TODO: registration below covers the COM class; the
// HKLM\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects entry (see
// WinApoReg.cpp) must also exist - either via the DDK RegisterAPO() helper or
// a RegApo-style registration script, as documented in README-uk.md.
STDAPI DllRegisterServer(void)
{
    return RegisterWinApo();
}

STDAPI DllUnregisterServer(void)
{
    return UnregisterWinApo();
}

BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID /* lpReserved */)
{
    if (DLL_PROCESS_ATTACH == dwReason)
    {
        g_hModule = hInstance;
    }
    // No global initialization/cleanup needed; APO state is per-instance.
    return TRUE;
}
