// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Examples.Fluid;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApi.Examples;

public sealed partial class CollabEditBox : UserControl
{
    private const string DocumentSharedStringName = "document";
    private const string SelectionsSharedMapName = "selections";

    private const string FluidServiceUri = "http://localhost:7070/";

    private readonly SynchronizationContext uiSyncContext;
    private readonly NodeEmbeddingThreadRuntime nodejs;
    private readonly JSMarshaller marshaller;

    private ITinyliciousClient fluidClient = null!;
    private string? clientId;
    private IFluidContainer? fluidContainer;
    private bool createdFluidContainer;
    private ISharedString? sharedDocument;
    private IDictionary<string, (int, int)>? sharedSelections;
    private string lastDocumentText = "\r";
    private (int, int) lastSelection = (0, 0);
    private IDictionary<string, CollabSelection> selections;

    public CollabEditBox()
    {
        InitializeComponent();

        this.selections = new Dictionary<string, CollabSelection>();

        this.uiSyncContext = SynchronizationContext.Current!;
        this.nodejs = App.Current.Nodejs;
        this.marshaller = new JSMarshaller { AutoCamelCase = true };

        try
        {
            LoadFluidClient(FluidServiceUri);
        }
        catch (Exception ex)
        {
            SetText("Failed to load Fluid Framework. Did you forget to run `npm install`?\r" + ex);
        }
    }

    private void LoadFluidClient(string fluidServiceUri)
    {
        // TODO: Replace code in this method with some form of [JSImport] and source-generation.
        this.nodejs.Run(() =>
        {
            JSValue logFunction = JSValue.CreateFunction("send", (args) =>
            {
                var e = this.marshaller.FromJS<TelemetryBaseEvent>(args[0]);
                Debug.WriteLine($"[fluid:{e.Category}] {e.EventName}");
                return JSValue.Undefined;
            });

            var uri = new Uri(fluidServiceUri);
            TinyliciousClientProps clientProps = new()
            {
                Connection = new()
                {
                    Domain = $"{uri.Scheme}://{uri.Host}",
                    Port = uri.Port,
                },
                Logger = new()
                {
                    Send = logFunction,
                },
            };

            JSValue tinyliciousClient =
                this.nodejs.Import("@fluidframework/tinylicious-client", "TinyliciousClient")
                .CallAsConstructor(this.marshaller.ToJS(clientProps));
            this.fluidClient = this.marshaller.FromJS<ITinyliciousClient>(tinyliciousClient);
        });
    }

    public string? SessionId { get; private set; }

    private JSValue ContainerSchema
    {
        get
        {
            // TODO: Improve marsahlling of this object.
            JSValue sharedStringClass = this.nodejs.Import("fluid-framework", "SharedString");
            JSValue sharedMapClass = this.nodejs.Import("fluid-framework", "SharedMap");
            Debug.Assert(sharedStringClass.IsFunction() && sharedMapClass.IsFunction());
            JSValue initialObjects = JSValue.CreateObject();
            initialObjects.SetProperty(DocumentSharedStringName, sharedStringClass);
            initialObjects.SetProperty(SelectionsSharedMapName, sharedMapClass);
            JSValue containerSchema = JSValue.CreateObject();
            containerSchema.SetProperty("initialObjects", initialObjects);
            return containerSchema;
        }
    }

    private ISharedString GetSharedDocument()
    {
        JSValue sharedString = this.fluidContainer!.InitialObjects.GetProperty(
            DocumentSharedStringName);

        // TODO: Automatic event marshalling.
        sharedString.CallMethod(
            "on",
            "sequenceDelta",
            JSValue.CreateFunction("sequenceDelta", OnSharedStringDelta));

        return this.marshaller.FromJS<ISharedString>(sharedString);
    }

