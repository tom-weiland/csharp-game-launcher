using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace GameLauncher
{
    enum LauncherStatus
    {
        ready,
        failed,
        downloadingGame,
        downloadingUpdate
    }

    public partial class MainWindow : Window
    {
        private readonly string rootPath;
        private readonly string versionFile;
        private readonly string gameZip;
        private readonly string gameExe;

        private LauncherStatus _status;
        private readonly IProgress<int> _downloadProgress;

        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = "Launch";
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = "Update Failed - Retry";
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = "Downloading files; wait until done.";
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = "Update found; Downloading...";
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            rootPath = Directory.GetCurrentDirectory();
            versionFile = Path.Combine(rootPath, "Version.txt");
            gameZip = Path.Combine(rootPath, "Renvirons Project.zip");
            gameExe = Path.Combine(rootPath, "RENVIRONS_BUILDFILES", "Renvirons Project.exe");

            _downloadProgress = new Progress<int>(percentage =>
            {
                ProgressBar.Value = percentage;
                StatusText.Text = $"Downloading... {percentage}%";
            });
        }

        private async void CheckForUpdates()
        {
            if (File.Exists(versionFile))
            {
                Version localVersion = new(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();

                try
                {
                    using HttpClient httpClient = new();
                    string versionString = await httpClient.GetStringAsync("https://www.dropbox.com/s/uowcriov5cod7wt/Version.txt?dl=1");
                    Version onlineVersion = new(versionString);

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
            else
            {
                try
                {
                    using HttpClient httpClient = new();
                    string versionString = await httpClient.GetStringAsync("https://www.dropbox.com/s/uowcriov5cod7wt/Version.txt?dl=1");
                    Version onlineVersion = new(versionString);

                    InstallGameFiles(false, onlineVersion);
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Error checking for game updates: {ex}");
                }
            }
        }

        private async void InstallGameFiles(bool _isUpdate, Version onlineVersion)
        {
            try
            {
                using HttpClient httpClient = new();
                if (_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                }

                using (HttpResponseMessage response = await httpClient.GetAsync("https://www.dropbox.com/s/4txqq97xuej54dq/Renvirons%20Project.zip?dl=1", HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using Stream contentStream = await response.Content.ReadAsStreamAsync(),
                        stream = new FileStream(gameZip, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                    long totalBytes = response.Content.Headers.ContentLength ?? -1;
                    long receivedBytes = 0;
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await stream.WriteAsync(buffer.AsMemory(0, bytesRead));
                        receivedBytes += bytesRead;
                        if (totalBytes > 0)
                        {
                            int percentage = (int)((receivedBytes / (double)totalBytes) * 100);
                            _downloadProgress.Report(percentage);
                        }
                    }
                }

                DownloadGameCompletedCallback(null, new AsyncCompletedEventArgs(null, false, onlineVersion));
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error installing game files: {ex}");
            }
        }

        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.UserState != null)
                {
                    string onlineVersion = ((Version)e.UserState).ToString();
                    ZipFile.ExtractToDirectory(gameZip, rootPath, true);
                    File.Delete(gameZip);
                    File.WriteAllText(versionFile, onlineVersion);

                    VersionText.Text = onlineVersion;
                    Status = LauncherStatus.ready;
                }
                else
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show("Error finishing download: UserState is null.");
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Error finishing download: {ex}");
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new(gameExe)
                {
                    WorkingDirectory = Path.Combine(rootPath, "RENVIRONS_BUILDFILES")
                };
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }

        struct Version
        {
            internal static Version zero = new(0, 0, 0);

            private readonly short major;
            private readonly short minor;
            private readonly short subMinor;

            internal Version(short _major, short _minor, short _subMinor)
            {
                major = _major;
                minor = _minor;
                subMinor = _subMinor;
            }
            internal Version(string _version)
            {
                string[] versionStrings = _version.Split('.');
                if (versionStrings.Length != 3)
                {
                    major = 0;
                    minor = 0;
                    subMinor = 0;
                    return;
                }

                major = short.Parse(versionStrings[0]);
                minor = short.Parse(versionStrings[1]);
                subMinor = short.Parse(versionStrings[2]);
            }

            internal bool IsDifferentThan(Version _otherVersion)
            {
                if (major != _otherVersion.major)
                {
                    return true;
                }
                else
                {
                    if (minor != _otherVersion.minor)
                    {
                        return true;
                    }
                    else
                    {
                        if (subMinor != _otherVersion.subMinor)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public override string ToString()
            {
                return $"{major}.{minor}.{subMinor}";
            }
        }
    }
}