## C# WinUI + JS Fluid Framework
This project is a C# WinUI application that demonstrates collaborative text editing
using the [Fluid Framework](https://fluidframework.com/), which has a JS-only API.

Before building and running this project:
 - If necessary, [install tools for WinUI C# development](
https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/set-up-your-development-environment
).
 - Download a build of `libnode.dll` that _includes Node API embedding support_. (Soon this will be
   available as a nuget package, but for now you can contact us to get a copy.) Place it at
   `bin\win-x64\libnode.dll` relative to the repo root (not this subdirectory).

| Command                 | Explanation
|-------------------------|--------------------------------------------------
| `dotnet pack ../..`     | Build Node API .NET packages.
| `npm install`           | Install JavaScript packages.
| `dotnet build`          | Install .NET nuget packages; build example project.
| `npx tinylicious`       | Start the local fluid development server on port 7070.
| `dotnet run --no-build` | Run the example project.

Launch two or more instances of the app to set up a collaborative editing session. Each instance of
the application can either host or join a session.
 - To host a new session, click the **Start** button. Then **Copy** the session ID and send it
   to a guest.
 - To join an existing session, paste the session ID (obtained from a host) and click **Join**.

All participants in the session can see each others' cursors and simultanesouly edit the document.

To join remotely, forward port 7070 using `ngrok` or a similar tool. See
[Testing with Tinylicious and multiple clients](
https://fluidframework.com/docs/testing/tinylicious/#testing-with-tinylicious-and-multiple-clients)
