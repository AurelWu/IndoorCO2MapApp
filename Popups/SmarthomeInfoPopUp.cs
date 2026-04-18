using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using CommunityToolkit.Maui.Views;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Resources.Strings;


namespace IndoorCO2MapAppV2.Popups
{
    partial class SmarthomeInfoPopUp : Popup
    {
        public SmarthomeInfoPopUp()
        {
            var display = DeviceDisplay.MainDisplayInfo;
            double screenW = display.Width / display.Density;
            double screenH = display.Height / display.Density;

            var description1 = new Label
            {
                Text = Localisation.SmartHomeStep1,
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Center
            };

            var image1 = new Image
            {
                Source = "aranetlogo.png",
                HeightRequest = 64,
                WidthRequest = 64,
                HorizontalOptions = LayoutOptions.Center
            };

            var description2 = new Label
            {
                Text = Localisation.SmartHomeStep2,
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Center
            };

            var image2 = new Image
            {
                Source = "smarthome_gears.png",
                HeightRequest = 125,
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Center
            };

            var description3 = new Label
            {
                Text = Localisation.SmartHomeStep3,
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Center
            };

            var image3 = new Image
            {
                Source = "smarthome_slider.png",
                HeightRequest = 200,
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Center
            };

            var closeButton = new Button
            {
                Text = Localisation.SmartHomeClose,
                Command = new Command(() => this.CloseAsync().SafeFireAndForget())
            };

            var stack = new VerticalStackLayout
            {
                Padding = new Thickness(12, 8),
                Spacing = 8,
                BackgroundColor = Color.FromArgb("#F0F8FF"),
                HorizontalOptions = LayoutOptions.Fill,
                Children = { description1, image1, description2, image2, description3, image3, closeButton }
            };

            var scrollView = new ScrollView
            {
                Content = stack,
                WidthRequest = screenW - 40,
                HeightRequest = screenH * 0.8
            };

            var tapGestureRecognizer = new TapGestureRecognizer();
            tapGestureRecognizer.Tapped += (s, e) => this.CloseAsync().SafeFireAndForget();
            stack.GestureRecognizers.Add(tapGestureRecognizer);

            Content = scrollView;
        }
    }
}
