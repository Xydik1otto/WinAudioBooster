namespace WinAudioBooster.Audio;

public class LevelMeter
{
    private float _currentLevel = 0f;
    private const float DecayRate = 0.05f;

    public float CurrentLevel => _currentLevel;

    public void ProcessSamples(float[] buffer, int offset, int count)
    {
        if (count == 0) return;

        float maxSample = 0f;
        int end = offset + count;

        for (int i = offset; i < end; i++)
        {
            float absSample = Math.Abs(buffer[i]);
            if (absSample > maxSample) maxSample = absSample;
        }

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