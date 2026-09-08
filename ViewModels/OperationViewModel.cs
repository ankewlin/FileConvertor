using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FileConvertor.Core;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using FileConvertor.Core.Utils;
using FileConvertor.Services;

namespace FileConvertor.ViewModels
{
    /// <summary>
    /// PDF 操作详情页 ViewModel
    /// </summary>
    public class OperationViewModel : ViewModelBase
    {
        private readonly DialogService _dialogService;
        private CancellationTokenSource _cts;

        public MainViewModel Main { get; }

        /// <summary>当前操作</summary>
        public IPdfOperation Operation { get; private set; }

        /// <summary>输入文件列表</summary>
        public ObservableCollection<string> InputFiles { get; } = new ObservableCollection<string>();

        /// <summary>动态参数列表（用于绑定 UI）</summary>
        public ObservableCollection<ParameterItemViewModel> Parameters { get; } = new ObservableCollection<ParameterItemViewModel>();

        /// <summary>是否正在执行</summary>
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set { if (SetProperty(ref _isRunning, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        /// <summary>进度 (0-100)</summary>
        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>状态文本</summary>
        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>当前文件</summary>
        private string _currentFile;
        public string CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);
        }

        /// <summary>输出目录</summary>
        private string _outputDirectory;
        public string OutputDirectory
        {
            get => _outputDirectory;
            set => SetProperty(ref _outputDirectory, value);
        }

        /// <summary>是否使用自定义输出目录</summary>
        private bool _useCustomOutput;
        public bool UseCustomOutput
        {
            get => _useCustomOutput;
            set => SetProperty(ref _useCustomOutput, value);
        }

        // 命令
        public ICommand BackCommand { get; }
        public ICommand SelectFilesCommand { get; }
        public ICommand RemoveFileCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand BrowseOutputCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand CancelCommand { get; }

        public OperationViewModel(MainViewModel main)
        {
            Main = main;
            _dialogService = new DialogService();

            BackCommand = new RelayCommand(() => Main.NavigateToHome());
            SelectFilesCommand = new RelayCommand(SelectFiles);
            RemoveFileCommand = new RelayCommand<string>(RemoveFile);
            ClearFilesCommand = new RelayCommand(ClearFiles, () => InputFiles.Count > 0);
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            StartCommand = new RelayCommand(async () => await StartAsync(), CanStart);
            CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        }

        /// <summary>
        /// 设置当前操作（导航进来时调用）
        /// </summary>
        public void SetOperation(IPdfOperation operation)
        {
            Operation = operation;
            InputFiles.Clear();
            Progress = 0;
            StatusText = "";
            CurrentFile = "";
            IsRunning = false;

            // 初始化参数
            Parameters.Clear();
            foreach (var param in operation.Parameters)
            {
                var vm = new ParameterItemViewModel(param);
                vm.PropertyChanged += Param_PropertyChanged;
                Parameters.Add(vm);
            }

            // 初始化可见性
            UpdateParameterVisibility();

            OnPropertyChanged(nameof(Operation));
            OnPropertyChanged(nameof(Parameters));
            OnPropertyChanged(nameof(FileSummaryText));
            OnPropertyChanged(nameof(HasFiles));
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// 参数值变化时，联动更新其他参数的可见性
        /// </summary>
        private void Param_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ParameterItemViewModel.Value) ||
                e.PropertyName == nameof(ParameterItemViewModel.StringValue))
            {
                UpdateParameterVisibility();
            }
        }

