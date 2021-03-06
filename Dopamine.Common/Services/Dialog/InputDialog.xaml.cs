﻿using Dopamine.Common.Controls;
using Dopamine.Common.Presentation.Utils;
using Dopamine.Core.Base;
using System.Windows;

namespace Dopamine.Common.Services.Dialog
{
    public partial class InputDialog : DopamineWindow
    {
        #region Variables
        private string mResponseText;
        private bool mTextChanged;
        #endregion

        #region Properties
        public string ResponseText
        {
            get { return mResponseText; }
            set { mResponseText = value; }
        }
        #endregion

        #region Construction
        public InputDialog(int iconCharCode, int iconSize, string title, string content, string okText, string cancelText, string defaultResponse)
            : base()
        {

            // This call is required by the designer.
            InitializeComponent();

            // Add any initialization after the InitializeComponent() call.
            this.TitleBarHeight = Constants.DefaultWindowButtonHeight + 10;
            this.Icon.Text = char.ConvertFromUtf32(iconCharCode);
            this.Icon.FontSize = iconSize;
            this.Title = title;
            //Me.TextBlockTitle.Text = iTitle.ToUpper
            this.TextBlockTitle.Text = title;
            this.TextBlockContent.Text = content;
            this.ButtonOK.Content = okText;
            this.ButtonCancel.Content = cancelText;
            this.TextBoxResponse.Text = defaultResponse;

            WindowUtils.CenterWindow(this);
        }
        #endregion

        #region Events
        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            this.ResponseText = TextBoxResponse.Text;
            DialogResult = true;
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        #endregion
    }
}
