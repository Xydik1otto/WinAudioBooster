// WinAudioApo - class factory and DLL plumbing declarations (starter skeleton).
// Copyright (c) WinAudioBooster contributors. License: BSD-2-Clause (see LICENSE-NOTE.md).
//
// The factory follows the classic hand-written IClassFactory pattern used by
// the SYSVAD APO samples' ApoFact.cpp, without an ATL module (the APO class
// itself still uses ATL's COM map internally).

#pragma once

#include <windows.h>

// Live factory/class-object references and live APO instances; both must be
// zero for DllCanUnloadNow to allow unloading. Defined in ApoFact.cpp.
extern volatile LONG g_lServerLock;
extern volatile LONG g_lObjectCount;

// DLL instance handle captured in DllMain (used by the registration code).
extern HINSTANCE g_hModule;

// Registration / unregistration implemented in WinApoReg.cpp.
STDAPI RegisterWinApo();
STDAPI UnregisterWinApo();

class CWinApoClassFactory : public IClassFactory
{
public:
    CWinApoClassFactory() : m_lRef(1) { InterlockedIncrement(&g_lServerLock); }
    ~CWinApoClassFactory() { InterlockedDecrement(&g_lServerLock); }

    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppv);
    STDMETHOD_(ULONG, AddRef)() { return InterlockedIncrement(&m_lRef); }
    STDMETHOD_(ULONG, Release)()
    {
        ULONG ulRef = InterlockedDecrement(&m_lRef);
        if (0 == ulRef)
        {
            delete this;
        }
        return ulRef;
    }

    // IClassFactory
    STDMETHOD(CreateInstance)(IUnknown* pUnkOuter, REFIID riid, void** ppv);
    STDMETHOD(LockServer)(BOOL fLock)
    {
        if (fLock) InterlockedIncrement(&g_lServerLock);
        else InterlockedDecrement(&g_lServerLock);
        return S_OK;
    }

private:
    LONG m_lRef;
};
