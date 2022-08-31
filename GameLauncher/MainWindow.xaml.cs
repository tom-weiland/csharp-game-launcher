using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using System.Windows.Controls;
using System.Text.Json;
using System.Windows.Navigation;
using System.Reflection;
using Outlook = Microsoft.Office.Interop.Outlook;
using System.Collections.Generic;
using System.Text;

namespace GameLauncher
{
    enum LauncherStatus
    {
        ready,
        pendingLogin,
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
        private string[] args;
        private LauncherStatus _status;
        private string launchParameter;
        const string UriScheme = "ceremeet";
        const string FriendlyName = "Ceremeet Protocol";
        private string newsPage = "https://pdate.ceremeet.com/cermeethaber.html";
        private string defaultLocalization = "eng-US";
        private string LocalizationInfo = "eng-US";
        private string access_token = " ";
        private string UserName = " ";
        private string UserMembership = " ";
        private string UserEmail = " ";
        private string meetingid = " ";
        private bool meetingid_invalid;


        /// <summary>
        /// API adresses
        /// </summary>
        private string ApiAddress = "https://api.ceremeet.com";
        private string LoginRoot = "/api/auth/login";
        private string RegisterRootad = "/api/auth/register";
        private string MeRoot = "/api/users/me";
        private string MeetingRoot = "/api/meetings";
        private string MeetingLinkPreferals = "ceremeet://ceremeet:com";
        private string OutlookLinkPreferals = "https://files.ceremeet.com";
        private string linkchecker;

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
                        MeetingGroup.Visibility = Visibility.Visible;
                        NewMeetingGroup.Visibility = Visibility.Visible;
                        break;
                    case LauncherStatus.pendingLogin:
                        PlayButton.Content = (string)Application.Current.FindResource("pendingLogin");
                        LoginGroup.Visibility = Visibility.Visible;
                        UserGroup.Visibility = Visibility.Collapsed;
                        if (DownloadProgress != null)
                        {
                            DownloadProgress.Visibility = Visibility.Collapsed;
                        }
                        MeetingGroup.Visibility = Visibility.Hidden;
                        NewMeetingGroup.Visibility = Visibility.Hidden;

                        break;
                    case LauncherStatus.pendingLink:
                        LoginGroup.Visibility = Visibility.Collapsed;
                        MeetingGroup.Visibility = Visibility.Visible;
                        NewMeetingGroup.Visibility = Visibility.Visible;
                        UserGroup.Visibility = Visibility.Visible;
                        PlayButton.Content = (string)Application.Current.FindResource("pendingLink");

                        if (DownloadProgress != null)
                        {
                            DownloadProgress.Visibility = Visibility.Collapsed;
                        }
                        if (MeetingLink.Text.Length >= 60)
                        {
                            linkchecker = MeetingLink.Text.Substring(0, 23);
                            GetMeetingButton.IsEnabled = true;

                            if (linkchecker == MeetingLinkPreferals)
                            {
                                if (MeetingLink.Text.Length >= 60)
                                {
                                    var meetingids = MeetingLink.Text.Remove(0, 23);
                                    meetingid = meetingids.Substring(0, 37);
                                    launchParameter = MeetingLink.Text;
                                    MeetingRequest(meetingid);
                                    Status = LauncherStatus.ready;
                                }
                            }
                        }
                        break;
                    case LauncherStatus.failed:
                        PlayButton.Content = (string)Application.Current.FindResource("updateFailed");
                        break;
                    case LauncherStatus.downloadingGame:
                        PlayButton.Content = (string)Application.Current.FindResource("downloadingGame");
                        TrButton.IsEnabled = false;
                        EngButton.IsEnabled = false;
                        break;
                    case LauncherStatus.downloadingUpdate:
                        PlayButton.Content = (string)Application.Current.FindResource("downloadingUpdate");
                        TrButton.IsEnabled = false;
                        EngButton.IsEnabled = false;
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
            gamePath = Path.Combine(appdataPath, "ceremeet");
            versionFile = Path.Combine(gamePath, "Version.txt");
            gameZip = Path.Combine(gamePath, "Ceremeet.zip");
            gameExe = Path.Combine(gamePath, "Ceremeet", "ceremeet.exe");

