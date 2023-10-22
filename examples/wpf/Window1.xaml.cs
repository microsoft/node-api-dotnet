using System;
using System.Threading;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Microsoft.JavaScript.NodeApi.Examples;

public partial class Window1 : Window
{
    private readonly string markdown;

    public static void CreateWebView2Window(string markdown)
    {
        StaThreadWrapper(() => { new Window1(markdown).ShowDialog(); });
    }

    private Window1(string markdown)
    {
        this.markdown = markdown;
        InitializeComponent();
    }

    private static void StaThreadWrapper(Action action)
    {
        var t = new Thread(o =>
        {
            action();
            ////System.Windows.Threading.Dispatcher.Run();
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        await webView.EnsureCoreWebView2Async(); // This will work just fine

        webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

        string html = $@"
	<!DOCTYPE html>
	<html lang=""en"">
	<body onload=""drawDiagram()"">
		<script type=""module"">
			import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.mjs';
			window.mermaid = mermaid;
		</script>
		<script>
			const drawDiagram = async function () {{
				mermaid.initialize({{ securityLevel: ""sandbox"" }})
				const graphDefinition = `{markdown}`;
				const {{ svg }} = await mermaid.render('graphDiv', graphDefinition);
				window.chrome.webview.postMessage(svg);
				document.getElementById('diagram').innerHTML = svg;
			}}
		</script>
		<div id=""diagram""></div>
	</body>
	</html>
	";
        webView.NavigateToString(html);
    }

    /// <summary>
    ///  Triggers when Mermaid svg is generated
    /// </summary>
    private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string data = e.TryGetWebMessageAsString();
        Console.Write(data);
        ////Close();
    }
}
