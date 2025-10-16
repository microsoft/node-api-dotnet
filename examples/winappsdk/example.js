

const dotnet = require('node-api-dotnet');

require('./bin/Microsoft.Windows.SDK.NET.js');
require('./bin/Microsoft.WindowsAppRuntime.Bootstrap.Net.js');
require('./bin/Microsoft.InteractiveExperiences.Projection.js');
require('./bin/Microsoft.Windows.ApplicationModel.WindowsAppRuntime.Projection.js');
require('./bin/Microsoft.Windows.AI.Text.Projection.js');
require('./bin/Microsoft.Windows.AI.ContentSafety.Projection.js');
require('./bin/Microsoft.Windows.AppNotifications.Projection.js');
require('./bin/Microsoft.Windows.AppNotifications.Builder.Projection.js');

const majorVersion = 1;
const minorVersion = 8;

console.log("Attempt to initialize the WindowsAppRuntime Bootstrapper.  (This requires the WindowsAppRuntime " + majorVersion + "." + minorVersion + " to be installed on the system.)");
const fullVersion = (majorVersion << 16) | minorVersion;
dotnet.Microsoft.Windows.ApplicationModel.DynamicDependency.Bootstrap.Initialize(fullVersion);
console.log("Initialized Bootstraper.  WindowsAppRuntime is now available.");
