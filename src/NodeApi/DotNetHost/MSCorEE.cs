// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Note while this code is used to load the .NET Framework host, it is compiled using .NET Native AOT.
#if !NETFRAMEWORK

using System;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// P/Invoke declarations and supporting code for the .net Framework 4.x CLR hosting APIs defined
/// in MetaHost.h & mscoree.h (mscoree.dll).
/// </summary>
/// <remarks>
/// https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/hosting/clr-hosting-interfaces-added-in-the-net-framework-4-and-4-5
/// CLR hosting interfaces are COM-like, in the sense that they use CLSIDs/IIDs and implement
/// IUnknown, however they do not use apartment models, aggregation, registry activation, etc.
/// So while .NET Native AOT does not support COM, these interfaces can still be used via P/Invoke.
/// </remarks>
internal static unsafe partial class MSCorEE
{
    public record struct HRESULT(int hr)
    {
        public readonly void ThrowIfFailed()
        {
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
        }

        public readonly T ThrowIfFailed<T>(T value)
        {
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            return value;
        }

        public readonly T* ThrowIfFailed<T>(T* value) where T : unmanaged
        {
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            return value;
        }
    }

    public static readonly Guid CLSID_CLRMetaHost =
        new(0x9280188d, 0x0e8e, 0x4867, 0xb3, 0x0c, 0x7f, 0xa8, 0x38, 0x84, 0xe8, 0xde);
    public static readonly Guid IID_ICLRMetaHost =
        new(0xD332DB9E, 0xB9B3, 0x4125, 0x82, 0x07, 0xA1, 0x48, 0x84, 0xF5, 0x32, 0x16);
    public static readonly Guid CLSID_CLRMetaHostPolicy =
        new(0x2ebcd49a, 0x1b47, 0x4a61, 0xb1, 0x3a, 0x4a, 0x03, 0x70, 0x1e, 0x59, 0x4b);
    public static readonly Guid IID_ICLRMetaHostPolicy =
        new(0xE2190695, 0x77B2, 0x492e, 0x8E, 0x14, 0xC4, 0xB3, 0xA7, 0xFD, 0xD5, 0x93);
    public static readonly Guid IID_ICLRRuntimeInfo =
        new(0xBD39D1D2, 0xBA2F, 0x486a, 0x89, 0xB0, 0xB4, 0xB0, 0xCB, 0x46, 0x68, 0x91);
    public static readonly Guid CLSID_CLRRuntimeHost =
        new(0x90F1A06E, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02);
    public static readonly Guid IID_ICLRRuntimeHost =
        new(0x90F1A06C, 0x7712, 0x4762, 0x86, 0xB5, 0x7A, 0x5E, 0xBA, 0x6B, 0xDB, 0x02);

    public static T* CLRCreateInstance<T>(Guid clsid, Guid iid) where T : unmanaged
    {
        T* pInterface;
        return CLRCreateInstance(&clsid, &iid, (void**)&pInterface).ThrowIfFailed(pInterface);
    }

#pragma warning disable SYSLIB1054 // Use [LibraryImport]
    [DllImport("mscoree")]
    private static extern HRESULT CLRCreateInstance(
        Guid* rclsid,
        Guid* riid,
        void** ppInterface);
#pragma warning restore SYSLIB1054

#pragma warning disable CS0649 // VTable fields are never assigned to

    public enum CLRMetaHostPolicyFlags : uint
    {
        HighCompat = 0x00,
        ApplyUpgradePolicy = 0x08,
        EmulateExeLaunch = 0x10,
        ShowErrorDialog = 0x20,
        UseProcessImagePath = 0x40,
        EnsureSkuSupported = 0x80,
        IgnoreErrorModed = 0x1000,
    }

    public struct ICLRMetaHostPolicy
    {
        public T* QueryInterface<T>(Guid iid) where T : unmanaged
        {
            T* pInterface;
            fixed (ICLRMetaHostPolicy* pThis = &this)
            {
                return _vtable->QueryInterface(pThis, &iid, (void**)&pInterface)
                    .ThrowIfFailed(pInterface);
            }
        }

        public uint AddRef()
        {
            fixed (ICLRMetaHostPolicy* pThis = &this)
            {
                return _vtable->AddRef(pThis);
            }
        }

        public uint Release()
        {
            fixed (ICLRMetaHostPolicy* pThis = &this)
            {
                return _vtable->Release(pThis);
            }
        }

        public ICLRRuntimeInfo* GetRequestedRuntime(
            CLRMetaHostPolicyFlags policyFlags,
            string assemblyPath,
            out string runtimeVersion)
        {
            ICLRRuntimeInfo* pRuntimeInfo;
            uint versionSize = 20;
            uint imageVersionSize = 0;
            fixed (ICLRMetaHostPolicy* pThis = &this)
            fixed (char* pAssemblyPath = assemblyPath)
            fixed (char* pVersion = stackalloc char[20])
            fixed (Guid* riid = &IID_ICLRRuntimeInfo)
            {
                _vtable->GetRequestedRuntime(
                    pThis,
                    (uint)policyFlags,
                    pAssemblyPath,
                    null,
                    pVersion,
                    &versionSize,
                    null,
                    &imageVersionSize,
                    null,
                    riid,
                    (void**)&pRuntimeInfo).ThrowIfFailed();
                runtimeVersion = new string(pVersion);
                return pRuntimeInfo;
            }
        }

        private readonly VTable* _vtable;

