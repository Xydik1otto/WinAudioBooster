// WinAudioApo - APO implementation (starter skeleton).
// Copyright (c) WinAudioBooster contributors. License: BSD-2-Clause (see LICENSE-NOTE.md).

#include "WinApo.h"
#include "ApoFact.h"

#include <cmath>
#include <cstring>

const GUID GUID_WinAudioApoEffect = // {52461AC9-574E-46E7-A0E8-77AB1E43827A}
{ 0x52461ac9, 0x574e, 0x46e7, { 0xa0, 0xe8, 0x77, 0xab, 0x1e, 0x43, 0x82, 0x7a } };

volatile LONG CWinApo::s_lObjectCount = 0;

// Band layout must match WinAudioBooster/Audio/DspProcessor.cs BandFrequencies.
static const float g_bandFrequencies[WINAPO_BAND_COUNT] =
{ 31.0f, 62.0f, 125.0f, 250.0f, 500.0f, 1000.0f, 2000.0f, 4000.0f, 8000.0f, 16000.0f };

CWinApo::CWinApo() :
    CBaseAudioProcessingObject(),
    m_nSampleRate(0),
    m_nChannels(0),
    m_targetGainDb(0.0f),
    m_appliedGainDb(0.0f),
    m_config()
{
    m_config.ResolvePath();
    InterlockedIncrement(&s_lObjectCount);
}

CWinApo::~CWinApo()
{
    InterlockedDecrement(&s_lObjectCount);
}

//-------------------------------------------------------------------------
// IAudioProcessingObject
//-------------------------------------------------------------------------

STDMETHODIMP CWinApo::Initialize(UINT32 cbDataSize, BYTE* pbyData)
{
    // System effects APOs are normally initialized with zero-length data.
    // The base class validates and stores initialization data.
    HRESULT hr = CBaseAudioProcessingObject::Initialize(cbDataSize, pbyData);
    if (FAILED(hr))
    {
        return hr;
    }

    m_targetGainDb = m_config.Current().GainDb;
    m_appliedGainDb = 0.0f; // start at unity and slew up to the target
    return S_OK;
}

STDMETHODIMP CWinApo::IsInputFormatSupported(IAudioMediaType* pOutputFormat,
    IAudioMediaType* pRequestedInputFormat,
    IAudioMediaType** ppSupportedInputFormat)
{
    UNREFERENCED_PARAMETER(pOutputFormat);

    // Starter skeleton: accept the requested float32 interleaved format as-is.
    // The audio engine normally negotiates the same format on both sides.
    // TODO: validate the sample rate / bit depth against pOutputFormat
    //       (see CBaseAudioProcessingObject::IsInputFormatSupported in the
    //       sysvad samples for the reference negotiation logic).
    if (ppSupportedInputFormat)
    {
        *ppSupportedInputFormat = nullptr;
    }
    if (!pRequestedInputFormat)
    {
        return APOERR_FORMAT_NOT_SUPPORTED;
    }
    if (ppSupportedInputFormat)
    {
        *ppSupportedInputFormat = pRequestedInputFormat;
        pRequestedInputFormat->AddRef();
    }
    return S_OK;
}

STDMETHODIMP CWinApo::IsOutputFormatSupported(IAudioMediaType* pInputFormat,
    IAudioMediaType* pRequestedOutputFormat,
    IAudioMediaType** ppSupportedOutputFormat)
{
    UNREFERENCED_PARAMETER(pInputFormat);

    // Same approach as IsInputFormatSupported; TODO: proper negotiation.
    if (ppSupportedOutputFormat)
    {
        *ppSupportedOutputFormat = nullptr;
    }
    if (!pRequestedOutputFormat)
    {
        return APOERR_FORMAT_NOT_SUPPORTED;
    }
    if (ppSupportedOutputFormat)
    {
        *ppSupportedOutputFormat = pRequestedOutputFormat;
        pRequestedOutputFormat->AddRef();
    }
    return S_OK;
}

STDMETHODIMP CWinApo::GetLatency(HNSTIME* pTime)
{
    // The whole point of the APO path: zero algorithmic latency (no
    // re-buffering, no virtual cable hop). The framework adds none either.
    if (!pTime)
    {
        return E_POINTER;
    }
    *pTime = 0;
    return S_OK;
}

//-------------------------------------------------------------------------
// IAudioProcessingObjectConfiguration
//-------------------------------------------------------------------------

