using System.Windows.Shapes;
using NAudio.Dsp;

namespace WinAudioBooster;

/// <summary>
/// Частковий клас MainWindow: спектроаналізатор виходу (справжній FFT-спектр).
/// Кожен тік _vuTimer (~33 мс) забирає останні моно-семпли після лімітера через
/// Dsp.CopySpectrumSamples, рахує FFT (2048, вікно Ханна) і малює 32 логарифмічні
/// смуги 20 Гц..20 кГц у SpectrumCanvas. Якщо даних немає — бари плавно спадають.
/// </summary>
public partial class MainWindow
{
    // Буфер семплів спектрального тапа (розмір за контрактом DspProcessor.SpectrumBufferSize)
    private readonly float[] _spectrumSamples = new float[DspProcessor.SpectrumBufferSize];

    private FftSpectrumAnalyzer? _spectrumVisualizer;

    private void InitializeSpectrumVisualizer()
    {
        _spectrumVisualizer = new FftSpectrumAnalyzer(SpectrumCanvas);
    }

    // Один кадр _vuTimer: забираємо семпли тапа і малюємо FFT-спектр
    private void UpdateSpectrumFrame(bool isActive)
    {
        var dsp = _audioEngine.Dsp;

        // DSP відсутній або бустер вимкнений — цільові рівні нульові, бари плавно падають
        if (dsp == null || !isActive)
        {
            _spectrumVisualizer?.Update(0, null, 0, isActive: false);
            return;
        }

        int copied = dsp.CopySpectrumSamples(_spectrumSamples);

        // Частота дискретизації джерела для перерахунку Гц → FFT-біни
        int sampleRate;
        try
        {
            sampleRate = dsp.WaveFormat.SampleRate;
        }
        catch (Exception)
        {
            // Джерело ще не готове — рахуємо як «немає даних» (спадання)
            sampleRate = 0;
        }

        _spectrumVisualizer?.Update(copied, _spectrumSamples, sampleRate, isActive: true);
    }
}

/// <summary>
/// Спектроаналізатор: FFT (2048, вікно Ханна) над останніми семплами виходу,
/// агрегація модуля спектра у 32 логарифмічні смуги (20 Гц..20 кГц),
/// згладжування між кадрами (швидкий підйом, спадання ~0.8 за тік) та
/// малювання 32 вертикальних барів у Canvas.
/// Колір барів — DynamicResource акцентного пензля теми (RazerGreen),
/// тому візуалізатор автоматично перекрашується у будь-якому скіні/світлому overlay.
/// Усі буфери преаллоковані й пере-використовуються — жодних алокацій у таймері.
/// </summary>
public sealed class FftSpectrumAnalyzer
{
    private const int FftSize = 2048;
    private const int FftM = 11; // (int)Math.Log2(2048)
    private const int BarCount = 32;

    // Межі спектра та динаміка згладжування
    private const double BandMinHz = 20.0;
    private const double BandMaxHz = 20000.0;
    private const double FallFactor = 0.8;  // бар спадає до 80% за тік
    private const double RiseFactor = 0.5;  // бар піднімається на половину різниці за тік

    // Мапінг амплітуди на висоту бара: -DbFloor дБ → 0, 0 дБ → повна висота
    private const double DbFloor = -60.0;

    private readonly Canvas _canvas;
    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly double[] _targets = new double[BarCount]; // цільові рівні кадру 0..1
    private readonly double[] _levels = new double[BarCount];  // згладжені рівні 0..1

    // Преаллоковані робочі буфери (пере-використовуються щотіка)
    private readonly float[] _hannWindow = new float[FftSize]; // вікно Ханна (прекомпутоване)
    private readonly float[] _frame = new float[FftSize];      // останні 2048 семплів
    private readonly Complex[] _fftBuffer = new Complex[FftSize];
    private readonly double[] _bandEdgesHz = new double[BarCount + 1]; // лог-межі смуг
    private readonly int[] _bandStartBin = new int[BarCount];
    private readonly int[] _bandBinCount = new int[BarCount];

    private int _configuredSampleRate;
    private bool _binsValid;

    public FftSpectrumAnalyzer(Canvas canvas)
    {
        _canvas = canvas;
        _canvas.SizeChanged += (_, _) => LayoutBars();

        // Прекомпутоване вікно Ханна (періодичне, як у класичних спектроаналізаторах)
        for (int i = 0; i < FftSize; i++)
        {
            _hannWindow[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / FftSize));
        }

        // Логарифмічні межі 32 смуг: 20 Гц * (20000/20)^(i/32)
        double ratio = BandMaxHz / BandMinHz;
        for (int i = 0; i <= BarCount; i++)
        {
            _bandEdgesHz[i] = BandMinHz * Math.Pow(ratio, (double)i / BarCount);
        }

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
        double barWidth = Math.Max(2, slot * (1 - 0.25));

