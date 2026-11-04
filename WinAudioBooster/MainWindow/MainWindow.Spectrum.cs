using System.Windows.Shapes;

namespace WinAudioBooster;

/// <summary>
/// Частковий клас MainWindow: візуалізатор рівня-в-часі (історія піків виходу).
/// 32 вертикальні бари: кожен тік _vuTimer (~33 мс) додає новий бар ліворуч,
/// решта історії зсувається вправо — як скрол рівня в мікшерних станціях.
/// Дані беруться виключно з Dsp.OutputMeter.CurrentLevel (нуль ризику для Audio/).
/// </summary>
public partial class MainWindow
{
    private OutputPeakHistoryVisualizer? _spectrumVisualizer;

    private void InitializeSpectrumVisualizer()
    {
        _spectrumVisualizer = new OutputPeakHistoryVisualizer(SpectrumCanvas);
    }

    // Один кадр _vuTimer: проштовхуємо поточний пік виходу в історію
    private void UpdatePeakHistory(double outputLevel, bool boosterEnabled)
    {
        _spectrumVisualizer?.Push(outputLevel, boosterEnabled);
    }
}

/// <summary>
/// Малює історію останніх піків виходу: 32 бари, новий зліва, зсув історії вправо.
/// Колір барів — DynamicResource акцентного пензля теми (RazerGreen),
/// тому візуалізатор автоматично перекрашується у будь-якому скіні/світлому overlay.
/// </summary>
public sealed class OutputPeakHistoryVisualizer
{
    private const int BarCount = 32;
    private const double BarGapFraction = 0.25;

    private readonly Canvas _canvas;
    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly double[] _history = new double[BarCount];

    public OutputPeakHistoryVisualizer(Canvas canvas)
    {
        _canvas = canvas;
        _canvas.SizeChanged += (_, _) => LayoutBars();

        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                RadiusX = 1,
                RadiusY = 1
            };

            // DynamicResource: пензель береться з активного скіна і оновлюється при зміні теми
            bar.SetResourceReference(Shape.FillProperty, "RazerGreen");

            _bars[i] = bar;
            _canvas.Children.Add(bar);
        }

        LayoutBars();
    }

    // Розкладає бари по ширині Canvas (також після зміни розміру вікна)
    private void LayoutBars()
    {
        double width = _canvas.ActualWidth;
        if (width <= 0) return;

        double slot = width / BarCount;
        double barWidth = Math.Max(2, slot * (1 - BarGapFraction));

        for (int i = 0; i < BarCount; i++)
        {
            _bars[i].Width = barWidth;
            Canvas.SetLeft(_bars[i], i * slot + (slot - barWidth) / 2);
            Canvas.SetBottom(_bars[i], 0);
        }
    }

    /// <summary>
    /// Один тік (~33 мс): новий пік стає лівим баром, історія зсувається вправо.
    /// Коли DSP відсутній або booster вимкнений — у історію пишуться нулі,
    /// тож «хвіст» рівня природно зникає зліва направо.
    /// </summary>
    public void Push(double outputLevel, bool isActive)
    {
        // Зсув історії: [31] випадає, [0] — найновіший (лівий) бар
        Array.Copy(_history, 0, _history, 1, BarCount - 1);
        _history[0] = isActive ? Math.Clamp(outputLevel, 0, 1) : 0;

        double height = _canvas.ActualHeight;
        if (height <= 0) return;

        for (int i = 0; i < BarCount; i++)
        {
            _bars[i].Height = _history[i] * height;
        }
    }
}