STDMETHODIMP CWinApo::LockForProcess(UINT32 u32NumInputConnections,
    APO_CONNECTION_DESCRIPTOR** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_DESCRIPTOR** ppOutputConnections)
{
    HRESULT hr = CBaseAudioProcessingObject::LockForProcess(u32NumInputConnections,
        ppInputConnections, u32NumOutputConnections, ppOutputConnections);
    if (FAILED(hr))
    {
        return hr;
    }

    // Starter skeleton: single input -> single output connection.
    // TODO: multi-connection handling (SFX/GFX wiring) if needed later.
    if (u32NumInputConnections < 1)
    {
        return S_OK;
    }

    IAudioMediaType* pFormat = ppInputConnections[0]->pFormat;
    UNCOMPRESSED_AUDIO_FORMAT fmt = {};
    if (pFormat)
    {
        pFormat->GetUncompressedAudioFormat(&fmt);
    }

    m_nChannels = static_cast<int>(fmt.ulChannels);
    m_nSampleRate = static_cast<int>(fmt.ulSamplesPerSec);
    if (m_nChannels < 1 || m_nSampleRate <= 0)
    {
        // Non-PCM or unexpected format; keep the APO inert.
        // TODO: return APOERR_FORMAT_NOT_SUPPORTED instead, once format
        //       negotiation is tightened.
        m_nChannels = 0;
        m_nSampleRate = 0;
        return S_OK;
    }

    RebuildFilters();
    return S_OK;
}

//-------------------------------------------------------------------------
// IAudioProcessingObjectRT - runs on the audio engine's real-time thread.
// No heap allocations, no blocking calls on the steady-state path (the only
// exception is the config reparse when the file actually changed; see TODO).
//-------------------------------------------------------------------------

STDMETHODIMP_(void) CWinApo::APOProcess(UINT32 u32NumInputConnections,
    APO_CONNECTION_PROPERTY** ppInputConnections,
    UINT32 u32NumOutputConnections,
    APO_CONNECTION_PROPERTY** ppOutputConnections)
{
    if (u32NumInputConnections != 1 || u32NumOutputConnections != 1)
    {
        return; // TODO: pass-through or multi-connection wiring
    }

    APO_CONNECTION_PROPERTY* pIn = ppInputConnections[0];
    APO_CONNECTION_PROPERTY* pOut = ppOutputConnections[0];

    const UINT32 frames = pIn->u32ValidFrameCount;
    // APO connections carry 32-bit float, interleaved samples.
    float* pOutBuffer = reinterpret_cast<float*>(pOut->pBuffer);
    const float* pInBuffer = reinterpret_cast<const float*>(pIn->pBuffer);

    // TODO: honor APO_CONNECTION_PROPERTY_FLAG_SILENCE / _UNALIGNED handling.
    if (pInBuffer != pOutBuffer)
    {
        memcpy(pOutBuffer, pInBuffer, static_cast<size_t>(frames) * m_nChannels * sizeof(float));
    }

    if (m_nChannels > 0 && m_nSampleRate > 0 && !CBaseAudioProcessingObject::IsApoInactive())
    {
        ProcessFrames(pOutBuffer, frames);
    }

    pOut->u32ValidFrameCount = frames;
}

void CWinApo::ProcessFrames(float* buffer, UINT32 frames)
{
    // FileSystemWatcher-style refresh: stat the ini file once per chunk and
    // reparse + rebuild coefficients only when the timestamp changed.
    // NOTE: the reparse itself runs on the RT thread here (a few ms glitch
    // once per config save). TODO: move parsing to a worker thread and
    // publish the new filter set via an atomic pointer swap - Equalizer APO
    // parses config off-thread for exactly this reason.
    if (m_config.CheckStale())
    {
        RebuildFilters();
    }

    const ApoConfigValues& cfg = m_config.Current();
    m_targetGainDb = cfg.GainDb;

    const float ceilingLinear = max(powf(10.0f, cfg.LimiterCeilingDb / 20.0f), 1e-4f);
    const float knee = WINAPO_LIMITER_KNEE_FACTOR * ceilingLinear;
    // Gain slew step per sample: 60 dB/s (same as DspProcessor.GainSlewRateDbPerSecond).
    const float gainStepDb = WINAPO_GAIN_SLEW_DB_PER_SECOND / static_cast<float>(m_nSampleRate);
    const bool useEq = m_filters.size() >= static_cast<size_t>(WINAPO_BAND_COUNT) * m_nChannels;
    const bool useLimiter = true; // TODO: expose an enable/disable flag in the ini

    for (UINT32 frame = 0; frame < frames; ++frame)
    {
        // 0. Slew-limited gain, moving in the dB domain (matches the C# chain).
        float delta = m_targetGainDb - m_appliedGainDb;
        if (fabsf(delta) > gainStepDb)
        {
            m_appliedGainDb += (delta > 0.0f ? gainStepDb : -gainStepDb);
        }
        else if (delta != 0.0f)
        {
            m_appliedGainDb = m_targetGainDb;
        }
        const float gain = powf(10.0f, m_appliedGainDb / 20.0f);

        float* frameSamples = buffer + frame * m_nChannels;

        // 1. Boost + 10-band peaking EQ, per channel.
        for (int ch = 0; ch < m_nChannels; ++ch)
        {
            float sample = frameSamples[ch] * gain;
            if (useEq)
            {
                for (int b = 0; b < WINAPO_BAND_COUNT; ++b)
                {
                    sample = m_filters[b * m_nChannels + ch].Process(sample);
                }
            }
            frameSamples[ch] = sample;
        }

        // 2. Soft tanh limiter (per channel; asymptote = ceiling).
        if (useLimiter)
        {
            for (int ch = 0; ch < m_nChannels; ++ch)
            {
                frameSamples[ch] = ApplySoftLimiter(frameSamples[ch], ceilingLinear, knee);
            }
        }
    }
}

