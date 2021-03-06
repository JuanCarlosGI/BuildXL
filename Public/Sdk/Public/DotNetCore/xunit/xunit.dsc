// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import {Artifact, Cmd, Transformer} from "Sdk.Transformers";

/**
 * Command line arguments that are required for running xunit.console.
 */
@@public
export interface Arguments {
    /** Test assembly */
    testAssembly: File;

    /** Do not show the copyright message. */
    noLogo?: boolean;

    /** Do not show the copyright message. */
    noColor?: boolean;
    
    /** Indicates whether app domains should be used to run test code. */
    noAppDomain?: boolean;

    /** Convert skipped tests into failures. */
    failSkips?: boolean;

    /** Stop on first test failure. */
    stopOnFail?: boolean;

    /** Set of parallelization based on otion. */
    parallel?: "none" | "collections" | "assemblies" | "all";

    /** Maximum thread count for collection/assmebly parallelization (0 - unbounded; >0 - limit to that number) */
    maxThreads?: number;

    /** Do not shadow copy assemblies. */
    noShadow?: boolean;

    /** Wait for input after completion. */
    wait?: boolean;

    /** Serialize all test cases (for diagnostic purposes only). */
    serialize?: boolean;

    /** Only run tests with matching name/value traits if specified more than once, acts as an OR operation. */
    traits?: {name: string, value: string}[];

    /** Do not run tests with matching name/value traits. If specified more than once, acts as AND operation. */
    noTraits?: {name: string, value: string}[];

    /** Run a given test method (should be fully specified; i.e., 'MyNamespace.MyClass.MyTestMethod'). */
    methods?: string[];

    /** Run all methods in a given test calss (should be fully specified; i.e., 'MyNamespace.MyClass'). */
    classes?: string[];

    // =========== Reporters =====================

    /** Forces TeamCity mode (normally auto-detected). */
    teamCity?: boolean;

    /** Forces AppVeyor CI mode (normally auto-detected). */
    appVeyor?: boolean;

    /** Do not show progress messages. */
    quiet?: boolean;

    /** Show progress messages in JSON format. */
    json?: boolean;

    // =========== Result Formats =====================

    /** Filename where an HTML report will be generated after run. No HTML generated by default. */
    htmlFile?: Path;

    /** Filename where an XML report (in NUnit format) will be generated after run. */
    nunitFile?: Path;

    /** Filename where an XML report (in xUnit.net v2 format) will be generated after run. */
    xmlFile?: Path;

    /** Filename where an XML report (in xUnit.net v1 format) will be generated after run. */
    xmlV1File?: Path;
}

@@public
export function commandLineArgs(args: Arguments): Argument[] {
    Contract.requires(args !== undefined);
    Contract.requires(args.testAssembly !== undefined);

    return [
        Cmd.argument(Artifact.input(args.testAssembly)),
        Cmd.option("-parallel ",   args.parallel),
        Cmd.option("-maxthreads ", args.maxThreads),

        Cmd.flag("-nologo",      args.noLogo),
        Cmd.flag("-nocolor",     args.noColor),
        Cmd.flag("-noshadow",    args.noShadow),
        Cmd.flag("-noappdomain", args.noAppDomain),
        Cmd.flag("-teamcity",    args.teamCity),
        Cmd.flag("-appveyor",    args.appVeyor),
        Cmd.flag("-quiet",       args.quiet),
        Cmd.flag("-wait",        args.wait),
        Cmd.flag("-serialize",   args.serialize),

        Cmd.options("-trait ",   (args.traits || []).map(t => t.name + "=" + t.value)),
        Cmd.options("-notrait ", (args.noTraits || []).map(t => t.name + "=" + t.value)),

        Cmd.options("-method ",  args.methods || []),
        Cmd.options("-class ",   args.classes || []),

        Cmd.option("-xmlV1 ",   Artifact.output(args.xmlV1File)),
        Cmd.option("-xml ",     Artifact.output(args.xmlFile)),
        Cmd.option("-html ",    Artifact.output(args.htmlFile)),
        Cmd.option("-nunit ",   Artifact.output(args.nunitFile)),
    ];
}
