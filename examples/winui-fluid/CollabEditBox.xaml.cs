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

namespace Microsoft.JavaScript.NodeApi.Examples;

public sealed partial class CollabEditBox : UserControl
{
    private const string DocumentSharedStringName = "document";
    private const string SelectionsSharedMapName = "selections";

    private readonly SynchronizationContext uiSyncContext;
    private readonly JSSynchronizationContext jsSyncContext;
    private readonly JSMarshaller marshaller;

    private JSReference fluid = null!;
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
        this.jsSyncContext = App.Current.Node.SynchronizationContext;
        this.marshaller = new JSMarshaller { AutoCamelCase = true };

        LoadFluid();
        LoadTinylicious();
    }

    private void LoadFluid()
    {
        this.jsSyncContext.Post(() =>
        {
            // TODO: Replace with [JSImport]?
            JSValue require = JSValue.Global["require"];
            this.fluid = new JSReference(require.Call(default, "fluid-framework"));
        });
    }

    private void LoadTinylicious()
    {
        // TODO: Replace code in this method with some form of [JSImport] and source-generation.
        this.jsSyncContext.Post(() =>
        {
            JSValue logFunction = JSValue.CreateFunction("send", (args) =>
            {
                JSValue logEvent = args[0];
                string category = (string)logEvent.GetProperty("category");
                string eventName = (string)logEvent.GetProperty("eventName");
                Debug.WriteLine($"[fluid:{category}] {eventName}");
                return JSValue.Undefined;
            });

            JSValue logger = JSValue.CreateObject();
            logger.SetProperty("send", logFunction);
            JSValue clientProperties = JSValue.CreateObject();
            clientProperties.SetProperty("logger", logger);

            JSValue require = JSValue.Global["require"];
            JSObject tinylicious = (JSObject)require.Call(
                default, "@fluidframework/tinylicious-client");
            JSValue tinyliciousClient = tinylicious["TinyliciousClient"]
                .CallAsConstructor(clientProperties);

            var interfaceAdapter = this.marshaller.GetFromJSValueDelegate<ITinyliciousClient>();
            this.fluidClient = interfaceAdapter.Invoke(tinyliciousClient);
        });
    }

    public string? SessionId { get; private set; }

    private JSValue ContainerSchema
    {
        get
        {
            // TODO: Improve marsahlling of this object.
            JSValue sharedStringClass = this.fluid!.GetValue()!.Value.GetProperty("SharedString");
            JSValue sharedMapClass = this.fluid!.GetValue()!.Value.GetProperty("SharedMap");
            Debug.Assert(sharedStringClass.IsFunction());
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

        var interfaceAdapter = this.marshaller.GetFromJSValueDelegate<ISharedString>();
        return interfaceAdapter(sharedString);
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

        this.lastDocumentText = text + "\r"; // Edit box always adds another \r ?
        this.editBox.Document.SetText(TextSetOptions.None, text);
        this.editBox.Document.Selection.SetRange(0, 0);
        this.editBox.Focus(FocusState.Programmatic);

        // Reset all selections and carets when starting a new session.
        this.selections.Clear();
        foreach (var child in this.editCanvas.Children.OfType<Path>())
        {
            child.Visibility = Visibility.Collapsed;
        }

        Path caret = AssignCaret()!;
        this.editBox.SelectionHighlightColor = (SolidColorBrush)caret.Fill;
        this.clientId = this.editBox.SelectionHighlightColor.Color.ToString();

        SessionId = await this.jsSyncContext.RunAsync(async () =>
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

            string sessionId = await this.fluidContainer.Attach();
            await connectedCompletion.Task;
            return sessionId;
        });

        CollabSelection selection = new(caret);
        this.selections.Add(this.clientId, selection);
        UpdateSelection(selection);
    }

    public async Task ConnectCollabSessionAsync(string id)
    {
        string text = await this.jsSyncContext.RunAsync(async () =>
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

            this.sharedDocument = GetSharedDocument();
            string text = this.sharedDocument.GetText();

            this.sharedSelections = GetSharedSelections();
            foreach (var (remoteClient, remoteSelection) in this.sharedSelections!)
            {
                this.uiSyncContext.Post((_) =>
                {
                    this.OnRemoteSelectionChanged(remoteClient, remoteSelection);
                }, null);
            }

            return text;
        });

        this.lastDocumentText = text + "\r"; // Edit box always adds another \r ?
        this.editBox.Document.SetText(TextSetOptions.None, text);
        this.editBox.Focus(FocusState.Programmatic);

        // Allow some time for other participants' carets to be assigned.
        await Task.Delay(10);

        Path caret = AssignCaret()!;
        this.editBox.SelectionHighlightColor = (SolidColorBrush)caret.Fill;
        this.clientId = this.editBox.SelectionHighlightColor.Color.ToString();
        CollabSelection selection = new(caret);
        this.selections.Add(this.clientId, selection);
        UpdateSelection(selection);
    }

    public void CloseCollabSession()
    {
        this.jsSyncContext.Run(async () =>
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
            this.jsSyncContext.Post(() =>
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
            this.jsSyncContext.Run(() =>
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
        var interfaceAdapter = this.marshaller.GetFromJSValueDelegate<ISequenceDeltaEvent>();
        var deltaEvent = interfaceAdapter(args[0]);

        Debug.WriteLine(
            $"SequenceDelta(IsLocal={deltaEvent.IsLocal}, ClientId={deltaEvent.ClientId})");

        if (!deltaEvent.IsLocal)
        {
            // Do not pass the op to the UI thread because it is a JS object. Just pass its props.
            IMergeTreeOp op = deltaEvent.OpArgs.Op;
            MergeTreeDeltaType deltaType = op.Type;
            int? pos1 = op.Pos1;
            int? pos2 = op.Pos2;
            string? seg = op.Seg;
            this.uiSyncContext.Post((_) => OnRemoteEdit(deltaType, pos1, pos2, seg), null);
        }

        return JSValue.Undefined;
    }

    private void OnRemoteEdit(MergeTreeDeltaType deltaType, int? pos1, int? pos2, string? seg)
    {
        var (selectionStart, selectionLength) = this.lastSelection;
        if (deltaType == MergeTreeDeltaType.Insert && pos1.HasValue && seg is not null)
        {
            Debug.WriteLine($"    insert ({pos1}) [{seg}]");

            if (pos1 <= selectionStart)
            {
                selectionStart += seg.Length;
            }
            else if (pos1 < selectionStart + selectionLength)
            {
                selectionLength += seg.Length;
            }

            string newText = this.lastDocumentText.Substring(0, pos1.Value) +
                seg + this.lastDocumentText.Substring(pos1.Value);
            this.lastDocumentText = newText;
            this.editBox.Document.SetText(
                TextSetOptions.None, newText.Substring(0, newText.Length - 1));
        }
        else if (deltaType == MergeTreeDeltaType.Remove && pos1.HasValue && pos2.HasValue)
        {
            Debug.WriteLine($"    remove ({pos1},{pos2})");

            if (pos1 <= selectionStart)
            {
                // Deletion range sarts before the selection.

                if (pos2 <= selectionStart)
                {
                    // Deletion range ends before the selection.
                    selectionStart -= (pos2.Value - pos1.Value);
                }
                else if (pos2 < selectionStart + selectionLength)
                {
                    // Deletion range ends within the selection.
                    selectionLength = selectionStart + selectionLength - pos2.Value;
                    selectionStart = pos1.Value;
                }
                else
                {
                    // Deletion range fully includes the selection.
                    selectionStart = pos1.Value;
                    selectionLength = 0;
                }
            }
            else if (pos1 < selectionStart + selectionLength)
            {
                // Deletion range starts within the selection.

                if (pos2 < selectionStart + selectionLength)
                {
                    // Deletion range is fully included by the selection.
                    selectionLength -= (pos2.Value - pos1.Value);
                }
                else
                {
                    // Deletion range ends beyond the selection.
                    selectionLength -= (selectionStart + selectionLength - pos1.Value);
                }
            }

            string newText = this.lastDocumentText.Substring(0, pos1.Value) +
                this.lastDocumentText.Substring(pos2.Value);
            this.lastDocumentText = newText;
            this.editBox.Document.SetText(
                TextSetOptions.None, newText.Substring(0, newText.Length - 1));
        }
        else
        {
            Debug.WriteLine("    op type: " + deltaType);
            return;
        }

        this.lastSelection = (selectionStart, selectionLength);
        this.editBox.Document.Selection.SetRange(
            selectionStart, selectionStart + selectionLength);
    }

    private JSValue OnSharedMapValueChanged(JSCallbackArgs args)
    {
        var interfaceAdapter =
            this.marshaller.GetFromJSValueDelegate<ISharedMapValueChangedEvent>();
        var changedEvent = interfaceAdapter(args[0]);
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
