using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Warp.Tools;

namespace Warp
{
    public class FileDiscoverer
    {
        List<Tuple<string, Stopwatch, long>> Incubator = new List<Tuple<string, Stopwatch, long>>();
        string FolderPath = "";
        string FileExtension = "*.*";
        BackgroundWorker DiscoveryThread;
        bool ShouldAbort = false;

        public FileDiscoverer()
        {
        }

        public void ChangePath(string newPath, string newExtension)
        {
            lock (Incubator)
            {
                if (DiscoveryThread != null && DiscoveryThread.IsBusy)
                {
                    DiscoveryThread.RunWorkerCompleted += (sender, args) =>
                    {
                        ShouldAbort = false;

                        FolderPath = newPath;
                        FileExtension = newExtension;

                        if (FolderPath == "" || !Directory.Exists(FolderPath) || !IOHelper.CheckFolderPermission(FolderPath))
                            return;

                        Incubator.Clear();
                        MainWindow.Options.Movies.Clear();
                        MainWindow.Options.DisplayedMovie = null;

                        DiscoveryThread = new BackgroundWorker();
                        DiscoveryThread.DoWork += WorkLoop;
                        DiscoveryThread.RunWorkerAsync();
                    };

                    ShouldAbort = true;
                }
                else
                {
                    FolderPath = newPath;
                    FileExtension = newExtension;

                    if (FolderPath == "" || !Directory.Exists(FolderPath) || !IOHelper.CheckFolderPermission(FolderPath))
                        return;

                    Incubator.Clear();
                    MainWindow.Options.Movies.Clear();
                    MainWindow.Options.DisplayedMovie = null;

                    DiscoveryThread = new BackgroundWorker();
                    DiscoveryThread.DoWork += WorkLoop;
                    DiscoveryThread.RunWorkerAsync();
                }
            }
        }

        void WorkLoop(object sender, EventArgs e)
        {
            while (true)
            {
                foreach (var fileName in Directory.EnumerateFiles(FolderPath, FileExtension, SearchOption.TopDirectoryOnly))
                {
                    if (ShouldAbort)
                        return;
                    
                    if (GetMovie(fileName) != null)
                        continue;

                    FileInfo Info = new FileInfo(fileName);
                    Tuple<string, Stopwatch, long> CurrentState = GetIncubating(fileName);
                    if (CurrentState == null)
                    {
                        Stopwatch Timer = new Stopwatch();
                        Timer.Start();
                        Incubator.Add(new Tuple<string, Stopwatch, long>(fileName, Timer, Info.Length));
                    }
                    else
                    {
                        if (Info.Length != CurrentState.Item3)
                        {
                            Incubator.Remove(CurrentState);
                            Stopwatch Timer = new Stopwatch();
                            Timer.Start();
                            Incubator.Add(new Tuple<string, Stopwatch, long>(fileName, Timer, Info.Length));
                        }
                        else if (CurrentState.Item2.ElapsedMilliseconds > 1000)
                        {
                            Incubator.Remove(CurrentState);
                            MainWindow.Options.Movies.Add(new Movie(fileName));
                        }
                    }
                }

                Thread.Sleep(500);
            }
        }

        public void Shutdown()
        {
            lock (Incubator)
            {
                ShouldAbort = true;
            }
        }

        Tuple<string, Stopwatch, long> GetIncubating(string path)
        {
            return Incubator.FirstOrDefault(tuple => tuple.Item1 == path);
        }

        static Movie GetMovie(string path)
        {
            return MainWindow.Options.Movies.FirstOrDefault(movie => movie.Path == path);
        }
    }
}
