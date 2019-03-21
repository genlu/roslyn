// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.Editor.UnitTests;
using Microsoft.CodeAnalysis.Editor.UnitTests.IntelliSense;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.Composition;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;

namespace IdeBenchmarks
{
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
        {
            _useExportProviderAttribute.Before(null);
            _state = WpfRunAsync(CreateTestState).Result.Result as TestStateBase;
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            _ = WpfRunAsync(DisposeTestState).Result;
            _useExportProviderAttribute.After(null);
        }


        [Benchmark(Baseline = true)]
        public async Task<object> Baseline()
        {
            // Only create the workspace, no typing
            return await WpfRunAsync(() => RunBenchMark(null));
        }

        [Benchmark]
        public async Task<object> NoImportCompletion()
        {
            // typing w/o import completion
            return await WpfRunAsync(() => RunBenchMark(false));
        }

        [Benchmark]
        public async Task<object> ImportCompletion()
        {
            // typing with import completion
            return await WpfRunAsync(() => RunBenchMark(true));
        }

        TestStateBase _state;

        private Task<object> DisposeTestState()
        {
            _state.Dispose();
            _state = null;
            return default;
        }

        private Task<object> CreateTestState()
        {
            return Task.FromResult((object)TestStateFactory.CreateTestStateFromWorkspace(CompletionImplementation.Modern, CreateTestInput()));

            XElement CreateTestInput()
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

                // Add a few extra assembly references
                var refPaths = GetAssemblyFiles(this.GetType().Assembly).ToImmutableArray();
                var metadataRefs = refPaths.Select(path => new XElement("MetadataReference", path));

                return new XElement(
                    "Workspace",
                    new XAttribute("FilePath", "SolutionPath.sln"),
                    new XElement(
                        "Project",
                        new XAttribute("AssemblyName", "CSharpAssembly"),
                        new XAttribute("Language", LanguageNames.CSharp),
                        //new XAttribute("CommonReferences", "true"),
                        metadataRefs,
                        document));
            }

            static IEnumerable<string> GetAssemblyFiles(Assembly assembly)
            {
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                return assembly.GetReferencedAssemblies()
                    .Select(name => loadedAssemblies.SingleOrDefault(a => a.FullName == name.FullName)?.Location)
                    .Where(l => l != null);
            }
        }

        private async Task<object> RunBenchMark(bool? showImportCompletion)
        {
            if (showImportCompletion.HasValue)
            {
                _state.Workspace.Options = _state.Workspace.Options.WithChangedOption(
                    CompletionOptions.ShowImportCompletionItems, LanguageNames.CSharp, showImportCompletion.Value);

                _state.SendTypeChars("a");
                await _state.AssertCompletionSession();
                _state.SendBackspace();
                await _state.AssertCompletionSession();
                var items = _state.GetCompletionItems();

                return items;
            }

            return default;
        }

        // Copied from WpfTestRunner
        private async Task<Task<T>> WpfRunAsync<T>(Func<Task<T>> work)
        {
            if (work == null)
            {
                return default;
            }

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
                        return work().JoinUsingDispatcher(CancellationToken.None);
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
