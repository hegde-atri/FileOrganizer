using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using FileOrganizer.Views;
using Microsoft.CodeAnalysis.Text;
using ReactiveUI;
using Splat.ApplicationPerformanceMonitoring;

namespace FileOrganizer.ViewModels
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class MainWindowViewModel : ViewModelBase
    {
        public string sourcePath = null!;
        public string destPath = null!;
        public string error = null!;
        public int sort;
        public string fileType = null!;
        public float progress;
        public string output = null!;
        public string startTime = null!;
        public string timeElapsed = null!;
        public int scanSubFolders;
        public int destSubFolders;
        

        public string SourcePath
        {
            get => sourcePath;
            set => this.RaiseAndSetIfChanged(ref sourcePath, value);
        }
        public string DestPath
        {
            get => destPath;
            set => this.RaiseAndSetIfChanged(ref destPath, value);
        }

        public string Error
        {
            get => error;
            set => this.RaiseAndSetIfChanged(ref error, value);
        }
        public int Sort
        {
            get => sort;
            set => this.RaiseAndSetIfChanged(ref sort, value);
        }

        public string FileType
        {
            get => fileType;
            set => this.RaiseAndSetIfChanged(ref fileType, value);
        }

        public float Progress
        {
            get => progress;
            set => this.RaiseAndSetIfChanged(ref progress, value);
        }

        public string StartTime
        {
            get => startTime;
            set => this.RaiseAndSetIfChanged(ref startTime, value);
        }
        
        public string TimeElapsed
        {
            get => timeElapsed;
            set => this.RaiseAndSetIfChanged(ref timeElapsed, value);
        }
        
        public string Output
        {
            get => output;
            set => this.RaiseAndSetIfChanged(ref output, value);
        }
        public int ScanSubFolders
        {
            get => scanSubFolders;
            set => this.RaiseAndSetIfChanged(ref scanSubFolders, value);
        }
        public int DestSubFolders
        {
            get => destSubFolders;
            set => this.RaiseAndSetIfChanged(ref destSubFolders, value);
        }
        
        public ICommand Organize { get; private set; }
        public ICommand SelectSourceFolder { get; private set; }
        public ICommand SelectDestinationFolder { get; private set; }

        public MainWindowViewModel()
        {
            SelectSourceFolder = ReactiveCommand.Create(async() =>
            {
                var x = new OpenFolderDialog();
                SourcePath = (await x.ShowAsync(new MainWindow()))!;
            });
            SelectDestinationFolder = ReactiveCommand.Create(async() =>
            {
                var x = new OpenFolderDialog();
                DestPath = (await x.ShowAsync(new MainWindow()))!;
            });

            Organize = ReactiveCommand.Create(() =>
            {
                if (OptionsValid())
                {
                    Error = "";
                    string[] sortType = {"None", "By Month", "By Year"};
                    Output = "Job started \n ----------------\n";
                    Output += $"Source path: {SourcePath}\nTarget path: {DestPath}\n" +
                              $"File type: {FileType}\nSort type: {sortType[Sort]}\n";
                    MoveFiles();
                }
                
            });


        }

        bool OptionsValid()
        {
            if (SourcePath is string && DestPath is string)
            {
                return true;
            }
            else
            {
                Error = "Please specify valid paths";
                return false;
            }

        }

        private bool IsFileTypeEmpty()
        {
            if (FileType is string || FileType == "") return false;
            return true;
        }

        private void MoveFiles()
        {
            StartTime = DateTime.Now.ToLongTimeString();
            try
            {
                List<FileInfo> fileInfoList = new List<FileInfo>();
                bool[] options = {true, false};
                Output += $"Scan Subfolders: {options[scanSubFolders]}\n";
                GetFiles(SourcePath, fileInfoList, options[scanSubFolders]);
                Output += $"Total files: {fileInfoList.Count}\n";
                int totalFiles = fileInfoList.Count;
                int fileCount = 0;

                foreach (var fi in fileInfoList)
                {
                    Task<string> task = Task<string>.Factory.StartNew(() =>
                    {
                        try
                        {
                            Output += $"{fi.Name}: {fi.CreationTime}, {fi.Length}";
                            MoveFile(fi);
                            fileCount++;
                            return "Success!";
                        }
                        catch (Exception e)
                        {
                            Error = e.ToString();
                            return "Error" + e.ToString();
                        }
                    });
                    Task updateUI = task.ContinueWith((ret) =>
                    {
                        Progress = (fileCount / totalFiles) * 100;
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }

            }
            catch (Exception e)
            {
                Error = e.ToString();
            }
            finally
            {
                // TODO: finished dialogue
            }
        }

        private void GetFiles(string path, List<FileInfo> list, bool scanSubFolders)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(path);
                FileInfo[] files;
                if (IsFileTypeEmpty())
                {
                    files = di.GetFiles();
                }
                else
                {
                    files = di.GetFiles("*" + FileType);
                }

                foreach (FileInfo file in files)
                {
                    list.Add(file);
                }

                if (scanSubFolders)
                {
                    DirectoryInfo[] dirs = di.GetDirectories();
                    if (!(dirs == null || dirs.Length < 1))
                    {
                        foreach (DirectoryInfo dir in dirs)
                        {
                            GetFiles(dir.FullName, list, scanSubFolders);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Error = e.ToString();
            }
        }

        private void MoveFile(FileInfo fi)
        {
            if (IsFileTypeEmpty())
            {
                try
                {
                    DateTime dt = fi.LastWriteTime;
                    CultureInfo ci = Thread.CurrentThread.CurrentCulture;
                    string month1 = ci.DateTimeFormat.GetMonthName(dt.Month);
                    string month2 = ci.DateTimeFormat.GetAbbreviatedMonthName(dt.Month);
                    string destinationPath = string.Empty, monthNumber = string.Empty;

                    if (dt.Month > 9) monthNumber = dt.Month.ToString();
                    else monthNumber = "0" + dt.Month.ToString();

                    if (Sort == 0)
                    {
                        if (DestSubFolders == 0)
                        {
                            destinationPath = destPath + "/" + fi.Extension.Substring(1);
                        }
                        else if (DestSubFolders == 1)
                        {
                            destinationPath = destPath;
                        }
                    }
                    else if (Sort == 1)
                    {
                        if (DestSubFolders == 0)
                        {
                            destinationPath = destPath + "/" + fi.Extension.Substring(1) + "/" + dt.Year.ToString()
                                              + "/" + monthNumber + "-" + month2;
                        }
                        else if (DestSubFolders == 1)
                        {
                            destinationPath = destPath + "/" + dt.Year.ToString()
                                              + "/" + monthNumber + "-" + month2;
                        }
                    }
                    else if (Sort == 2)
                    {
                        if (DestSubFolders == 0)
                        {
                            destinationPath = destPath + "/" + fi.Extension.Substring(1) + "/" + dt.Year.ToString();
                        }
                        else if (DestSubFolders == 1)
                        {
                            destinationPath = destPath + "/" + dt.Year.ToString();
                        }
                    }

                    if (!Directory.Exists(destinationPath))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }

                    fi.MoveTo(destinationPath + "/" + fi.Name);

                }
                catch (Exception e)
                {
                    FileStream fs = new FileStream(DestPath + "/" + "Exception.log", FileMode.Append, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(fs);
                    sw.AutoFlush = true;                
                    sw.WriteLine("Failed to copy : " + fi.FullName);
                    sw.WriteLine(e.Message);
                    sw.WriteLine(e.StackTrace);
                    sw.WriteLine(e.Source);
                    sw.WriteLine("Failed to copy : " + fi.FullName);
                    if (e.InnerException != null) sw.WriteLine(e.InnerException.Message);
                    sw.WriteLine("");
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                }
                
            }
            else
            {
                try
                {
                    DateTime dt = fi.LastWriteTime;
                    if (fi.Extension.ToLower() != FileType.ToLower()) return;
                    CultureInfo ci = Thread.CurrentThread.CurrentCulture;
                    string month1 = ci.DateTimeFormat.GetMonthName(dt.Month);
                    string month2 = ci.DateTimeFormat.GetAbbreviatedMonthName(dt.Month);
                    string destinationPath = string.Empty, monthNumber = string.Empty;

                    if (dt.Month > 9) monthNumber = dt.Month.ToString();
                    else monthNumber = "0" + dt.Month.ToString();

                    if (Sort == 0)
                    {
                        if (DestSubFolders == 0)
                        {
                            destinationPath = destPath + "/" + FileType.Substring(1);
                        }
                        else if (DestSubFolders == 1)
                        {
                            destinationPath = destPath;
                        }
                    }
                    else if (Sort == 1)
                    {
                        if (DestSubFolders == 0)
                        {
                            destinationPath = destPath + "/" + FileType.Substring(1) + "/" + dt.Year.ToString()
                                              + "/" + monthNumber + "-" + month2;
                        }
                        else if (DestSubFolders == 1)
                        {
                            destinationPath = destPath + "/" + dt.Year.ToString()
                                              + "/" + monthNumber + "-" + month2;
                        }
                    }
                    else if (Sort == 2)
                    {
                        if (DestSubFolders == 0)
                        {
                            destinationPath = destPath + "/" + FileType.Substring(1) + "/" + dt.Year.ToString();
                        }
                        else if (DestSubFolders == 1)
                        {
                            destinationPath = destPath + "/" + dt.Year.ToString();
                        }
                    }

                    if (!Directory.Exists(destinationPath))
                    {
                        Directory.CreateDirectory(destinationPath);
                    }

                    fi.MoveTo(destinationPath + "/" + fi.Name);

                }
                catch (Exception e)
                {
                    FileStream fs = new FileStream(DestPath + "/" + "Exception.log", FileMode.Append, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(fs);
                    sw.AutoFlush = true;                
                    sw.WriteLine("Failed to copy : " + fi.FullName);
                    sw.WriteLine(e.Message);
                    sw.WriteLine(e.StackTrace);
                    sw.WriteLine(e.Source);
                    sw.WriteLine("Failed to copy : " + fi.FullName);
                    if (e.InnerException != null) sw.WriteLine(e.InnerException.Message);
                    sw.WriteLine("");
                    sw.Flush();
                    sw.Close();
                    fs.Close();
                }
                
            }
        }

    }
}