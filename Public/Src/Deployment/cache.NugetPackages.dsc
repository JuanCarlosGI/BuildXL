// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import * as Deployment from "Sdk.Deployment";
import * as BuildXLSdk from "Sdk.BuildXL";
import * as Nuget from "Sdk.Managed.Tools.NuGet";

namespace Cache.NugetPackages {
    export declare const qualifier : { configuration: "debug" | "release"};

    const Net451ContentStore = importFrom("BuildXL.Cache.ContentStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "net451", targetRuntime: "win-x64" });
    const Net472ContentStore = importFrom("BuildXL.Cache.ContentStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "net472", targetRuntime: "win-x64" });
    const WinX64ContentStore = importFrom("BuildXL.Cache.ContentStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "win-x64" });
    const OsxX64ContentStore = importFrom("BuildXL.Cache.ContentStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "osx-x64" });

    const Net451MemoizationStore = importFrom("BuildXL.Cache.MemoizationStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "net451", targetRuntime: "win-x64" });
    const Net472MemoizationStore = importFrom("BuildXL.Cache.MemoizationStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "net472", targetRuntime: "win-x64" });
    const WinX64MemoizationStore = importFrom("BuildXL.Cache.MemoizationStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "win-x64" });
    const OsxX64MemoizationStore = importFrom("BuildXL.Cache.MemoizationStore").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "osx-x64" });

    const Net472DistributedCacheHost = importFrom("BuildXL.Cache.DistributedCache.Host").withQualifier({ configuration: qualifier.configuration, targetFramework: "net472", targetRuntime: "win-x64" });
    const WinX64DistributedCacheHost = importFrom("BuildXL.Cache.DistributedCache.Host").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "win-x64" });
    const OsxX64DistributedCacheHost = importFrom("BuildXL.Cache.DistributedCache.Host").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "osx-x64" });

    const Net451BxlUtilities = importFrom("BuildXL.Utilities").withQualifier({ configuration: qualifier.configuration, targetFramework: "net451", targetRuntime: "win-x64" });
    const Net472BxlUtilities = importFrom("BuildXL.Utilities").withQualifier({ configuration: qualifier.configuration, targetFramework: "net472", targetRuntime: "win-x64" });
    const WinX64BxlUtilities = importFrom("BuildXL.Utilities").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "win-x64" });
    const OsxX64BxlUtilities = importFrom("BuildXL.Utilities").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "osx-x64" });
    
    const Net451BxlUtilitiesInstrumentation = importFrom("BuildXL.Utilities.Instrumentation").withQualifier({ configuration: qualifier.configuration, targetFramework: "net451", targetRuntime: "win-x64" });
    const Net472BxlUtilitiesInstrumentation = importFrom("BuildXL.Utilities.Instrumentation").withQualifier({ configuration: qualifier.configuration, targetFramework: "net472", targetRuntime: "win-x64" });
    const WinX64BxlUtilitiesInstrumentation = importFrom("BuildXL.Utilities.Instrumentation").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "win-x64" });
    const OsxX64BxlUtilitiesInstrumentation = importFrom("BuildXL.Utilities.Instrumentation").withQualifier({ configuration: qualifier.configuration, targetFramework: "netcoreapp3.0", targetRuntime: "osx-x64" });

    export const tools : Deployment.Definition = {
        contents: [
            {
                subfolder: r`tools`,
                contents: [
                    Net472ContentStore.App.exe,
                    Net472MemoizationStore.App.exe,
                    Net472DistributedCacheHost.Configuration.dll,
                    Net472DistributedCacheHost.Service.dll,
                ]
            },
        ]
    };

    export const libraries : Deployment.Definition = {
        contents: [
            // ContentStore.Distributed
            Nuget.createAssemblyLayout(Net451ContentStore.Distributed.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Distributed.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Distributed.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Distributed.dll, "osx-x64", false),
            // ContentStore.Library
            Nuget.createAssemblyLayout(Net451ContentStore.Library.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Library.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Library.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Library.dll, "osx-x64", false),
            // ContentStore.Grpc
            Nuget.createAssemblyLayout(Net451ContentStore.Grpc.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Grpc.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Grpc.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Grpc.dll, "osx-x64", false),

            // ContentStore.Vsts
            ...addIfLazy(BuildXLSdk.Flags.isVstsArtifactsEnabled, () => [
                Nuget.createAssemblyLayout(Net451ContentStore.Vsts.dll),
                Nuget.createAssemblyLayout(Net472ContentStore.Vsts.dll)
            ]),

            // ContentStore.VstsInterfaces
            Nuget.createAssemblyLayout(Net451ContentStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.VstsInterfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.VstsInterfaces.dll, "osx-x64", false),

            // MemoizationStore.Distributed
            Nuget.createAssemblyLayout(Net451MemoizationStore.Distributed.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.Distributed.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.Distributed.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.Distributed.dll, "osx-x64", false),
            // MemoizationStore.Library
            Nuget.createAssemblyLayout(Net451MemoizationStore.Library.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.Library.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.Library.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.Library.dll, "osx-x64", false),

            // MemoizationStore.Vsts
            ...addIfLazy(BuildXLSdk.Flags.isVstsArtifactsEnabled, () => [
                Nuget.createAssemblyLayout(Net451MemoizationStore.Vsts.dll),
                Nuget.createAssemblyLayout(Net472MemoizationStore.Vsts.dll)
            ]),

            // MemoizationStore.VstsInterfaces
            Nuget.createAssemblyLayout(Net451MemoizationStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.VstsInterfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.VstsInterfaces.dll, "osx-x64", false),

            // BuildXL.Cache.Host.Services
            Nuget.createAssemblyLayout(Net472DistributedCacheHost.Service.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64DistributedCacheHost.Service.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64DistributedCacheHost.Service.dll, "osx-x64", false),

            // BuildXL.Cache.Host.Configuration
            Nuget.createAssemblyLayout(Net472DistributedCacheHost.Configuration.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64DistributedCacheHost.Configuration.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64DistributedCacheHost.Configuration.dll, "osx-x64", false),
        ]
    };

    export const interfaces : Deployment.Definition = {
        contents: [
            // ContentStore.Interfaces
            Nuget.createAssemblyLayout(Net451ContentStore.Interfaces.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Interfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Interfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Interfaces.dll, "osx-x64", false),
            Nuget.createAssemblyLayout(importFrom("BuildXL.Cache.ContentStore").Interfaces.withQualifier(
                { configuration: qualifier.configuration, targetFramework: "netstandard2.0", targetRuntime: "win-x64" }
            ).dll),

            // MemoizationStore.Interfaces
            Nuget.createAssemblyLayout(Net451MemoizationStore.Interfaces.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.Interfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.Interfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.Interfaces.dll, "osx-x64", false),
        ]
    };

    export const hashing : Deployment.Definition = {
        contents: [
            // ContentStore.Hashing
            Nuget.createAssemblyLayout(Net451ContentStore.Hashing.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Hashing.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Hashing.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Hashing.dll, "osx-x64", false),
            Nuget.createAssemblyLayout(importFrom("BuildXL.Cache.ContentStore").Hashing.withQualifier(
                { configuration: qualifier.configuration, targetFramework: "netstandard2.0", targetRuntime: "win-x64" }
            ).dll),

            // ContentStore.UtilitiesCore
            Nuget.createAssemblyLayout(Net451ContentStore.UtilitiesCore.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.UtilitiesCore.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.UtilitiesCore.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.UtilitiesCore.dll, "osx-x64", false),
            Nuget.createAssemblyLayout(importFrom("BuildXL.Cache.ContentStore").UtilitiesCore.withQualifier(
                { configuration: qualifier.configuration, targetFramework: "netstandard2.0", targetRuntime: "win-x64" }
            ).dll),
        ]
    };

    export const interfaces2 : Deployment.Definition = {
        contents: [
            // ContentStore.Interfaces
            Nuget.createAssemblyLayout(Net451ContentStore.Interfaces.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Interfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Interfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Interfaces.dll, "osx-x64", false),
            Nuget.createAssemblyLayout(importFrom("BuildXL.Cache.ContentStore").Interfaces.withQualifier(
                { configuration: qualifier.configuration, targetFramework: "netstandard2.0", targetRuntime: "win-x64" }
            ).dll),

            // BuildXL.Cache.Host.Configuration
            Nuget.createAssemblyLayout(Net472DistributedCacheHost.Configuration.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64DistributedCacheHost.Configuration.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64DistributedCacheHost.Configuration.dll, "osx-x64", false),
        ]
    };

    export const library2 : Deployment.Definition = {
        contents: [
            // ContentStore.Library
            Nuget.createAssemblyLayout(Net451ContentStore.Library.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Library.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Library.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Library.dll, "osx-x64", false),

            // ContentStore.Grpc
            Nuget.createAssemblyLayout(Net451ContentStore.Grpc.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Grpc.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Grpc.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Grpc.dll, "osx-x64", false),
        ]
    };

    export const distributed2 : Deployment.Definition = {
        contents: [
            // ContentStore.Distributed
            Nuget.createAssemblyLayout(Net451ContentStore.Distributed.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.Distributed.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.Distributed.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.Distributed.dll, "osx-x64", false),

            // MemoizationStore.Distributed
            Nuget.createAssemblyLayout(Net451MemoizationStore.Distributed.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.Distributed.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.Distributed.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.Distributed.dll, "osx-x64", false),
            // MemoizationStore.Library
            Nuget.createAssemblyLayout(Net451MemoizationStore.Library.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.Library.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.Library.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.Library.dll, "osx-x64", false),
            // MemoizationStore.Interfaces
            Nuget.createAssemblyLayout(Net451MemoizationStore.Interfaces.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.Interfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.Interfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.Interfaces.dll, "osx-x64", false),
        ]
    };

    export const service : Deployment.Definition = {
        contents: [
            // BuildXL.Cache.Host.Service
            Nuget.createAssemblyLayout(Net472DistributedCacheHost.Service.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64DistributedCacheHost.Service.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64DistributedCacheHost.Service.dll, "osx-x64", false),
        ]
    };

    export const vsts : Deployment.Definition = {
        contents: [
            // ContentStore.Vsts
            ...addIfLazy(BuildXLSdk.Flags.isVstsArtifactsEnabled, () => [
                Nuget.createAssemblyLayout(Net451ContentStore.Vsts.dll),
                Nuget.createAssemblyLayout(Net472ContentStore.Vsts.dll)
            ]),
            // ContentStore.VstsInterfaces
            Nuget.createAssemblyLayout(Net451ContentStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayout(Net472ContentStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64ContentStore.VstsInterfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64ContentStore.VstsInterfaces.dll, "osx-x64", false),

            // MemoizationStore.Vsts
            ...addIfLazy(BuildXLSdk.Flags.isVstsArtifactsEnabled, () => [
                Nuget.createAssemblyLayout(Net451MemoizationStore.Vsts.dll),
                Nuget.createAssemblyLayout(Net472MemoizationStore.Vsts.dll)
            ]),
            // MemoizationStore.VstsInterfaces
            Nuget.createAssemblyLayout(Net451MemoizationStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayout(Net472MemoizationStore.VstsInterfaces.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64MemoizationStore.VstsInterfaces.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64MemoizationStore.VstsInterfaces.dll, "osx-x64", false),
        ]
    };

    export const bxlUtilities : Deployment.Definition = {
        contents: [
            // Utilities
            Nuget.createAssemblyLayout(Net451BxlUtilities.dll),
            Nuget.createAssemblyLayout(Net472BxlUtilities.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64BxlUtilities.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64BxlUtilities.dll, "osx-x64", false),

            // Utilities.Branding
            Nuget.createAssemblyLayout(Net451BxlUtilities.Branding.dll),
            Nuget.createAssemblyLayout(Net472BxlUtilities.Branding.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64BxlUtilities.Branding.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64BxlUtilities.Branding.dll, "osx-x64", false),
            
            // Utilities.KeyValueStore
            Nuget.createAssemblyLayout(Net451BxlUtilities.KeyValueStore.dll),
            Nuget.createAssemblyLayout(Net472BxlUtilities.KeyValueStore.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64BxlUtilities.KeyValueStore.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64BxlUtilities.KeyValueStore.dll, "osx-x64", false),
            
            // Utilities.Native
            Nuget.createAssemblyLayout(Net451BxlUtilities.Native.dll),
            Nuget.createAssemblyLayout(Net472BxlUtilities.Native.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64BxlUtilities.Native.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64BxlUtilities.Native.dll, "osx-x64", false),
            
            // Utilities.Collections
            Nuget.createAssemblyLayout(Net451BxlUtilities.Collections.dll),
            Nuget.createAssemblyLayout(Net472BxlUtilities.Collections.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64BxlUtilities.Collections.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64BxlUtilities.Collections.dll, "osx-x64", false),
            
            // Utilities.Interop
            Nuget.createAssemblyLayout(Net451BxlUtilities.Interop.dll),
            Nuget.createAssemblyLayout(Net472BxlUtilities.Interop.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64BxlUtilities.Interop.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64BxlUtilities.Interop.dll, "osx-x64", false),

            // Utilities.Instrumentation.Common
            Nuget.createAssemblyLayout(Net451BxlUtilitiesInstrumentation.Common.dll),
            Nuget.createAssemblyLayout(Net472BxlUtilitiesInstrumentation.Common.dll),
            Nuget.createAssemblyLayoutWithSpecificRuntime(WinX64BxlUtilitiesInstrumentation.Common.dll, "win-x64", true),
            Nuget.createAssemblyLayoutWithSpecificRuntime(OsxX64BxlUtilitiesInstrumentation.Common.dll, "osx-x64", false),
        ]
    };
}