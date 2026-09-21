using System;
using NAudio.Wave;

namespace WinAudioBooster.Audio
{
    /// <summary>
    /// Повний DSP-процесор: Gain, Компресор, Лімітер та вимірювання рівнів (VU-метри).
    /// </summary>
    public class DspProcessor : ISampleProvider
    {
        private readonly ISampleProvider _source;

        // Показники рівня для VU-метрів (0.0 - 1.0)
        public LevelMeter InputMeter { get; } = new();
        public LevelMeter OutputMeter { get; } = new();

        public WaveFormat WaveFormat => _source.WaveFormat;
        public bool IsEnabled { get; set; } = true;

        // 1. Gain
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

        // 2. Компресор
        public bool IsCompressorEnabled { get; set; } = true;
        public float CompressorThresholdDb { get; set; } = -20f;
        public float CompressorRatio { get; set; } = 3f;
        public float CompressorAttackMs { get; set; } = 10f;
        public float CompressorReleaseMs { get; set; } = 100f;

        private float _envelope = 0f;

        // 3. Лімітер
        public bool IsLimiterEnabled { get; set; } = true;
        public float LimiterThresholdDb { get; set; } = -1.0f;
        public float LimiterCeilingDb { get; set; } = -0.3f;

        public DspProcessor(ISampleProvider source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);
            if (samplesRead == 0) return 0;

            // Вимірюємо вхідний рівень до обробки
            InputMeter.ProcessSamples(buffer, offset, samplesRead);

            if (!IsEnabled)
            {
                OutputMeter.ProcessSamples(buffer, offset, samplesRead);
                return samplesRead;
            }

            int sampleRate = WaveFormat.SampleRate;

            for (int i = 0; i < samplesRead; i++)
            {
                int index = offset + i;
                float sample = buffer[index];

                // Крок 1: Застосування Gain
                sample *= _linearGain;

                // Крок 2: Компресор
                if (IsCompressorEnabled)
                {
                    sample = ApplyCompressor(sample, sampleRate);
                }

                // Крок 3: Лімітер (Limiter / Ceiling)
                if (IsLimiterEnabled)
                {
                    sample = ApplyLimiter(sample);
                }

                buffer[index] = sample;
            }

            // Вимірюємо вихідний рівень після обробки
            OutputMeter.ProcessSamples(buffer, offset, samplesRead);

            return samplesRead;
        }

        private float ApplyCompressor(float sample, int sampleRate)
        {
            float absSample = Math.Abs(sample);

            // Обчислення огинаючої (envelope) з урахуванням Attack/Release
            float attackCoef = (float)Math.Exp(-1.0 / (sampleRate * (CompressorAttackMs / 1000.0)));
            float releaseCoef = (float)Math.Exp(-1.0 / (sampleRate * (CompressorReleaseMs / 1000.0)));

            if (absSample > _envelope)
                _envelope = attackCoef * _envelope + (1 - attackCoef) * absSample;
            else
                _envelope = releaseCoef * _envelope + (1 - releaseCoef) * absSample;

            float envDb = 20f * (float)Math.Log10(Math.Max(_envelope, 1e-6f));

            if (envDb > CompressorThresholdDb)
            {
                float overDb = envDb - CompressorThresholdDb;
                float compressedDb = CompressorThresholdDb + (overDb / CompressorRatio);
                float gainReductionDb = compressedDb - envDb;
                float gainReductionLinear = (float)Math.Pow(10, gainReductionDb / 20.0);

                sample *= gainReductionLinear;
            }

            return sample;
        }

        private float ApplyLimiter(float sample)
        {
            float ceilingLinear = (float)Math.Pow(10, LimiterCeilingDb / 20.0);

            if (sample > ceilingLinear) return ceilingLinear;
            if (sample < -ceilingLinear) return -ceilingLinear;

            return sample;
        }
    }
}