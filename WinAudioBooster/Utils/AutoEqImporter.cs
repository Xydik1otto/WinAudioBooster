namespace WinAudioBooster.Utils;

/// <summary>
/// Парсер кривих AutoEq (формати Peace / EqualizerAPO GraphicEQ):
/// CSV "frequency,gain", десяткові коми, рядки-коментарі (#), однорядковий
/// формат "GraphicEQ: 20;-5.2; 25;-4.8; ...". Діапазон частот — довільний.
/// Значення інтерполюються (лінійно за log-шкалою частот) на смуги нашого EQ.
/// </summary>
public static class AutoEqImporter
{
    /// <summary>
    /// Розбирає вміст AutoEq-файлу та інтерполює криву на задані частоти смуг.
    /// Повертає масив підсилень (дБ) довжини bandFrequencies.Length.
    /// Кидає FormatException із описом, якщо валідних точок не знайдено.
    /// </summary>
    public static double[] ParseAndInterpolate(string content, float[] bandFrequencies)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new FormatException("Файл порожній (empty file).");

        var points = new List<(double Freq, double Gain)>();

        // Однорядковий формат EqualizerAPO: "GraphicEQ: 20;-5.2; 25;-4.8; ..."
        int graphicEqIndex = content.IndexOf("GraphicEQ:", StringComparison.OrdinalIgnoreCase);
        if (graphicEqIndex >= 0)
        {
            ParseGraphicEqInline(content.Substring(graphicEqIndex + "GraphicEQ:".Length), points);
        }
        else
        {
            ParseCsvLines(content, points);
        }

        if (points.Count == 0)
            throw new FormatException(
                "Не знайдено жодної валідної пари «частота;підсилення».\n" +
                "Очікується CSV-формат AutoEq: заголовки frequency,gain або рядки виду «20,-5.2».");

        // Сортування за частотою + дедуплікація (остання точка на частоту перемагає)
        points.Sort((a, b) => a.Freq.CompareTo(b.Freq));

        var freqs = new List<double>(points.Count);
        var gains = new List<double>(points.Count);
        foreach (var (freq, gain) in points)
        {
            if (freqs.Count > 0 && Math.Abs(freqs[^1] - freq) < 1e-9)
            {
                gains[^1] = gain; // та сама частота — беремо останнє значення
            }
            else
            {
                freqs.Add(freq);
                gains.Add(gain);
            }
        }

        // Інтерполяція на смуги EQ (лінійна за log-шкалою частот)
        var result = new double[bandFrequencies.Length];
        for (int i = 0; i < bandFrequencies.Length; i++)
        {
            result[i] = InterpolateAtLogScale(freqs, gains, bandFrequencies[i]);
        }

        return result;
    }

    // CSV: рядки виду "frequency,gain" (або через ';' / таб), коментарі #, десяткові коми
    private static void ParseCsvLines(string content, List<(double Freq, double Gain)> points)
    {
        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0) continue;

            // Рядки-коментарі та заголовки пропускаємо
            if (line.StartsWith("#") || line.StartsWith("//")) continue;

            string[] tokens = SplitRow(line);

            // Шукаємо перші два поля, що парсяться як число (частота, підсилення)
            double freq = 0, gain = 0;
            int found = 0;

            foreach (string token in tokens)
            {
                if (!TryParseNumber(token, out double value)) continue;

                if (found == 0) freq = value;
                else gain = value;
                found++;

                if (found == 2) break;
            }

            // Рядок, де перше поле — текстовий заголовок («frequency,gain»), пропускається сам
            if (found == 2 && freq > 0)
            {
                points.Add((freq, gain));
            }
        }
    }

    // Інлайн-формат GraphicEQ: пари «частота;підсилення», розділені ';'/пробілами/комами
    private static void ParseGraphicEqInline(string payload, List<(double Freq, double Gain)> points)
    {
        // Прибираємо коментарі в кінці рядка
        int commentIndex = payload.IndexOf('#');
        if (commentIndex >= 0) payload = payload[..commentIndex];

        var numbers = new List<double>();
        foreach (string token in payload.Split(new[] { ';', ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParseNumber(token, out double value)) numbers.Add(value);
        }

        // План: пари частота/підсилення
        for (int i = 0; i + 1 < numbers.Count; i += 2)
        {
            if (numbers[i] > 0) points.Add((numbers[i], numbers[i + 1]));
        }
    }

    // Розбиває рядок CSV на поля: ';' > ',' > табуляція
    private static string[] SplitRow(string line)
    {
        if (line.Contains(';')) return line.Split(';');
        if (line.Contains(',')) return line.Split(',');
        return line.Split('\t');
    }

    // Число: інваріантна культура → поточна → десяткова кома як роздільник
    private static bool TryParseNumber(string token, out double value)
    {
        value = 0;
        token = token.Trim().Trim('"');
        if (token.Length == 0) return false;

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;

        // Десяткова кома: "20,1" → "20.1"
        if (double.TryParse(token.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;

        value = 0;
        return false;
    }

    /// <summary>
    /// Лінійна інтерполяція підсилення за log-шкалою частот.
    /// Поза діапазоном точок — значення найближчого краю кривої.
    /// </summary>
    private static double InterpolateAtLogScale(List<double> freqs, List<double> gains, double bandFreq)
    {
        int count = freqs.Count;
        if (bandFreq <= freqs[0]) return gains[0];
        if (bandFreq >= freqs[count - 1]) return gains[count - 1];

        // Бінарний пошук відрізка [i-1, i], що містить bandFreq
        int lo = 0, hi = count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (freqs[mid] <= bandFreq) lo = mid;
            else hi = mid;
        }

        double logF = Math.Log(bandFreq);
        double logF0 = Math.Log(freqs[lo]);
        double logF1 = Math.Log(freqs[hi]);

        double t = (logF - logF0) / (logF1 - logF0);
        return gains[lo] + (gains[hi] - gains[lo]) * t;
    }
}
