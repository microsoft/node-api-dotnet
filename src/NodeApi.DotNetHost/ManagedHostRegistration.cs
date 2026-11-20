// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Native handshake structure the managed host fills in at initialization, so the native host can
/// keep the managed host alive for the environment lifetime and notify it when the environment is
/// torn down.
/// </summary>
/// <remarks>
/// The layout must exactly match the native host's own copy of this structure (in the NodeApi
/// assembly). Both are two pointer-sized fields, passed by pointer across the native/managed
/// boundary. The native host and managed host run in separate .NET runtimes, so the structure is
/// defined independently in each and only its binary layout is shared.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct ManagedHostRegistration
{
    /// <summary>
    /// A strong <see cref="GCHandle"/> to the managed host, allocated and freed only by managed
    /// code. The native host treats it as an opaque pointer.
    /// </summary>
    public nint AddonGCHandle;

    /// <summary>
    /// A native callback pointer (<c>delegate* unmanaged&lt;nint, void&gt;</c>) the native host
    /// invokes at environment teardown, or default when the native host uses another channel
    /// (the .NET Framework host invokes the finalize method through the default AppDomain instead).
    /// </summary>
    public nint OnEnvFinalize;
}
