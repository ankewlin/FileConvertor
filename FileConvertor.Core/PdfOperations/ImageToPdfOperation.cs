using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// 图片转 PDF：将多张图片合并为一个 PDF 文件
    /// </summary>
    public class ImageToPdfOperation : IPdfOperation
    {
        public string Name => "图片转 PDF";
        public string Description => "将 JPG/PNG 等图片转换为 PDF，多张图片自动合并";
        public string Icon => "🖼️";
        public PdfOperationCategory Category => PdfOperationCategory.Convert;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "jpg", "jpeg", "png", "bmp", "tiff", "gif" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("PageSize", "页面大小", ParameterType.Select, "FitImage")
                {
                    Options = new object[] { "适应图片大小", "A4", "Letter" }
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "images.pdf")
                {
                    IsRequired = true,
                    Placeholder = "images.pdf"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => ConvertInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult ConvertInternal(
            PdfOperationContext context,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (context.InputFiles == null || context.InputFiles.Count == 0)
                    return PdfOperationResult.Fail("请至少选择一张图片");

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(context.InputFiles[0]);

                string outputName = context.GetString("OutputFileName", "images.pdf");
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                string pageSizeMode = context.GetString("PageSize", "适应图片大小");

                using (var pdf = new PdfDocument())
                {
                    int total = context.InputFiles.Count;
                    for (int i = 0; i < total; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string imageFile = context.InputFiles[i];
                        if (!File.Exists(imageFile))
                            continue;

                        using (var img = Image.FromFile(imageFile))
                        {
                            PdfPage page = pdf.AddPage();

                            switch (pageSizeMode)
                            {
                                case "A4":
                                    page.Width = XUnit.FromMillimeter(210);
                                    page.Height = XUnit.FromMillimeter(297);
                                    break;
                                case "Letter":
                                    page.Width = XUnit.FromMillimeter(215.9);
                                    page.Height = XUnit.FromMillimeter(279.4);
                                    break;
                                case "适应图片大小":
                                default:
                                    // 使用图片的像素大小，假设 96 DPI
                                    page.Width = XUnit.FromPoint(img.Width * 72.0 / img.HorizontalResolution);
                                    page.Height = XUnit.FromPoint(img.Height * 72.0 / img.VerticalResolution);
                                    break;
                            }

                            using (var gfx = XGraphics.FromPdfPage(page))
                            {
                                using (var xImg = XImage.FromFile(imageFile))
                                {
                                    if (pageSizeMode == "适应图片大小")
                                    {
                                        gfx.DrawImage(xImg, 0, 0, page.Width, page.Height);
                                    }
                                    else
                                    {
                                        // 等比缩放居中
                                        double imgRatio = (double)img.Width / img.Height;
                                        double pageRatio = page.Width.Point / page.Height.Point;

                                        double drawWidth, drawHeight, offsetX, offsetY;
                                        if (imgRatio > pageRatio)
                                        {
                                            // 图片更宽，以宽度为准
                                            drawWidth = page.Width.Point - 40; // 留 20pt 边距
                                            drawHeight = drawWidth / imgRatio;
                                            offsetX = 20;
                                            offsetY = (page.Height.Point - drawHeight) / 2;
                                        }
                                        else
                                        {
                                            // 图片更高，以高度为准
                                            drawHeight = page.Height.Point - 40;
                                            drawWidth = drawHeight * imgRatio;
                                            offsetX = (page.Width.Point - drawWidth) / 2;
                                            offsetY = 20;
                                        }

                                        gfx.DrawImage(xImg, offsetX, offsetY, drawWidth, drawHeight);
                                    }
                                }
                            }
                        }

                        progress?.Report((double)(i + 1) / total);
                    }

                    if (pdf.PageCount == 0)
                        return PdfOperationResult.Fail("没有成功添加任何图片");

                    pdf.Save(outputPath);
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
                return PdfOperationResult.Fail($"转换失败: {ex.Message}");
            }
        }
    }
}
