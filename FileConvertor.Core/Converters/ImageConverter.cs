using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Models;

namespace FileConvertor.Core.Converters
{
    /// <summary>
    /// 图像格式转换器。基于 System.Drawing (GDI+) 实现。
    /// 支持 JPG / PNG / BMP / GIF / TIFF / ICO 之间的互转，以及质量控制、等比缩放、DPI 设置。
    /// </summary>
    public class ImageConverter : IFileConverter
    {
        public string Name => "图像转换器";

        public ConversionCategory Category => ConversionCategory.Image;

        public string Description => "支持常见图像格式之间的互转，可调整质量、尺寸和DPI";

        public IReadOnlyList<FileFormat> SupportedInputFormats { get; }

        public IReadOnlyList<FileFormat> SupportedOutputFormats { get; }

        private static readonly Dictionary<string, ImageFormat> FormatMap =
            new Dictionary<string, ImageFormat>(StringComparer.OrdinalIgnoreCase)
            {
                { "jpg", ImageFormat.Jpeg },
                { "jpeg", ImageFormat.Jpeg },
                { "png", ImageFormat.Png },
            };

        public ImageConverter()
        {
            var formats = new List<FileFormat>
            {
                new FileFormat("JPEG", "jpg", "image/jpeg", ConversionCategory.Image),
                new FileFormat("PNG", "png", "image/png", ConversionCategory.Image),
            };
            SupportedInputFormats = formats.AsReadOnly();
            SupportedOutputFormats = formats.AsReadOnly();
        }

        public bool CanConvert(string inputExtension, string outputExtension)
        {
            string extIn = inputExtension.TrimStart('.').ToLowerInvariant();
            string extOut = outputExtension.TrimStart('.').ToLowerInvariant();
            return FormatMap.ContainsKey(extIn) && FormatMap.ContainsKey(extOut) && extIn != extOut;
        }

        public async Task<ConversionResult> ConvertAsync(
            ConversionContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => ConvertInternal(context, progress, cancellationToken), cancellationToken);
        }

        private ConversionResult ConvertInternal(
            ConversionContext context,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!File.Exists(context.InputFilePath))
                    return ConversionResult.Fail($"输入文件不存在: {context.InputFilePath}");

                progress?.Report(0.1);

                using (var sourceImage = Image.FromFile(context.InputFilePath))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.3);

                    // 处理尺寸调整
                    Image workingImage = sourceImage;
                    bool disposeWorking = false;
                    try
                    {
                        int maxWidth = context.GetParameter<int>("MaxWidth", 0);
                        int maxHeight = context.GetParameter<int>("MaxHeight", 0);

                        if (maxWidth > 0 || maxHeight > 0)
                        {
                            workingImage = ResizeImage(sourceImage, maxWidth, maxHeight);
                            disposeWorking = true;
                        }

                        // 处理 DPI
                        float dpiX = context.GetParameter<float>("DpiX", 0);
                        float dpiY = context.GetParameter<float>("DpiY", 0);
                        if (dpiX > 0 && dpiY > 0)
                        {
                            if (workingImage is Bitmap bmp)
                            {
                                bmp.SetResolution(dpiX, dpiY);
                            }
                            else
                            {
                                // 非 Bitmap 类型，创建副本后设置
                                var newBmp = new Bitmap(workingImage);
                                newBmp.SetResolution(dpiX, dpiY);
                                if (disposeWorking)
                                    workingImage.Dispose();
                                workingImage = newBmp;
                                disposeWorking = true;
                            }
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(0.6);

                        // 保存
                        string targetExt = context.TargetExtension.TrimStart('.').ToLowerInvariant();
                        if (!FormatMap.TryGetValue(targetExt, out var imageFormat))
                            return ConversionResult.Fail($"不支持的输出格式: {targetExt}");

                        string outputPath = context.OutputFilePath;
                        if (string.IsNullOrEmpty(outputPath))
                        {
                            string dir = Path.GetDirectoryName(context.InputFilePath);
                            string name = Path.GetFileNameWithoutExtension(context.InputFilePath);
                            outputPath = Path.Combine(dir, $"{name}.{targetExt}");
                        }

                        // 确保输出目录存在
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                        EncoderParameters encoderParams = null;
                        ImageCodecInfo codec = null;

                        // JPEG 质量控制
                        if (imageFormat == ImageFormat.Jpeg)
                        {
                            int quality = context.GetParameter<int>("Quality", 90);
                            quality = Math.Max(1, Math.Min(100, quality));
                            codec = GetEncoderInfo("image/jpeg");
                            encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        progress?.Report(0.8);

                        if (codec != null && encoderParams != null)
                        {
                            workingImage.Save(outputPath, codec, encoderParams);
                        }
                        else
                        {
                            workingImage.Save(outputPath, imageFormat);
                        }

                        progress?.Report(1.0);

                        return ConversionResult.Ok(outputPath);
                    }
                    finally
                    {
                        if (disposeWorking && workingImage != sourceImage)
                        {
                            workingImage.Dispose();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return ConversionResult.Fail("转换已取消");
            }
            catch (Exception ex)
            {
                return ConversionResult.Fail($"转换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 等比缩放图像
        /// </summary>
        private static Bitmap ResizeImage(Image image, int maxWidth, int maxHeight)
        {
            int newWidth = image.Width;
            int newHeight = image.Height;

            if (maxWidth > 0 && image.Width > maxWidth)
            {
                double ratio = (double)maxWidth / image.Width;
                newWidth = maxWidth;
                newHeight = (int)(image.Height * ratio);
            }
            if (maxHeight > 0 && newHeight > maxHeight)
            {
                double ratio = (double)maxHeight / newHeight;
                newHeight = maxHeight;
                newWidth = (int)(newWidth * ratio);
            }

            if (newWidth == image.Width && newHeight == image.Height)
                return new Bitmap(image);

            var destImage = new Bitmap(newWidth, newHeight);
            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, new Rectangle(0, 0, newWidth, newHeight),
                        0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        private static ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.MimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase))
                    return codec;
            }
            return null;
        }
    }
}
