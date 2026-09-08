using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileConvertor.Core;
using FileConvertor.Core.Utils;
using FileConvertor.ViewModels;

namespace FileConvertor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
        }

        #region 首页卡片点击

        private void Card_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border == null) return;

            var operation = border.DataContext as IPdfOperation;
            if (operation != null)
            {
                _vm.NavigateToOperation(operation);
            }
        }

        private void ImageConvert_Click(object sender, MouseButtonEventArgs e)
        {
            // 图像转换：先显示提示，后续再做详情页
            string msg = "图像转换功能即将支持更丰富的详情页，目前仍在优化中。\n\n您可以通过 PDF 工具中的「图片转 PDF」功能将图片转为 PDF。";
            Logger.Info($"[弹窗-提示] 图像转换: {msg}");
            MessageBox.Show(msg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 操作详情页拖拽

        private void OpDropArea_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                var border = sender as Border;
                if (border != null)
                {
                    border.BorderBrush = System.Windows.Media.Brushes.SteelBlue;
                    border.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                        .ConvertFromString("#F0F7FF");
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OpDropArea_DragLeave(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                border.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                    .ConvertFromString("#D0D0D0");
                border.Background = System.Windows.Media.Brushes.White;
            }
        }

        private void OpDropArea_Drop(object sender, DragEventArgs e)
        {
            var border = sender as Border;
            if (border != null)
            {
                border.BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                    .ConvertFromString("#D0D0D0");
                border.Background = System.Windows.Media.Brushes.White;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0 && _vm.CurrentView is OperationViewModel opVm)
                {
                    opVm.AddFiles(files);
                }
            }
        }

        private void OpDropArea_Click(object sender, MouseButtonEventArgs e)
        {
            if (_vm.CurrentView is OperationViewModel opVm)
            {
                opVm.SelectFilesCommand.Execute(null);
            }
        }

        #endregion
    }
}
