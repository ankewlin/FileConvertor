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
    /// Excel 转 PDF（基于 Microsoft Excel，通过反射调用）
    /// 支持 .xls 和 .xlsx
    /// </summary>
    public class ExcelToPdfOperation : IPdfOperation
    {
        public string Name => "Excel 转 PDF";
        public string Description => "将 Excel 表格（.xls/.xlsx）转换为 PDF 格式";
        public string Icon => "📊";
        public PdfOperationCategory Category => PdfOperationCategory.Convert;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "xls", "xlsx" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则与原文件名相同"
                },
                new OperationParameter("ExportWhat", "导出内容", ParameterType.Select, "整个工作簿")
                {
                    Options = new object[] { "整个工作簿", "当前活动工作表" }
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
                    return PdfOperationResult.Fail("请选择一个 Excel 文件");

                string inputFile = context.InputFiles[0];
                if (!File.Exists(inputFile))
                    return PdfOperationResult.Fail($"文件不存在: {inputFile}");

                // 检查是否安装了 Excel
                Type excelAppType = Type.GetTypeFromProgID("Excel.Application");
                if (excelAppType == null)
                    return PdfOperationResult.Fail("未检测到 Microsoft Excel，请先安装 Office Excel 后再使用此功能");

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
                Logger.Info($"Excel转PDF: 开始转换 {Path.GetFileName(inputFile)}");

                dynamic excelApp = null;
                dynamic workbook = null;
                try
                {
                    excelApp = Activator.CreateInstance(excelAppType);
                    excelApp.Visible = false;
                    excelApp.DisplayAlerts = false;
                    excelApp.ScreenUpdating = false;

                    progress?.Report(0.2);

                    // 打开工作簿（dynamic 调用 COM 时直接传具体类型，不要包 object）
                    object missing = System.Reflection.Missing.Value;

                    var workbooks = excelApp.Workbooks;
                    workbook = workbooks.Open(inputFile, missing, true,
                        missing, missing, missing, missing, missing, missing,
                        missing, missing, missing, missing, missing, missing);

                    progress?.Report(0.5);

                    cancellationToken.ThrowIfCancellationRequested();

                    string exportWhat = context.GetString("ExportWhat", "整个工作簿");
                    // xlTypePDF = 0
                    const int xlTypePDF = 0;
                    // xlQualityStandard = 0
                    const int xlQualityStandard = 0;

                    if (exportWhat == "当前活动工作表")
                    {
                        // 导出当前活动工作表
                        var activeSheet = workbook.ActiveSheet;
                        activeSheet.ExportAsFixedFormat(xlTypePDF, outputPath,
                            xlQualityStandard, true, false,
                            missing, missing, false, missing);
                    }
                    else
                    {
                        // 导出整个工作簿
                        workbook.ExportAsFixedFormat(xlTypePDF, outputPath,
                            xlQualityStandard, true, false,
                            missing, missing, false, missing);
                    }

                    progress?.Report(0.95);
                }
                finally
                {
                    if (workbook != null)
                    {
                        try { workbook.Close(false); } catch { }
                        workbook = null;
                    }
                    if (excelApp != null)
                    {
                        try { excelApp.Quit(); } catch { }
                        excelApp = null;
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
                Logger.Info($"Excel转PDF: 成功 {Path.GetFileName(outputPath)}");
                return PdfOperationResult.Ok(outputPath);
            }
            catch (OperationCanceledException)
            {
                return PdfOperationResult.Fail("操作已取消");
            }
            catch (Exception ex)
            {
                Logger.Error($"Excel转PDF失败", ex);
                return PdfOperationResult.Fail($"转换失败: {ex.Message}");
            }
        }
    }
}
