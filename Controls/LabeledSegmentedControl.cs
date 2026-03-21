using Microsoft.Maui.Controls;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace IndoorCO2MapAppV2.Controls
{
    public class LabeledSegmentedControl : VerticalStackLayout
    {
        public static readonly BindableProperty LabelProperty = BindableProperty.Create(
            nameof(Label), typeof(string), typeof(LabeledSegmentedControl), default(string), propertyChanged: OnLabelChanged);

        public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
            nameof(Items), typeof(IEnumerable<string>), typeof(LabeledSegmentedControl), default(IEnumerable<string>), propertyChanged: OnItemsChanged);

        public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
            nameof(SelectedItem), typeof(string), typeof(LabeledSegmentedControl), default(string), BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

        private readonly Label _labelControl;
        private readonly HorizontalStackLayout _buttonsLayout;
        private readonly List<Button> _buttons = new();

        private Color _selectedBg  = Color.FromArgb("#512BD4");
        private Color _selectedFg  = Colors.White;
        private Color _unselectedBg = Color.FromArgb("#E0E0E0");
        private Color _unselectedFg = Color.FromArgb("#616161");

        private void UpdateColors()
        {
            bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            _selectedBg  = Color.FromArgb("#512BD4");
            _selectedFg  = Colors.White;
            _unselectedBg = dark ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");
            _unselectedFg = dark ? Color.FromArgb("#BDBDBD") : Color.FromArgb("#616161");
        }

        public event EventHandler<string>? SelectionChanged;

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public IEnumerable<string> Items
        {
            get => (IEnumerable<string>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public string SelectedItem
        {
            get => (string)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly BindableProperty SelectionChangedCommandProperty =
    BindableProperty.Create(
        nameof(SelectionChangedCommand),
        typeof(ICommand),
        typeof(LabeledSegmentedControl),
        default(ICommand));

        public ICommand? SelectionChangedCommand
        {
            get => (ICommand?)GetValue(SelectionChangedCommandProperty);
            set => SetValue(SelectionChangedCommandProperty, value);
        }


        public LabeledSegmentedControl()
        {
            Spacing = 4;

            _labelControl = new Label();
            _buttonsLayout = new HorizontalStackLayout { Spacing = 4 };

            Children.Add(_labelControl);
            Children.Add(_buttonsLayout);

            UpdateColors();
            if (Application.Current != null)
                Application.Current.RequestedThemeChanged += (_, _) => { UpdateColors(); UpdateSelection(); };
        }

        private static void OnLabelChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (LabeledSegmentedControl)bindable;

            string text = (string)newValue;

            control._labelControl.Text = text;
            control._labelControl.IsVisible = !string.IsNullOrWhiteSpace(text); //hides Label if it has empty text
        }

        private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (LabeledSegmentedControl)bindable;
            control.UpdateButtons();
        }

        private static void OnSelectedItemChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (LabeledSegmentedControl)bindable;

            control.UpdateSelection();

            // old event is fine to keep
            control.SelectionChanged?.Invoke(control, (string)newValue);

            // NEW: trigger ICommand
            if (control.SelectionChangedCommand?.CanExecute(newValue) == true)
                control.SelectionChangedCommand.Execute(newValue);
        }



        private void UpdateButtons()
        {
            _buttonsLayout.Children.Clear();
            _buttons.Clear();

            if (Items == null) return;

            foreach (var item in Items)
            {
                var button = new Button
                {
                    Text = item,
                    Padding = new Thickness(10, 4),
                    BackgroundColor = _unselectedBg,
                    TextColor = _unselectedFg,
                };
                button.Clicked += (s, e) => SelectedItem = item;

                _buttons.Add(button);
                _buttonsLayout.Children.Add(button);
            }

            UpdateSelection();
        }

        private void UpdateSelection()
        {
            foreach (var button in _buttons)
            {
                button.BackgroundColor = button.Text == SelectedItem ? _selectedBg : _unselectedBg;
                button.TextColor       = button.Text == SelectedItem ? _selectedFg : _unselectedFg;
            }
        }
    }
}
