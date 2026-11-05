using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinAudioBooster.Services.ViewModels;

/// <summary>
/// Одна смуга еквалайзера для бідингу слайдера:
/// Band — індекс смуги (0..9), Frequency — частота в Гц, GainDb — підсилення в дБ.
/// </summary>
public partial class EqBandViewModel : ObservableObject
{
    public int Band { get; }

    public int Frequency { get; }

    [ObservableProperty]
    private double _gainDb;

    public EqBandViewModel(int band, int frequency, double gainDb = 0)
    {
        Band = band;
        Frequency = frequency;
        _gainDb = gainDb;
    }
}

/// <summary>
/// ViewModel 10-смугового еквалайзера: колекція смуг для бідингу слайдерів
/// (Value="{Binding Eq.Bands[i].GainDb}") + зчитування/запис усіх смуг одним викликом.
/// </summary>
public partial class EqViewModel : ObservableObject
{
    // Межі підсилення смуги (такі самі, як Minimum/Maximum слайдерів EQ у XAML)
    public const double MinGainDb = -12.0;
    public const double MaxGainDb = 12.0;

    public const int BandCount = 10;

    // Частоти смуг EQ (Гц) — ті самі, що й у MainWindow.Eq.cs
    private static readonly int[] BandFrequencies = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

    public ObservableCollection<EqBandViewModel> Bands { get; } = new(
        Enumerable.Range(0, BandCount).Select(i => new EqBandViewModel(i, BandFrequencies[i])));

    /// <summary>Повертає масив підсилень усіх смуг (для збереження в налаштування/пресет).</summary>
    public double[] GetGains()
    {
        var gains = new double[BandCount];
        for (int i = 0; i < BandCount; i++)
        {
            gains[i] = Bands[i].GainDb;
        }
        return gains;
    }

    /// <summary>Виставляє підсилення смуг (з обмеженням у діапазон слайдерів, як раніше робив Slider).</summary>
    public void SetGains(double[]? gains)
    {
        if (gains == null || gains.Length != BandCount) return;

        for (int i = 0; i < BandCount; i++)
        {
            Bands[i].GainDb = Math.Clamp(gains[i], MinGainDb, MaxGainDb);
        }
    }
}
