using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Globalization;
using System.Threading;
using System.Resources;
using System.Reflection;

namespace GameLauncher
{
    enum LauncherStatus
    {
        ready,
        pendingLink,
        failed,
        downloadingGame,
        downloadingUpdate
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string rootPath;
        private string appdataPath;
        private string versionFile;
        private string gameZip;
        private string gameExe;
        private string launcherExe;
        private string gamePath;
        private string [] args;
        private LauncherStatus _status;
        private string launchParameter;
        const string UriScheme = "ceremeet";
        const string FriendlyName = "Ceremeet Protocol";
        private string newsPage = "https://pdate.ceremeet.com/cermeethaber.html";
        private string defaultLocalization = "eng-US";
        private string LocalizationInfo = "eng-US";
        /// <summary>
        /// Localize UI text
        /// </summary>


        internal LauncherStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                switch (_status)
                {
                    case LauncherStatus.ready:
                        PlayButton.Content = (string)Application.Current.FindResource("start");
                        if (DownloadProgress != null)
                        {
                            DownloadProgress.Visibility = Visibility.Collapsed;
                        }
                        break;
                    case LauncherStatus.pendingLink:
                        string selectedLanguage = (string)Application.Current.FindResource("pendingLink");
                        PlayButton.Content = selectedLanguage;
                        if (DownloadProgress != null)
                        {
                            DownloadProgress.Visibility = Visibility.Collapsed;
                        }
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = (string)Application.Current.FindResource("updateFailed");
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = (string)Application.Current.FindResource("downloadingGame");
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = (string)Application.Current.FindResource("downloadingUpdate");
                        break;
                    default:
                        break;
                }
            }
        }

        public MainWindow()
        {
            defaultLocalization = CultureInfo.InstalledUICulture.ToString();
            SwitchLanguage(defaultLocalization);
            args = Environment.GetCommandLineArgs();
            InitializeComponent();
            appdataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            rootPath = Directory.GetCurrentDirectory();
            launcherExe = Path.Combine(rootPath, "CeremeetLauncher.exe");
            gamePath = Path.Combine(appdataPath, "ceremeet");
            versionFile = Path.Combine(gamePath, "Version.txt");
            gameZip = Path.Combine(gamePath, "Ceremeet.zip");
            gameExe = Path.Combine(gamePath, "Ceremeet", "ceremeet.exe");
            RegisterUriScheme();
            if (args.Length > 1)
            {
                MeetingLink.Text = args[1];
                launchParameter = args[1] + " ";
            }
            else
            {
                launchParameter = " ";
            }

            bool exists = System.IO.Directory.Exists(gamePath);

            if (!exists)
                System.IO.Directory.CreateDirectory(gamePath);

            
        }
        private void LoadWebPage()
        {
            webBrowser.Navigate(newsPage);
        }

        private void SetLocalizationButton()
        {
            if (defaultLocalization == "tr-TR")
            {
                EngButton.Visibility = Visibility.Visible;
            }
            else
            {
                TrButton.Visibility = Visibility.Visible;
            }
        }

        private void CheckForUpdates()
        {
            MeetingLink.IsEnabled = false;
            if (File.Exists(versionFile))
            {
                Version localVersion = new Version(File.ReadAllText(versionFile));
                VersionText.Text = localVersion.ToString();

                try
                {
                    WebClient webClient = new WebClient();
                    Version onlineVersion = new Version(webClient.DownloadString("https://pdate.ceremeet.com/Version.txt"));

                    if (onlineVersion.IsDifferentThan(localVersion))
                    {
                        InstallGameFiles(true, onlineVersion);
                    }
                    else if (launchParameter == " ")
                    {
                        Status = LauncherStatus.pendingLink;
                        MeetingLink.IsEnabled = true;
                    }
                    else
                    {
                        Status = LauncherStatus.ready;
                        MeetingLink.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    Status = LauncherStatus.failed;
                    MessageBox.Show($"Oyun dosyaları alınamadı: {ex}");
                }
            }
            else
            {
                InstallGameFiles(false, Version.zero);
            }
        }
        private void InstallGameFiles(bool _isUpdate, Version _onlineVersion)
        {
            try
            {
                WebClient webClient = new WebClient();
                if (_isUpdate)
                {
                    Status = LauncherStatus.downloadingUpdate;
                }
                else
                {
                    Status = LauncherStatus.downloadingGame;
                    _onlineVersion = new Version(webClient.DownloadString("https://pdate.ceremeet.com/Version.txt"));
                }
                webClient.DownloadProgressChanged += (s, e) =>
                {
                    DownloadProgress.Visibility = Visibility.Visible;
                    DownloadProgress.Value = e.ProgressPercentage;
                };
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadGameCompletedCallback);
                webClient.DownloadFileAsync(new Uri("https://pdate.ceremeet.com/Ceremeet.zip"), gameZip, _onlineVersion);
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"Oyun dosyaları yüklenemedi: {ex}");
            }
        }
        private void RegisterUriScheme()
        {
            using (var key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + UriScheme))
            {
                // Replace typeof(App) by the class that contains the Main method or any class located in the project that produces the exe.
                // or replace typeof(App).Assembly.Location by anything that gives the full path to the exe
                
                

                key.SetValue("", "URL:" + FriendlyName);
                key.SetValue("URL Protocol", "");

                using (var defaultIcon = key.CreateSubKey("DefaultIcon"))
                {
                    defaultIcon.SetValue("", launcherExe + ",1");
                }

                using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                {
                    commandKey.SetValue("", "\"" + launcherExe + "\" \"%1\"");
                }
            }
        }
        private void DownloadGameCompletedCallback(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                string onlineVersion = ((Version)e.UserState).ToString();
                ZipFile.ExtractToDirectory(gameZip, gamePath, true);
                File.Delete(gameZip);

                File.WriteAllText(versionFile, onlineVersion);
                VersionText.Text = onlineVersion;
                if (launchParameter == " ")
                {
                    Status = LauncherStatus.pendingLink;
                    MeetingLink.IsEnabled = true;
                }
                else
                {
                    Status = LauncherStatus.ready;
                    MeetingLink.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Status = LauncherStatus.failed;
                MessageBox.Show($"İndirme tamamlanamadı: {ex}");
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            CheckForUpdates();
            LoadWebPage();
            SetLocalizationButton();
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(gameExe) && Status == LauncherStatus.ready)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(gameExe);
                startInfo.Arguments = launchParameter + " " + LocalizationInfo;
                startInfo.WorkingDirectory = Path.Combine(gamePath, "Ceremeet");
                Process.Start(startInfo);

                Close();
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }


        private void MeetingLink_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            launchParameter = MeetingLink.Text;
            Status = LauncherStatus.ready;

        }





        public void SwitchLanguage(string languageCode)
        {
            ResourceDictionary dictionary = new ResourceDictionary();
            switch (languageCode)
            {
                case "eng-US":
                    dictionary.Source = new Uri("..\\Resources\\Dictionary_en-US.xaml", UriKind.Relative);
                    break;
                case "eng-UK":
                    dictionary.Source = new Uri("..\\Resources\\Dictionary_en-US.xaml", UriKind.Relative);
                    break;
                case "tr-TR":
                    dictionary.Source = new Uri("..\\Resources\\Dictionary_tr-TR.xaml", UriKind.Relative);
                    break;
                default:
                    dictionary.Source = new Uri("..\\Resources\\Dictionary_en-US.xaml", UriKind.Relative);
                    break;
            }
            Application.Current.Resources.MergedDictionaries.Add(dictionary);
        }

        private void tr_Click(object sender, RoutedEventArgs e)
        {
            SwitchLanguage("tr-TR");
            LocalizationInfo = "tr-TR";
            TrButton.Visibility = Visibility.Hidden;
            EngButton.Visibility = Visibility.Visible;
            CheckForUpdates();
        }

        private void eng_Click(object sender, RoutedEventArgs e)
        {
            SwitchLanguage("eng-US");
            LocalizationInfo = "eng-US";
            EngButton.Visibility = Visibility.Hidden;
            TrButton.Visibility = Visibility.Visible;
            CheckForUpdates();

        }

        private void DownloadProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }
    }

    struct Version
    {
        internal static Version zero = new Version(0, 0, 0);

        private short major;
        private short minor;
        private short subMinor;

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
