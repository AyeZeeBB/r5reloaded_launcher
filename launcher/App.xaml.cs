using launcher.Core;
using launcher.Services;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows;

namespace launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string MutexName = "R5RLauncherMutex";
        private const string PipeName = "R5RLauncherPipe";

        private Mutex _mutex;
        private CancellationTokenSource _cancellationTokenSource;

        // Import necessary Windows API functions to bring window to foreground
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        public ResourceDictionary ThemeDictionary
        {
            get { return Resources.MergedDictionaries[0]; }
        }

        public void ChangeTheme(Uri uri)
        {
            ThemeDictionary.MergedDictionaries.Clear();
            ThemeDictionary.MergedDictionaries.Add(new ResourceDictionary() { Source = uri });
            ThemeDictionary.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri("pack://application:,,,/Styles/styles.xaml") });
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool isNewInstance;
            _mutex = new Mutex(true, MutexName, out isNewInstance);

            if (isNewInstance)
            {
                // This is the first instance
                // Start listening for incoming pipe connections
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => ListenForPipeMessages(_cancellationTokenSource.Token));

                this.Exit += OnApplicationExit;
            }
            else
            {
                // Another instance is already running
                if (e.Args.Length > 0)
                {
                    SendURLProtocolMessage(e.Args);
                } else
                {
                    // Send a message to the existing instance to show the MainWindow
                    SendShowWindowMessage();
                }

                // Shutdown the new instance
                Shutdown();
            }
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            // Cancel the listening task
            _cancellationTokenSource?.Cancel();

            // Release and dispose the mutex
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }

        /// <summary>
        /// Listens for incoming messages via named pipe and handles them.
        /// </summary>
        /// <param name="token">Cancellation token to stop listening.</param>
        private async Task ListenForPipeMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
                    {
                        // Wait for a client to connect
                        await server.WaitForConnectionAsync(token);

                        if (server.IsConnected)
                        {
                            using (var reader = new StreamReader(server))
                            {
                                string message = await reader.ReadLineAsync();
                                if (message == "SHOW_WINDOW")
                                {
                                    // Show the MainWindow on the UI thread
                                    Dispatcher.Invoke(() =>
                                    {
                                        if (this.MainWindow != null)
                                        {
                                            if (this.MainWindow.WindowState == WindowState.Minimized)
                                            {
                                                this.MainWindow.WindowState = WindowState.Normal;
                                            }

                                            this.MainWindow.Show();
                                            this.MainWindow.Activate();
                                            (this.MainWindow as MainWindow)?.OnOpen();

                                            // Bring the window to foreground using Windows API
                                            var hwnd = new System.Windows.Interop.WindowInteropHelper(this.MainWindow).Handle;
                                            ShowWindow(hwnd, SW_RESTORE);
                                            SetForegroundWindow(hwnd);
                                        }
                                    });
                                } else
                                {
                                    Dispatcher.Invoke(async () =>
                                    {
                                        if (this.MainWindow != null)
                                        {
                                            if (this.MainWindow.WindowState == WindowState.Minimized)
                                            {
                                                this.MainWindow.WindowState = WindowState.Normal;
                                            }

                                            this.MainWindow.Show();
                                            this.MainWindow.Activate();
                                            (this.MainWindow as MainWindow)?.OnOpen();

                                            // Bring the window to foreground using Windows API
                                            var hwnd = new System.Windows.Interop.WindowInteropHelper(this.MainWindow).Handle;
                                            ShowWindow(hwnd, SW_RESTORE);
                                            SetForegroundWindow(hwnd);
                                        }

                                        AppController.ShowModManagerControl();

                                        // TODO prevent from calling if another mod is already downloading
                                        LibraryModItem loadingModItem = new LibraryModItem();
                                        ModsService.AddLoadingModItem(loadingModItem);
                                        await ModsService.DownloadMod(message, new Progress<float>(p => loadingModItem.UpdateDownloadProgress(p)));
                                    });
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Listening was canceled
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Pipe listening error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Sends a message to the existing instance to show the MainWindow.
        /// </summary>
        private void SendShowWindowMessage()
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    // Attempt to connect to the server with a timeout
                    client.Connect(2000); // 2 seconds timeout

                    using (var writer = new StreamWriter(client))
                    {
                        writer.AutoFlush = true;
                        writer.WriteLine("SHOW_WINDOW");
                        Debug.WriteLine("Sent SHOW_WINDOW message to existing instance.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pipe client error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a message to the existing instance with command line args (called from browser URL protocol)
        /// </summary>
        private void SendURLProtocolMessage(string[] args)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    // Attempt to connect to the server with a timeout
                    client.Connect(2000); // 2 seconds timeout

                    using (var writer = new StreamWriter(client))
                    {
                        writer.AutoFlush = true;
                        // Modern browsers turn r5rlauncher://https:// into r5rlauncher:https// so our URLs don't include it and we have to add it back
                        string cleanedMessage = args[0].Replace("r5rlauncher://", "https://");
                        writer.WriteLine(cleanedMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pipe client error: {ex.Message}");
            }
        }
    }
}