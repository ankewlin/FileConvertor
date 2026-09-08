using System;
using System.IO;
using System.Linq;
using System.Windows;
using FileConvertor.Core.Utils;
using Microsoft.Win32;

namespace FileConvertor.Services
{
    /// <summary>
    /// 对话框服务：封装文件/目录选择对话框 + 消息弹窗日志记录
    /// </summary>
    public class DialogService
    {
        /// <summary>
        /// 打开多文件选择对话框
        /// </summary>
        /// <param name="filter">文件过滤器，如 "图像文件|*.jpg;*.png;*.bmp"</param>
        /// <returns>选中的文件路径数组，取消则返回 null</returns>
        public string[] OpenFiles(string filter = "所有文件|*.*")
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = true,
                Title = "选择文件"
            };

            if (dialog.ShowDialog() == true)
                return dialog.FileNames;
            return null;
        }

        /// <summary>
        /// 选择目录
        /// </summary>
        public string SelectFolder(string initialPath = null)
        {
            // 使用 FolderBrowserDialog（需要引用 System.Windows.Forms）
            // 这里用一个简单的 WinForms dialog
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择输出目录";
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
                    dialog.SelectedPath = initialPath;

                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                    return dialog.SelectedPath;
            }
            return null;
        }

        /// <summary>
        /// 显示消息框
        /// </summary>
        public void ShowMessage(string message, string title = "提示", MessageBoxImage icon = MessageBoxImage.Information)
        {
            // 根据图标类型记入日志
            switch (icon)
            {
                case MessageBoxImage.Error:
                    Logger.Error($"[弹窗-错误] {title}: {message}");
                    break;
                case MessageBoxImage.Warning:
                    Logger.Warning($"[弹窗-警告] {title}: {message}");
                    break;
                case MessageBoxImage.Information:
                case MessageBoxImage.None:
                default:
                    Logger.Info($"[弹窗-提示] {title}: {message}");
                    break;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        /// <summary>
        /// 询问确认
        /// </summary>
        public bool Confirm(string message, string title = "确认")
        {
            Logger.Info($"[弹窗-确认] {title}: {message}");
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
    }
}
