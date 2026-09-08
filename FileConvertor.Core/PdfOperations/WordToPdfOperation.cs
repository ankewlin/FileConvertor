using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using FileConvertor.Core.Utils;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// Word 转 PDF（基于 Microsoft Word，通过反射调用，无需编译时引用 COM）
    /// 支持 .doc 和 .docx
    /// </summary>
    public class WordToPdfOperation : IPdfOperation
    {
        public string Name => "Word 转 PDF";
        public string Description => "将 Word 文档（.doc/.docx）转换为 PDF 格式";
        public string Icon => "📄";
        public PdfOperationCategory Category => PdfOperationCategory.Convert;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "doc", "docx" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则与原文件名相同"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            // Office COM 必须在 STA 线程运行
            var tcs = new TaskCompletionSource<PdfOperationResult>();
            var thread = new Thread(() =>
            {
                try
                {
                    var result = ConvertInternal(context, progress, cancellationToken);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return await tcs.Task;
        }

        private PdfOperationResult ConvertInternal(
            PdfOperationContext context,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (context.InputFiles == null || context.InputFiles.Count == 0)
                    return PdfOperationResult.Fail("请选择一个 Word 文件");

                string inputFile = context.InputFiles[0];
                if (!File.Exists(inputFile))
                    return PdfOperationResult.Fail($"文件不存在: {inputFile}");

                // 检查是否安装了 Word
                Type wordAppType = Type.GetTypeFromProgID("Word.Application");
                if (wordAppType == null)
                    return PdfOperationResult.Fail("未检测到 Microsoft Word，请先安装 Office Word 后再使用此功能");

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "");
                if (string.IsNullOrWhiteSpace(outputName))
                    outputName = Path.GetFileNameWithoutExtension(inputFile) + ".pdf";
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                progress?.Report(0.1);
                Logger.Info($"Word转PDF: 开始转换 {Path.GetFileName(inputFile)}");

                dynamic wordApp = null;
                dynamic doc = null;
                try
                {
                    wordApp = Activator.CreateInstance(wordAppType);
                    wordApp.Visible = false;
                    wordApp.DisplayAlerts = false; // wdAlertsNone = 0

                    progress?.Report(0.2);

                    // 打开文档
                    object missing = System.Reflection.Missing.Value;

                    var docs = wordApp.Documents;
                    // 动态调用无需 ref，直接传参
                    doc = docs.Open(inputFile, missing, true,
                        missing, missing, missing, missing,
                        missing, missing, missing, missing,
                        false, missing, missing, missing, missing);

                    progress?.Report(0.6);

                    cancellationToken.ThrowIfCancellationRequested();

                    // 导出为 PDF
                    // WdExportFormat.wdExportFormatPDF = 17
                    int wdExportFormatPDF = 17;

                    doc.ExportAsFixedFormat(outputPath, wdExportFormatPDF,
                        false, 0, 0,
                        missing, missing, 0, true,
                        true, 0, true,
                        true, false,
                        missing);

                    progress?.Report(0.95);
                }
                finally
                {
                    // 关闭文档和 Word（在 STA 线程内正常关闭即可，RCW 由 GC 回收）
                    if (doc != null)
                    {
                        try { doc.Close(false); } catch { }
                        doc = null;
                    }
                    if (wordApp != null)
                    {
                        try { wordApp.Quit(); } catch { }
                        wordApp = null;
                    }
                    // 强制清理 COM RCW，确保 Word 进程及时退出
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                if (!File.Exists(outputPath))
                    return PdfOperationResult.Fail("转换失败，未生成输出文件");

                progress?.Report(1.0);
                Logger.Info($"Word转PDF: 成功 {Path.GetFileName(outputPath)}");
                return PdfOperationResult.Ok(outputPath);
            }
            catch (OperationCanceledException)
            {
                return PdfOperationResult.Fail("操作已取消");
            }
            catch (Exception ex)
            {
                Logger.Error($"Word转PDF失败", ex);
                return PdfOperationResult.Fail($"转换失败: {ex.Message}");
            }
        }
    }
}
