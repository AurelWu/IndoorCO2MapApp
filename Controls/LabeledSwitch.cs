using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Controls
{
    public class LabeledSwitch : HorizontalStackLayout
    {
        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text), typeof(string), typeof(LabeledSwitch), default(string), propertyChanged: OnTextChanged);

        public static readonly BindableProperty IsToggledProperty = BindableProperty.Create(
            nameof(IsToggled), typeof(bool), typeof(LabeledSwitch), false, BindingMode.TwoWay, propertyChanged: OnIsToggledChanged);

        private readonly Switch _switch;
        private readonly Label _label;

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public bool IsToggled
        {
            get => (bool)GetValue(IsToggledProperty);
            set => SetValue(IsToggledProperty, value);
        }

        public LabeledSwitch()
        {
            Spacing = 8;
            VerticalOptions = LayoutOptions.Center;

            _switch = new Switch();
            _switch.Toggled += (s, e) => IsToggled = e.Value;

            _label = new Label
            {
                VerticalTextAlignment = TextAlignment.Center
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => _switch.IsToggled = !_switch.IsToggled;
            _label.GestureRecognizers.Add(tap);

            Children.Add(_label);
            Children.Add(_switch);
        }

        private static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (LabeledSwitch)bindable;
            control._label.Text = (string)newValue;
        }

        private static void OnIsToggledChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (LabeledSwitch)bindable;
            control._switch.IsToggled = (bool)newValue;
        }
    }
}