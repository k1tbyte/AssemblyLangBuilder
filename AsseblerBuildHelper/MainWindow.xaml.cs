using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
using static System.Net.WebRequestMethods;

namespace AsseblerBuildHelper
{
    public partial class MainWindow : Window
    {
        private string FileName;
        private string SrcPath;
        public MainWindow()
        {
            InitializeComponent();
            Config.Load();
            if(!String.IsNullOrEmpty(Config.properties.MASMPath))
                MasmPath.Text = Config.properties.MASMPath;
            
            if(!String.IsNullOrEmpty(Config.properties.OutputPath))
                OutputPath.Text = Config.properties.OutputPath;
            
            if (!String.IsNullOrEmpty(Config.properties.LastSrcPath) && System.IO.File.Exists(Config.properties.LastSrcPath))
            {
                SrcPath = Config.properties.LastSrcPath;
                FileName = SelectedText.Text = SrcPath.Split('\\').Last();
            }
                


            convertEncoding.IsChecked = Config.properties.ConvertToCp;
            openLogAfter.IsChecked    = Config.properties.OpenAssemblyResults;
            dontGenObj.IsChecked      = Config.properties.DontGenObj;
            saveLog.IsChecked         = Config.properties.SaveLog;
            
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
                return;
            }
            

            FileName = SelectedText.Text = file.Split('\\').Last();
            SrcPath  = Config.properties.LastSrcPath = file;
            Config.Save();
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
            else if(String.IsNullOrEmpty(MasmPath.Text))
            {
                System.Windows.Forms.MessageBox.Show("MASM dir is not selected!");
                return;
            }
            else if (!System.IO.File.Exists(SrcPath))
            {
                System.Windows.Forms.MessageBox.Show($"File {FileName} is not found!");
                SelectedText.Text = Config.properties.LastSrcPath = SrcPath = null;
                Config.Save();
                return;
            }
            else if (!Directory.Exists(MasmPath.Text))
            {
                MasmPath.Text = Config.properties.MASMPath = null;
                Config.Save();
                System.Windows.Forms.MessageBox.Show("MASM dir is not found!");
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
            var outputFileExe = MasmPath.Text + "\\bin\\" + FileName.Replace(".asm", ".exe");
            var outputFileObj = outputFileExe.Replace(".exe", ".obj");
            var objCommand = dontGenObj.IsChecked == true ? $"DEL /F /Q \"{outputFileObj}\"" : $"move /Y \"{outputFileObj}\" \"{outputPath}\"";

            if (convertEncoding.IsChecked == true)
            {
                var output = System.IO.File.ReadAllLines(SrcPath);
                if(System.IO.File.Exists($"{MasmPath.Text}\\bin\\{FileName}"))
                {
                    System.IO.File.Delete($"{MasmPath.Text}\\bin\\{FileName}");
                }
                System.IO.File.WriteAllLines($"{MasmPath.Text}\\bin\\{FileName}", output, Encoding.GetEncoding(866));
            }
            else
            {
                System.IO.File.Copy(SrcPath, MasmPath.Text + "\\bin\\"+FileName, true);
            }

            if ((saveLog.IsChecked == true || openLogAfter.IsChecked == true) && !System.IO.Directory.Exists(".\\Logs"))
            {
                System.IO.Directory.CreateDirectory(".\\Logs");
            }

            var logFile = $".\\Logs\\[{UnixTimeNow()}] {FileName.Replace(".asm", "")}.txt";
            using (var process = new Process())
            {
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = "cmd";
                process.StartInfo.Arguments =  $"/c cd /d \"{MasmPath.Text}\"\\bin && ml.exe /c /coff \"{FileName}\" && link.exe /SUBSYSTEM:CONSOLE \"{FileName.Replace(".asm",".obj")}\" &&" +
                    $" move /Y \"{outputFileExe}\" \"{outputPath}\" && {objCommand}";
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                var result = process.StandardOutput.ReadToEnd();

                if(saveLog.IsChecked == true || openLogAfter.IsChecked == true)
                     System.IO.File.WriteAllText(logFile, result);
                process.WaitForExit();   
                if(result.Contains("error"))
                {
                    System.Windows.Forms.MessageBox.Show(result,"Compile error");
                }
            }


            if(openLogAfter.IsChecked == true)
                Process.Start(logFile);

            System.IO.File.Delete(MasmPath.Text + "\\bin\\" + FileName);
           
        }

        private void SetMasmDir_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && !String.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MasmPath.Text = dialog.SelectedPath;
                    Config.properties.MASMPath = dialog.SelectedPath;
                    Config.Save();
                }
            }
                
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

        private void openLogAfter_Click(object sender, RoutedEventArgs e)
        {
            Config.properties.OpenAssemblyResults = openLogAfter.IsChecked == true;
            Config.Save();
        }

        private void OpenEditor_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(SrcPath) && !System.IO.File.Exists(SrcPath))
            {
                System.Windows.Forms.MessageBox.Show($"File {FileName} is not found!");
                SelectedText.Text = Config.properties.LastSrcPath = SrcPath = null;
                Config.Save();
                return;
            }
            Process.Start(SrcPath);

        }
    }
}
