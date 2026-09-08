using System;
using System.Windows;
using FileConvertor.Core.Models;

namespace FileConvertor.ViewModels
{
    /// <summary>
    /// 参数项 ViewModel，用于动态生成参数编辑控件
    /// </summary>
    public class ParameterItemViewModel : ViewModelBase
    {
        private object _value;
        private bool _visible = true;

        /// <summary>参数定义</summary>
        public OperationParameter Definition { get; }

        /// <summary>参数值</summary>
        public object Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>是否可见（用于条件显示）</summary>
        public bool Visible
        {
            get => _visible;
            set
            {
                if (SetProperty(ref _visible, value))
                    OnPropertyChanged(nameof(Visibility));
            }
        }

        /// <summary>可见性（方便 XAML 直接绑定）</summary>
        public Visibility Visibility => Visible ? Visibility.Visible : Visibility.Collapsed;

        public string Key => Definition.Key;
        public string Label => Definition.Label;
        public Core.Enums.ParameterType Type => Definition.Type;
        public object[] Options => Definition.Options;
        public double MinValue => Definition.MinValue;
        public double MaxValue => Definition.MaxValue;
        public string Placeholder => Definition.Placeholder;

        /// <summary>数值的整型表示（用于 Slider 绑定）</summary>
        public int IntValue
        {
            get
            {
                if (_value == null) return 0;
                if (_value is int i) return i;
                if (double.TryParse(_value.ToString(), out double d)) return (int)d;
                return 0;
            }
            set
            {
                _value = value;
                OnPropertyChanged(nameof(IntValue));
                OnPropertyChanged(nameof(Value));
            }
        }

        /// <summary>字符串值</summary>
        public string StringValue
        {
            get => _value?.ToString() ?? "";
            set
            {
                _value = value;
                OnPropertyChanged(nameof(StringValue));
                OnPropertyChanged(nameof(Value));
            }
        }

        /// <summary>布尔值</summary>
        public bool BoolValue
        {
            get
            {
                if (_value is bool b) return b;
                return false;
            }
            set
            {
                _value = value;
                OnPropertyChanged(nameof(BoolValue));
                OnPropertyChanged(nameof(Value));
            }
        }

        public ParameterItemViewModel(OperationParameter definition)
        {
            Definition = definition;
            _value = definition.DefaultValue;
        }
    }
}