        private struct VTable
        {
            public delegate* unmanaged<ICLRMetaHostPolicy*, Guid*, void**, HRESULT> QueryInterface;
            public delegate* unmanaged<ICLRMetaHostPolicy*, uint> AddRef;
            public delegate* unmanaged<ICLRMetaHostPolicy*, uint> Release;

            public delegate* unmanaged<
                ICLRMetaHostPolicy*, // this
                uint,   // dwPolicyFlags
                char*,  // pwzBinary
                void*,  // pCfgStream
                char*,  // pwzVersion
                uint*,  // pcchVersion
                char*,  // pwzImageVersion
                uint*,  // pcchImageVersion
                uint*,  // pdwConfigFlags
                Guid*,  // riid
                void**, // ppRuntime
                HRESULT
            > GetRequestedRuntime;
        }
    }

    public struct ICLRRuntimeInfo
    {
        public T* QueryInterface<T>(Guid iid) where T : unmanaged
        {
            T* pInterface;
            fixed (ICLRRuntimeInfo* pThis = &this)
            {
                return _vtable->QueryInterface(pThis, &iid, (void**)&pInterface)
                    .ThrowIfFailed(pInterface);
            }
        }

        public uint AddRef()
        {
            fixed (ICLRRuntimeInfo* pThis = &this)
            {
                return _vtable->AddRef(pThis);
            }
        }

        public uint Release()
        {
            fixed (ICLRRuntimeInfo* pThis = &this)
            {
                return _vtable->Release(pThis);
            }
        }

        public T* GetInterface<T>(Guid clsid, Guid iid) where T : unmanaged
        {
            T* pInterface;
            fixed (ICLRRuntimeInfo* pThis = &this)
            {
                return _vtable->GetInterface(pThis, &clsid, &iid, (void**)&pInterface)
                    .ThrowIfFailed(pInterface);
            }
        }

        private readonly VTable* _vtable;

        private struct VTable
        {
            public delegate* unmanaged<ICLRRuntimeInfo*, Guid*, void**, HRESULT> QueryInterface;
            public delegate* unmanaged<ICLRRuntimeInfo*, uint> AddRef;
            public delegate* unmanaged<ICLRRuntimeInfo*, uint> Release;

            public nint GetVersionString;
            public nint GetRuntimeDirectory;
            public nint IsLoaded;
            public nint LoadErrorString;
            public nint LoadLibrary;
            public nint GetProcAddress;

            public delegate* unmanaged<
                ICLRRuntimeInfo*, // this
                Guid*,            // rclsid
                Guid*,            // riid
                void**,           // ppUnk
                HRESULT
            > GetInterface;

            public nint IsLoadable;
            public nint SetDefaultStartupFlags;
            public nint GetDefaultStartupFlags;
            public nint BindAsLegacyV2Runtime;
            public nint IsStarted;
        }
    }

    public struct ICLRRuntimeHost
    {
        public T* QueryInterface<T>(Guid iid) where T : unmanaged
        {
            T* pInterface;
            fixed (ICLRRuntimeHost* pThis = &this)
            {
                return _vtable->QueryInterface(pThis, &iid, (void**)&pInterface)
                    .ThrowIfFailed(pInterface);
            }
        }

        public uint AddRef()
        {
            fixed (ICLRRuntimeHost* pThis = &this)
            {
                return _vtable->AddRef(pThis);
            }
        }

        public uint Release()
        {
            fixed (ICLRRuntimeHost* pThis = &this)
            {
                return _vtable->Release(pThis);
            }
        }

        public void Start()
        {
            fixed (ICLRRuntimeHost* pThis = &this)
            {
                _vtable->Start(pThis).ThrowIfFailed();
            }
        }

        public void Stop()
        {
            fixed (ICLRRuntimeHost* pThis = &this)
            {
                _vtable->Stop(pThis).ThrowIfFailed();
            }
        }

        public uint ExecuteInDefaultAppDomain(
            string assemblyPath,
            string typeName,
            string methodName,
            string argument)
        {
            uint result;
            fixed (ICLRRuntimeHost* pThis = &this)
            fixed (char* pAssemblyPath = assemblyPath)
            fixed (char* pTypeName = typeName)
            fixed (char* pMethodName = methodName)
            fixed (char* pArgument = argument)
            {
                return _vtable->ExecuteInDefaultAppDomain(
                    pThis,
                    pAssemblyPath,
                    pTypeName,
                    pMethodName,
                    pArgument,
                    &result).ThrowIfFailed(result);
            }
        }

        private readonly VTable* _vtable;

        private struct VTable
        {
            public delegate* unmanaged<ICLRRuntimeHost*, Guid*, void**, HRESULT> QueryInterface;
            public delegate* unmanaged<ICLRRuntimeHost*, uint> AddRef;
            public delegate* unmanaged<ICLRRuntimeHost*, uint> Release;

            public delegate* unmanaged<ICLRRuntimeHost*, HRESULT> Start;
            public delegate* unmanaged<ICLRRuntimeHost*, HRESULT> Stop;
            public nint SetHostControl;
            public nint GetCLRControl;
            public nint UnloadAppDomain;
            public nint ExecuteInAppDomain;
            public nint GetCurrentAppDomainId;
            public nint ExecuteApplication;

            public delegate* unmanaged<
                ICLRRuntimeHost*, // this
                char*, // pwzAssemblyPath
                char*, // pwzTypeName
                char*, // pwzMethodName
                char*, // pwzArgument
                uint*, // pReturnValue
                HRESULT
            > ExecuteInDefaultAppDomain;
        }
    }
}

#endif // NETFRAMEWORK
