using System;
using System.Windows;
using System.Windows.Threading;
using FileConvertor.Core;
using FileConvertor.Core.Converters;
using FileConvertor.Core.PdfOperations;
using FileConvertor.Core.Utils;

namespace FileConvertor
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 全局 UI 线程异常捕获
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // 记录启动日志
            Logger.Info("===== FileConvertor 启动 =====");

            // 注册所有转换器和操作
            RegisterConverters();
            RegisterPdfOperations();

            Logger.Debug("转换器与 PDF 操作注册完成");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info($"===== FileConvertor 退出 (退出码: {e.ApplicationExitCode}) =====");
            base.OnExit(e);
        }

        /// <summary>
        /// 全局未捕获异常处理
        /// </summary>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error("未捕获的 UI 线程异常", e.Exception);
            try
            {
                MessageBox.Show($"程序遇到未预期的错误：\n{e.Exception.Message}\n\n详细信息已记录到日志文件。",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            e.Handled = true;
        }

        private void RegisterConverters()
        {
            var registry = ConverterRegistry.Instance;
            registry.Register(new ImageConverter());
        }

        private void RegisterPdfOperations()
        {
            var registry = PdfOperationRegistry.Instance;

            // PDF 编辑类
            registry.Register(new MergePdfOperation());
            registry.Register(new SplitPdfOperation());
            registry.Register(new ExtractPagesOperation());
            registry.Register(new DeletePagesOperation());
            registry.Register(new RotatePdfOperation());
            registry.Register(new CompressPdfOperation());
            registry.Register(new WatermarkPdfOperation());
            registry.Register(new EncryptPdfOperation());
            registry.Register(new DecryptPdfOperation());

            // 格式转换类
            registry.Register(new ImageToPdfOperation());
            registry.Register(new PdfToImageOperation());
            registry.Register(new WordToPdfOperation());
            registry.Register(new ExcelToPdfOperation());
            registry.Register(new PowerPointToPdfOperation());
        }
    }
}
