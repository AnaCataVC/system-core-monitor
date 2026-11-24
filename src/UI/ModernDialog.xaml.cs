using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SystemCoreMonitor.Core;

namespace SystemCoreMonitor.UI
{
    public enum DialogKind
    {
        Info,
        Warning,
        Danger,
        Success
    }

    public partial class ModernDialog : Window
    {
        public bool Confirmed { get; private set; }

        public ModernDialog(
            string message,
            string title,
            DialogKind kind,
            bool showCancel,
            string? confirmText = null,
            string? cancelText = null)
        {
            InitializeComponent();

            TxtTitle.Text = title;
            TxtMessage.Text = message;

            bool isEs = LocalizationManager.CurrentLanguage == "es";
            BtnConfirm.Content = confirmText ?? (showCancel ? (isEs ? "Sí" : "Yes") : (isEs ? "Aceptar" : "OK"));
            BtnCancel.Content = cancelText ?? (isEs ? "Cancelar" : "Cancel");
            BtnCancel.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

            ApplyVisualKind(kind);

            var mainWin = Application.Current?.MainWindow;
            if (mainWin != null && mainWin != this && mainWin.IsVisible)
            {
                Owner = mainWin;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void ApplyVisualKind(DialogKind kind)
        {
            string accentKey;
            string iconKey;

            switch (kind)
            {
                case DialogKind.Danger:
                    accentKey = "StatusCrit";
                    iconKey = "IconAlert";
                    break;
                case DialogKind.Warning:
                    accentKey = "StatusWarn";
                    iconKey = "IconAlert";
                    break;
                case DialogKind.Success:
                    accentKey = "StatusOk";
                    iconKey = "IconCheck";
                    break;
                default:
                    accentKey = "AccentCpu";
                    iconKey = "IconInfo";
                    break;
            }

            var accentBrush = TryFindResource(accentKey) as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(147, 168, 253));
            var iconGeom = TryFindResource(iconKey) as Geometry;

            IconPath.Data = iconGeom;
            IconPath.Fill = accentBrush;
            IconBadge.Background = new SolidColorBrush(Color.FromArgb(38, accentBrush.Color.R, accentBrush.Color.G, accentBrush.Color.B));

            BtnConfirm.Background = accentBrush;
            BtnConfirm.Foreground = new SolidColorBrush(Color.FromRgb(16, 18, 26));
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }

        public static bool Confirm(
            string message,
            string title,
            DialogKind kind = DialogKind.Warning,
            string? confirmText = null,
            string? cancelText = null)
        {
            var dlg = new ModernDialog(message, title, kind, showCancel: true, confirmText: confirmText, cancelText: cancelText);
            return dlg.ShowDialog() == true && dlg.Confirmed;
        }

        public static void Info(
            string message,
            string title,
            DialogKind kind = DialogKind.Info,
            string? okText = null)
        {
            var dlg = new ModernDialog(message, title, kind, showCancel: false, confirmText: okText);
            dlg.ShowDialog();
        }
    }
}
