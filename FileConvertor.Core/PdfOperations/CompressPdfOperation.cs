using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// 压缩 PDF：通过降低内嵌图像质量和分辨率来减小文件大小
    /// </summary>
    public class CompressPdfOperation : IPdfOperation
    {
        public string Name => "压缩 PDF";
        public string Description => "压缩 PDF 文件大小，降低内嵌图像质量";
        public string Icon => "📦";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("Quality", "图像质量", ParameterType.Slider, 60)
                {
                    MinValue = 20,
                    MaxValue = 100
                },
                new OperationParameter("MaxDpi", "最大 DPI", ParameterType.Select, "150")
                {
                    Options = new object[] { "72", "96", "150", "200", "300" }
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则在原文件名后加 _compressed"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => CompressInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult CompressInternal(
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

                int quality = context.GetInt("Quality", 60);
                int maxDpi = int.Parse(context.GetString("MaxDpi", "150"));

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "");
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputName = $"{name}_compressed.pdf";
                }
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                long originalSize = new FileInfo(inputFile).Length;
                int imagesProcessed = 0;
                int imagesFound = 0;

                progress?.Report(0.1);

                using (var doc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Modify))
                {
                    // 遍历所有页面的资源，找到图像
                    foreach (PdfPage page in doc.Pages)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var resources = page.Elements.GetDictionary("/Resources");
                        if (resources == null) continue;

                        var xObjects = resources.Elements.GetDictionary("/XObject");
                        if (xObjects == null) continue;

                        foreach (var key in xObjects.Elements.Keys)
                        {
                            var item = xObjects.Elements[key] as PdfReference;
                            if (item == null) continue;

                            var xObj = item.Value as PdfDictionary;
                            if (xObj == null) continue;

                            // 检查是否是图像
                            string subType = xObj.Elements.GetName("/Subtype");
                            if (subType != "/Image") continue;

                            imagesFound++;

                            try
                            {
                                // 尝试获取图像流并重新压缩
                                var stream = xObj.Stream;
                                if (stream == null) continue;

                                // 只有 JPEG 格式的图像我们重新压缩
                                string filter = xObj.Elements.GetName("/Filter");
                                if (filter == "/DCTDecode") // JPEG
                                {
                                    byte[] imageBytes = stream.Value;
                                    if (imageBytes.Length > 5000) // 只处理大于 5KB 的图像
                                    {
                                        byte[] compressed = RecompressJpeg(imageBytes, quality, maxDpi);
                                        if (compressed != null && compressed.Length < imageBytes.Length)
                                        {
                                            stream.Value = compressed;
                                            imagesProcessed++;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // 跳过处理失败的图像
                            }
                        }
                    }

                    progress?.Report(0.8);
                    doc.Save(outputPath);
                }

                progress?.Report(1.0);

                long newSize = new FileInfo(outputPath).Length;
                double reduction = (1.0 - (double)newSize / originalSize) * 100;

                if (imagesFound == 0)
                    return PdfOperationResult.Ok(outputPath);

                return PdfOperationResult.Ok(outputPath);
            }
            catch (OperationCanceledException)
            {
                return PdfOperationResult.Fail("操作已取消");
            }
            catch (Exception ex)
            {
                return PdfOperationResult.Fail($"压缩失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重新压缩 JPEG 图像
        /// </summary>
        private static byte[] RecompressJpeg(byte[] jpegBytes, int quality, int maxDpi)
        {
            try
            {
                using (var ms = new MemoryStream(jpegBytes))
                using (var img = Image.FromStream(ms))
                {
                    // 检查 DPI，如果过高则缩放
                    float dpiX = img.HorizontalResolution;
                    float dpiY = img.VerticalResolution;

                    Bitmap bmp;
                    if (dpiX > maxDpi || dpiY > maxDpi)
                    {
                        // 计算新尺寸
                        double scale = Math.Min((double)maxDpi / dpiX, (double)maxDpi / dpiY);
                        int newWidth = (int)(img.Width * scale);
                        int newHeight = (int)(img.Height * scale);
                        bmp = new Bitmap(newWidth, newHeight);
                        bmp.SetResolution(maxDpi, maxDpi);

                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.CompositingQuality = CompositingQuality.HighQuality;
                            g.DrawImage(img, 0, 0, newWidth, newHeight);
                        }
                    }
                    else
                    {
                        bmp = new Bitmap(img);
                    }

                    // 保存为 JPEG
                    var encoder = GetJpegEncoder();
                    var encParams = new EncoderParameters(1);
                    encParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

                    using (var outMs = new MemoryStream())
                    {
                        bmp.Save(outMs, encoder, encParams);
                        bmp.Dispose();
                        return outMs.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            foreach (var codec in ImageCodecInfo.GetImageEncoders())
            {
                if (codec.MimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
                    return codec;
            }
            return null;
        }
    }
}
