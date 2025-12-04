
namespace IndoorCO2MapAppV2.Controls
{
    public class LabeledCheckBox : HorizontalStackLayout
    {
        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text), typeof(string), typeof(LabeledCheckBox), default(string), propertyChanged: OnTextChanged);

        public static readonly BindableProperty IsCheckedProperty = BindableProperty.Create(
            nameof(IsChecked), typeof(bool), typeof(LabeledCheckBox), false, BindingMode.TwoWay, propertyChanged: OnIsCheckedChanged);

        private readonly CheckBox _checkBox;
        private readonly Label _label;

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public LabeledCheckBox()
        {
            Spacing = 8;
            VerticalOptions = LayoutOptions.Center;

            _checkBox = new CheckBox();
            _checkBox.CheckedChanged += (s, e) => IsChecked = e.Value;

            _label = new Label
            {
                VerticalTextAlignment = TextAlignment.Center
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => _checkBox.IsChecked = !_checkBox.IsChecked;
            _label.GestureRecognizers.Add(tap);

            Children.Add(_checkBox);
            Children.Add(_label);
        }

        private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (LabeledCheckBox)bindable;
            control._label.Text = (string)newValue;
        }

        private static void OnIsCheckedChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (LabeledCheckBox)bindable;
            control._checkBox.IsChecked = (bool)newValue;
        }
    }
}
