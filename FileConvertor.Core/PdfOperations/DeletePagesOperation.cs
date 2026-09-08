using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// 删除页面：从 PDF 中删除指定页
    /// </summary>
    public class DeletePagesOperation : IPdfOperation
    {
        public string Name => "删除页面";
        public string Description => "从 PDF 中删除指定的页面";
        public string Icon => "🗑️";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("PageRange", "要删除的页面", ParameterType.PageRange, "")
                {
                    Placeholder = "如: 2,5,8-10",
                    IsRequired = true
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则在原文件名后加 _deleted"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => DeleteInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult DeleteInternal(
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
                    return PdfOperationResult.Fail("请填写要删除的页面范围");

                if (!PageRangeParser.IsValid(pageRangeStr))
                    return PdfOperationResult.Fail("页面范围格式不正确，示例: 2,5,8-10");

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "");
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputName = $"{name}_deleted.pdf";
                }
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                using (var sourceDoc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import))
                {
                    var pagesToDelete = new HashSet<int>(PageRangeParser.Parse(pageRangeStr, sourceDoc.PageCount));
                    var pagesToKeep = Enumerable.Range(1, sourceDoc.PageCount)
                        .Where(p => !pagesToDelete.Contains(p))
                        .ToList();

                    if (pagesToKeep.Count == 0)
                        return PdfOperationResult.Fail("所有页面都被删除了，至少需要保留 1 页");

                    using (var outputDoc = new PdfDocument())
                    {
                        for (int i = 0; i < pagesToKeep.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            outputDoc.AddPage(sourceDoc.Pages[pagesToKeep[i] - 1]);
                            progress?.Report((double)(i + 1) / pagesToKeep.Count);
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
                return PdfOperationResult.Fail($"删除失败: {ex.Message}");
            }
        }
    }
}
