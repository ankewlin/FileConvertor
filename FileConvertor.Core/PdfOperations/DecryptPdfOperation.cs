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
    /// 解密 PDF：移除密码保护（需要提供正确的密码）
    /// </summary>
    public class DecryptPdfOperation : IPdfOperation
    {
        public string Name => "解密 PDF";
        public string Description => "移除 PDF 的密码保护（需提供正确密码）";
        public string Icon => "🔓";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("Password", "PDF 密码", ParameterType.Password, "")
                {
                    IsRequired = true,
                    Placeholder = "请输入 PDF 密码"
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则在原文件名后加 _decrypted"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => DecryptInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult DecryptInternal(
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

                string password = context.GetString("Password", "");
                if (string.IsNullOrWhiteSpace(password))
                    return PdfOperationResult.Fail("请输入 PDF 密码");

                Logger.Debug($"解密PDF: 输入={Path.GetFileName(inputFile)}, 密码长度={password.Length}");

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "");
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputName = $"{name}_decrypted.pdf";
                }
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                progress?.Report(0.1);

                // 先用密码打开源文档
                using (var sourceDoc = PdfReader.Open(inputFile, password, PdfDocumentOpenMode.Import))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.4);

                    // 新建文档（无密码），复制所有页面
                    using (var outputDoc = new PdfDocument())
                    {
                        for (int i = 0; i < sourceDoc.PageCount; i++)
                        {
                            outputDoc.AddPage(sourceDoc.Pages[i]);
                        }

                        progress?.Report(0.8);
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
                Logger.Warning($"解密PDF失败: {ex.Message}", ex);
                // PdfSharp 密码错误时的异常提示
                string msg = ex.Message;
                if (msg.Contains("password") || msg.Contains("Password") || msg.Contains("密码")
                    || msg.Contains("invalid") || msg.Contains("Invalid"))
                    return PdfOperationResult.Fail("密码错误，无法打开 PDF");
                return PdfOperationResult.Fail($"解密失败: {ex.Message}");
            }
        }
    }
}
