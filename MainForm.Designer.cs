using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;
using NAudio.Dsp;
using NAudio.Wave.SampleProviders;

namespace MusicPlayer
{
    public partial class MainForm : Form
    {
        // UI
        private Panel leftPanel;
        private Panel rightPanel;
        private Button btnSelectFolder;
        private ListBox lbTracks;
        private Label lblNowPlaying;


        private Panel controlPanel;
        private Button btnPrev, btnPlayPause, btnNext, btnStop;
        private TrackBar trackBarPosition;
        private Label lblTime;
        private TrackBar tbVolume;
        private System.Windows.Forms.Timer timer;

        // Visualizer control
        private VisualizerControl visualizerControl;

        // Playback
        private List<string> playlist = new List<string>();
        private int currentIndex = -1;
        private IWavePlayer outputDevice;
        private AudioFileReader audioFile;
        private bool isDraggingPosition = false;

        // visualizer sample provider (wraps audio samples)
        private VisualizerSampleProvider visualizerSampleProvider;

        private void InitializeComponent()
        {
            // Form
            this.Text = "MusicPlayer";
            this.ClientSize = new Size(900, 500);
            this.BackColor = Color.FromArgb(28, 30, 34);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.MinimumSize = new Size(750, 420);

            // Left panel (playlist)
            leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 320,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(38, 40, 44)
            };

            btnSelectFolder = new Button
            {
                Text = "📁  Select folder",
                Height = 40,
                Dock = DockStyle.Top,
                FlatStyle = FlatStyle.Flat,
            };
            btnSelectFolder.FlatAppearance.BorderSize = 0;
            btnSelectFolder.ForeColor = Color.White;
            btnSelectFolder.Click += BtnSelectFolder_Click;

