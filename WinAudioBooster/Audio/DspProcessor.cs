using System;
using System.Numerics;
using NAudio.Wave;

namespace WinAudioBooster.Audio
{
    public class DspProcessor : ISampleProvider
    {
        private readonly ISampleProvider _source;

        public LevelMeter InputMeter { get; } = new();
        public LevelMeter OutputMeter { get; } = new();

        public WaveFormat WaveFormat => _source.WaveFormat;
        public bool IsEnabled { get; set; } = true;

        private float _gainDb = 0f;
        private float _linearGain = 1.0f;
        public float GainDb
        {
            get => _gainDb;
            set
            {
                _gainDb = value;
                _linearGain = (float)Math.Pow(10, _gainDb / 20.0);
            }
        }

        public bool IsCompressorEnabled { get; set; } = true;
        public float CompressorThresholdDb { get; set; } = -20f;
        public float CompressorRatio { get; set; } = 3f;
        public float CompressorAttackMs { get; set; } = 10f;
        public float CompressorReleaseMs { get; set; } = 100f;

        private float _envelope = 0f;

        public bool IsLimiterEnabled { get; set; } = true;
        public float LimiterThresholdDb { get; set; } = -1.0f;
        
        private float _limiterCeilingDb = -0.3f;
        private float _ceilingLinear = 0.9659f; // cached Pow(10, -0.3/20)

        public float LimiterCeilingDb
        {
            get => _limiterCeilingDb;
            set
            {
                _limiterCeilingDb = value;
                _ceilingLinear = (float)Math.Pow(10, _limiterCeilingDb / 20.0);
            }
        }

        public DspProcessor(ISampleProvider source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            if (samplesRead == 0) return 0;

            InputMeter.ProcessSamples(buffer, offset, samplesRead);

            if (!IsEnabled)
            {
                OutputMeter.ProcessSamples(buffer, offset, samplesRead);
                return samplesRead;
            }

            int sampleRate = WaveFormat.SampleRate;
            float gain = _linearGain;
            bool useSimd = Vector.IsHardwareAccelerated && samplesRead >= Vector<float>.Count;

            int i = 0;
            int endBoundary = offset + samplesRead;

            // SIMD-прискорене векторне підсилення (Gain)
            if (useSimd && Math.Abs(gain - 1.0f) > 0.001f)
            {
                int vectorSize = Vector<float>.Count;
                var gainVector = new Vector<float>(gain);

                for (; i <= endBoundary - vectorSize; i += vectorSize)
                {
                    var v = new Vector<float>(buffer, i);
                    (v * gainVector).CopyTo(buffer, i);
                }
            }

            // Скалярна обробка залишкових семплів та ефектів
            float attackCoef = (float)Math.Exp(-1.0 / (sampleRate * (CompressorAttackMs / 1000.0)));
            float releaseCoef = (float)Math.Exp(-1.0 / (sampleRate * (CompressorReleaseMs / 1000.0)));
            float ceiling = _ceilingLinear;

            for (; i < endBoundary; i++)
            {
                float sample = buffer[i];

                if (!useSimd)
                {
                    sample *= gain;
                }

                if (IsCompressorEnabled)
                {
                    float absSample = Math.Abs(sample);
                    _envelope = absSample > _envelope
                        ? attackCoef * _envelope + (1 - attackCoef) * absSample
                        : releaseCoef * _envelope + (1 - releaseCoef) * absSample;

                    float envDb = 20f * (float)Math.Log10(Math.Max(_envelope, 1e-6f));

                    if (envDb > CompressorThresholdDb)
                    {
                        float overDb = envDb - CompressorThresholdDb;
                        float compressedDb = CompressorThresholdDb + (overDb / CompressorRatio);
                        float gainReductionLinear = (float)Math.Pow(10, (compressedDb - envDb) / 20.0);
                        sample *= gainReductionLinear;
                    }
                }

                if (IsLimiterEnabled)
                {
                    if (sample > ceiling) sample = ceiling;
                    else if (sample < -ceiling) sample = -ceiling;
                }

                buffer[i] = sample;
            }

            OutputMeter.ProcessSamples(buffer, offset, samplesRead);
            return samplesRead;
        }
    }
}