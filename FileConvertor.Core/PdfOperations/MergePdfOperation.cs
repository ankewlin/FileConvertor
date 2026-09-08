using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// 合并 PDF：将多个 PDF 文件按顺序合并为一个
    /// </summary>
    public class MergePdfOperation : IPdfOperation
    {
        public string Name => "合并 PDF";
        public string Description => "将多个 PDF 文件按顺序合并为一个 PDF 文件";
        public string Icon => "🧩";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => true;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "merged.pdf")
                {
                    Placeholder = "merged.pdf",
                    IsRequired = true
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => MergeInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult MergeInternal(
            PdfOperationContext context,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (context.InputFiles == null || context.InputFiles.Count < 2)
                    return PdfOperationResult.Fail("请至少选择 2 个 PDF 文件");

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(context.InputFiles[0]);

                string outputName = context.GetString("OutputFileName", "merged.pdf");
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                using (var outputDoc = new PdfDocument())
                {
                    int totalPages = 0;
                    int processedPages = 0;

                    // 先统计总页数用于计算进度
                    foreach (var file in context.InputFiles)
                    {
                        if (!File.Exists(file))
                            return PdfOperationResult.Fail($"文件不存在: {file}");
                        try
                        {
                            using (var doc = PdfReader.Open(file, PdfDocumentOpenMode.InformationOnly))
                                totalPages += doc.PageCount;
                        }
                        catch (Exception ex)
                        {
                            return PdfOperationResult.Fail($"无法打开文件 {Path.GetFileName(file)}: {ex.Message}");
                        }
                    }

                    foreach (var file in context.InputFiles)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using (var doc = PdfReader.Open(file, PdfDocumentOpenMode.Import))
                        {
                            for (int i = 0; i < doc.PageCount; i++)
                            {
                                outputDoc.AddPage(doc.Pages[i]);
                                processedPages++;
                                progress?.Report((double)processedPages / totalPages);
                            }
                        }
                    }

                    outputDoc.Save(outputPath);
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
                return PdfOperationResult.Fail($"合并失败: {ex.Message}");
            }
        }
    }
}
