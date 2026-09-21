using System;

namespace WinAudioBooster.Audio
{
    /// <summary>
    /// Обчислювач рівнів звукового сигналу (Peak/RMS) для VU-метрів.
    /// </summary>
    public class LevelMeter
    {
        private float _currentLevel = 0f;
        private const float DecayRate = 0.05f; // Швидкість спадання стрілки/смуги VU-метра

        public float CurrentLevel => _currentLevel;

        /// <summary>
        /// Оновлює рівень сигналу за блоком семплів.
        /// </summary>
        public void ProcessSamples(float[] buffer, int offset, int count)
        {
            if (count == 0) return;

            float maxSample = 0f;
            for (int i = 0; i < count; i++)
            {
                float absSample = Math.Abs(buffer[offset + i]);
                if (absSample > maxSample)
                {
                    maxSample = absSample;
                }
            }

            // Плавне підняття та затухання
            if (maxSample > _currentLevel)
            {
                _currentLevel = maxSample;
            }
            else
            {
                _currentLevel = Math.Max(0f, _currentLevel - DecayRate);
            }
        }
    }
}