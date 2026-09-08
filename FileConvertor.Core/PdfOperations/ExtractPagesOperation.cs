using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using FileConvertor.Core.Utils;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// 提取页面：从 PDF 中提取指定页保存为新 PDF
    /// </summary>
    public class ExtractPagesOperation : IPdfOperation
    {
        public string Name => "提取页面";
        public string Description => "从 PDF 中提取指定页面，保存为新的 PDF 文件";
        public string Icon => "📄";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("PageRange", "页面范围", ParameterType.PageRange, "")
                {
                    Placeholder = "如: 1,3,5-10",
                    IsRequired = true
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "extracted.pdf")
                {
                    Placeholder = "extracted.pdf"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => ExtractInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult ExtractInternal(
            PdfOperationContext context,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (context.InputFiles == null || context.InputFiles.Count == 0)
                    return PdfOperationResult.Fail("请选择一个 PDF 文件");

                string inputFile = context.InputFiles[0];
                if (!File.Exists(inputFile))
                    return PdfOperationResult.Fail($"文件不存在: {inputFile}");

                string pageRangeStr = context.GetString("PageRange", "");
                if (string.IsNullOrWhiteSpace(pageRangeStr))
                    return PdfOperationResult.Fail("请填写页面范围");

                if (!PageRangeParser.IsValid(pageRangeStr))
                    return PdfOperationResult.Fail("页面范围格式不正确，示例: 1,3,5-10");

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "extracted.pdf");
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                using (var sourceDoc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import))
                {
                    var pages = PageRangeParser.Parse(pageRangeStr, sourceDoc.PageCount);
                    if (pages.Count == 0)
                        return PdfOperationResult.Fail("没有匹配的页面，请检查页面范围");

                    using (var outputDoc = new PdfDocument())
                    {
                        for (int i = 0; i < pages.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            outputDoc.AddPage(sourceDoc.Pages[pages[i] - 1]);
                            progress?.Report((double)(i + 1) / pages.Count);
                        }

                        outputDoc.Save(outputPath);
                    }
                }

                progress?.Report(1.0);
                return PdfOperationResult.Ok(outputPath);
            }
            catch (OperationCanceledException)
            {
                return PdfOperationResult.Fail("操作已取消");
            }
            catch (Exception ex)
            {
                return PdfOperationResult.Fail($"提取失败: {ex.Message}");
            }
        }
    }
}
