using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using NAudio.Wave;

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
        private PictureBox pbCover;
        private Panel controlPanel;
        private Button btnPrev, btnPlayPause, btnNext, btnStop;
        private TrackBar trackBarPosition;
        private Label lblTime;
        private TrackBar tbVolume;
        private System.Windows.Forms.Timer timer;


        // Playback
        private List<string> playlist = new List<string>();
        private int currentIndex = -1;
        private IWavePlayer outputDevice;
        private AudioFileReader audioFile;
        private bool isDraggingPosition = false;

      

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

            // Right panel (player area)
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

            pbCover = new PictureBox
            {
                Size = new Size(300, 300),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(45, 47, 51),
                Anchor = AnchorStyles.Top,
                Location = new Point((rightPanel.ClientSize.Width - 300) / 2, 60)
            };

            // control panel (bottom)
            controlPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 140,
                BackColor = Color.FromArgb(22, 23, 25),
                Padding = new Padding(8)
            };

            // Buttons
            btnPrev = MakeControlButton("⏮");
            btnPlayPause = MakeControlButton("▶");
            btnNext = MakeControlButton("⏭");
            btnStop = MakeControlButton("■");

            btnPrev.Click += BtnPrev_Click;
            btnPlayPause.Click += BtnPlayPause_Click;
            btnNext.Click += BtnNext_Click;
            btnStop.Click += BtnStop_Click;

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
                if (audioFile == null)
                {
                }
                else
                {
                    audioFile.CurrentTime = TimeSpan.FromSeconds(trackBarPosition.Value);
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
                Orientation = Orientation.Vertical,
                Height = 80,
                Dock = DockStyle.Right
            };
            tbVolume.ValueChanged += TbVolume_ValueChanged;

            // Layout buttons (FlowLayoutPanel)
            var buttonsFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4)
            };
            buttonsFlow.Controls.Add(btnPrev);
            buttonsFlow.Controls.Add(btnPlayPause);
            buttonsFlow.Controls.Add(btnNext);
            buttonsFlow.Controls.Add(btnStop);

            controlPanel.Controls.Add(buttonsFlow);
            controlPanel.Controls.Add(tbVolume);
            controlPanel.Controls.Add(trackBarPosition);
            controlPanel.Controls.Add(lblTime);

            rightPanel.Controls.Add(lblNowPlaying);
            rightPanel.Controls.Add(pbCover);
            rightPanel.Controls.Add(controlPanel);

            // timer to update position
            timer = new System.Windows.Forms.Timer { Interval = 500 };

            timer.Tick += Timer_Tick;
            timer.Start();

            this.Controls.Add(rightPanel);
            this.Controls.Add(leftPanel);

            // center cover on resize
            this.Resize += (s, e) =>
            {
                pbCover.Left = leftPanel.Width + (rightPanel.ClientSize.Width - pbCover.Width) / 2;
            };
        }

        private Button MakeControlButton(string text)
        {
            return new Button
            {
                Text = text,
                Width = 64,
                Height = 42,
                Margin = new Padding(6),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(68, 70, 75),
                ForeColor = Color.White
            };
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
                audioFile = new AudioFileReader(file);
                outputDevice = new WaveOutEvent();
                outputDevice.Init(audioFile);
                outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
                outputDevice.Play();
                btnPlayPause.Text = "⏸";
                lblNowPlaying.Text = Path.GetFileNameWithoutExtension(file);

                // set trackbar maximum in seconds
                var totalSeconds = (int)Math.Max(1, audioFile.TotalTime.TotalSeconds);
                trackBarPosition.Maximum = totalSeconds;
                trackBarPosition.Value = 0;

                // set volume control to match current reader volume
                tbVolume.Value = (int)(audioFile.Volume * 100);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot play file: " + ex.Message, "Playback error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            if (audioFile != null)
            {
                audioFile.Dispose();
                audioFile = null;
            }
            btnPlayPause.Text = "▶";
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
                btnPlayPause.Text = "▶";
            }
            else
            {
                outputDevice.Play();
                btnPlayPause.Text = "⏸";
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (outputDevice != null)
            {
                outputDevice.Stop();
                if (audioFile != null) audioFile.Position = 0;
                btnPlayPause.Text = "▶";
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
            // if playback stopped due to reaching end -> play next
            if (audioFile != null)
            {
                var nearEnd = audioFile.CurrentTime >= audioFile.TotalTime - TimeSpan.FromMilliseconds(200);
                if (nearEnd)
                {
                    // use BeginInvoke to marshal to UI thread
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
                if (sec >= 0 && sec <= trackBarPosition.Maximum)
                {
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
    }
}