            lbTracks = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(28, 30, 34),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F)
            };
            lbTracks.DoubleClick += LbTracks_DoubleClick;

            leftPanel.Controls.Add(lbTracks);
            leftPanel.Controls.Add(btnSelectFolder);


            rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12)
            };

            lblNowPlaying = new Label
            {
                Text = "No track selected",
                Dock = DockStyle.Top,
                Height = 42,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // small cover placeholder (prevent null reference if other files expect it)


            visualizerControl = new VisualizerControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 24)
            };


            // control panel (bottom)
            controlPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                BackColor = Color.FromArgb(22, 23, 25),
                Padding = new Padding(8)
            };

            btnPrev = MakeControlButton(Properties.Resources.previous);
            btnPlayPause = MakeControlButton(Properties.Resources.play);
            btnNext = MakeControlButton(Properties.Resources.next);


            btnPrev.Click += BtnPrev_Click;
            btnPlayPause.Click += BtnPlayPause_Click;
            btnNext.Click += BtnNext_Click;


            // position trackbar and time label
            trackBarPosition = new TrackBar
            {
                Dock = DockStyle.Top,
                Height = 45,
                Minimum = 0,
                Maximum = 100,
                TickStyle = TickStyle.None
            };
            trackBarPosition.MouseDown += (s, e) => isDraggingPosition = true;
            trackBarPosition.MouseUp += (s, e) =>
            {
                if (audioFile != null)
                {
                    var sec = Math.Clamp(trackBarPosition.Value, trackBarPosition.Minimum, trackBarPosition.Maximum);
                    audioFile.CurrentTime = TimeSpan.FromSeconds(sec);
                }
                isDraggingPosition = false;
            };

            lblTime = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "00:00 / 00:00",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.LightGray
            };

            // volume
            tbVolume = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 80,
                TickStyle = TickStyle.None,
                Orientation = Orientation.Horizontal,
                Height = 80,
                Dock = DockStyle.Right
            };
            tbVolume.ValueChanged += TbVolume_ValueChanged;


            var buttonsFlow = new FlowLayoutPanel
            {
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0),
                Dock = DockStyle.None,         // don't dock
                Anchor = AnchorStyles.None     // allow centering
            };


            buttonsFlow.Location = new Point(
    (controlPanel.Width - buttonsFlow.PreferredSize.Width) / 2,
    controlPanel.Height - buttonsFlow.PreferredSize.Height - 5 // small bottom margin
);




            controlPanel.Resize += (s, e) =>
            {
                int rightReserved = tbVolume.Visible ? tbVolume.Width : 0;

                buttonsFlow.Location = new Point(
                    (controlPanel.Width - rightReserved - buttonsFlow.PreferredSize.Width) / 2,
                    controlPanel.Height - buttonsFlow.PreferredSize.Height - 12 // margin from bottom
                );
            };




            buttonsFlow.Controls.Add(btnPrev);
            buttonsFlow.Controls.Add(btnPlayPause);
            buttonsFlow.Controls.Add(btnNext);


            controlPanel.Controls.Add(buttonsFlow);
            controlPanel.Controls.Add(tbVolume);
            controlPanel.Controls.Add(trackBarPosition);
            controlPanel.Controls.Add(lblTime);

            // Add in correct order so visualizer is above control panel
            rightPanel.Controls.Add(lblNowPlaying);

            rightPanel.Controls.Add(visualizerControl);
            rightPanel.Controls.Add(controlPanel);

            // timer to update position
            timer = new System.Windows.Forms.Timer { Interval = 500 };
            timer.Tick += Timer_Tick;
            timer.Start();

            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);
        }

        private Button MakeControlButton(Image icon)
        {
            var btn = new Button
            {
                Text = "",
                Image = icon,
                Width = 62,
                Height = 62,
                Margin = new Padding(6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(225, 226, 230),
                ForeColor = Color.White
            };

            btn.FlatAppearance.BorderSize = 0;

            using (var gp = new System.Drawing.Drawing2D.GraphicsPath())
            {
                gp.AddEllipse(0, 0, btn.Width, btn.Height);
                btn.Region = new Region(gp);
            }

            return btn;
        }

        private void BtnSelectFolder_Click(object sender, EventArgs e)
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Select folder with audio files";
                if (d.ShowDialog() == DialogResult.OK)
                {
                    LoadPlaylist(d.SelectedPath);
                }
            }
        }

        private void LoadPlaylist(string folder)
        {
            var exts = new[] { "*.mp3", "*.wav" };
            var files = exts.SelectMany(ext => Directory.EnumerateFiles(folder, ext, SearchOption.TopDirectoryOnly)).ToList();
            files.Sort(StringComparer.InvariantCultureIgnoreCase);

            playlist = files;
            lbTracks.Items.Clear();
            foreach (var f in playlist)
            {
                lbTracks.Items.Add(Path.GetFileNameWithoutExtension(f));
            }

            if (playlist.Count > 0)
            {
                currentIndex = 0;
                lbTracks.SelectedIndex = 0;
                PlayTrack(0);
            }
            else
            {
                currentIndex = -1;
                lblNowPlaying.Text = "No tracks found";
            }
        }

        private void LbTracks_DoubleClick(object sender, EventArgs e)
        {
            if (lbTracks.SelectedIndex >= 0) PlayTrack(lbTracks.SelectedIndex);
        }

        private void PlayTrack(int index)
        {
            if (index < 0 || index >= playlist.Count) return;

            StopPlayback();

            currentIndex = index;
            lbTracks.SelectedIndex = index;

            var file = playlist[index];
            try
            {
                // create reader
                audioFile = new AudioFileReader(file);

                // create the sample provider chain that also produces FFT data
                var sampleProv = audioFile.ToSampleProvider();

                // use VisualizerControl.BarCount so sample provider emits same number of bars
                visualizerSampleProvider = new VisualizerSampleProvider(sampleProv, fftLength: 2048, barCount: visualizerControl.BarCount);
                visualizerSampleProvider.FftCalculated += VisualizerSampleProvider_FftCalculated;

                var waveProv = new SampleToWaveProvider16(visualizerSampleProvider);

                outputDevice = new WaveOutEvent();
                outputDevice.Init(waveProv);
                outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
                outputDevice.Play();

                btnPlayPause.Image = Properties.Resources.pause;
                lblNowPlaying.Text = Path.GetFileNameWithoutExtension(file);

                var totalSeconds = (int)Math.Max(1, audioFile.TotalTime.TotalSeconds);
                trackBarPosition.Maximum = totalSeconds;
                trackBarPosition.Value = 0;

                tbVolume.Value = (int)(audioFile.Volume * 100);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot play file: " + ex.Message, "Playback error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void VisualizerSampleProvider_FftCalculated(float[] magnitudes)
        {
            if (visualizerControl != null)
            {
                try
                {
                    visualizerControl.SetFftData(magnitudes); // thread-safe write
                }
                catch
                {
                    // ignore during shutdown
                }
            }
        }

        private void StopPlayback()
        {
            if (outputDevice != null)
            {
                try { outputDevice.Stop(); } catch { }
                outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                outputDevice.Dispose();
                outputDevice = null;
            }
            if (visualizerSampleProvider != null)
            {
                visualizerSampleProvider.FftCalculated -= VisualizerSampleProvider_FftCalculated;
                visualizerSampleProvider = null;
            }
            if (audioFile != null)
            {
                audioFile.Dispose();
                audioFile = null;
            }
            btnPlayPause.Image = Properties.Resources.play;
        }

        private void BtnPlayPause_Click(object sender, EventArgs e)
        {
            if (outputDevice == null)
            {
                if (playlist.Count > 0)
                {
                    PlayTrack(currentIndex >= 0 ? currentIndex : 0);
                }
                return;
            }

            if (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Pause();
                btnPlayPause.Image = Properties.Resources.play;
            }
            else
            {
                outputDevice.Play();
                btnPlayPause.Image = Properties.Resources.pause;
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (outputDevice != null)
            {
                outputDevice.Stop();
                if (audioFile != null) audioFile.Position = 0;
                btnPlayPause.Image = Properties.Resources.play;
            }
        }

        private void BtnNext_Click(object sender, EventArgs e) => PlayNext();
        private void BtnPrev_Click(object sender, EventArgs e) => PlayPrevious();

        private void PlayNext()
        {
            if (playlist.Count == 0) return;
            var next = (currentIndex + 1) % playlist.Count;
            PlayTrack(next);
        }

        private void PlayPrevious()
        {
            if (playlist.Count == 0) return;
            var prev = (currentIndex - 1 + playlist.Count) % playlist.Count;
            PlayTrack(prev);
        }

        private void OutputDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (audioFile != null)
            {
                var nearEnd = audioFile.CurrentTime >= audioFile.TotalTime - TimeSpan.FromMilliseconds(200);
                if (nearEnd)
                {
                    this.BeginInvoke(new Action(PlayNext));
                }
            }
            if (e.Exception != null)
            {
                MessageBox.Show("Playback error: " + e.Exception.Message);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (audioFile != null && !isDraggingPosition)
            {
                var cur = audioFile.CurrentTime;
                var tot = audioFile.TotalTime;
                var sec = (int)Math.Clamp(cur.TotalSeconds, 0, trackBarPosition.Maximum);
                if (sec >= trackBarPosition.Minimum && sec <= trackBarPosition.Maximum)
                {
                    if (trackBarPosition.Value != sec)
                        trackBarPosition.Value = sec;
                }
                lblTime.Text = $"{FormatTime(cur)} / {FormatTime(tot)}";
            }
        }

        private void TbVolume_ValueChanged(object sender, EventArgs e)
        {
            if (audioFile != null)
            {
                audioFile.Volume = tbVolume.Value / 100f;
            }
        }

        private static string FormatTime(TimeSpan t)
        {
            return $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopPlayback();
            base.OnFormClosing(e);
        }

        /*******************************
         * VISUALIZER SUPPORT CLASSES *
         *******************************/

        private class VisualizerSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider source;
            public WaveFormat WaveFormat => source.WaveFormat;
            public event Action<float[]> FftCalculated;

            private readonly int fftLength;
            private readonly float[] fftBuffer;
            private int fftBufferPos;
            private readonly float[] window;
            private readonly int fftExponent;
            private readonly int channels;
            private readonly int barCount;
            private readonly int sampleRate;
            private readonly float minFrequency = 20f;
            private readonly float maxDb = 0f;
            private readonly float minDb = -80f;

            public VisualizerSampleProvider(ISampleProvider source, int fftLength = 2048, int barCount = 48)
            {
                if ((fftLength & (fftLength - 1)) != 0)
                    throw new ArgumentException("fftLength must be a power of two");

                this.source = source;
                this.fftLength = fftLength;
                this.fftBuffer = new float[fftLength];
                this.fftBufferPos = 0;
                this.fftExponent = (int)Math.Round(Math.Log(fftLength, 2));
                this.window = CreateHannWindow(fftLength);
                this.channels = Math.Max(1, source.WaveFormat.Channels);
                this.barCount = Math.Max(1, barCount);
                this.sampleRate = source.WaveFormat.SampleRate;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int samplesRead = source.Read(buffer, offset, count);

                int i = offset;
                int end = offset + samplesRead;
                while (i < end)
                {
                    float mono = 0f;
                    for (int c = 0; c < channels && (i + c) < end; c++)
                        mono += buffer[i + c];
                    mono /= channels;

                    fftBuffer[fftBufferPos++] = mono;

                    if (fftBufferPos >= fftLength)
                    {
                        var complex = new Complex[fftLength];
                        for (int n = 0; n < fftLength; n++)
                        {
                            complex[n].X = fftBuffer[n] * window[n];
                            complex[n].Y = 0f;
                        }

                        FastFourierTransform.FFT(true, fftExponent, complex);

                        int half = fftLength / 2;
                        var mags = new float[half];
                        for (int n = 0; n < half; n++)
                        {
                            float re = complex[n].X;
                            float im = complex[n].Y;
                            float mag = (float)Math.Sqrt(re * re + im * im);
                            mags[n] = mag;
                        }

                        var barValues = new float[barCount];
                        float nyquist = sampleRate / 2f;
                        float maxFreq = Math.Max(minFrequency * 1.1f, nyquist);

                        for (int b = 0; b < barCount; b++)
                        {
                            double fracA = (double)b / barCount;
                            double fracB = (double)(b + 1) / barCount;
                            double fStart = minFrequency * Math.Pow(maxFreq / minFrequency, fracA);
                            double fEnd = minFrequency * Math.Pow(maxFreq / minFrequency, fracB);

                            int binStart = (int)Math.Floor((fStart / sampleRate) * fftLength);
                            int binEnd = (int)Math.Ceiling((fEnd / sampleRate) * fftLength);

                            binStart = Math.Clamp(binStart, 0, half - 1);
                            binEnd = Math.Clamp(binEnd, binStart + 1, half);

                            float acc = 0f;
                            int cnt = 0;
                            for (int bi = binStart; bi < binEnd; bi++)
                            {
                                acc += mags[bi];
                                cnt++;
                            }

                            float avg = cnt > 0 ? acc / cnt : 0f;

                            double db = 20.0 * Math.Log10(avg + 1e-9);
                            double norm = (db - minDb) / (maxDb - minDb);
                            if (norm < 0) norm = 0;
                            if (norm > 1) norm = 1;

                            float finalVal = (float)Math.Pow(norm, 0.8);

                            barValues[b] = finalVal;
                        }

                        try
                        {
                            FftCalculated?.Invoke(barValues);
                        }
                        catch
                        {
                        }

                        int halfShift = fftLength / 2;
                        Array.Copy(fftBuffer, halfShift, fftBuffer, 0, halfShift);
                        fftBufferPos = halfShift;
                    }

                    i += channels;
                }

                return samplesRead;
            }

            private static float[] CreateHannWindow(int length)
            {
                var w = new float[length];
                for (int n = 0; n < length; n++)
                {
                    w[n] = 0.5f * (1f - (float)Math.Cos(2.0 * Math.PI * n / (length - 1)));
                }
                return w;
            }
        }

        /// <summary>
        /// Rounded-top bars, even spacing.
        /// Attack (rise) is smoothed (no instant jump) and release (fall) uses a slower exponential-like decay for smoother visuals.
        /// Both attack and release are configurable (attackSpeed, releaseSpeed).
        /// </summary>
        private class VisualizerControl : Control
        {
            private readonly int barCount = 48;
            public int BarCount => barCount;

            private readonly float[] bars;
            private readonly float[] targets;

            private readonly object sync = new object();

            // attack and release speeds: fraction of the delta applied per frame.
            // attackSpeed: how fast bars move up toward targets (0..1). Lower = slower rise.
            // releaseSpeed: how fast bars move down toward targets (0..1). Lower = slower fall (smoother).
            private readonly float attackSpeed = 0.28f;   // ~28% of gap per frame -> smooth non-instant rise
            private readonly float releaseSpeed = 0.18f; // ~4.5% of gap per frame -> slower, smoother fall

            // optional clamp to limit how much a bar can jump in a single frame (avoids huge spikes).
            private readonly float maxStep = 0.15f; // maximum change (absolute) per frame

            private readonly System.Windows.Forms.Timer frameTimer;

            private Color bgTop = Color.FromArgb(18, 20, 24);
            private Color bgBottom = Color.FromArgb(14, 15, 18);

            private GraphicsPath cachedBlurPath;
            private Size lastSize = Size.Empty;

            // reusable render buffer to avoid per-paint allocation
            private float[] renderBars;

            public VisualizerControl()
            {
                this.DoubleBuffered = true;
                this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                              ControlStyles.OptimizedDoubleBuffer |
                              ControlStyles.UserPaint, true);

                bars = new float[barCount];
                targets = new float[barCount];
                renderBars = new float[barCount];

                // ~40 FPS - smooth but reasonable CPU usage
                frameTimer = new System.Windows.Forms.Timer { Interval = 25 };
                frameTimer.Tick += FrameTimer_Tick;
                frameTimer.Start();
            }

            private void FrameTimer_Tick(object sender, EventArgs e)
            {
                bool needInvalidate = false;

                lock (sync)
                {
                    for (int i = 0; i < barCount; i++)
                    {
                        float t = targets[i];

                        // Smooth attack (rise) and smooth release (fall).
                        // Move a fraction of the delta per frame rather than jumping instantly.
                        float delta = t - bars[i];
                        float step = 0f;

                        if (delta > 0f)
                        {
                            // rising toward higher target
                            step = delta * attackSpeed;
                        }
                        else if (delta < 0f)
                        {
                            // falling toward lower target
                            step = delta * releaseSpeed; // delta is negative -> step negative
                        }

                        // clamp step to avoid extremely large jumps in a single frame
                        if (step > maxStep) step = maxStep;
                        if (step < -maxStep) step = -maxStep;

                        // apply step
                        bars[i] += step;

                        // ensure we don't overshoot target due to clamping/float error
                        if (delta > 0f && bars[i] > t) bars[i] = t;
                        if (delta < 0f && bars[i] < t) bars[i] = t;

                        bars[i] = Math.Clamp(bars[i], 0f, 1f);

                        if (Math.Abs(targets[i] - bars[i]) > 0.0005f) needInvalidate = true;
                    }
                }

                if (needInvalidate)
                {
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // gradient background
                using (var grad = new LinearGradientBrush(ClientRectangle, bgTop, bgBottom, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(grad, ClientRectangle);
                }

                // cached rounded background area
                EnsureBlurPath();
                using (var blurBrush = new SolidBrush(Color.FromArgb(48, 20, 24, 30)))
                {
                    if (cachedBlurPath != null)
                    {
                        g.FillPath(blurBrush, cachedBlurPath);
                    }
                }

                int w = ClientRectangle.Width;
                int h = ClientRectangle.Height;

                int pad = 14;
                int areaW = w - pad * 2;
                int areaH = h - pad * 2;

                if (areaW <= 0 || areaH <= 0) return;

                // compute even spacing: choose a gap that's a small portion of areaW but clamp
                float preferredGap = Math.Max(4f, areaW * 0.016f); // 1.6% of area width or at least 4px
                float gap = preferredGap;
                float barWidth = (areaW - gap * (barCount - 1)) / barCount;

                // if barWidth becomes too small, reduce gap to keep bars visible
                if (barWidth < 4f)
                {
                    gap = Math.Max(2f, (areaW - 4f * barCount) / (barCount - 1));
                    barWidth = (areaW - gap * (barCount - 1)) / barCount;
                }

                // copy current bars into render buffer once under lock
                lock (sync)
                {
                    Array.Copy(bars, renderBars, barCount);
                }

                // single reusable brush to avoid allocating many brushes each paint
                using (var brush = new SolidBrush(Color.Black))
                {
                    for (int i = 0; i < barCount; i++)
                    {
                        float x = pad + i * (barWidth + gap);
                        float val = Math.Clamp(renderBars[i], 0f, 1f);
                        float barH = val * areaH;

                        // ensure minimal visible height for very small values
                        if (barH > 0 && barH < 2f)
                            barH = 2f;

                        float y = pad + (areaH - barH);

                        // color interpolation
                        Color lowColor = Color.FromArgb(140, 190, 255);   // soft blue
                        Color highColor = Color.FromArgb(255, 110, 200);  // pinkish
                        Color col = LerpColor(lowColor, highColor, val);

                        brush.Color = col;

                        // draw rounded-top bar: rectangle body + top ellipse cap (cheap)
                        float radius = Math.Min(barWidth * 0.5f, 10f); // cap radius
                        float ellipseHeight = radius * 2f;

                        // body rect starts at y + radius, extends to bottom
                        RectangleF bodyRect = new RectangleF(x, y + radius, barWidth, Math.Max(0f, barH - radius));
                        if (bodyRect.Height > 0)
                        {
                            g.FillRectangle(brush, bodyRect);
                        }

                        // draw top cap as ellipse to make a rounded top
                        RectangleF topEllipse = new RectangleF(x, y, barWidth, Math.Min(ellipseHeight, Math.Max(0f, barH)));
                        g.FillEllipse(brush, topEllipse);
                    }
                }
            }

            /// <summary>
            /// Thread-safe: receives magnitudes from FFT (normalized 0..1). Can be called from any thread.
            /// </summary>
            public void SetFftData(float[] magnitudes)
            {
                if (magnitudes == null || magnitudes.Length == 0) return;

                int len = Math.Min(magnitudes.Length, barCount);
                lock (sync)
                {
                    for (int i = 0; i < len; i++)
                    {
                        // directly store the incoming magnitude as the target
                        targets[i] = Math.Clamp(magnitudes[i], 0f, 1f);
                    }
                    for (int i = len; i < barCount; i++) targets[i] = 0f;
                }
                // don't Invalidate here; the UI timer will pick up changes on the UI thread.
            }

            private void EnsureBlurPath()
            {
                if (cachedBlurPath != null && lastSize == this.ClientSize) return;

                cachedBlurPath?.Dispose();
                cachedBlurPath = null;

                var blurRect = new RectangleF(8, 8, ClientRectangle.Width - 16, ClientRectangle.Height - 16);
                if (blurRect.Width > 0 && blurRect.Height > 0)
                {
                    cachedBlurPath = RoundedRect(blurRect, 26);
                }

                lastSize = this.ClientSize;
            }

            private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
            {
                var gp = new GraphicsPath();
                float d = Math.Max(0.1f, radius) * 2f;
                gp.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                gp.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                gp.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                gp.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                gp.CloseFigure();
                return gp;
            }

            private static Color LerpColor(Color c1, Color c2, float t)
            {
                t = Math.Clamp(t, 0f, 1f);
                int r = (int)(c1.R + (c2.R - c1.R) * t);
                int g = (int)(c1.G + (c2.G - c1.G) * t);
                int b = (int)(c1.B + (c2.B - c1.B) * t);
                return Color.FromArgb(255, r, g, b);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    frameTimer?.Stop();
                    frameTimer?.Dispose();
                    cachedBlurPath?.Dispose();
                }
                base.Dispose(disposing);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                EnsureBlurPath();
                if (renderBars == null || renderBars.Length != barCount)
                    renderBars = new float[barCount];
                Invalidate();
            }
        }
    }
}