float CWinApo::ApplySoftLimiter(float sample, float ceilingLinear, float knee)
{
    // Port of DspProcessor.ApplySoftLimiter: below the knee the sample passes
    // unchanged; above it the peak is smoothly compressed (tanh) with the
    // asymptote at the ceiling, so the output never exceeds the ceiling.
    const float abs = fabsf(sample);
    if (abs <= knee)
    {
        return sample;
    }

    const float over = abs - knee;
    const float compressed = knee + (ceilingLinear - knee) * tanhf(over / (ceilingLinear - knee));
    return (sample < 0.0f ? -compressed : compressed);
}

void CWinApo::RebuildFilters()
{
    if (m_nChannels < 1 || m_nSampleRate <= 0)
    {
        return;
    }

    const ApoConfigValues& cfg = m_config.Current();
    m_filters.assign(static_cast<size_t>(WINAPO_BAND_COUNT) * m_nChannels, ApoBiquad{});

    for (int b = 0; b < WINAPO_BAND_COUNT; ++b)
    {
        // High bands must not exceed Nyquist (same guard as the C# code).
        const float freq = min(g_bandFrequencies[b], m_nSampleRate * 0.45f);
        const float gainDb = min(max(cfg.EqGainsDb[b], WINAPO_MIN_BAND_GAIN_DB), WINAPO_MAX_BAND_GAIN_DB);

        // RBJ cookbook peaking EQ - identical formulas to NAudio's
        // BiQuadFilter.PeakingEQ so the C# and C++ filters agree.
        const float A = powf(10.0f, gainDb / 40.0f);
        const float w0 = 2.0f * 3.14159265358979323846f * freq / static_cast<float>(m_nSampleRate);
        const float cosw = cosf(w0);
        const float alpha = sinf(w0) / (2.0f * WINAPO_EQ_Q);

        const float a0 = 1.0f + alpha / A;
        ApoBiquad biq = {};
        biq.b0 = (1.0f + alpha * A) / a0;
        biq.b1 = (-2.0f * cosw) / a0;
        biq.b2 = (1.0f - alpha * A) / a0;
        biq.a1 = (-2.0f * cosw) / a0;
        biq.a2 = (1.0f - alpha / A) / a0;
        biq.Reset();

        for (int ch = 0; ch < m_nChannels; ++ch)
        {
            m_filters[b * m_nChannels + ch] = biq;
        }
    }
    // NOTE: biquad states are reset on rebuild - same observable behavior as
    // the C# RebuildEqFilters, which replaces the filter objects wholesale.
}

//-------------------------------------------------------------------------
// IAudioSystemEffects2
//-------------------------------------------------------------------------

STDMETHODIMP CWinApo::GetEffectsList(LPGUID* ppEffectsIds, UINT* pcEffects, HANDLE Event)
{
    if (!ppEffectsIds || !pcEffects)
    {
        return E_POINTER;
    }

    if (Event)
    {
        SetEvent(Event);
    }

    *ppEffectsIds = nullptr;
    *pcEffects = 0;

    // Report exactly one effect: this APO.
    LPGUID pGuid = static_cast<LPGUID>(CoTaskMemAlloc(sizeof(GUID)));
    if (!pGuid)
    {
        return E_OUTOFMEMORY;
    }
    *pGuid = GUID_WinAudioApoEffect;
    *ppEffectsIds = pGuid;
    *pcEffects = 1;
    return S_OK;
}

#if __has_include(<audioengineextensionapo.h>)
//-------------------------------------------------------------------------
// IAudioSystemEffects3 (Windows 11 SDK)
//-------------------------------------------------------------------------

STDMETHODIMP CWinApo::GetControllableSystemEffectsList(
    AUDIO_SYSTEMEFFECT** effects, UINT* numEffects, HANDLE event)
{
    // Starter skeleton: nothing user-controllable through the Win11 APO APIs
    // yet - the WPF configurator is the control surface via apo-config.ini.
    // TODO: expose an on/off toggle here so the Effects page of the modern
    //       settings UI can disable the APO as well.
    UNREFERENCED_PARAMETER(effects);
    if (!numEffects)
    {
        return E_POINTER;
    }
    if (event)
    {
        SetEvent(event);
    }
    *numEffects = 0;
    return S_OK;
}

STDMETHODIMP CWinApo::SetAudioSystemEffectState(GUID effectId, AUDIO_SYSTEMEFFECT_STATE state)
{
    // TODO: track enable/disable state from the system settings UI.
    UNREFERENCED_PARAMETER(effectId);
    UNREFERENCED_PARAMETER(state);
    return S_OK;
}
#endif
