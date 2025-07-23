﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Numerics;
using System.Windows.Media.Animation;
using launcher.Core;
using static launcher.Core.UiReferences;
using static launcher.Core.Application;
using launcher.Configuration;
using launcher.Core.Models;

namespace launcher
{
    public partial class Popup_Tour : UserControl
    {
        private int currentIndex = 0;

        public Popup_Tour()
        {
            InitializeComponent();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (currentIndex + 1 >= DataCollections.OnBoardingItems.Count)
            {
                EndTour();
                return;
            }

            SetItem(currentIndex + 1);
        }

        public void SetItem(int index)
        {
            currentIndex = index;

            Next.Content = "Next";
            Skip.Visibility = Visibility.Visible;

            if (currentIndex == DataCollections.OnBoardingItems.Count - 1)
            {
                Next.Content = "Finish";
                Skip.Visibility = Visibility.Hidden;
            }

            OnBoardingItem item = DataCollections.OnBoardingItems[index];

            Title.Text = item.Title;
            Desc.Text = item.Description;
            Page.Text = $"{index + 1} of {DataCollections.OnBoardingItems.Count}";

            if (OnBoard_Control.RenderTransform is TransformGroup transformGroup)
            {
                var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                AnimateTranslate(translateTransform, item.translatePos);
                AnimateGeoRect(OnBoardingClip, item.geoRect);
            }
        }

        private static void AnimateTranslate(TranslateTransform translateTransform, Vector2 xy)
        {
            if (translateTransform == null)
            {
                System.Diagnostics.Debug.WriteLine("TranslateTransform is null. Cannot animate.");
                return;
            }

            // Determine animation speed
            double speed = (bool)IniSettings.Get(IniSettings.Vars.Disable_Transitions) ? 1 : 400;

            // Animate X property
            var moveXAnimation = new DoubleAnimation
            {
                From = translateTransform.X,
                To = xy.X,
                Duration = new Duration(TimeSpan.FromMilliseconds(speed)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            // Animate Y property
            var moveYAnimation = new DoubleAnimation
            {
                From = translateTransform.Y,
                To = xy.Y,
                Duration = new Duration(TimeSpan.FromMilliseconds(speed)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            // Apply animations directly
            translateTransform.BeginAnimation(TranslateTransform.XProperty, moveXAnimation);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, moveYAnimation);
        }

        private static void AnimateGeoRect(RectangleGeometry geo, Rect newRect)
        {
            if (geo == null)
            {
                System.Diagnostics.Debug.WriteLine("RectangleGeometry is null. Cannot animate.");
                return;
            }

            // Ensure the geometry is not frozen
            if (geo.IsFrozen)
            {
                // Clone the geometry to make it unfrozen
                geo = geo.Clone();
            }

            double speed = (bool)IniSettings.Get(IniSettings.Vars.Disable_Transitions) ? 1 : 400;

            var rectAnimation = new RectAnimation
            {
                From = geo.Rect,
                To = newRect,
                Duration = new Duration(TimeSpan.FromMilliseconds(speed)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            // Apply the animation directly
            geo.BeginAnimation(RectangleGeometry.RectProperty, rectAnimation);
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            EndTour();
        }
    }
}