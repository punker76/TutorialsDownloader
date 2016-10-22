namespace PluralsightDownloader
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;

    using Helpers;
    using HtmlAgilityPack;
    using MahApps.Metro.Controls;
    using Models;


    public partial class MainWindow : MetroWindow
    {
        #region Constructor

        public MainWindow()
        {
            this.InitializeComponent();
            this.Loaded += (sender, args) =>
                {
                    this.LoadSettings();
                    this.DialogHost.IsOpen = true;
                };
        }

        #endregion

        #region Events

        private void DownloadButton_OnClick(object sender, RoutedEventArgs e)
        {
            this.DownloadCourse();
        }

        private void SaveConfigButton_OnClick(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Username = this.UserNameTextbox.Text;
            Properties.Settings.Default.Password = this.PasswordBox.Password;
            Properties.Settings.Default.Downloads = this.DownloadTextbox.Text;
            Properties.Settings.Default.Save();
            this.DialogHost.IsOpen = false;
        }
        
        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            var category = (e.Source as Button).Content.ToString();
            var directory = Path.Combine(Properties.Settings.Default.Downloads, CourseTitleLabel.Text, category);
            Process.Start(directory);
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            this.UpdateButton.IsEnabled = false;
            this.SaveConfigButton.IsEnabled = false;
            this.UpdateProgress.Visibility = Visibility.Visible;

            await Task.Run(() =>
            {
                var arguments = @"--update";
                var psi = new ProcessStartInfo(@"Resources\youtube-dl.exe", arguments);

                if (!Debugger.IsAttached)
                {
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                }

                var process = Process.Start(psi);
                process.WaitForExit();
            })
            .ContinueWith(a =>
            {
                this.UpdateButton.IsEnabled = true;
                this.SaveConfigButton.IsEnabled = true;
                this.UpdateProgress.Visibility = Visibility.Hidden;
            },
            TaskScheduler.FromCurrentSynchronizationContext());
        }

        #endregion

        #region Private-Methods

        private async void DownloadCourse()
        {
            Action<List<VideoModel>> update = vdos =>
                {
                    this.PlaylistDisplay.ItemsSource = this.GroupToDictionary(vdos);

                    this.ElapsedChip.Content =
                        TimeDisplayConverter.TimeToReadbleFormat(vdos.Where(v => v.Downloaded).Sum(v => v.WaitTime));

                    this.ETAChip.Content =
                        TimeDisplayConverter.TimeToReadbleFormat(vdos.Where(v => !v.Downloaded).Sum(v => v.WaitTime));
                };

            // list all playlist items
            var url = this.PlaulistUrlTextbox.Text;
            var data = new KeyValuePair<string, Dictionary<VideoModel, List<VideoModel>>>();
            var start = 0;
            List<VideoModel> videos = new List<VideoModel>();
            await Task.Run(() => this.ParsePlaylist(url)).ContinueWith(
                a =>
                    {
                        data = a.Result;
                        start =
                            Directory.GetFiles(
                                Path.Combine(Properties.Settings.Default.Downloads, data.Key),
                                "*.mp4",
                                SearchOption.AllDirectories).Length;
                        videos = data.Value.SelectMany(v => v.Value).ToList();

                        for (var i = 0; i < start; i++)
                        {
                            videos[i].Downloaded = true;
                        }

                        this.CourseTitleLabel.Text = data.Key;
                        update(videos);

                    },
                TaskScheduler.FromCurrentSynchronizationContext());

            // update playlist items with status, after every download
            var playlist = this.PlaylistDisplay.ItemsSource as Dictionary<VideoModel, List<VideoModel>>;
            videos = playlist.SelectMany(g => g.Value).ToList();

            for (var i = start; i < videos.Count; i++)
            {
                var video = videos[i];

                await Task.Run(
                    () =>
                        {
                            var startTime = DateTime.Now;
                            var result = this.DownloadVideo(data.Key, url, i + 1, video.Category, video.Name);
                            var endTime = DateTime.Now;
                            var timeTaken = (int)endTime.Subtract(startTime).TotalMilliseconds;
                            var waitTime = video.WaitTime * 1000 >= timeTaken ? video.WaitTime * 1000 - timeTaken : 0;

                            if (!result)
                            {
                                i--;
                                waitTime = 5 * 1000;
                            }

                            Thread.Sleep(waitTime);

                            return result;
                        }).ContinueWith(
                            a =>
                                {
                                    if (a.Result)
                                    {
                                        videos[i].Downloaded = true;
                                        playlist = this.GroupToDictionary(videos);
                                        update(videos);
                                    }
                                },
                            TaskScheduler.FromCurrentSynchronizationContext());
            }

            await Task.Run(
                () =>
                    {
                        var psi = new ProcessStartInfo("shutdown", "/s /t 30");

                        if (!Debugger.IsAttached)
                        {
                            psi.CreateNoWindow = true;
                            psi.UseShellExecute = false;
                        }

                        return psi;
                    }).ContinueWith(
                        a =>
                            {
                                if (this.ShutdownToggleSwitch.IsChecked.Value)
                                {
                                    Process.Start(a.Result);
                                    this.Close();
                                }
                            },
                        TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void LoadSettings()
        {
            this.UserNameTextbox.Text = Properties.Settings.Default.Username;
            this.PasswordBox.Password = Properties.Settings.Default.Password;
            this.DownloadTextbox.Text = Properties.Settings.Default.Downloads;
        }

        private KeyValuePair<string, Dictionary<VideoModel, List<VideoModel>>> ParsePlaylist(string uri)
        {
            Func<string, string> removeInvalidCharacters =
                input => string.Join(string.Empty, input.Split(Path.GetInvalidFileNameChars()));

            using (var client = new WebClient())
            {
                var html = client.DownloadString(uri);
                var htmlPack = new HtmlDocument();
                htmlPack.LoadHtml(html);

                var course =
                    htmlPack.DocumentNode.Descendants("div")
                        .FirstOrDefault(
                            d => d.Attributes["class"] != null && d.Attributes["class"].Value == "title section")
                        .Descendants("h2")
                        .FirstOrDefault()
                        .InnerText;
                course = WebUtility.HtmlDecode(course).Trim();
                course = removeInvalidCharacters(course);

                var toc =
                    htmlPack.DocumentNode.Descendants("div")
                        .Where(
                            d =>
                            d.Attributes["id"] != null
                            && d.Attributes["id"].Value.Equals("tab-toc__accordion"))
                        .FirstOrDefault();

                var divs =
                    toc.Descendants("div")
                        .Where(
                            d =>
                            d.Attributes["class"] != null
                            && (d.Attributes["class"].Value.Equals("accordion-title clearfix")
                                || d.Attributes["class"].Value.Equals("accordion-content clearfix")))
                        .ToList();

                var playlist = new List<VideoModel>();
                for (var i = 0; i < divs.Count; i+=2)
                {
                    var title = divs[i];
                    var content = divs[i + 1];

                    var category =
                        title.Descendants("a")
                            .FirstOrDefault(
                                a =>
                                a.Attributes["class"] != null
                                && a.Attributes["class"].Value.StartsWith("accordion-title__title")).InnerText;

                    category = string.Format(
                        "{0}. {1}",
                        (i / 2 + 1).ToString("D2"),
                        WebUtility.HtmlDecode(category.Trim()));
                    category = removeInvalidCharacters(category);

                    var videos =
                        content.Descendants("span")
                            .Where(
                                s =>
                                s.Attributes["class"] != null
                                && s.Attributes["class"].Value.StartsWith("accordion-content__row__title"))
                            .Select(
                                (v, index) =>
                                string.Format(
                                    "{0}. {1}.mp4",
                                    (index + 1).ToString("D2"),
                                    WebUtility.HtmlDecode(v.InnerText.Trim())))
                            .ToList();
                    videos =
                        videos.Select(v => string.Join(string.Empty, v.Split(Path.GetInvalidFileNameChars()))).ToList();

                    var waitTimes =
                        content.Descendants("span")
                            .Where(
                                s =>
                                s.Attributes["class"] != null
                                && s.Attributes["class"].Value.StartsWith("accordion-content__row__time"))
                            .Select(
                                t =>
                                    {
                                        var time = t.InnerText.Split(
                                            new string[] { "m ", "s" },
                                            StringSplitOptions.RemoveEmptyEntries);

                                        return int.Parse(time[0].Trim()) * 60 + int.Parse(time[1].Trim());
                                    }).ToList();

                    playlist.AddRange(
                        Enumerable.Range(0, videos.Count)
                            .Select(
                                index =>
                                new VideoModel
                                    {
                                        Category = category,
                                        Name = removeInvalidCharacters(videos[index]),
                                        WaitTime = waitTimes[index],
                                        Downloaded = false
                                    }));
                }

                var headers = playlist.Select(p => p.Category).Distinct().ToList();
                this.CreateDirectories(course, headers);

                var dictionary = this.GroupToDictionary(playlist);
                return new KeyValuePair<string, Dictionary<VideoModel, List<VideoModel>>>(course, dictionary);
            }
        }

        private void CreateDirectories(string course, List<string> headers)
        {
            var baseDirectory = Properties.Settings.Default.Downloads;
            var root = Directory.CreateDirectory(Path.Combine(baseDirectory, course)).FullName;
            foreach (var header in headers)
            {
                Directory.CreateDirectory(Path.Combine(root, header));
            }
        }

        private bool DownloadVideo(string course, string uri, int index, string category, string name)
        {
            var userName = Properties.Settings.Default.Username;
            var password = Properties.Settings.Default.Password;
            var fileName = Path.Combine(Properties.Settings.Default.Downloads, course, category, name);

            var arguments =
                string.Format(
                    "--username {0} --password {1} -o \"{2}\" --playlist-start {3} --playlist-end {3} --max-downloads 1 --rate-limit 100K \"{4}\"",
                    userName,
                    password,
                    fileName,
                    index,
                    uri);

            var psi = new ProcessStartInfo(@"Resources\youtube-dl.exe", arguments);

            if (!Debugger.IsAttached)
            {
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
            }

            var process = Process.Start(psi);
            process.WaitForExit();

            return File.Exists(fileName);
        }

        private Dictionary<VideoModel, List<VideoModel>> GroupToDictionary(List<VideoModel> videos)
        {
            return
                videos.GroupBy(v => v.Category)
                    .Select(
                        g =>
                        new
                            {
                                Key =
                            new VideoModel
                                {
                                    Category = g.Key,
                                    Name = g.Key,
                                    WaitTime = g.Sum(v => v.WaitTime),
                                    Downloaded = g.Any(v => !v.Downloaded)
                                },
                                Value = g.ToList()
                            })
                    .ToDictionary(g => g.Key, g => g.Value);
        }

        #endregion
    }
}