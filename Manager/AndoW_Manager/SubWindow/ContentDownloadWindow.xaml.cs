using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace AndoW_Manager
{
    public partial class ContentDownloadWindow : Window
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _allowClose;

        public ContentDownloadWindow()
        {
            InitializeComponent();
            Closing += ContentDownloadWindow_Closing;
        }

        public CancellationToken CancellationToken => _cts.Token;

        public bool IsCancellationRequested => _cts.IsCancellationRequested;

        public void UpdateProgress(int completed, int total, string fileName)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateProgress(completed, total, fileName));
                return;
            }

            double percent = total > 0 ? (double)completed / total * 100d : 0d;
            percent = Math.Max(0d, Math.Min(100d, percent));

            ProgressBar.Value = percent;
            ProgressText.Text = total > 0 ? $"{completed} / {total} ({percent:0}%)" : "0 / 0";

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                FileText.Text = $"Downloading: {fileName}";
            }
        }

        public void SetTitle(string title)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetTitle(title));
                return;
            }

            if (!string.IsNullOrWhiteSpace(title))
            {
                TitleText.Text = title;
            }
        }

        public void RequestClose()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(RequestClose);
                return;
            }

            _allowClose = true;
            Close();
        }

        private void ContentDownloadWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            _cts.Cancel();
            _allowClose = true;
        }
    }
}