        /// <summary>
        /// 根据 VisibleWhenKey / VisibleWhenValue 更新所有参数的可见性
        /// </summary>
        private void UpdateParameterVisibility()
        {
            foreach (var param in Parameters)
            {
                var def = param.Definition;
                if (string.IsNullOrEmpty(def.VisibleWhenKey))
                {
                    param.Visible = true;
                    continue;
                }

                var dependParam = Parameters.FirstOrDefault(p => p.Key == def.VisibleWhenKey);
                if (dependParam == null)
                {
                    param.Visible = true;
                    continue;
                }

                // 比较值：字符串比较最稳妥
                string currentVal = dependParam.Value?.ToString() ?? "";
                string targetVal = def.VisibleWhenValue?.ToString() ?? "";
                param.Visible = string.Equals(currentVal, targetVal, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>文件摘要文本</summary>
        public string FileSummaryText
        {
            get
            {
                if (InputFiles.Count == 0) return "";
                long total = 0;
                foreach (var f in InputFiles)
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
                return $"{InputFiles.Count} 个文件 ({FormatSize(total)})";
            }
        }

        /// <summary>是否有文件（用于 UI 可见性）</summary>
        public bool HasFiles => InputFiles.Count > 0;

        /// <summary>
        /// 添加文件（从拖拽或对话框）
        /// </summary>
        public void AddFiles(IEnumerable<string> files)
        {
            bool added = false;
            var supportedExts = new HashSet<string>(
                Operation.SupportedInputExtensions,
                StringComparer.OrdinalIgnoreCase);

            foreach (var path in files)
            {
                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
                    if (supportedExts.Contains(ext) && !InputFiles.Contains(path))
                    {
                        InputFiles.Add(path);
                        added = true;
                    }
                }
                else if (Directory.Exists(path))
                {
                    try
                    {
                        foreach (var file in Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            string ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                            if (supportedExts.Contains(ext) && !InputFiles.Contains(file))
                            {
                                InputFiles.Add(file);
                                added = true;
                            }
                        }
                    }
                    catch { }
                }
            }

            if (added)
            {
                OnPropertyChanged(nameof(FileSummaryText));
                OnPropertyChanged(nameof(HasFiles));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void SelectFiles()
        {
            var exts = Operation.SupportedInputExtensions.Select(e => $"*.{e}").ToArray();
            string filter = $"支持的文件|{string.Join(";", exts)}|所有文件|*.*";
            var files = _dialogService.OpenFiles(filter);
            if (files != null && files.Length > 0)
                AddFiles(files);
        }

        private void RemoveFile(string file)
        {
            if (InputFiles.Remove(file))
            {
                OnPropertyChanged(nameof(FileSummaryText));
                OnPropertyChanged(nameof(HasFiles));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ClearFiles()
        {
            InputFiles.Clear();
            OnPropertyChanged(nameof(FileSummaryText));
            OnPropertyChanged(nameof(HasFiles));
            CommandManager.InvalidateRequerySuggested();
        }

        private void BrowseOutput()
        {
            string folder = _dialogService.SelectFolder(OutputDirectory);
            if (!string.IsNullOrEmpty(folder))
            {
                OutputDirectory = folder;
                UseCustomOutput = true;
            }
        }

        private bool CanStart()
        {
            if (IsRunning) return false;
            if (Operation == null) return false;
            if (Operation.RequiresMultipleInputs)
                return InputFiles.Count >= 2;
            return InputFiles.Count >= 1;
        }

        private async Task StartAsync()
        {
            if (!CanStart()) return;

            IsRunning = true;
            Progress = 0;
            StatusText = "准备中...";
            CurrentFile = "";
            _cts = new CancellationTokenSource();

            var context = new PdfOperationContext();
            foreach (var file in InputFiles)
                context.InputFiles.Add(file);

            if (UseCustomOutput && !string.IsNullOrWhiteSpace(OutputDirectory))
                context.OutputDirectory = OutputDirectory;

            // 从 Parameters 收集参数值
            foreach (var param in Parameters)
                context.Parameters[param.Key] = param.Value;

            var progress = new Progress<double>(p => Progress = p * 100);

            try
            {
                Logger.Info($"开始操作: {Operation.Name}, 文件数: {InputFiles.Count}");

                var result = await Task.Run(() =>
                    Operation.ExecuteAsync(context, progress, _cts.Token), _cts.Token);

                if (result.Success)
                {
                    StatusText = $"完成：生成 {result.OutputFiles.Count} 个文件";
                    Logger.Info($"操作成功: {Operation.Name}, 输出 {result.OutputFiles.Count} 个文件");
                    _dialogService.ShowMessage(
                        $"操作成功！\n\n生成了 {result.OutputFiles.Count} 个文件。\n输出目录: {context.OutputDirectory ?? Path.GetDirectoryName(InputFiles[0])}",
                        "完成", MessageBoxImage.Information);
                }
                else
                {
                    StatusText = "失败";
                    Logger.Warning($"操作失败: {Operation.Name}, 原因: {result.ErrorMessage}");
                    _dialogService.ShowMessage(result.ErrorMessage, "操作失败", MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "已取消";
                Logger.Info($"操作已取消: {Operation.Name}");
            }
            catch (Exception ex)
            {
                StatusText = "出错了";
                Logger.Error($"操作异常: {Operation.Name}", ex);
                _dialogService.ShowMessage($"操作失败：{ex.Message}", "错误", MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;
                CurrentFile = "";
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                StatusText = "正在取消...";
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}
