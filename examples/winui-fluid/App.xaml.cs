// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.JavaScript.NodeApi.Engines;

namespace Microsoft.JavaScript.NodeApi.Examples;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? window;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        // Node.js require() searches for modules/packages relative to the CWD.
        Environment.CurrentDirectory = Path.GetDirectoryName(typeof(App).Assembly.Location)!;

        string libnodePath = Path.Combine(
            Path.GetDirectoryName(typeof(App).Assembly.Location)!,
            "libnode.dll");
        NodejsPlatform nodePlatform = new(libnodePath);

        Node = nodePlatform.CreateEnvironment();
        if (Debugger.IsAttached)
        {
            int pid = Process.GetCurrentProcess().Id;
            Uri inspectionUri = Node.StartInspector();
            Debug.WriteLine(
                $"Node.js ({pid}) inspector listening at {inspectionUri.AbsoluteUri}");
        }

        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched normally by the end user.  Other entry points
    /// will be used such as when the application is launched to open a specific file.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = new MainWindow();
        window.Activate();

        window.Closed += OnMainWindowClosed;
    }

    private void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        Node.Dispose();
    }

    public static new App Current => (App)Application.Current;

    public NodejsEnvironment Node { get; }
}
