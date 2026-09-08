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
using PdfSharp.Pdf.Security;

namespace FileConvertor.Core.PdfOperations
{
    /// <summary>
    /// 加密 PDF：设置打开密码
    /// </summary>
    public class EncryptPdfOperation : IPdfOperation
    {
        public string Name => "加密 PDF";
        public string Description => "为 PDF 文件设置打开密码保护";
        public string Icon => "🔒";
        public PdfOperationCategory Category => PdfOperationCategory.Edit;
        public bool RequiresMultipleInputs => false;
        public bool ProducesMultipleOutputs => false;

        public IReadOnlyList<string> SupportedInputExtensions { get; } =
            new List<string> { "pdf" }.AsReadOnly();

        public IReadOnlyList<OperationParameter> Parameters { get; } =
            new List<OperationParameter>
            {
                new OperationParameter("UserPassword", "打开密码", ParameterType.Text, "")
                {
                    IsRequired = true,
                    Placeholder = "请输入打开密码"
                },
                new OperationParameter("OwnerPassword", "权限密码（可选）", ParameterType.Text, "")
                {
                    Placeholder = "用于权限控制的所有者密码"
                },
                new OperationParameter("OutputFileName", "输出文件名", ParameterType.Text, "")
                {
                    Placeholder = "留空则在原文件名后加 _encrypted"
                }
            }.AsReadOnly();

        public async Task<PdfOperationResult> ExecuteAsync(
            PdfOperationContext context,
            IProgress<double> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => EncryptInternal(context, progress, cancellationToken), cancellationToken);
        }

        private PdfOperationResult EncryptInternal(
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

                string userPassword = context.GetString("UserPassword", "");
                if (string.IsNullOrWhiteSpace(userPassword))
                    return PdfOperationResult.Fail("请输入打开密码");

                string ownerPassword = context.GetString("OwnerPassword", "");
                if (string.IsNullOrWhiteSpace(ownerPassword))
                    ownerPassword = userPassword;

                string outputDir = context.OutputDirectory;
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Path.GetDirectoryName(inputFile);

                string outputName = context.GetString("OutputFileName", "");
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    string name = Path.GetFileNameWithoutExtension(inputFile);
                    outputName = $"{name}_encrypted.pdf";
                }
                if (!outputName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    outputName += ".pdf";

                string outputPath = Path.Combine(outputDir, outputName);
                Directory.CreateDirectory(outputDir);

                Logger.Debug($"加密PDF: 输入={Path.GetFileName(inputFile)}, 打开密码长度={userPassword.Length}, 权限密码长度={ownerPassword.Length}");

                progress?.Report(0.1);

                // 打开源文档（导入模式）
                using (var sourceDoc = PdfReader.Open(inputFile, PdfDocumentOpenMode.Import))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.3);

                    // 创建新文档并设置密码（新建文档的加密更可靠）
                    using (var outputDoc = new PdfDocument())
                    {
                        // 复制所有页面
                        for (int i = 0; i < sourceDoc.PageCount; i++)
                        {
                            outputDoc.AddPage(sourceDoc.Pages[i]);
                        }

                        // 设置安全设置
                        var security = outputDoc.SecuritySettings;
                        security.UserPassword = userPassword;
                        security.OwnerPassword = ownerPassword;

                        // 尝试设置加密级别（PdfSharp 1.5 可能通过反射可用）
                        try
                        {
                            // 尝试设置 128 位加密（如果支持）
                            var secType = security.GetType();
                            var levelProp = secType.GetProperty("DocumentSecurityLevel");
                            if (levelProp != null)
                            {
                                // PdfDocumentSecurityLevel.Encrypted128Bit 通常值为 2
                                var enumType = levelProp.PropertyType;
                                if (enumType.IsEnum)
                                {
                                    var values = Enum.GetValues(enumType);
                                    object highest = null;
                                    foreach (var v in values)
                                    {
                                        highest = v; // 取最后一个（通常加密级别最高）
                                    }
                                    if (highest != null)
                                        levelProp.SetValue(security, highest, null);
                                }
                            }
                        }
                        catch { /* 忽略，使用默认加密 */ }

                        progress?.Report(0.8);
                        outputDoc.Save(outputPath);
                    }
                }

                // 验证：用密码重新打开确认加密成功
                try
                {
                    using (var verify = PdfReader.Open(outputPath, userPassword, PdfDocumentOpenMode.InformationOnly))
                    {
                        Logger.Debug($"加密PDF验证成功: 输出文件可使用密码打开，共 {verify.PageCount} 页");
                    }
                }
                catch (Exception vex)
                {
                    Logger.Warning($"加密PDF验证失败: 无法用设置的密码打开输出文件", vex);
                    return PdfOperationResult.Fail("加密失败：生成的文件无法用设置的密码打开，请重试");
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
                Logger.Error("加密PDF失败", ex);
                return PdfOperationResult.Fail($"加密失败: {ex.Message}");
            }
        }
    }
}
