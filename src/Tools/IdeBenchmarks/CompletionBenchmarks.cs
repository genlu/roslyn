// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.UnitTests;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.FindSymbols.SymbolTree;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Storage;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.Composition;
using Microsoft.CodeAnalysis.Editor.UnitTests.IntelliSense;
using Roslyn.Test.Utilities;
using System.Diagnostics;
using Roslyn.Utilities;
using System.Windows.Threading;
using Microsoft.CodeAnalysis.Completion;

namespace IdeBenchmarks
{
    [MemoryDiagnoser]
    public class CompletionBenchmarks
    {
        private readonly UseExportProviderAttribute _useExportProviderAttribute = new UseExportProviderAttribute();
        private ComposableCatalog _catalog;
        private IExportProviderFactory _exportProviderFactory;

        public WpfTestSharedData SharedData => WpfTestSharedData.Instance;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _catalog = TestExportProvider.EntireAssemblyCatalogWithCSharpAndVisualBasic;
            _exportProviderFactory = ExportProviderCache.GetOrCreateExportProviderFactory(_catalog);
        }

        [IterationSetup]
        public void IterationSetup()
            => _useExportProviderAttribute.Before(null);

        [IterationCleanup]
        public void IterationCleanup()
            => _useExportProviderAttribute.After(null);

        [Params(false, true)]
        public bool Input { get; set; }

        public async Task<object> TestProject(bool showImportCompletion)
        {
            using (var state = TestStateFactory.CreateTestStateFromWorkspace(CompletionImplementation.Legacy, CreateTestInput()))
            {
                if (showImportCompletion)
                {
                    state.Workspace.Options = state.Workspace.Options.WithChangedOption(
                        CompletionOptions.TriggerOnDeletion, LanguageNames.CSharp, true);
                }

                state.SendTypeChars("a");
                await state.AssertCompletionSession();
                state.SendBackspace();
                await state.AssertCompletionSession();
                var items = state.GetCompletionItems();
                return items;
            }
        }

        private static XElement CreateTestInput()
        {
            var document = new XElement("Document",
                    new XAttribute("FilePath", $"Class1.cs"),
                    new XText(@"
class Class1
{
    static void Main()
    {
        $$
    }
}
"));

            return new XElement(
                "Workspace",
                new XAttribute("FilePath", "SolutionPath.sln"),
                new XElement(
                    "Project",
                    new XAttribute("AssemblyName", "CSharpAssembly"),
                    new XAttribute("Language", LanguageNames.CSharp),
                    new XAttribute("CommonReferences", "true"),
                    new[] { document }));
        }

        [Benchmark]
        public async Task<object> InvokeTestMethodAsync()
        {
            var sta = StaTaskScheduler.DefaultSta;
            var task = Task.Factory.StartNew(async () =>
            {
                Debug.Assert(sta.StaThread == Thread.CurrentThread);

                using (await SharedData.TestSerializationGate.DisposableWaitAsync(CancellationToken.None))
                {
                    try
                    {
                        Debug.Assert(SynchronizationContext.Current is DispatcherSynchronizationContext);

                        // Just call back into the normal xUnit dispatch process now that we are on an STA Thread with no synchronization context.
                        return TestProject(Input).JoinUsingDispatcher(CancellationToken.None);
                    }
                    finally
                    {
                        // Cleanup the synchronization context even if the test is failing exceptionally
                        SynchronizationContext.SetSynchronizationContext(null);
                    }
                }
            }, CancellationToken.None, TaskCreationOptions.None, new SynchronizationContextTaskScheduler(sta.DispatcherSynchronizationContext));

            return await task;
        }
    }
}
