## C# WinUI + JS Fluid Framework
This project is a C# WinUI application that demonstrates collaborative text editing
using the [Fluid Framework](https://fluidframework.com/), which has a JS-only API.

Before building and running this project:
 - If necessary, [install tools for WinUI C# development](
https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment
).
 - `libnode` is restored from NuGet (x64/arm64); no manual download is needed.

| Command                                                         | Explanation
|-----------------------------------------------------------------|--------------------------------------------------
| `dotnet pack ../.. --configuration Release`                     | Build Node API .NET packages.
| `npm install`                                                   | Install JavaScript packages.
| `dotnet build --configuration Release -p:Platform=x64`          | Install .NET nuget packages; build example project.
| `npx tinylicious`                                               | Start the local fluid development server on port 7070.
| `dotnet run --configuration Release --no-build -p:Platform=x64` | Run the example project.

Launch two or more instances of the app to set up a collaborative editing session. Each instance of
the application can either host or join a session.
 - To host a new session, click the **Start** button. Then **Copy** the session ID and send it
   to a guest.
 - To join an existing session, paste the session ID (obtained from a host) and click **Join**.

All participants in the session can see each others' cursors and simultanesouly edit the document.

To join remotely, forward port 7070 using `ngrok` or a similar tool. See
[Testing with Tinylicious and multiple clients](
https://fluidframework.com/docs/testing/tinylicious/#testing-with-tinylicious-and-multiple-clients)