        for (int i = 0; i < BarCount; i++)
        {
            _bars[i].Width = barWidth;
            Canvas.SetLeft(_bars[i], i * slot + (slot - barWidth) / 2);
            Canvas.SetBottom(_bars[i], 0);
        }
    }

    /// <summary>
    /// Один тік (~33 мс):
    /// copiedCount — скільки семплів повернув CopySpectrumSamples (0 = немає даних → спадання),
    /// source — буфер тапа (останні семпли у хронологічному порядку; беремо хвіст до 2048),
    /// sampleRate — частота дискретизації джерела (0 = невідома → спадання).
    /// </summary>
    public void Update(int copiedCount, float[]? source, int sampleRate, bool isActive)
    {
        if (isActive && ConfigureBins(sampleRate) && copiedCount > 0 && source != null)
        {
            // Останні 2048 семплів (вікно FFT) з хронологічного буфера тапа
            int frameCount = Math.Min(Math.Min(copiedCount, FftSize), source.Length);
            int srcStart = copiedCount - frameCount; // хвостові (найновіші) семпли

            Array.Clear(_frame, 0, FftSize);
            for (int i = 0; i < frameCount; i++)
            {
                _frame[i] = source[srcStart + i];
            }

            ComputeBandTargets();
        }
        else
        {
            // Немає даних/частоти — ціль нульова, бари плавно спадають
            Array.Clear(_targets, 0, BarCount);
        }

        SmoothAndDraw(isActive);
    }

    // Перерахунок меж смуг у FFT-біни (лише при зміні частоти дискретизації)
    private bool ConfigureBins(int sampleRate)
    {
        if (sampleRate <= 0)
        {
            _binsValid = false;
            return false;
        }

        if (_binsValid && _configuredSampleRate == sampleRate) return true;

        double binHz = (double)sampleRate / FftSize;
        int maxBin = FftSize / 2 - 1;

        for (int i = 0; i < BarCount; i++)
        {
            int startBin = (int)Math.Floor(_bandEdgesHz[i] / binHz);
            int endBin = (int)Math.Ceiling(_bandEdgesHz[i + 1] / binHz);

            // Пропускаємо DC (бін 0); смуга має містити хоча б один бін
            startBin = Math.Clamp(startBin, 1, maxBin);
            endBin = Math.Clamp(endBin, startBin, maxBin);

            _bandStartBin[i] = startBin;
            _bandBinCount[i] = endBin - startBin + 1;
        }

        _configuredSampleRate = sampleRate;
        _binsValid = true;
        return true;
    }

    // Цільові рівні кадру: вікно Ханна → FFT → модуль спектра → агрегація у 32 смуги
    private void ComputeBandTargets()
    {
        // Вікно Ханна поверх кадру → комплексний FFT-буфер (NAudio.Dsp.Complex)
        for (int i = 0; i < FftSize; i++)
        {
            _fftBuffer[i].X = _frame[i] * _hannWindow[i];
            _fftBuffer[i].Y = 0;
        }

        // in-place FFT (NAudio.Dsp): forward = true, m = log2(2048)
        FastFourierTransform.FFT(true, FftM, _fftBuffer);

        // Нормування модуля до амплітуди сигналу.
        // NAudio FFT(forward=true) сам ділить на N, тож пік повномасштабної
        // синусоїди з вікном Ханна дає |X| ≈ A/4 → амплітуда A = |X| * 4
        double amplitudeScale = 4.0;
        double binHz = (double)_configuredSampleRate / FftSize;
        int maxBin = FftSize / 2 - 1;

        for (int band = 0; band < BarCount; band++)
        {
            int startBin = _bandStartBin[band];
            int binCount = _bandBinCount[band];

            // Агрегація: середнє модулів бінів смуги з лінійною інтерполяцією
            // між сусідніми бінами (вузькі НЧ-смуги чесно ділять один бін)
            double sum = 0;
            int points = 0;

            for (int k = 0; k < binCount; k++)
            {
                double binPos = startBin + k;
                double freqHz = binPos * binHz;
                if (freqHz < _bandEdgesHz[band] || freqHz > _bandEdgesHz[band + 1]) continue;

                sum += InterpolatedMagnitude(binPos, maxBin, amplitudeScale);
                points++;
            }

            // Смуга між бінами (дуже вузькі НЧ-смуги близько 20 Гц): центр смуги
            if (points == 0)
            {
                double centerBin = (_bandEdgesHz[band] + _bandEdgesHz[band + 1]) / 2.0 / binHz;
                sum = InterpolatedMagnitude(centerBin, maxBin, amplitudeScale);
                points = 1;
            }

            double meanAmplitude = sum / points;

            // Амплітуда → дБ → нормований рівень 0..1 (-60 дБ — підлога шуму)
            double db = 20.0 * Math.Log10(meanAmplitude + 1e-9);
            _targets[band] = Math.Clamp((db - DbFloor) / -DbFloor, 0.0, 1.0);
        }
    }

    // Модуль спектра у дробовій позиції біна (лінійна інтерполяція сусідніх бінів)
    private double InterpolatedMagnitude(double binPos, int maxBin, double amplitudeScale)
    {
        int binLow = Math.Clamp((int)binPos, 1, maxBin);
        int binHigh = Math.Min(binLow + 1, maxBin);
        double frac = binPos - binLow;

        double magLow = MagnitudeAt(binLow, amplitudeScale);
        if (binHigh == binLow) return magLow;

        return magLow + (MagnitudeAt(binHigh, amplitudeScale) - magLow) * frac;
    }

    // Модуль k-го біна, нормований до амплітуди сигналу
    private double MagnitudeAt(int bin, double amplitudeScale)
    {
        double re = _fftBuffer[bin].X;
        double im = _fftBuffer[bin].Y;
        return Math.Sqrt(re * re + im * im) * amplitudeScale;
    }

    // Згладжування (швидкий підйом / fall ~0.8) та малювання барів
    private void SmoothAndDraw(bool isActive)
    {
        double height = _canvas.ActualHeight;
        if (height <= 0) return;

        for (int i = 0; i < BarCount; i++)
        {
            // Неактивний стан: ціль нульова — бари плавно зникають
            double target = isActive ? _targets[i] : 0.0;
            double prev = _levels[i];

            // Згладжування: підйом — часткова інтерполяція, спадання — fall ~0.8 за тік
            double smoothed = target > prev
                ? prev + (target - prev) * RiseFactor
                : prev * FallFactor;

            _levels[i] = smoothed;
            _bars[i].Height = smoothed * height;
        }
    }
}
