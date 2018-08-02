using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Youtube_NowPlaying
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("User32", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr windowHandle, StringBuilder stringBuilder, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "GetWindowTextLength", SetLastError = true)]
        internal static extern int GetWindowTextLength(IntPtr hwnd);

        // Stuff for looking for chrome window titles
        private static List<IntPtr> windowList;
        private static string _className;
        private static StringBuilder apiResult = new StringBuilder(256); //256 Is max class name length.
        private delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        // Settings and logic stuff
        private static IntPtr _youtubeWindowID;
        private string nowPlayingFilePath = $@"C:\Users\{System.Environment.UserName}\Documents\nowplaying.txt";
        private bool CancelWindowWatchLoop = false;
        private int CutStringCharacterCount;

        public MainWindow()
        {
            InitializeComponent();

            // Set the default file location
            txtSaveLocation.Text = nowPlayingFilePath;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            // Get each chrome window process and get their window titles, save the Youtube tab's ID for later
            List<IntPtr> ChromeWindows = ProcessFinder("Chrome_WidgetWin_1", "chrome");

            foreach (IntPtr windowHandle in ChromeWindows)
            {
                int length = GetWindowTextLength(windowHandle);
                StringBuilder windowTitle = new StringBuilder(length + 1);
                GetWindowText(windowHandle, windowTitle, windowTitle.Capacity);
                if (windowTitle.ToString().ToUpper().Contains($"{txtStringToSearch.Text.ToUpper()} - GOOGLE CHROME"))
                {
                    _youtubeWindowID = windowHandle;
                    break;
                }
            }

            // Get the amount of characters to cut from the end of the window title
            CutStringCharacterCount = txtStringToSearch.Text.Length + 16;

            txtStringToSearch.IsEnabled = false;
            btnStart.IsEnabled = false;

            CancelWindowWatchLoop = false;

            WindowWatchLoop();
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            txtStringToSearch.IsEnabled = true;
            btnStart.IsEnabled = true;

            CancelWindowWatchLoop = true;
        }

        private void btnBrowseSaveLocation_Click(object sender, RoutedEventArgs e)
        {
            CommonSaveFileDialog dialog = new CommonSaveFileDialog();
            dialog.InitialDirectory = $@"C:\Users\{System.Environment.UserName}\Documents";
            dialog.DefaultFileName = "nowplaying.txt";
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                nowPlayingFilePath = dialog.FileName;
                txtSaveLocation.Text = nowPlayingFilePath;
            }
        }

        private void WindowWatchLoop()
        {
            // UI context for setting the txtNowPlaying field
            var uiContext = TaskScheduler.FromCurrentSynchronizationContext();

            // Vars for doing the janky check later
            string windowTitleCleaned;
            string windowTitleCleaned_old = "";

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    // Break check, if we press Stop
                    if (CancelWindowWatchLoop)
                    {
                        break;
                    }

                    // Logic for getting Chrome window title from the process selected earlier
                    int length = GetWindowTextLength(_youtubeWindowID);
                    StringBuilder windowTitle = new StringBuilder(length + 1);
                    GetWindowText(_youtubeWindowID, windowTitle, windowTitle.Capacity);

                    // If the window title happens to be shorter than the text cut char count, then we try again next iteration to avoid a crash
                    if (windowTitle.ToString().Length - CutStringCharacterCount < 0)
                    {
                        continue;
                    }

                    // Cut the excess off the end of the window title
                    windowTitleCleaned = windowTitle.ToString().Substring(0, windowTitle.ToString().Length - CutStringCharacterCount);

                    // this is janky as fuck - check if the variable's changed. If it has...
                    //    set the Now-Playing field, save the title to a text file, and set the _old var to the new var for use in this check again
                    if (!windowTitleCleaned.Equals(windowTitleCleaned_old))
                    {
                        Task.Factory.StartNew(() =>
                        {
                            txtNowPlaying.Text = windowTitleCleaned;
                            System.IO.File.WriteAllText($@"{nowPlayingFilePath}", windowTitleCleaned);
                            windowTitleCleaned_old = windowTitleCleaned;
                        }, CancellationToken.None, TaskCreationOptions.None, uiContext);
                    }

                    Thread.Sleep(2000);
                }
            });
        }

        // Get the full list of Chrome processes and get each thread's id
        private static List<IntPtr> ProcessFinder(string className, string process)
        {
            _className = className;
            windowList = new List<IntPtr>();

            Process[] chromeList = Process.GetProcessesByName(process);

            if (chromeList.Length > 0)
            {
                foreach (Process chrome in chromeList)
                {
                    if (chrome.MainWindowHandle != IntPtr.Zero)
                    {
                        foreach (ProcessThread thread in chrome.Threads)
                        {
                            EnumThreadWindows((uint)thread.Id, new EnumThreadDelegate(EnumThreadCallback), IntPtr.Zero);
                        }
                    }
                }
            }
            return windowList;
        }

        static bool EnumThreadCallback(IntPtr hWnd, IntPtr lParam)
        {
            if (GetClassName(hWnd, apiResult, apiResult.Capacity) != 0)
            {
                if (string.CompareOrdinal(apiResult.ToString(), _className) == 0)
                {
                    windowList.Add(hWnd);
                }
            }
            return true;
        }
    }
}
