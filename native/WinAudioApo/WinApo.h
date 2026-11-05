// WinAudioApo - WinAudioBooster system effects APO (starter skeleton).
// Copyright (c) WinAudioBooster contributors. License: BSD-2-Clause (see LICENSE-NOTE.md).
//
// Implementation pattern follows the Microsoft SYSVAD APO samples (MIT license):
//   https://github.com/microsoft/Windows-driver-samples/tree/main/audio/sysvad/APO
// The registration approach follows the recipe documented by the
// Equalizer APO project (GPL-2.0) - ideas only, no source copied; see LICENSE-NOTE.md.
//
// DSP chain (subset of the C# DspProcessor, matched semantics):
//   gain (slew-limited 60 dB/s) -> 10-band peaking EQ (biquad, RBJ cookbook)
//   -> soft tanh limiter (knee = 0.9 * ceiling)
// TODO: extend the chain with the remaining DspProcessor stages
//       (sub-bass HPF, bass/treble shelves, widen, crossfeed, mono/balance,
//       compressor, RMS-based AGC) once the skeleton is verified end to end.

#pragma once

#include <atlcom.h>
#include <audioenginebaseapo.h>
#include <BaseAudioProcessingObject.h>
#include <mmreg.h>
#include <vector>

#include "ApoConfig.h"

// {FA1C7572-2A88-41EE-B5D0-D4B8B0B04557}
class DECLSPEC_UUID("FA1C7572-2A88-41EE-B5D0-D4B8B0B04557") CWinApo;

// Defined in ApoFact.cpp as __uuidof(CWinApo).
extern const GUID CLSID_WinAudioApo;

// Effect identifier reported by IAudioSystemEffects2::GetEffectsList.
// {52461AC9-574E-46E7-A0E8-77AB1E43827A}
extern const GUID GUID_WinAudioApoEffect;

// RBJ-cookbook biquad (direct form I) - same math as NAudio BiQuadFilter,
// which the C# DspProcessor uses (BiQuadFilter.PeakingEQ).
struct ApoBiquad
{
    float b0, b1, b2; // numerators (normalized by a0)
    float a1, a2;     // feedback terms (normalized by a0)
    float x1, x2;     // input state
    float y1, y2;     // output state

    void Reset() { x1 = x2 = y1 = y2 = 0.0f; }
    float Process(float x)
    {
        float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
        x2 = x1; x1 = x;
        y2 = y1; y1 = y;
        return y;
    }
};

class ATL_NO_VTABLE CWinApo :
    public CComObjectRootEx<CComMultiThreadModel>,
    public CComCoClass<CWinApo, &CLSID_WinAudioApo>,
    public CBaseAudioProcessingObject,
    public IAudioSystemEffects2
#if __has_include(<audioengineextensionapo.h>)
    , public IAudioSystemEffects3
#endif
{
public:
    CWinApo();
    ~CWinApo();

    // Registration properties: when your WDK/SDK requires non-default
    // construction of CBaseAudioProcessingObject, pass a
    // CRegAPOProperties<1> member here exactly like the sysvad AecApo sample
    // (static const CRegAPOProperties<1> sm_RegProperties). See README-uk.md.
    DECLARE_PROTECT_FINAL_CONSTRUCT()

    BEGIN_COM_MAP(CWinApo)
        COM_INTERFACE_ENTRY(IAudioSystemEffects)
        COM_INTERFACE_ENTRY(IAudioSystemEffects2)
#if __has_include(<audioengineextensionapo.h>)
        COM_INTERFACE_ENTRY(IAudioSystemEffects3)
#endif
        COM_INTERFACE_ENTRY(IAudioProcessingObjectRT)
        COM_INTERFACE_ENTRY(IAudioProcessingObject)
        COM_INTERFACE_ENTRY(IAudioProcessingObjectConfiguration)
    END_COM_MAP()

    HRESULT FinalConstruct() { return S_OK; }
    void FinalRelease() { }

    // IAudioProcessingObject
    STDMETHOD(Initialize)(UINT32 cbDataSize, BYTE* pbyData);
    STDMETHOD(IsInputFormatSupported)(IAudioMediaType* pOutputFormat, IAudioMediaType* pRequestedInputFormat, IAudioMediaType** ppSupportedInputFormat);
    STDMETHOD(IsOutputFormatSupported)(IAudioMediaType* pInputFormat, IAudioMediaType* pRequestedOutputFormat, IAudioMediaType** ppSupportedOutputFormat);
    STDMETHOD(GetLatency)(HNSTIME* pTime);

    // IAudioProcessingObjectConfiguration
    STDMETHOD(LockForProcess)(UINT32 u32NumInputConnections,
        APO_CONNECTION_DESCRIPTOR** ppInputConnections,
        UINT32 u32NumOutputConnections,
        APO_CONNECTION_DESCRIPTOR** ppOutputConnections);

    // IAudioProcessingObjectRT
    STDMETHOD_(void, APOProcess)(UINT32 u32NumInputConnections,
        APO_CONNECTION_PROPERTY** ppInputConnections,
        UINT32 u32NumOutputConnections,
        APO_CONNECTION_PROPERTY** ppOutputConnections);

    // IAudioSystemEffects2
    STDMETHOD(GetEffectsList)(_Outptr_result_buffer_maybenull_(*pcEffects) LPGUID* ppEffectsIds,
        _Out_ UINT* pcEffects, _In_ HANDLE Event);

#if __has_include(<audioengineextensionapo.h>)
    // IAudioSystemEffects3 (Windows 11 SDK)
    STDMETHOD(GetControllableSystemEffectsList)(
        _Outptr_result_buffer_maybenull_(*numEffects) AUDIO_SYSTEMEFFECT** effects,
        _Out_ UINT* numEffects, _In_opt_ HANDLE event);
    STDMETHOD(SetAudioSystemEffectState)(GUID effectId, AUDIO_SYSTEMEFFECT_STATE state);
#endif

    // DSP internals
private:
    void ProcessFrames(float* buffer, UINT32 frames);
    void RebuildFilters();
    static float ApplySoftLimiter(float sample, float ceilingLinear, float knee);

    int m_nSampleRate;   // negotiated in LockForProcess
    int m_nChannels;     // negotiated in LockForProcess
    float m_targetGainDb;  // from config (GainDb)
    float m_appliedGainDb; // slew-limited gain actually applied

    ApoConfig m_config;
    // One biquad per band per channel: [band * m_nChannels + channel].
    std::vector<ApoBiquad> m_filters;

public:
    // Reference counting for DllCanUnloadNow (live instances).
    static volatile LONG s_lObjectCount;
};
