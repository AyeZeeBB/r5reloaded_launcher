using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Numerics;
using System.Windows.Media.Animation;
using static launcher.Core.AppContext;
using static launcher.Core.AppController;
using launcher.Core.Models;
using launcher.Services;

namespace launcher
{
    public partial class Popup_Tour : UserControl
    {
        private static List<TourStep> TourSteps { get; } = [
            new TourStep("Launcher Menu", "Quick access to settings and useful resources can be found in this menu.", new Rect(1,1,24,14), new Vector2(6,64)),
            new TourStep("Service Status", "Monitor the status of R5R services here. If there are any performance or service interruptions, you will see it here.", new Rect(200,1,33,14), new Vector2(562,64)),
            new TourStep("Downloads And Tasks", "Follow the progress of your game downloads / updates.", new Rect(236,1,38,14), new Vector2(745,64)),
            new TourStep("channels And Installing", "Here you can select the release channel you want to install, update, or play", new Rect(20,75,71,63), new Vector2(86,538)),
            new TourStep("Game Settings", "Clicking this allows you to access advanced settings for the selected release channel, as well as verify game files or uninstall.", new Rect(75,101,16,16), new Vector2(334,455)),
            new TourStep("News And Updates", "View latest updates, patch notes, guides, and anything else related to R5Reloaded straight from the R5R Team.", new Rect(102,77,190,116), new Vector2(455,128)),
            new TourStep("You're All Set", "You've successfully completed the Launcher Tour. If you have any questions or need further assistance, feel free to join our discord!", new Rect(135,95,0,0), new Vector2(430,305)),
            ];

        private int _tourStep = 0;

        public Popup_Tour()
        {
            InitializeComponent();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_tourStep + 1 >= TourSteps.Count)
            {
                EndTour();
                return;
            }

            SetItem(_tourStep + 1);
        }

        public void SetItem(int index)
        {
            _tourStep = index;

            Next.Content = _tourStep == TourSteps.Count - 1 ? "Finish" : "Next";
            Skip.Visibility = _tourStep == TourSteps.Count - 1 ? Visibility.Hidden : Visibility.Visible;

            TourStep item = TourSteps[index];

            Title.Text = item.Title;
            Desc.Text = item.Description;
            Page.Text = $"{index + 1} of {TourSteps.Count}";

            if (OnBoard_Control.RenderTransform is not TransformGroup transformGroup) return;

            var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (translateTransform != null)
            {
                Animate(translateTransform, TranslateTransform.XProperty, item.translatePos.X);
                Animate(translateTransform, TranslateTransform.YProperty, item.translatePos.Y);
            }
            Animate(OnBoardingClip, RectangleGeometry.RectProperty, item.geoRect);
        }

        private static void Animate(TranslateTransform target, DependencyProperty property, double to)
        {
            double speed = (bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions) ? 1 : 400;
            var animation = new DoubleAnimation
            {
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(speed)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            target.BeginAnimation(property, animation);
        }

        private static void Animate(RectangleGeometry target, DependencyProperty property, Rect to)
        {
            double speed = (bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions) ? 1 : 400;
            var animation = new RectAnimation
            {
                To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(speed)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            target.BeginAnimation(property, animation);
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            EndTour();
        }
    }
}