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
    /// 拆分 PDF：将 PDF 拆分为多个文件
    /// - 每页一个文件
    /// - 按页面范围拆分
    /// </summary>
    public class SplitPdfOperation : IPdfOperation
    {
        public string Name => "拆分 PDF";
        public string Description => "将 PDF 拆分为多个文件（每页一个，或按范围拆分）";
        public string Icon => "✂️";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => true;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("SplitMode", "拆分方式", ParameterType.Select, "每页一个文件")
                {
                    Options = new object[] { "每页一个文件", "按页码范围拆分" }
                },
                new OperationParameter("PageRanges", "页码范围", ParameterType.Text, "")
                {
                    VisibleWhenKey = "SplitMode",
                    VisibleWhenValue = "按页码范围拆分",
                    Placeholder = "例：1-3, 5, 7-10  （多个范围用换行分隔）"
                },
                new OperationParameter("OutputPrefix", "文件名前缀", ParameterType.Text, "page_")
                {
                    Placeholder = "生成的文件名前缀，如 page_"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => SplitInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult SplitInternal(
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

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputDir = Path.Combine(Path.GetDirectoryName(inputFile), $"{name}_split");
                }
                Directory.CreateDirectory(outputDir);

                string prefix = context.GetString("OutputPrefix", "page_");
                string splitMode = context.GetString("SplitMode", "每页一个文件");

                var outputFiles = new List<string>();

                using (var sourceDoc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import))
                {
                    int totalPages = sourceDoc.PageCount;

                    if (splitMode == "每页一个文件" || splitMode == "EachPage")
                    {
                        for (int i = 0; i < totalPages; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            string outPath = Path.Combine(outputDir, $"{prefix}{i + 1}.pdf");
                            using (var outDoc = new PdfDocument())
                            {
                                outDoc.AddPage(sourceDoc.Pages[i]);
                                outDoc.Save(outPath);
                            }
                            outputFiles.Add(outPath);
                            progress?.Report((double)(i + 1) / totalPages);
                        }
                    }
                    else
                    {
                        // 按页面范围拆分
                        string rangesStr = context.GetString("PageRanges", "");
                        if (string.IsNullOrWhiteSpace(rangesStr))
                            return PdfOperationResult.Fail("请填写拆分范围");

                        string[] ranges = rangesStr.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                        int rangeIdx = 0;
                        foreach (string range in ranges)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            string trimmed = range.Trim();
                            if (string.IsNullOrEmpty(trimmed)) continue;

                            var pages = PageRangeParser.Parse(trimmed, totalPages);
                            if (pages.Count == 0) continue;

                            string outPath = Path.Combine(outputDir, $"{prefix}{rangeIdx + 1}.pdf");
                            using (var outDoc = new PdfDocument())
                            {
                                foreach (int p in pages)
                                {
                                    outDoc.AddPage(sourceDoc.Pages[p - 1]);
                                }
                                outDoc.Save(outPath);
                            }
                            outputFiles.Add(outPath);
                            rangeIdx++;
                            progress?.Report((double)rangeIdx / ranges.Length);
                        }
                    }
                }

                if (outputFiles.Count == 0)
                    return PdfOperationResult.Fail("没有生成任何输出文件");

                progress?.Report(1.0);
                return PdfOperationResult.Ok(outputFiles);
            }
            catch (OperationCanceledException)
            {
                return PdfOperationResult.Fail("操作已取消");
            }
            catch (Exception ex)
            {
                return PdfOperationResult.Fail($"拆分失败: {ex.Message}");
            }
        }
    }
}
