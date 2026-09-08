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
    /// 旋转 PDF 页面
    /// </summary>
    public class RotatePdfOperation : IPdfOperation
    {
        public string Name => "旋转 PDF";
        public string Description => "旋转 PDF 页面方向（90°/180°/270°）";
        public string Icon => "🔄";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("Rotation", "旋转角度", ParameterType.Select, "90")
                {
                    Options = new object[] { "顺时针 90°", "180°", "逆时针 90°" }
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则在原文件名后加 _rotated"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => RotateInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult RotateInternal(
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

                int rotation;
                string rotationStr = context.GetString("Rotation", "顺时针 90°");
                switch (rotationStr)
                {
                    case "顺时针 90°": rotation = 90; break;
                    case "180°": rotation = 180; break;
                    case "逆时针 90°": rotation = 270; break;
                    default: rotation = 90; break;
                }

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "");
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputName = $"{name}_rotated.pdf";
                }
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                using (var doc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Modify))
                {
                    for (int i = 0; i < doc.PageCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var page = doc.Pages[i];
                        // 在当前旋转角度基础上累加
                        int currentRotation = page.Rotate;
                        page.Rotate = (currentRotation + rotation) % 360;

                        progress?.Report((double)(i + 1) / doc.PageCount);
                    }
                    doc.Save(outputPath);
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
                return PdfOperationResult.Fail($"旋转失败: {ex.Message}");
            }
        }
    }
}
