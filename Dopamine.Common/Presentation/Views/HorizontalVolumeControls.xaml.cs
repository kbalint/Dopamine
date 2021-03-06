﻿using Dopamine.Common.Services.Playback;
using Dopamine.Core.Logging;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Dopamine.Common.Presentation.Views
{
    public partial class HorizontalVolumeControls : UserControl
    {
        #region Variables
        private IPlaybackService playBackService;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty ShowPercentProperty = DependencyProperty.Register("ShowPercent", typeof(bool), typeof(HorizontalVolumeControls), new PropertyMetadata(true));
        public static readonly DependencyProperty SliderLengthProperty = DependencyProperty.Register("SliderLength", typeof(double), typeof(HorizontalVolumeControls), new PropertyMetadata(100.0));
        #endregion

        #region Properties
        public bool ShowPercent
        {
            get { return Convert.ToBoolean(GetValue(ShowPercentProperty)); }

            set { SetValue(ShowPercentProperty, value); }
        }

        public double SliderLength
        {
            get { return Convert.ToDouble(GetValue(SliderLengthProperty)); }

            set { SetValue(SliderLengthProperty, value); }
        }
        #endregion

        #region Construction
        public HorizontalVolumeControls()
        {
            InitializeComponent();

            // We need a parameterless constructor to be able to use this UserControl in other UserControls without dependency injection.
            // So for now there is no better solution than to find the EventAggregator by using the ServiceLocator.
            this.playBackService = ServiceLocator.Current.GetInstance<IPlaybackService>();
        }
        #endregion

        #region Private
        private void StackPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                this.playBackService.Volume = Convert.ToSingle(this.playBackService.Volume + ((double)e.Delta / 5000));
            }
            catch (Exception ex)
            {
                LogClient.Instance.Logger.Error("There was a problem changing the volume by mouse scroll. Exception: {0}", ex.Message);
            }
        }
        #endregion

    }
}