    private IDictionary<string, (int, int)> GetSharedSelections()
    {
        JSValue sharedMap = this.fluidContainer!.InitialObjects.GetProperty(
            SelectionsSharedMapName);

        // TODO: Automatic event marshalling.
        sharedMap.CallMethod(
           "on",
           "valueChanged",
           JSValue.CreateFunction("valueChanged", OnSharedMapValueChanged));

        return ((JSMap)sharedMap).AsDictionary<string, (int, int)>(
            (key) => (string)key,
            (value) => ((int)((JSArray)value)[0], (int)((JSArray)value)[1]),
            (key) => (JSValue)key,
            (value) =>
            {
                JSArray array = new(2);
                array[0] = value.Item1;
                array[1] = value.Item2;
                return array;
            });
    }

    private Path? AssignCaret()
    {
        int availableCount = this.editCanvas.Children.OfType<Path>()
            .Count((c) => c.Visibility == Visibility.Collapsed);
        if (availableCount == 0) return null;

        int index = (int)new Random().NextInt64(availableCount);
        Path caret = this.editCanvas.Children.OfType<Path>()
            .Where((c) => c.Visibility == Visibility.Collapsed)
            .Skip(index)
            .First();
        caret.Visibility = Visibility.Visible;

        return caret;
    }

    public async Task CreateCollabSessionAsync(string text)
    {
        text = text.Replace("\r\n", "\r").Replace("\n", "\r");

        // Reset all selections and carets when starting a new session.
        this.selections.Clear();
        foreach (var child in this.editCanvas.Children.OfType<Path>())
        {
            child.Visibility = Visibility.Collapsed;
        }

        Path caret = AssignCaret()!;
        this.editBox.SelectionHighlightColor = (SolidColorBrush)caret.Fill;
        this.clientId = this.editBox.SelectionHighlightColor.Color.ToString();

        SessionId = await this.nodejs.RunAsync(async () =>
        {
            TinyliciousContainerInfo containerInfo =
                await this.fluidClient.CreateContainer(ContainerSchema);
            this.fluidContainer = containerInfo.Container;
            this.createdFluidContainer = true;

            TaskCompletionSource connectedCompletion = new();
            JSValue OnConnected(JSCallbackArgs args)
            {
                connectedCompletion.SetResult();
                return JSValue.Undefined;
            }
            JSInterface.GetJSValue(this.fluidContainer)!.Value.CallMethod(
                "once", "connected", JSValue.CreateFunction("connected", OnConnected));

            this.sharedDocument = GetSharedDocument();
            this.sharedDocument.InsertText(0, text, default);
            this.sharedSelections = GetSharedSelections();
            this.sharedSelections.Add(this.clientId, (0, 0));

            var timeoutToken = new CancellationTokenSource(5000).Token;
            try
            {
                string sessionId = await this.fluidContainer.Attach().WaitAsync(timeoutToken);
                await connectedCompletion.Task.WaitAsync(timeoutToken);
                return sessionId;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        });

        if (SessionId == null)
        {
            SetText($"Failed to connect to the Fluid service at {FluidServiceUri}");
            return;
        }

        SetText(text);
        this.editBox.Focus(FocusState.Programmatic);

        CollabSelection selection = new(caret);
        this.selections.Add(this.clientId, selection);
        UpdateSelection(selection);
    }

    public async Task ConnectCollabSessionAsync(string id)
    {
        KeyValuePair<string, (int, int)>[]? remoteSelections = null;
        string? text = await this.nodejs.RunAsync(async () =>
        {
            TinyliciousContainerInfo containerInfo =
                await this.fluidClient.GetContainer(id, ContainerSchema);
            this.fluidContainer = containerInfo.Container;

            TaskCompletionSource connectedCompletion = new();
            JSValue OnConnected(JSCallbackArgs args)
            {
                connectedCompletion.SetResult();
                return JSValue.Undefined;
            }
            JSInterface.GetJSValue(this.fluidContainer)!.Value.CallMethod(
                "once", "connected", JSValue.CreateFunction("connected", OnConnected));

            var timeoutToken = new CancellationTokenSource(5000).Token;
            try
            {
                await connectedCompletion.Task.WaitAsync(timeoutToken);
            }
            catch (OperationCanceledException)
            {
                return null;
            }

            this.sharedDocument = GetSharedDocument();
            string text = this.sharedDocument.GetText();

            // Assign caret colors for each of the remote participants' selections.
            this.sharedSelections = GetSharedSelections();
            remoteSelections = this.sharedSelections.ToArray();

            return text;
        });

        if (text == null)
        {
            SetText($"Failed to connect to the Fluid service at {FluidServiceUri}");
            return;
        }

        SetText(text);
        this.editBox.Focus(FocusState.Programmatic);

        // Assign and update carets for each of the remote selections.
        foreach (var (remoteClientId, remoteSelection) in remoteSelections!)
        {
            this.OnRemoteSelectionChanged(remoteClientId, remoteSelection);
        }

        Path caret = AssignCaret()!;
        this.editBox.SelectionHighlightColor = (SolidColorBrush)caret.Fill;
        this.clientId = this.editBox.SelectionHighlightColor.Color.ToString();
        CollabSelection selection = new(caret);
        this.selections.Add(this.clientId, selection);
        UpdateSelection(selection);
    }

    public void CloseCollabSession()
    {
        this.nodejs.Run(async () =>
        {
            if (this.clientId != null)
            {
                this.sharedSelections?.Remove(this.clientId);

                // Allow some time for the remove message to be sent.
                await Task.Delay(10);
            }

            if (!this.createdFluidContainer)
            {
                this.fluidContainer?.Disconnect();
            }

            this.fluidContainer?.Dispose();
        });
    }

    private void SetText(string text)
    {
        this.lastDocumentText = text + "\r"; // Edit box always adds another \r ?
        this.editBox.Document.SetText(TextSetOptions.None, text);
        this.editBox.Document.Selection.SetRange(0, 0);
    }

    private void OnEditBoxSelectionChanging(
        RichEditBox sender,
        RichEditBoxSelectionChangingEventArgs e)
    {
        if (this.clientId != null &&
            this.selections.TryGetValue(this.clientId, out CollabSelection? selection))
        {
            selection!.SelectionStart = e.SelectionStart;
            selection.SelectionLength = e.SelectionLength;
            UpdateSelection(selection);
        }

        this.editBox.Document.GetText(TextGetOptions.None, out string currentText);
        int currentLength = currentText.Length;

        var (lastSelectionStart, lastSelectionLength) = this.lastSelection;
        int previousLength = this.lastDocumentText.Length;
        int delta = currentLength - previousLength;

        if (lastSelectionLength > 0 && e.SelectionLength == 0 &&
            e.SelectionStart == lastSelectionStart + lastSelectionLength + delta &&
            delta != -lastSelectionLength &&
            (delta != 0 || currentText != this.lastDocumentText))
        {
            // Replace selection (typing, pasting, etc while text is selected) - Afterward the
            // selection is at the end of the replaced text.
            string replacementText = currentText.Substring(
                e.SelectionStart - (lastSelectionLength + delta),
                lastSelectionLength + delta);
            Debug.WriteLine(
                $"SelectionChanged({lastSelectionStart}, {lastSelectionLength}) =[{replacementText}]");

            OnTextChanged(lastSelectionStart, lastSelectionLength, replacementText);
        }
        else if (lastSelectionLength == 0 && e.SelectionLength > 0 &&
            (delta != 0 || currentText != this.lastDocumentText))
        {
            // Restore selection (undo after replace selection) - Afterward the selection contains
            // the restored text.
            string replacementText = currentText.Substring(e.SelectionStart, e.SelectionLength);
            int replaceIndex = lastSelectionStart - e.SelectionLength + delta;
            int replaceLength = e.SelectionLength - delta;
            Debug.WriteLine(
                $"SelectionChanged({replaceIndex}, {replaceLength}) =[{replacementText}]");

            OnTextChanged(replaceIndex, replaceLength, replacementText);
        }
        else if (delta > 0)
        {
            // Insertion (typing, pasting, etc) - Afterward the end of the selection is
            // assumed to be at the end of the inserted text.
            int insertIndex = e.SelectionStart + e.SelectionLength - delta;
            string insertText = currentText.Substring(insertIndex, delta);
            Debug.WriteLine($"SelectionChanged({insertIndex}, {delta}) +[{insertText}]");

            OnTextChanged(insertIndex, 0, insertText);
        }
        else if (delta < 0)
        {
            // Deletion (backspace, cut, etc) - Afterward the selection is assumed to be
            // 0 length and at the position of the deleted text.
            Debug.Assert(e.SelectionLength == 0);
            int deleteIndex = e.SelectionStart;
            int deleteLength = -delta;
            string deleteText = this.lastDocumentText.Substring(e.SelectionStart, -delta);
            Debug.WriteLine($"SelectionChanged({deleteIndex}, {deleteLength}) -[{deleteText}]");

            OnTextChanged(deleteIndex, deleteLength, string.Empty);
        }
        else
        {
            // Selection change
            Debug.WriteLine($"({e.SelectionStart}, {e.SelectionLength})");
        }

        if (this.clientId != null)
        {
            (int, int) selectionRange = (e.SelectionStart, e.SelectionLength);
            this.nodejs.Post(() =>
            {
                this.sharedSelections![this.clientId] = selectionRange;
            });
        }

        this.lastDocumentText = currentText;
        this.lastSelection = (e.SelectionStart, e.SelectionLength);
    }

    private void OnTextChanged(int index, int length, string text)
    {
        if (this.sharedDocument != null)
        {
            this.nodejs.Run(() =>
            {
                if (length == 0)
                {
                    this.sharedDocument?.InsertText(index, text);
                }
                else if (text.Length == 0)
                {
                    this.sharedDocument?.RemoveText(index, index + length);
                }
                else
                {
                    this.sharedDocument?.ReplaceText(index, index + length, text);
                }
            });
        }
    }

    private JSValue OnSharedStringDelta(JSCallbackArgs args)
    {
        var deltaEvent = this.marshaller.FromJS<SequenceDeltaEvent>(args[0]);

        Debug.WriteLine(
            $"SequenceDelta(IsLocal={deltaEvent.IsLocal}, ClientId={deltaEvent.ClientId})");

        if (!deltaEvent.IsLocal)
        {
            this.uiSyncContext.Post((_) => OnRemoteEdit(deltaEvent.OpArgs.Op), null);
        }

        return JSValue.Undefined;
    }

    private void OnRemoteEdit(MergeTreeOp op)
    {
        var (selectionStart, selectionLength) = this.lastSelection;
        if (op.Type == MergeTreeDeltaType.Insert && op.Pos1.HasValue && op.Seg is not null)
        {
            Debug.WriteLine($"    insert ({op.Pos1}) [{op.Seg}]");

            if (op.Pos1 <= selectionStart)
            {
                selectionStart += op.Seg.Length;
            }
            else if (op.Pos1 < selectionStart + selectionLength)
            {
                selectionLength += op.Seg.Length;
            }

            string newText = this.lastDocumentText.Substring(0, op.Pos1.Value) +
                op.Seg + this.lastDocumentText.Substring(op.Pos1.Value);
            this.lastDocumentText = newText;
            this.editBox.Document.SetText(
                TextSetOptions.None, newText.Substring(0, newText.Length - 1));
        }
        else if (op.Type == MergeTreeDeltaType.Remove && op.Pos1.HasValue && op.Pos2.HasValue)
        {
            Debug.WriteLine($"    remove ({op.Pos1},{op.Pos2})");

            if (op.Pos1 <= selectionStart)
            {
                // Deletion range sarts before the selection.

                if (op.Pos2 <= selectionStart)
                {
                    // Deletion range ends before the selection.
                    selectionStart -= (op.Pos2.Value - op.Pos1.Value);
                }
                else if (op.Pos2 < selectionStart + selectionLength)
                {
                    // Deletion range ends within the selection.
                    selectionLength = selectionStart + selectionLength - op.Pos2.Value;
                    selectionStart = op.Pos1.Value;
                }
                else
                {
                    // Deletion range fully includes the selection.
                    selectionStart = op.Pos1.Value;
                    selectionLength = 0;
                }
            }
            else if (op.Pos1 < selectionStart + selectionLength)
            {
                // Deletion range starts within the selection.

                if (op.Pos2 < selectionStart + selectionLength)
                {
                    // Deletion range is fully included by the selection.
                    selectionLength -= (op.Pos2.Value - op.Pos1.Value);
                }
                else
                {
                    // Deletion range ends beyond the selection.
                    selectionLength -= (selectionStart + selectionLength - op.Pos1.Value);
                }
            }

            string newText = this.lastDocumentText.Substring(0, op.Pos1.Value) +
                this.lastDocumentText.Substring(op.Pos2.Value);
            this.lastDocumentText = newText;
            this.editBox.Document.SetText(
                TextSetOptions.None, newText.Substring(0, newText.Length - 1));
        }
        else
        {
            Debug.WriteLine("    op type: " + op.Type);
            return;
        }

        this.lastSelection = (selectionStart, selectionLength);
        this.editBox.Document.Selection.SetRange(
            selectionStart, selectionStart + selectionLength);
    }

    private JSValue OnSharedMapValueChanged(JSCallbackArgs args)
    {
        var changedEvent = this.marshaller.FromJS<SharedMapValueChangedEvent>(args[0]);
        bool isLocal = (bool)args[1];

        if (!isLocal)
        {
            string clientId = changedEvent.Key;
            if (this.sharedSelections!.TryGetValue(clientId, out (int, int) selectionRange))
            {
                // Item was added or updated in the shared map.
                this.uiSyncContext.Post((_) =>
                {
                    OnRemoteSelectionChanged(clientId, selectionRange);
                }, null);
            }
            else
            {
                // Item was removed from the shared map.
                this.uiSyncContext.Post((_) =>
                {
                    if (this.selections.TryGetValue(clientId, out CollabSelection? selection))
                    {
                        selection.Caret.Visibility = Visibility.Collapsed;
                        this.selections.Remove(clientId);
                    }
                }, null);
            }
        }

        return JSValue.Undefined;
    }

    private void OnRemoteSelectionChanged(string clientId, (int, int) selectionRange)
    {
        if (!this.selections.TryGetValue(clientId, out CollabSelection? selection))
        {
            foreach (Path caret in this.editCanvas.Children.OfType<Path>()
                .Where((c) => c.Visibility == Visibility.Collapsed))
            {
                if (((SolidColorBrush)caret.Fill).Color.ToString() == clientId)
                {
                    caret.Visibility = Visibility.Visible;
                    selection = new CollabSelection(caret);
                    this.selections.Add(clientId, selection);
                    break;
                }
            }
        }

        if (selection != null)
        {
            selection.SelectionStart = selectionRange.Item1;
            selection.SelectionLength = selectionRange.Item2;
            UpdateSelection(selection);
        }
    }

    private void UpdateSelection(CollabSelection selection)
    {
        var selectionRange = this.editBox.Document.GetRange(
            selection.SelectionStart, selection.SelectionStart);
        selectionRange.GetPoint(
            HorizontalCharacterAlignment.Left,
            VerticalCharacterAlignment.Baseline,
            PointOptions.ClientCoordinates,
            out var selectionPoint);

        var padding = this.editBox.Padding;
        selection.Caret.SetValue(Canvas.LeftProperty, padding.Left + selectionPoint.X + 2);
        selection.Caret.SetValue(Canvas.TopProperty, padding.Top + selectionPoint.Y);

        // TODO: Render selection highlight.
    }

    private class CollabSelection
    {
        public CollabSelection(Path caret)
        {
            Caret = caret;
        }

        public Path Caret { get; }

        public int SelectionStart { get; set; }

        public int SelectionLength { get; set; }
    }
}
