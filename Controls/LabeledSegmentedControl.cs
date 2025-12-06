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
                    BackgroundColor = Colors.LightGray,
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
                if (button.Text == SelectedItem)
                {
                    button.BackgroundColor = Colors.DodgerBlue;
                    button.TextColor = Colors.White;
                }
                else
                {
                    button.BackgroundColor = Colors.LightGray;
                    button.TextColor = Colors.Black;
                }
            }
        }
    }
}
