using FeiSharpStudio;
using HtmlAgilityPack;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FMusicPlayer
{
    public class MusicPlayer
    {
        private string _mode;
        private string _downloadPath;
        private WaveOutEvent _waveOut;
        private AudioFileReader _audioFileReader;
        private string _currentMusicPath;
        private bool _isPaused;
        private HttpClient _httpClient;
        private string _currentSongName;
        private bool _isStopping;
        private string _cookieContainer;

        public event EventHandler<MusicEventArgs> MusicStarted;
        public event EventHandler<MusicEventArgs> MusicStopped;
        public event EventHandler<MusicEventArgs> MusicPaused;
        public event EventHandler<MusicEventArgs> MusicResumed;
        public event EventHandler<DownloadProgressEventArgs> DownloadProgress;
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        public MusicPlayer(string mode = "local", string downloadPath = null)
        {
            if (mode != "online" && mode != "local")
                throw new ArgumentException("Mode must be 'online' or 'local'");

            _mode = mode;
            _downloadPath = downloadPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "DownloadedMusic");

            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer()
            };

            _httpClient = new HttpClient(handler);
            _httpClient.Timeout = TimeSpan.FromMinutes(10);

            // Headers from the request
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36 Edg/152.0.0.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,zh-CN;q=0.8,zh;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br, zstd");
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            _httpClient.DefaultRequestHeaders.Add("DNT", "1");
            _httpClient.DefaultRequestHeaders.Add("Priority", "u=0, i");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"152\", \"Not?A_Brand\";v=\"24\", \"Microsoft Edge\";v=\"152\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
            _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
            _httpClient.DefaultRequestHeaders.Add("Prefer", "safe");

            _waveOut = new WaveOutEvent();
            _isPaused = false;

            if (!Directory.Exists(_downloadPath))
                Directory.CreateDirectory(_downloadPath);
        }

        private void SetReferer(string referer)
        {
            if (_httpClient.DefaultRequestHeaders.Contains("Referer"))
                _httpClient.DefaultRequestHeaders.Remove("Referer");
            _httpClient.DefaultRequestHeaders.Add("Referer", referer);
        }

        private void SetCookies(string cookies)
        {
            _cookieContainer = cookies;
            if (!string.IsNullOrEmpty(cookies))
            {
                var cookieContainer = new System.Net.CookieContainer();
                var cookieCollection = new System.Net.CookieCollection();
                var cookiePairs = cookies.Split(';');
                foreach (var pair in cookiePairs)
                {
                    var parts = pair.Trim().Split('=');
                    if (parts.Length == 2)
                    {
                        var cookie = new System.Net.Cookie(parts[0].Trim(), parts[1].Trim(), "/", "www.gequbao.com");
                        cookieCollection.Add(cookie);
                    }
                }
                cookieContainer.Add(cookieCollection);
                var handler = (HttpClientHandler)_httpClient.GetType().GetProperty("Handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_httpClient);
                if (handler != null)
                {
                    handler.CookieContainer = cookieContainer;
                }
            }
        }

        public async Task PlayMusic(string musicIdentifier)
        {
            try
            {
                if (_mode == "online")
                {
                    await PlayOnlineMusic(musicIdentifier);
                }
                else
                {
                    PlayLocalMusic(musicIdentifier);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs(ex.Message, ex));
                throw;
            }
        }

        public void PlayLocalMusic(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Music file not found: {filePath}");

                StopMusic();

                _currentMusicPath = filePath;
                _currentSongName = Path.GetFileName(filePath);
                _audioFileReader = new AudioFileReader(filePath);
                _waveOut = new WaveOutEvent();
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _waveOut.Init(_audioFileReader);
                _waveOut.Play();
                _isPaused = false;

                OnMusicStarted(new MusicEventArgs(_currentMusicPath, _currentSongName));
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs($"Failed to play local music: {ex.Message}", ex));
                throw;
            }
        }

        public async Task PlayOnlineMusic(string songName)
        {
            try
            {
                string fileName = $"{SanitizeFileName(songName)}.mp3";
                string savePath = Path.Combine(_downloadPath, fileName);
                await DownloadOnlineMusic(songName, savePath);
                PlayLocalMusic(savePath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs($"Failed to play online music: {ex.Message}", ex));
                throw;
            }
        }

        public async Task DownloadOnlineMusic(string songName, string savePath)
        {
            string? downloadUrl = await GetMusicDownloadUrl(songName);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new InvalidOperationException("No available download link found");
            }

            string? directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await DownloadMusicAsync(downloadUrl, savePath);
        }

        private async Task<string> GetMusicDownloadUrl(string songName)
        {
            try
            {
                string encodedSongName = Uri.EscapeDataString(songName);
                string searchUrl = $"https://www.gequcn.com/search.php?q={encodedSongName}";

                SetReferer("https://www.gequcn.com/");

                var html = await _httpClient.GetStringAsync(searchUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var links = doc.DocumentNode.SelectNodes("//a[@class='text-primary font-weight-bold']");
                if (links == null || links.Count == 0)
                {
                    OnErrorOccurred(new ErrorEventArgs("No matching song links found", null));
                    return null;
                }
                HtmlNode matchingLink = null;

                foreach (var link in links)
                {
                    string title = link.InnerText;
                    if (!string.IsNullOrEmpty(title) && title.Contains(songName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingLink = link;
                        break;
                    }
                }

                if (matchingLink == null)
                {
                    matchingLink = links.FirstOrDefault();
                    if (matchingLink == null)
                    {
                        OnErrorOccurred(new ErrorEventArgs("No valid song link found", null));
                        return null;
                    }
                }

                string href = matchingLink.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href))
                {
                    OnErrorOccurred(new ErrorEventArgs("Link address is empty", null));
                    return null;
                }

                string detailPageUrl = href;

                SetReferer(searchUrl);

                
                var url = href;
                using var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate |
                        DecompressionMethods.Brotli,
                    AllowAutoRedirect = true,
                    UseCookies = true
                };

                using var client = new HttpClient(handler);

                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                    "(KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");

                client.DefaultRequestHeaders.Accept.ParseAdd(
                    "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");

                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
                client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

                var response = await client.GetAsync(url);
                var html1 = await response.Content.ReadAsStringAsync();
                var m4aurl = $"https://cdn1.m3u8.liyuncss.guoguo.org.cn/links/hiresmp3/news/{ScriptTitleExtractor.ProcessTitle(html1)}.m4a";
                string downloadUrl = m4aurl;
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    OnErrorOccurred(new ErrorEventArgs("Download link address is empty", null));
                    return null;
                }
                if (!downloadUrl.StartsWith("http"))
                {
                    downloadUrl = downloadUrl.StartsWith("/") ?
                        $"https://www.gequbao.com{downloadUrl}" :
                        $"{detailPageUrl.TrimEnd('/')}/{downloadUrl.TrimStart('/')}";
                }
                return downloadUrl;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs($"Failed to get download link: {ex.Message}", ex));
                return null;
            }
        }

        public async Task DownloadMusicAsync(string url, string savePath, IProgress<int> progress = null)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/152.0.0.0 Safari/537.36 Edg/152.0.0.0");
                    request.Headers.Add("Accept", "*/*");
                    request.Headers.Add("Accept-Encoding", "gzip, deflate, br, zstd");
                    request.Headers.Add("Accept-Language", "en-US,en;q=0.9,zh-CN;q=0.8,zh;q=0.7");
                    request.Headers.Add("Connection", "keep-alive");
                    request.Headers.Add("Referer", "https://www.gequcn.com/");
                    request.Headers.Add("Cache-Control", "no-cache");
                    request.Headers.Add("Pragma", "no-cache");
                    request.Headers.Add("DNT", "1");
                    request.Headers.Add("Sec-Ch-Ua", "\"Chromium\";v=\"152\", \"Not?A_Brand\";v=\"24\", \"Microsoft Edge\";v=\"152\"");
                    request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
                    request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
                    request.Headers.Add("Sec-Fetch-Dest", "document");
                    request.Headers.Add("Sec-Fetch-Mode", "navigate");
                    request.Headers.Add("Sec-Fetch-Site", "same-origin");
                    request.Headers.Add("Sec-Fetch-User", "?1");
                    request.Headers.Add("Upgrade-Insecure-Requests", "1");

                    var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    var canReportProgress = totalBytes != -1 && progress != null;

                    using (var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fs.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;

                            if (canReportProgress)
                            {
                                int percent = (int)((totalRead * 100) / totalBytes);
                                progress.Report(percent);
                                DownloadProgress?.Invoke(this, new DownloadProgressEventArgs(percent, totalRead, totalBytes));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs($"Failed to download music: {ex.Message}", ex));
                throw;
            }
        }

        public void PauseMusic()
        {
            try
            {
                if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                {
                    _waveOut.Pause();
                    _isPaused = true;
                    OnMusicPaused(new MusicEventArgs(_currentMusicPath, _currentSongName));
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs($"Failed to pause music: {ex.Message}", ex));
            }
        }

        public void ResumeMusic()
        {
            try
            {
                if (_waveOut != null && _isPaused)
                {
                    _waveOut.Play();
                    _isPaused = false;
                    OnMusicResumed(new MusicEventArgs(_currentMusicPath, _currentSongName));
                }
                else if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Stopped)
                {
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(new ErrorEventArgs($"Failed to resume music: {ex.Message}", ex));
            }
        }

        public void StopMusic()
        {
            _isStopping = true;
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _audioFileReader?.Dispose();
            _waveOut = null;
            _audioFileReader = null;
            _isPaused = false;
            _isStopping = false;
        }

        public string GetPlaybackState()
        {
            if (_waveOut == null)
                return "Stopped";
            return _waveOut.PlaybackState switch
            {
                PlaybackState.Playing => _isPaused ? "Paused" : "Playing",
                PlaybackState.Paused => "Paused",
                PlaybackState.Stopped => "Stopped",
                _ => "Unknown",
            };
        }

        public void SetDownloadPath(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            _downloadPath = path;
        }

        public void SwitchToOnlineMode()
        {
            _mode = "online";
        }

        public void SwitchToLocalMode()
        {
            _mode = "local";
        }

        public string GetCurrentMode() => _mode;

        public MusicInfo GetCurrentMusicInfo()
        {
            return new MusicInfo
            {
                Path = _currentMusicPath,
                Name = _currentSongName,
                State = GetPlaybackState(),
                Mode = _mode
            };
        }

        protected virtual void OnMusicStarted(MusicEventArgs e)
        {
            MusicStarted?.Invoke(this, e);
        }

        protected virtual void OnMusicStopped(MusicEventArgs e)
        {
            MusicStopped?.Invoke(this, e);
        }

        protected virtual void OnMusicPaused(MusicEventArgs e)
        {
            MusicPaused?.Invoke(this, e);
        }

        protected virtual void OnMusicResumed(MusicEventArgs e)
        {
            MusicResumed?.Invoke(this, e);
        }

        protected virtual void OnErrorOccurred(ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (!_isPaused && !_isStopping)
            {
                OnMusicStopped(new MusicEventArgs(_currentMusicPath, _currentSongName));
            }
        }

        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        public void Dispose()
        {
            StopMusic();
            _httpClient?.Dispose();
            _waveOut?.Dispose();
            _audioFileReader?.Dispose();
        }
    }

    public class MusicEventArgs : EventArgs
    {
        public string MusicPath { get; }
        public string MusicName { get; }
        public DateTime Timestamp { get; }

        public MusicEventArgs(string path, string name)
        {
            MusicPath = path;
            MusicName = name;
            Timestamp = DateTime.Now;
        }
    }

    public class DownloadProgressEventArgs : EventArgs
    {
        public int Percent { get; }
        public long DownloadedBytes { get; }
        public long TotalBytes { get; }

        public DownloadProgressEventArgs(int percent, long downloaded, long total)
        {
            Percent = percent;
            DownloadedBytes = downloaded;
            TotalBytes = total;
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        public string Message { get; }
        public Exception Exception { get; }

        public ErrorEventArgs(string message, Exception exception)
        {
            Message = message;
            Exception = exception;
        }
    }

    public class MusicInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public string Mode { get; set; }
    }
}
