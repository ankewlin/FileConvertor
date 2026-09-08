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
    /// PowerPoint 转 PDF（基于 Microsoft PowerPoint，通过反射调用）
    /// 支持 .ppt 和 .pptx
    /// </summary>
    public class PowerPointToPdfOperation : IPdfOperation
    {
        public string Name => "PPT 转 PDF";
        public string Description => "将 PowerPoint 演示文稿（.ppt/.pptx）转换为 PDF 格式";
        public string Icon => "📽️";
        public PdfOperationCategory Category => PdfOperationCategory.Convert;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "ppt", "pptx" }.AsReadOnly();

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
                    return PdfOperationResult.Fail("请选择一个 PowerPoint 文件");

                string inputFile = context.InputFiles[0];
                if (!File.Exists(inputFile))
                    return PdfOperationResult.Fail($"文件不存在: {inputFile}");

                // 检查是否安装了 PowerPoint
                Type pptAppType = Type.GetTypeFromProgID("PowerPoint.Application");
                if (pptAppType == null)
                    return PdfOperationResult.Fail("未检测到 Microsoft PowerPoint，请先安装 Office 后再使用此功能");

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
                Logger.Info($"PPT转PDF: 开始转换 {Path.GetFileName(inputFile)}");

                dynamic pptApp = null;
                dynamic presentation = null;
                try
                {
                    pptApp = Activator.CreateInstance(pptAppType);
                    // PowerPoint 的 Visible 需要特殊设置，设为窗口隐藏
                    try { pptApp.Visible = false; } catch { }

                    progress?.Report(0.2);

                    // 打开演示文稿
                    // Presentations.Open(FileName, ReadOnly, WithWindow)
                    var presentations = pptApp.Presentations;
                    // dynamic 调用 COM 时直接传具体类型值
                    presentation = presentations.Open(inputFile, true, false);

                    progress?.Report(0.5);

                    cancellationToken.ThrowIfCancellationRequested();

                    // SaveAs 导出 PDF
                    // PpSaveAsFileType.ppSaveAsPDF = 32
                    const int ppSaveAsPDF = 32;
                    const int ppFixedFormatTypePDF = 2;

                    // 用 SaveAs 方法（兼容性更好）
                    try
                    {
                        presentation.SaveAs(outputPath, ppSaveAsPDF);
                    }
                    catch
                    {
                        // 如果 SaveAs 不支持 PDF，用 ExportAsFixedFormat
                        object missing = System.Reflection.Missing.Value;
                        const int ppFixedFormatIntentPrint = 1;
                        const int ppPrintOutputSlides = 1;
                        presentation.ExportAsFixedFormat(outputPath, ppFixedFormatTypePDF,
                            ppFixedFormatIntentPrint, missing, ppPrintOutputSlides,
                            missing, missing, missing, missing, missing, missing,
                            missing, missing, missing, missing, missing, missing, missing);
                    }

                    progress?.Report(0.95);
                }
                finally
                {
                    if (presentation != null)
                    {
                        try { presentation.Close(); } catch { }
                        presentation = null;
                    }
                    if (pptApp != null)
                    {
                        try { pptApp.Quit(); } catch { }
                        pptApp = null;
                    }
                    // 强制清理 COM RCW
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                if (!File.Exists(outputPath))
                    return PdfOperationResult.Fail("转换失败，未生成输出文件");

                progress?.Report(1.0);
                Logger.Info($"PPT转PDF: 成功 {Path.GetFileName(outputPath)}");
                return PdfOperationResult.Ok(outputPath);
            }
            catch (OperationCanceledException)
            {
                return PdfOperationResult.Fail("操作已取消");
            }
            catch (Exception ex)
            {
                Logger.Error($"PPT转PDF失败", ex);
                return PdfOperationResult.Fail($"转换失败: {ex.Message}");
            }
        }
    }
}
