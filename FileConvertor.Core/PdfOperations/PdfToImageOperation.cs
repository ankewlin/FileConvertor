using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FileConvertor.Core.Enums;
using FileConvertor.Core.Models;
using PdfiumViewer;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// PDF 转图片：将 PDF 每页转换为图片（JPG/PNG）
    /// </summary>
    public class PdfToImageOperation : IPdfOperation
    {
        public string Name => "PDF 转图片";
        public string Description => "将 PDF 每页转换为 JPG 或 PNG 图片";
        public string Icon => "📷";
        public PdfOperationCategory Category => PdfOperationCategory.Convert;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => true;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("Format", "输出格式", ParameterType.Select, "JPG")
                {
                    Options = new object[] { "JPG", "PNG" }
                },
                new OperationParameter("Dpi", "分辨率 (DPI)", ParameterType.Select, "150")
                {
                    Options = new object[] { "72", "96", "150", "200", "300" }
                },
                new OperationParameter("Quality", "JPG 质量", ParameterType.Slider, 85)
                {
                    MinValue = 10,
                    MaxValue = 100
                },
                new OperationParameter("PageRange", "页面范围（留空=全部）", ParameterType.PageRange, "")
                {
                    Placeholder = "如: 1,3,5-10，留空转换全部页面"
                },
                new OperationParameter("OutputPrefix", "文件名前缀", ParameterType.Text, "page_")
                {
                    Placeholder = "输出文件名前缀"
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
                    return PdfOperationResult.Fail("请选择一个 PDF 文件");

                string inputFile = context.InputFiles[0];
                if (!File.Exists(inputFile))
                    return PdfOperationResult.Fail($"文件不存在: {inputFile}");

                string format = context.GetString("Format", "JPG").ToUpper();
                int dpi = int.Parse(context.GetString("Dpi", "150"));
                int quality = context.GetInt("Quality", 85);
                string prefix = context.GetString("OutputPrefix", "page_");
                string pageRangeStr = context.GetString("PageRange", "");

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputDir = Path.Combine(Path.GetDirectoryName(inputFile), $"{name}_images");
                }
                Directory.CreateDirectory(outputDir);

                string ext = format == "PNG" ? ".png" : ".jpg";
                var outputFiles = new List<string>();

                // 获取页面范围
                List<int> pagesToConvert;
                using (var pdf = PdfDocument.Load(inputFile))
                {
                    int totalPages = pdf.PageCount;

                    if (string.IsNullOrWhiteSpace(pageRangeStr))
                    {
                        pagesToConvert = new List<int>();
                        for (int i = 0; i < totalPages; i++)
                            pagesToConvert.Add(i);
                    }
                    else
                    {
                        // 注意：PageRangeParser 返回 1-based，需要转为 0-based
                        var oneBased = Utils.PageRangeParser.Parse(pageRangeStr, totalPages);
                        pagesToConvert = new List<int>();
                        foreach (int p in oneBased)
                            pagesToConvert.Add(p - 1);
                    }

                    if (pagesToConvert.Count == 0)
                        return PdfOperationResult.Fail("没有要转换的页面");

                    ImageCodecInfo jpegEncoder = null;
                    EncoderParameters jpegParams = null;
                    if (format == "JPG")
                    {
                        jpegEncoder = GetJpegEncoder();
                        jpegParams = new EncoderParameters(1);
                        jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                    }

                    for (int i = 0; i < pagesToConvert.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int pageIndex = pagesToConvert[i];

                        using (var image = pdf.Render(pageIndex, dpi, dpi, PdfRenderFlags.CorrectFromDpi))
                        {
                            string outPath = Path.Combine(outputDir, $"{prefix}{pageIndex + 1}{ext}");

                            if (format == "PNG")
                            {
                                image.Save(outPath, ImageFormat.Png);
                            }
                            else
                            {
                                if (jpegEncoder != null)
                                    image.Save(outPath, jpegEncoder, jpegParams);
                                else
                                    image.Save(outPath, ImageFormat.Jpeg);
                            }

                            outputFiles.Add(outPath);
                        }

                        progress?.Report((double)(i + 1) / pagesToConvert.Count);
                    }

                    if (jpegParams != null) jpegParams.Dispose();
                }

                progress?.Report(1.0);
                return PdfOperationResult.Ok(outputFiles);
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
