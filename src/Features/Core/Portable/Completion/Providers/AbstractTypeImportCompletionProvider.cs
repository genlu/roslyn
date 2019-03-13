// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.AddImports;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Completion.Providers
{
    internal abstract class AbstractTypeImportCompletionProvider : CommonCompletionProvider
    {
        private readonly SyntaxAnnotation _annotation = new SyntaxAnnotation();

        protected abstract Task<SyntaxContext> CreateContextAsync(Document document, int position, CancellationToken cancellationToken);
        protected abstract HashSet<INamespaceSymbol> GetNamespacesInScope(SemanticModel semanticModel, SyntaxNode location, CancellationToken cancellationToken);
        protected abstract Task<bool> IsSemanticTriggerCharacterAsync(Document document, int characterPosition, CancellationToken cancellationToken);

        public override async Task ProvideCompletionsAsync(CompletionContext completionContext)
        {
            var document = completionContext.Document;
            var position = completionContext.Position;
            var options = completionContext.Options;
            var cancellationToken = completionContext.CancellationToken;

            if (completionContext.Trigger.Kind == CompletionTriggerKind.Insertion)
            {
                var isSemanticTriggerCharacter = await IsSemanticTriggerCharacterAsync(document, position - 1, cancellationToken).ConfigureAwait(false);
                if (!isSemanticTriggerCharacter)
                {
                    return;
                }
            }

            var syntaxContext = await CreateContextAsync(document, position, cancellationToken).ConfigureAwait(false);

            var items = await GetCompletionItemsAsync(document, syntaxContext, position, cancellationToken).ConfigureAwait(false);
            completionContext.AddItems(items);

            return;
        }

        public override async Task<CompletionChange> GetChangeAsync(Document document, CompletionItem item, char? commitKey = default, CancellationToken cancellationToken = default)
        {
            var newDocument = await ComputeNewDocumentAsync(document, item, cancellationToken).ConfigureAwait(false);
            var newText = await newDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = await newDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            int? newPosition = null;

            // Attempt to find the inserted node and move the caret appropriately
            if (newRoot != null)
            {
                var caretTarget = newRoot.GetAnnotatedNodesAndTokens(_annotation).FirstOrNullable();
                if (caretTarget != null)
                {
                    var targetPosition = caretTarget.Value.AsNode().GetLastToken().Span.End;

                    // Something weird happened and we failed to get a valid position.
                    // Bail on moving the caret.
                    if (targetPosition > 0 && targetPosition <= newText.Length)
                    {
                        newPosition = targetPosition;
                    }
                }
            }

            var changes = await newDocument.GetTextChangesAsync(document, cancellationToken).ConfigureAwait(false);
            var change = Utilities.Collapse(newText, changes.ToList());

            return CompletionChange.Create(change, newPosition, includesCommitCharacter: true);
        }

        private async Task<Document> ComputeNewDocumentAsync(Document document, CompletionItem completionItem, CancellationToken cancellationToken)
        {
            var tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            var root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);

            var finalText = root.GetText(text.Encoding)
                .Replace(completionItem.Span, completionItem.DisplayText.Trim());

            document = document.WithText(finalText);

            tree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
            root = await tree.GetRootAsync(cancellationToken).ConfigureAwait(false);
            var addedNode = root.FindNode(completionItem.Span);
            var annotatedNode = addedNode.WithAdditionalAnnotations(_annotation);

            root = root.ReplaceNode(addedNode, annotatedNode);
            document = document.WithSyntaxRoot(root);

            if (TypeImportCompletionItem.TryGetContainingNamespace(completionItem, out var containingnNamespace))
            {
                var optionSet = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
                var placeSystemNamespaceFirst = optionSet.GetOption(GenerationOptions.PlaceSystemNamespaceFirst, document.Project.Language);
                var importNode = CreateImport(document, containingnNamespace);

                var addImportService = document.GetLanguageService<IAddImportsService>();
                var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

                root = addImportService.AddImport(compilation, root, annotatedNode, importNode, placeSystemNamespaceFirst);
                document = document.WithSyntaxRoot(root);

                document = await Formatter.FormatAsync(document, Formatter.Annotation, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            return document;
        }

        private static SyntaxNode CreateImport(Document document, string namespaceName)
        {
            var syntaxGenerator = SyntaxGenerator.GetGenerator(document);
            return syntaxGenerator.NamespaceImportDeclaration(namespaceName).WithAdditionalAnnotations(Formatter.Annotation);
        }

        protected override async Task<CompletionDescription> GetDescriptionWorkerAsync(
            Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            if (TypeImportCompletionItem.TryGetMetadataName(item, out var metadataName))
            {
                var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                var symbol = compilation.GetTypeByMetadataName(metadataName);
                if (symbol != null)
                {
                    return CompletionDescription.FromText(DebugText);
                    //return await CommonCompletionUtilities.CreateDescriptionAsync(
                    //    document.Project.Solution.Workspace,
                    //    semanticModel,
                    //    0,
                    //    new[] { symbol },
                    //    null,
                    //    cancellationToken).ConfigureAwait(false);
                }
            }

            return CompletionDescription.Empty;
        }

        private static readonly SymbolDisplayFormat QualifiedNameOnlyFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private async Task<ImmutableArray<CompletionItem>> GetCompletionItemsAsync(Document document, SyntaxContext context, int position, CancellationToken cancellationToken)
        {
            DebugClear();

            if (context.IsTypeContext)
            {
                var project = document.Project;
                var node = context.LeftToken.Parent;

                var namespacesInScope = GetNamespacesInScope(context.SemanticModel, node, cancellationToken)
                    .Select(symbol => symbol.ToDisplayString(QualifiedNameOnlyFormat))
                    .ToImmutableHashSet();

                if (project.SupportsCompilation)
                {
                    var tick = Environment.TickCount;

                    var builder = ArrayBuilder<CompletionItem>.GetInstance();

                    using (Logger.LogBlock(FunctionId.Completion_TypeImportCompletionProvider_GetCompletionItemsAsync, cancellationToken))
                    {
                        var declarationsInCurrentProject = await GetAccessibleOutOfScopeDeclarationInfosFromProjectAsync(project, namespacesInScope, true, cancellationToken)
                            .ConfigureAwait(false);

                        builder.AddRange(declarationsInCurrentProject.Select(TypeImportCompletionItem.Create));

                        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

                        // get declarations from directly referenced projects and metadata
                        foreach (var referencedAssembly in compilation.GetReferencedAssemblySymbols())
                        {
                            var isInternalsVisible = compilation.Assembly.IsSameAssemblyOrHasFriendAccessTo(referencedAssembly);
                            var assemblyProject = project.Solution.GetProject(referencedAssembly, cancellationToken);

                            var declarationsFromReference = assemblyProject != null
                                ? await GetAccessibleOutOfScopeDeclarationInfosFromProjectAsync(assemblyProject, namespacesInScope, isInternalsVisible, cancellationToken).ConfigureAwait(false)
                                : GetAccessibleOutOfScopeDeclarationInfosFromAssembly(referencedAssembly, namespacesInScope, isInternalsVisible);

                            builder.AddRange(declarationsFromReference.Select(TypeImportCompletionItem.Create));
                        }
                    }

                    _debug_total_time_with_ItemCreation = Environment.TickCount - tick;
                    return builder.ToImmutableAndFree();
                }
            }

            return ImmutableArray<CompletionItem>.Empty;
        }

        private async Task<IEnumerable<TypeDeclarationInfo>> GetAccessibleOutOfScopeDeclarationInfosFromProjectAsync(
            Project fromProject,
            ImmutableHashSet<string> namespacesInScope,
            bool isInternalsVisible,
            CancellationToken cancellationToken)
        {
            var tick = Environment.TickCount;

            var infos = await fromProject.GetTopLevelTypeDeclarationInfosAsync(cancellationToken).ConfigureAwait(false);

            tick = Environment.TickCount - tick;

            _debug_total_compilation++;
            _debug_total_compilation_decl += infos.Length;
            _debug_total_compilation_time += tick;

            return infos.Where(info => IsTypeOutOfScopeButAccessible(info, namespacesInScope, isInternalsVisible));
        }

        private IEnumerable<TypeDeclarationInfo> GetAccessibleOutOfScopeDeclarationInfosFromAssembly(
            IAssemblySymbol fromAssembly,
            ImmutableHashSet<string> namespacesInScope,
            bool isInternalsVisible)
        {
            var tick = Environment.TickCount;

            var infos = GetTypeDeclarationInfos(fromAssembly);

            tick = Environment.TickCount - tick;

            _debug_total_pe++;
            _debug_total_pe_decl += infos.Length;
            _debug_total_pe_time += tick;

            return infos.Where(info => IsTypeOutOfScopeButAccessible(info, namespacesInScope, isInternalsVisible));
        }

        private static bool IsTypeOutOfScopeButAccessible(TypeDeclarationInfo info, ImmutableHashSet<string> namespacesInScope, bool isInternalsVisible)
        {
            var minimumAccessibility = isInternalsVisible ? Accessibility.Internal : Accessibility.Public;

            return info.ContainingNamespace.Length > 0 &&
                   !namespacesInScope.Contains(info.ContainingNamespace) &&
                   info.Accessibility >= minimumAccessibility;
        }

        public static ImmutableArray<TypeDeclarationInfo> GetTypeDeclarationInfos(IAssemblySymbol assemblySymbol)
        {
            var builder = ArrayBuilder<TypeDeclarationInfo>.GetInstance();
            var root = assemblySymbol.GlobalNamespace;
            VisitSymbol(root, string.Empty, builder);
            return builder.ToImmutableAndFree();
        }

        private static void VisitSymbol(ISymbol symbol, string containingNamespace, ArrayBuilder<TypeDeclarationInfo> builder)
        {
            if (symbol is INamespaceSymbol namespaceSymbol)
            {
                containingNamespace = ConcatNamespace(containingNamespace, namespaceSymbol.Name);
                foreach (var memberSymbol in namespaceSymbol.GetMembers())
                {
                    VisitSymbol(memberSymbol, containingNamespace, builder);
                }
            }
            else if (symbol is INamedTypeSymbol typeSymbol)
            {
                var info = new TypeDeclarationInfo(typeSymbol.Name, containingNamespace, typeSymbol.TypeKind, typeSymbol.DeclaredAccessibility, typeSymbol.Arity);
                builder.Add(info);
            }

            string ConcatNamespace(string prefix, string name)
            {
                Debug.Assert(prefix != null && name != null);
                if (prefix.Length == 0)
                {
                    return name;
                }

                return prefix + "." + name;
            }
        }

        private string DebugText => string.Format(
            DebugTextFormat,
            _debug_total_compilation, _debug_total_compilation_decl, _debug_total_compilation_time,
            _debug_total_pe, _debug_total_pe_decl, _debug_total_pe_time,
            _debug_total_time_with_ItemCreation);

        private int _debug_total_compilation = 0;
        private int _debug_total_compilation_decl = 0;
        private int _debug_total_compilation_time = 0;

        private int _debug_total_pe = 0;
        private int _debug_total_pe_decl = 0;
        private int _debug_total_pe_time = 0;

        private int _debug_total_time_with_ItemCreation = 0;

        private void DebugClear()
        {
            _debug_total_compilation = 0;
            _debug_total_compilation_decl = 0;
            _debug_total_compilation_time = 0;

            _debug_total_pe = 0;
            _debug_total_pe_decl = 0;
            _debug_total_pe_time = 0;

            _debug_total_time_with_ItemCreation = 0;
        }

        private const string DebugTextFormat = @"
Total Compilations: {0}
Total Declarations: {1}
Elapsed time: {2}

Total PEs: {3}
Total Declarations: {4}
Elapsed time: {5}

Total time: {6}";
    }
}
