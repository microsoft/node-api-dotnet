## C# WinUI + JS Fluid Framework
This project is a C# WinUI application that demonstrates collaborative text editing
using the [Fluid Framework](https://fluidframework.com/), which has a JS-only API.

| Command                 | Explanation
|-------------------------|--------------------------------------------------
| `dotnet pack ../..`     | Build Node API .NET packages.
| `npm install`           | Install JavaScript packages.
| `dotnet build`          | Install .NET nuget packages; build example project.
| `npx tinylicious`       | Start the local fluid development server on port 7070.
| `dotnet run --no-build` | Run the example project.

Each instance of the application can either host or join a collaborative editing session.
 - To host a new session, click the **Start** button. Then **Copy** the session ID and send it
   to a guest.
 - To join an existing session, paste the session ID (obtained from a host) and click **Join**.

All participants in the session can see each others' cursors and simultanesouly edit the document.

To join remotely, forward port 7070 using `ngrok` or a similar tool. See
[Testing with Tinylicious and multiple clients](
https://fluidframework.com/docs/testing/tinylicious/#testing-with-tinylicious-and-multiple-clients)