            if (args.Length > 1)
            {
                MeetingLink.Text = args[1];
                launchParameter = args[1] + " ";
            }
            else
            {
                launchParameter = " ";
                RegisterUriScheme();
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
                    else if (access_token == " ")
                    {
                        Status = LauncherStatus.pendingLogin;
                        MeetingLink.IsEnabled = true;
                    }
                    else if (launchParameter.Length <= 25)
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
            rootPath = Directory.GetCurrentDirectory();
            launcherExe = Path.Combine(rootPath, "CeremeetLauncher.exe");

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
                TrButton.IsEnabled = true;
                EngButton.IsEnabled = true;
                if (access_token == " ")
                {
                    Status = LauncherStatus.pendingLogin;
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
                startInfo.Arguments = launchParameter + " " + LocalizationInfo + " " + access_token;
                startInfo.WorkingDirectory = Path.Combine(gamePath, "Ceremeet");
                MeetingRequest(meetingid);
                if (meetingid_invalid == false)
                {
                    Process.Start(startInfo);
                    PlayButton.IsEnabled = false;
                    Thread.Sleep(10000);
                    PlayButton.IsEnabled = true;
                }
            }
            else if (Status == LauncherStatus.failed)
            {
                CheckForUpdates();
            }
        }


        private void MeetingLink_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (MeetingLink.Text.Length >= 60)
            {
                linkchecker = MeetingLink.Text.Substring(0, 23);
                GetMeetingButton.IsEnabled = true;

                if (linkchecker == MeetingLinkPreferals)
                {
                    if (MeetingLink.Text.Length >= 60)
                    {
                        var meetingids = MeetingLink.Text.Remove(0, 23);
                        meetingid = meetingids.Substring(0, 37);
                        launchParameter = MeetingLink.Text;
                        Status = LauncherStatus.ready;
                    }
                    }
            }
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
        public class UserLogin
        {
            public string status { get; set; }
            public string access_token { get; set; }
            public string refressh_token { get; set; }
        }
        public void LoginRequest(string email, string password)
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create(ApiAddress + LoginRoot);
            httpRequest.ContentType = "application/json";
            httpRequest.Accept = "application/json";
            httpRequest.Method = "POST";
            //var json = "{\"email\":\"" + email + "\", \"password\":\"" + password + "\"}";
            var json = "{\"email\":\"alp@cerebrumtechnologies.com\",\"password\":\"password1222\"}";
            using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
            {
               

                streamWriter.Write(json);
            }
            try
            {
                var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    UserLogin userobject = JsonSerializer.Deserialize<UserLogin>(result);

                    if (userobject.status == "success")
                    {
                        access_token = userobject.access_token;
                        Status = LauncherStatus.pendingLink;
                        MeRequest();
                    }

                }


            }
            catch (Exception ex)
            {
                MessageBox.Show((string)Application.Current.FindResource("userWrongLogin"));
            }
        }
        public class Data
        {
            public User user { get; set; }
        }

        public class Root
        {
            public string status { get; set; }
            public Data data { get; set; }
        }

        public class User
        {
            public string id { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public string name { get; set; }
            public string email { get; set; }
            public string role { get; set; }
            public string photo { get; set; }
            public string membership { get; set; }
            public string nickname { get; set; }
        }
        public void MeRequest()
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create(ApiAddress + MeRoot);
            httpRequest.Accept = "application/json";
            httpRequest.Method = "GET";
            var BearerToken = "Bearer " + access_token;
            httpRequest.Headers["Authorization"] = BearerToken;

            var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                
                Root MeObj = JsonSerializer.Deserialize<Root>(result);
                UserName = MeObj.data.user.name;
                UserEmail = MeObj.data.user.email;
                UserMembership = MeObj.data.user.membership;

                if (UserMembership == "free")
                {
                    UserInfo.Text = (string)Application.Current.FindResource("Greeting") + " " + UserName + ", \n" +
                    (string)Application.Current.FindResource("GreetingMembership") + " " + UserMembership + (string)Application.Current.FindResource("GreetingMembership2") + "\n" +
                    (string)Application.Current.FindResource("GreetingMembershipFree") + " " + (string)Application.Current.FindResource("GreetingInstructions");
                }
                else
                {
                    UserInfo.Text = (string)Application.Current.FindResource("Greeting") + " " + UserName + ", \n" +
(string)Application.Current.FindResource("GreetingMembership") + " " + UserMembership + " " + (string)Application.Current.FindResource("GreetingMembership2") + " " +
(string)Application.Current.FindResource("GreetingMembershipPremium") + " \n \n" + (string)Application.Current.FindResource("GreetingInstructions");
                }


            }
        }
        public class NewmData
        {
            public NewmMeeting meeting { get; set; }
        }

        public class NewmMeeting
        {
            public string presentation { get; set; }
            public string password { get; set; }
            public string title { get; set; }
            public string userId { get; set; }
            public User user { get; set; }
            public string id { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
        }

        public class NewmRoot
        {
            public string status { get; set; }
            public NewmData data { get; set; }
        }

        public class NewmUser
        {
            public string id { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public string name { get; set; }
            public string email { get; set; }
            public string role { get; set; }
            public string photo { get; set; }
            public string membership { get; set; }
            public string nickname { get; set; }
        }
        public void CreateNewMeetingRequest(string title)
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create(ApiAddress + MeetingRoot);
            httpRequest.ContentType = "application/json";
            httpRequest.Accept = "application/json";
            httpRequest.Method = "POST";
            var BearerToken = "Bearer " + access_token;
            httpRequest.Headers["Authorization"] = BearerToken;
            var json = "{\"title\":\"" + title + "\", \"password\":\"" + "password" +  "\", \"presentation\":\"" + "pdf.pdf" + "\"}";
            using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
            {


                streamWriter.Write(json);
            }
            //try
            //{
                var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    NewmRoot newmobject = JsonSerializer.Deserialize<NewmRoot>(result);

                    if (newmobject.status == "success")
                    {
                        MeetingLink.Text = "ceremeet://ceremeet:com/" + newmobject.data.meeting.id + "?pwd=" + newmobject.data.meeting.password;
                    meetingid = newmobject.data.meeting.id;
                    }


                }


            //}
           // catch (Exception ex)
           // {
           //     MessageBox.Show("Wrong Password");
           // }
        }
        public class mData
        {
            public mMeeting meeting { get; set; }
        }

        public class mMeeting
        {
            public string id { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public string presentation { get; set; }
            public string password { get; set; }
            public string title { get; set; }
            public string userId { get; set; }
        }

        public class mRoot
        {
            public string status { get; set; }
            public mData data { get; set; }
        }
        public void MeetingRequest(string meetingid)
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create(ApiAddress + MeetingRoot + meetingid);
            httpRequest.Accept = "application/json";
            httpRequest.Method = "GET";
            var BearerToken = "Bearer " + access_token;
            httpRequest.Headers["Authorization"] = BearerToken;
            try
            {
                var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();

                    mRoot MeetingObj = JsonSerializer.Deserialize<mRoot>(result);
                    if (MeetingObj.data.meeting.title != null) { 
                    var MeetingTitle = MeetingObj.data.meeting.title;
                    MeetingInfo.Visibility = Visibility.Visible;

                    MeetingInfo.Text = (string)Application.Current.FindResource("meetingInfoJoining") + " \n" + MeetingTitle;
                        NewMeetingTitle.Text = MeetingTitle;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show((string)Application.Current.FindResource("meetingLinkWrong"));
                meetingid_invalid = true;
            }
        }
        private void DownloadProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }


        private void SelectMeeetingText(object sender, RoutedEventArgs e)

        {

            TextBox tb = (sender as TextBox);

            if (tb != null)

            {

                tb.SelectAll();

            }

        }



        private void SelectivelyIgnoreMouseButton(object sender,

            MouseButtonEventArgs e)

        {

            TextBox tb = (sender as TextBox);

            if (tb != null)

            {

                if (!tb.IsKeyboardFocusWithin)

                {

                    e.Handled = true;

                    tb.Focus();

                }

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

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginRequest(email.Text, password.Password);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            
            Status = LauncherStatus.pendingLogin;
        }

        private void GetMeetingButton_Click(object sender, RoutedEventArgs e)
        {
            var meetingids = MeetingLink.Text.Remove(0, 23);
            meetingid = meetingids.Substring(0, 37);
            launchParameter = MeetingLink.Text;
            MeetingRequest(meetingid);
            ShareButton.Visibility = Visibility.Visible;
        }


        private void NewMeetingButton_Click(object sender, RoutedEventArgs e)
        {
            if (NewMeetingTitle.Text == (string)Application.Current.FindResource("meetingCreateNewTitle"))
            {
                MessageBox.Show((string)Application.Current.FindResource("meetingCreateNewChange"));
            }
            else
            {
                CreateNewMeetingRequest(NewMeetingTitle.Text);
            }
         }

        private void NewMeetingTitle_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
        
        
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var sInfo = new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(sInfo);
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            LoginGroup.Visibility = Visibility.Hidden;
            RegisterGroup.Visibility = Visibility.Visible;
        }
        public class RegisterRoot
        {
            public string status { get; set; }
            public string message { get; set; }
        }

        public void RegisterRequest(string email, string password, string passwordconfirm, string name)
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create(ApiAddress + RegisterRootad);
            httpRequest.ContentType = "application/json";
            httpRequest.Accept = "application/json";
            httpRequest.Method = "POST";
            //var json = "{\"email\":\"" + email + "\", \"password\":\"" + password + "\"," + "\"name\":\"" + name + "\"," + "\"passwordConfirm\":\"" + passwordconfirm + "\"}";
            var json = "{\"email\":\"alp@cerebrumtechnologies.com\",\"password\":\"password1222\"}";
            using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
            {


                streamWriter.Write(json);
            }
            try
            {
                var httpResponse = (HttpWebResponse)httpRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    RegisterRoot registerobject = JsonSerializer.Deserialize<RegisterRoot>(result);

                    if (registerobject.status == "success")
                    {
                        Status = LauncherStatus.pendingLogin;
                        LoginGroup.Visibility = Visibility.Visible;
                        RegisterGroup.Visibility = Visibility.Hidden;
                        MessageBox.Show((string)Application.Current.FindResource("UserVerifyEmail"));
                    }
                    else
                    {
                    }
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show((string)Application.Current.FindResource("AccountExists"));
            }
        }
        private void SendRegisterButton_Click(object sender, RoutedEventArgs e)
        {
            if (newpasswordconfirm.Password != newpassword.Password)
            {
                MessageBox.Show((string)Application.Current.FindResource("PasswordMatchError"));
            }
            else if (newpassword.Password.Length < 8 | newpassword.Password.Length > 32)
            {
                MessageBox.Show((string)Application.Current.FindResource("PasswordWeak"));
            }
            else if (IsValidEmail(newemail.Text) != true)
            {
                MessageBox.Show((string)Application.Current.FindResource("EmailisnotValid"));
            }

            else if (Name.Text.Length < 1 | newpassword.Password.Length < 1)
            {
                MessageBox.Show((string)Application.Current.FindResource("EmailisnotValid"));

            }
            else
            {
                RegisterRequest(newemail.Text, newpassword.Password, newpasswordconfirm.Password, Name.Text);
            }
        }

        private void ReturnLoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginGroup.Visibility = Visibility.Visible;
            RegisterGroup.Visibility = Visibility.Hidden;
        }

        static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void ShareButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {


                Outlook.Application outlookApp = new Outlook.Application();
                Outlook._AppointmentItem oAppointmentItem = (Outlook.AppointmentItem)outlookApp.CreateItem(Outlook.OlItemType.olAppointmentItem);
                Outlook.Inspector oInspector = oAppointmentItem.GetInspector;
                // Thread.Sleep(10000);

                // Recipient





                oAppointmentItem.Subject = NewMeetingTitle.Text;
                oAppointmentItem.Location = "CereMeet";
                oAppointmentItem.Start = DateTime.Now;
                oAppointmentItem.End = DateTime.Now.AddHours(1);
                oAppointmentItem.BodyFormat = Outlook.OlBodyFormat.olFormatHTML;
                oAppointmentItem.Body = "\n \n \n \n \n \n \n " + (string)Application.Current.FindResource("OutlookBody") + "\n \n" +
                    (string)Application.Current.FindResource("OutlookBody2") + " " + OutlookLinkPreferals + meetingid + "?pwd=password";
                //Display the mailbox
                oAppointmentItem.Display(true);
            }
            catch (Exception objEx)
            {
                MessageBox.Show(objEx.ToString());
            }
        }
    }


}
