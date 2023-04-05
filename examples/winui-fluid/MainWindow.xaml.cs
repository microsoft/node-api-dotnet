// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.JavaScript.NodeApi.Engines;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.JavaScript.NodeApi.Examples;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "C# WinUI + JS Fluid Framework Demo";
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // The Fluid container must be disposed to allow the Node.js thread to exit.
        this.collabEditBox.CloseCollabSession();
    }

    private void OnSessionTextChanged(object sender, TextChangedEventArgs e)
    {
        string sessionId = this.sessionTextBox.Text.Trim();
        if (sessionId == this.collabEditBox.SessionId)
        {
            this.joinButton.Content = "Copy";
        }
        else if (sessionId.Length > 0)
        {
            this.joinButton.Content = "Join";
        }
        else
        {
            this.joinButton.Content = "Start";
        }
    }

    private async void OnJoinButtonClick(object sender, RoutedEventArgs e)
    {
        string sessionId = this.sessionTextBox.Text.Trim();
        if (sessionId == this.collabEditBox.SessionId)
        {
            DataPackage clipData = new();
            clipData.SetText(sessionId);
            Clipboard.SetContent(clipData);
        }
        else if (sessionId.Length > 0)
        {
            try
            {
                await this.collabEditBox.ConnectCollabSessionAsync(sessionId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
        else
        {
            try
            {
                await this.collabEditBox.CreateCollabSessionAsync(LoadDocument());
                this.sessionTextBox.Text = this.collabEditBox.SessionId;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }

    private static string LoadDocument()
    {
        var resourceName = $"{typeof(MainWindow).Namespace}.README.md";
        var readmeStream = typeof(MainWindow).Assembly.GetManifestResourceStream(resourceName)!;
        return new StreamReader(readmeStream).ReadToEnd();
    }
}
