using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace EZBlocker
{
    class SpotifyHook
    {
        public Process Spotify { get; private set; }
        private HashSet<int> Children;
        public AudioUtils.VolumeControl VolumeControl { get; private set; }
        public string WindowName { get; private set; }
        public IntPtr Handle { get; private set; }

        private readonly Timer RefreshTimer;
        private float peak = 0f;
        private float lastPeak = 0f;

        public SpotifyHook()
        {
            RefreshTimer = new Timer((e) =>
            {
                if (IsRunning())
                {
                    WindowName = Spotify.MainWindowTitle;
                    Handle = Spotify.MainWindowHandle;
                    if (VolumeControl == null)
                    {
                        VolumeControl = AudioUtils.GetVolumeControl(Children);
                    }
                    else
                    {
                        lastPeak = peak;
                        peak = AudioUtils.GetPeakVolume(VolumeControl.Control);
                    }
                }
                else
                {
                    ClearHooks();
                    HookSpotify();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        }

        public bool IsPlaying()
        {
            return peak > 0 && lastPeak > 0;
        }

        public bool SearchForOtherWindow()
        {
            var result = Process.GetProcessesByName("spotify");
            foreach (Process p in result)
            {
                if (p.MainWindowTitle != WindowName){  //If the window isn't the main window, and this process have an audio output we mute it
                    var otherVolumeControl = AudioUtils.GetVolumeControl(new HashSet<int> { p.Id });
                    if (otherVolumeControl != null)
                    {
                        AudioUtils.SetMute(otherVolumeControl.Control, true);
                        var mutedProcess = AudioUtils.IsMuted(otherVolumeControl.Control) != null ? (bool)AudioUtils.IsMuted(otherVolumeControl.Control) : false;
                    }
                }
             
            }
            return true;
        }

        public bool IsAdPlaying()
        {
            if ((WindowName.Equals("Advertisement") || !WindowName.Contains(" - ")) && !WindowName.Equals("") && !WindowName.Equals("Drag") && IsPlaying())
            {
                Debug.WriteLine("Ad: " + lastPeak.ToString() + " " + peak.ToString());
                SearchForOtherWindow(); //Quick fix to detect and mute all other processes
                return true;
            }
            return false;
        }

        public bool IsRunning()
        {
            if (Spotify == null)
                return false;

            Spotify.Refresh();
            return !Spotify.HasExited;
        }

        public string GetArtist()
        {
            if (IsPlaying())
            {
                if (WindowName.Contains(" - "))
                    return WindowName.Split(new[] { " - " }, StringSplitOptions.None)[0];
                else
                    return WindowName;
            }

            return "";
        }

        private void ClearHooks()
        {
            Spotify = null;
            WindowName = "";
            Handle = IntPtr.Zero;
            if (VolumeControl != null) Marshal.ReleaseComObject(VolumeControl.Control);
            VolumeControl = null;
        }

        private bool HookSpotify()
        {
            Children = new HashSet<int>();

            // Try hooking through window title
            foreach (Process p in Process.GetProcessesByName("spotify"))
            {
                Children.Add(p.Id);
                Spotify = p;
                if (p.MainWindowTitle.Length > 1)
                {
                    return true;
                }
            }

            // Try hooking through audio device
            VolumeControl = AudioUtils.GetVolumeControl(Children);
            if (VolumeControl != null)
            {
                Spotify = Process.GetProcessById(VolumeControl.ProcessId);
                return true;
            }

            return false;
        }

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
    }
}
