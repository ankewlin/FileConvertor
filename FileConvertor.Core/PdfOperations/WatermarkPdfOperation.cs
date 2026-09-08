using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// 添加文字水印到 PDF
    /// </summary>
    public class WatermarkPdfOperation : IPdfOperation
    {
        public string Name => "添加水印";
        public string Description => "为 PDF 添加半透明文字水印";
        public string Icon => "💧";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("WatermarkText", "水印文字", ParameterType.Text, "CONFIDENTIAL")
                {
                    IsRequired = true,
                    Placeholder = "请输入水印文字"
                },
                new OperationParameter("Opacity", "透明度", ParameterType.Slider, 30)
                {
                    MinValue = 5,
                    MaxValue = 100
                },
                new OperationParameter("FontSize", "字体大小", ParameterType.Slider, 48)
                {
                    MinValue = 12,
                    MaxValue = 200
                },
                new OperationParameter("Rotation", "旋转角度", ParameterType.Select, "45")
                {
                    Options = new object[] { "0°", "30°", "45°", "60°", "90°" }
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则在原文件名后加 _watermark"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => WatermarkInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult WatermarkInternal(
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

                string watermarkText = context.GetString("WatermarkText", "");
                if (string.IsNullOrWhiteSpace(watermarkText))
                    return PdfOperationResult.Fail("请输入水印文字");

                double opacity = context.GetInt("Opacity", 30) / 100.0;
                int fontSize = context.GetInt("FontSize", 48);

                int rotation;
                string rotStr = context.GetString("Rotation", "45°");
                switch (rotStr)
                {
                    case "0°": rotation = 0; break;
                    case "30°": rotation = 30; break;
                    case "45°": rotation = 45; break;
                    case "60°": rotation = 60; break;
                    case "90°": rotation = 90; break;
                    default: rotation = 45; break;
                }

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "");
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputName = $"{name}_watermark.pdf";
                }
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                using (var sourceDoc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import))
                {
                    XFont font = new XFont("Arial", fontSize, XFontStyle.Bold);
                    XBrush brush = new XSolidBrush(XColor.FromArgb(
                        (int)(opacity * 255), 128, 128, 128));

                    using (var outputDoc = new PdfDocument())
                    {
                        for (int i = 0; i < sourceDoc.PageCount; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // 导入页面到新文档
                            var page = outputDoc.AddPage(sourceDoc.Pages[i]);

                            // 在页面上方绘制水印（Append = 叠加在内容之上）
                            using (var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
                            {
                                // 计算文字大小
                                XSize textSize = gfx.MeasureString(watermarkText, font);

                                // 页面中心
                                double centerX = page.Width.Point / 2;
                                double centerY = page.Height.Point / 2;

                                // 应用旋转
                                gfx.RotateAtTransform(rotation, new XPoint(centerX, centerY));

                                // 绘制水印（中心对齐）
                                gfx.DrawString(watermarkText, font, brush, centerX, centerY,
                                    XStringFormats.Center);
                            }

                            progress?.Report((double)(i + 1) / sourceDoc.PageCount);
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
                return PdfOperationResult.Fail($"添加水印失败: {ex.Message}");
            }
        }
    }
}
