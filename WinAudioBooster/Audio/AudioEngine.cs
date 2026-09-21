using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace WinAudioBooster.Audio
{
    /// <summary>
    /// Аудіо-рушій, що об'єднує WasapiLoopbackCapture, DspProcessor та WasapiOut.
    /// </summary>
    public class AudioEngine : IDisposable
    {
        private WasapiLoopbackCapture? _capture;
        private WasapiOut? _output;
        private BufferedWaveProvider? _waveBuffer;
        private DspProcessor? _dspProcessor;

        public DspProcessor? Dsp => _dspProcessor;
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Подія, що викликається при виникненні помилки аудіопотоку.
        /// </summary>
        public event EventHandler<string>? ErrorOccurred;

        /// <summary>
        /// Ініціалізація та запуск аудіопотоку.
        /// </summary>
        /// <param name="captureDevice">Пристрій, з якого захоплюється звук (якщо null — використовується дефолтний)</param>
        /// <param name="outputDevice">Пристрій, куди виводиться оброблений звук (наприклад, VB-Cable)</param>
        public void Start(MMDevice? captureDevice, MMDevice? outputDevice)
        {
            if (IsRunning) Stop();

            try
            {
                // 1. Ініціалізація захоплення (Loopback Capture)
                _capture = captureDevice != null 
                    ? new WasapiLoopbackCapture(captureDevice) 
                    : new WasapiLoopbackCapture();

                // 2. Створення проміжного буфера
                _waveBuffer = new BufferedWaveProvider(_capture.WaveFormat)
                {
                    DiscardOnBufferOverflow = true, // Забігає накопиченню затримки (latency)
                    BufferDuration = TimeSpan.FromMilliseconds(500)
                };

                // 3. Підключення обробника події надходження аудіоданих
                _capture.DataAvailable += (s, e) =>
                {
                    _waveBuffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
                };

                // 4. Обертаємо буфер у SampleProvider для DSP
                var sampleProvider = _waveBuffer.ToSampleProvider();
                _dspProcessor = new DspProcessor(sampleProvider);

                // 5. Ініціалізація виводу (WasapiOut)
                _output = outputDevice != null
                    ? new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, 20)
                    : new WasapiOut(AudioClientShareMode.Shared, 20);

                _output.Init(_dspProcessor);

                // 6. Старт запеленгованих потоків
                _output.Play();
                _capture.StartRecording();

                IsRunning = true;
            }
            catch (Exception ex)
            {
                Stop();
                ErrorOccurred?.Invoke(this, $"Помилка запуску аудіопотоку: {ex.Message}");
            }
        }

        /// <summary>
        /// Зупинка аудіо-рушія та звільнення ресурсів.
        /// </summary>
        public void Stop()
        {
            IsRunning = false;

            if (_capture != null)
            {
                try { _capture.StopRecording(); } catch { }
                _capture.Dispose();
                _capture = null;
            }

            if (_output != null)
            {
                try { _output.Stop(); } catch { }
                _output.Dispose();
                _output = null;
            }

            _waveBuffer = null;
            _dspProcessor = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}