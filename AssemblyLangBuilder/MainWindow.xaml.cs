using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace AssemblyLangBuilder
{
    public partial class MainWindow : Window
    {
        private string FileName;
        private string SrcPath;
        private readonly string DragDropMsg = "Choose a source code .asm\r\nand drag it here or click";
        private readonly string DestPath = "masm64";

        public MainWindow()
        {
            App.Current.DispatcherUnhandledException += (sender, e) => System.Windows.Forms.MessageBox.Show(e.Exception.ToString());
            if (AppDomain.CurrentDomain.BaseDirectory.Length > 3)
            {
                System.Windows.Forms.MessageBox.Show("You must run the program from a path that does not contain attachments. For example: \"D:\\\", \"E:\\\". Drive C:\\ may require administrator rights.");
                App.Current.Shutdown();
            }
            DestPath = AppDomain.CurrentDomain.BaseDirectory + DestPath;

            InitializeComponent();
            Config.Load();
            
            if(!String.IsNullOrEmpty(Config.properties.OutputPath))
                OutputPath.Text = Config.properties.OutputPath;

            if (IsRunningAsAdministrator())
                adminTitle.Visibility = Visibility.Visible;


            if (!String.IsNullOrEmpty(Config.properties.LastSrcPath) && System.IO.File.Exists(Config.properties.LastSrcPath))
            {
                SrcPath = Config.properties.LastSrcPath;
                FileName = SelectedText.Text = SrcPath.Split('\\').Last();
            }

            if (!Directory.Exists(DestPath))
            {
                try
                {
                    ExtractSource();
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show("ERROR: " + e.ToString());
                }
            }
            else
                expandPropBttn.IsChecked = true;


            convertEncoding.IsChecked     = Config.properties.ConvertToCp;
            openProgramAfter.IsChecked    = Config.properties.OpenProgramAfterBuild;
            dontGenObj.IsChecked          = Config.properties.DontGenObj;
            saveLog.IsChecked             = Config.properties.SaveLog;
        }

        private static bool IsRunningAsAdministrator()
        {
            // Get current Windows user
            WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();

            // Get current Windows user principal
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);

            // Return TRUE if user is in role "Administrator"
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private async void ExtractSource()
        {
            splash.Visibility = Visibility.Visible;
            await Task.Run(() =>
            {
                using (var stream = new MemoryStream(Properties.Resources.MASM64))
                {
                    using (var archive = new ZipArchive(stream))
                    {
                        if (!Directory.Exists(DestPath))
                            Directory.CreateDirectory(DestPath);

                        archive.ExtractToDirectory(DestPath);
                    }
                }
                
            });
            splash.Visibility = Visibility.Collapsed;
            expandPropBttn.IsChecked = true;
        }

        private void close_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Border_Drop(object sender, DragEventArgs e)
        {
            var file = ((string[])e.Data.GetData(DataFormats.FileDrop)).FirstOrDefault();

            if (!file.EndsWith(".asm"))
            {
                System.Windows.Forms.MessageBox.Show("Select .asm file!");
                dragText.Text = DragDropMsg;
                return;
            }
            

            FileName = SelectedText.Text = file.Split('\\').Last();
            SrcPath  = Config.properties.LastSrcPath = file;
            Config.Save();
            dragText.Text = DragDropMsg;
        }

        private long UnixTimeNow()
        {
            var timeSpan = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            return (long)timeSpan.TotalSeconds;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(String.IsNullOrEmpty(SrcPath))
            {
                System.Windows.Forms.MessageBox.Show("Source file is not selected!");
                return;
            }
            else if (!System.IO.File.Exists(SrcPath))
            {
                System.Windows.Forms.MessageBox.Show($"File {FileName} is not found!");
                SelectedText.Text = Config.properties.LastSrcPath = SrcPath = null;
                Config.Save();
                return;
            }
            else if(!String.IsNullOrEmpty(OutputPath.Text) && OutputPath.Text != "Click for select . . ." && !Directory.Exists(OutputPath.Text))
            {
                Config.properties.OutputPath = null;
                OutputPath.Text = "Click for select . . .";
                Config.Save();
                System.Windows.Forms.MessageBox.Show("Output dir is not valid!");   
                return;

            }

            var outputPath = (String.IsNullOrWhiteSpace(OutputPath.Text) || OutputPath.Text == "Click for select . . .") ? Directory.GetCurrentDirectory() : OutputPath.Text;
            var outputFileExe = $"{DestPath}\\bin64\\" + FileName.Replace(".asm", ".exe");
            var outputFileObj = outputFileExe.Replace(".exe", ".obj");
            var objCommand = dontGenObj.IsChecked == true ? $"DEL /F /Q \"{outputFileObj}\"" : $"move /Y \"{outputFileObj}\" \"{outputPath}\"";

            if (convertEncoding.IsChecked == true)
            {
                var output = System.IO.File.ReadAllLines(SrcPath);
                if(System.IO.File.Exists($"{DestPath}\\bin64\\{FileName}"))
                {
                    System.IO.File.Delete($"{DestPath}\\bin64\\{FileName}");
                }
                System.IO.File.WriteAllLines($"{DestPath}\\bin64\\{FileName}", output, Encoding.GetEncoding(866));
            }
            else
            {
                System.IO.File.Copy(SrcPath, $"{DestPath}\\bin64\\" + FileName, true);
            }

            if ((saveLog.IsChecked == true) && !System.IO.Directory.Exists(".\\Logs"))
            {
                System.IO.Directory.CreateDirectory(".\\Logs");
            }

            var logFile = $".\\Logs\\[{UnixTimeNow()}] {FileName.Replace(".asm", "")}.txt";
            using (var process = new Process())
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = "cmd";
                process.StartInfo.Arguments =  $"/c cd /d \"{DestPath}\\bin64\" && ml64.exe /c \"{FileName}\" && link.exe /SUBSYSTEM:CONSOLE /ENTRY:main \"{FileName.Replace(".asm",".obj")}\" &&" +
                    $" move /Y \"{outputFileExe}\" \"{outputPath}\" && {objCommand}"; ;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                var result = process.StandardOutput.ReadToEnd();

                if(saveLog.IsChecked == true)
                     File.WriteAllText(logFile, result);
                process.WaitForExit();   
                if(result.Contains("error"))
                {
                    System.Windows.Forms.MessageBox.Show(result,"Compile error");
                }
            }

            if(openProgramAfter.IsChecked == true)
            {
                using (var process = new Process())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = false;
                    process.StartInfo.FileName = "cmd";
                    process.StartInfo.Arguments = $"/c cd /d \"{outputPath}\" && \"{FileName.Replace(".asm",".exe")}\" && pause";
                    process.Start();
                }
            }

            System.IO.File.Delete($"{DestPath}\\bin64\\" + FileName);
           
        }

        private void SetOutputPath_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && !String.IsNullOrEmpty(dialog.SelectedPath))
                {
                    OutputPath.Text = dialog.SelectedPath;
                    Config.properties.OutputPath = dialog.SelectedPath;
                    Config.Save();
                }
            }

        }

        private void convertEncoding_Click(object sender, RoutedEventArgs e)
        {
            Config.properties.ConvertToCp = convertEncoding.IsChecked == true;
            Config.Save();
        }

        private void dontGenObj_Click(object sender, RoutedEventArgs e)
        {
            Config.properties.DontGenObj = dontGenObj.IsChecked == true;
            Config.Save();
        }

        private void openProgramAfter_Click(object sender, RoutedEventArgs e)
        {
            Config.properties.OpenProgramAfterBuild = openProgramAfter.IsChecked == true;
            Config.Save();
        }

        private void OpenEditor_Click(object sender, RoutedEventArgs e)
        {
            if(SelectedText.Text == "File not selected")
            {
                return;
            }
            else if (String.IsNullOrEmpty(SrcPath) || !System.IO.File.Exists(SrcPath))
            {
                System.Windows.Forms.MessageBox.Show($"File {FileName} is not found!");
                Config.properties.LastSrcPath = SrcPath = null;
                SelectedText.Text = "File not selected";
                Config.Save();
                return;
            }
            Process.Start(SrcPath);

        }

        private void Border_DragEnter(object sender, DragEventArgs e)
        {
            dragText.Text = "Release the left\nmouse button";
        }

        private void Border_DragLeave(object sender, DragEventArgs e)
        {
            dragText.Text = DragDropMsg;
        }

        private void saveLog_Click(object sender, RoutedEventArgs e)
        {
            Config.properties.SaveLog = saveLog.IsChecked == true;
            Config.Save();
        }

        private void GenerateClick(object sender, RoutedEventArgs e)
        {
            var outputPath = (String.IsNullOrWhiteSpace(OutputPath.Text) || OutputPath.Text == "Click for select . . ." || !Directory.Exists(OutputPath.Text)) ? Directory.GetCurrentDirectory() : OutputPath.Text;

            if (File.Exists(outputPath + "\\sample.asm"))
                File.Delete(outputPath + "\\sample.asm");

            File.WriteAllText(outputPath + "\\sample.asm", Properties.Resources.SampleAsm);
            Process.Start(outputPath + "\\sample.asm").Dispose();
        }

        private void AttachFile(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog() { Filter = "Assembly source code (.asm)|*.asm" };
            if(dialog.ShowDialog() == true)
            {
                FileName = SelectedText.Text = dialog.FileName.Split('\\').Last();
                SrcPath = Config.properties.LastSrcPath = dialog.FileName;
                Config.Save();
            }
            e.Handled = true;
        }

        private void Path_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Process.Start("https://github.com/Explynex/AssemblyLangBuilder").Dispose();
            e.Handled = true;
        }
    }
}
