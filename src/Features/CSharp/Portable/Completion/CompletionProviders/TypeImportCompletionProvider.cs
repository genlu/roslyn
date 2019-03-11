// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.Completion.Providers
{
    internal class TypeImportCompletionProvider : CommonCompletionProvider
    {
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

            var span = new TextSpan(position, length: 0);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var syntaxTree = semanticModel.SyntaxTree;

            var syntaxFacts = document.GetLanguageService<ISyntaxFactsService>();
            var semanticFacts = document.GetLanguageService<ISemanticFactsService>();

            var syntaxContext = CSharpSyntaxContext.CreateContext(document.Project.Solution.Workspace, semanticModel, position, cancellationToken);

            var items = await GetCompletionItemsAsync(document, syntaxContext, position, cancellationToken).ConfigureAwait(false);
            completionContext.AddItems(items);

            return;
        }

        private const string MetadataNameString = "MetadataName";

        protected override async Task<CompletionDescription> GetDescriptionWorkerAsync(
            Document document, CompletionItem item, CancellationToken cancellationToken)
        {
            if (item.Properties.TryGetValue(MetadataNameString, out var metadataName))
            {
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
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

        internal override bool IsInsertionTrigger(SourceText text, int characterPosition, OptionSet options)
        {
            return CompletionUtilities.IsTriggerCharacter(text, characterPosition, options);
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
                    var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false) as CSharpCompilation;

                    using (Logger.LogBlock(FunctionId.Completion_TypeImportCompletionProvider_GetCompletionItemsAsync, cancellationToken))
                    {
                        foreach (var info in GetAccessibleOutOfScopeDeclarationInfos(compilation, compilation, namespacesInScope, cancellationToken))
                        {
                            builder.Add(CreateCompletionItem(info));
                        }

                        // get declarations from directly referenced projects and metadata
                        foreach (var assembly in compilation.GetReferencedAssemblySymbols())
                        {
                            IEnumerable<TypeDeclarationInfo> infos;
                            var assemblyProject = project.Solution.GetProject(assembly, cancellationToken);
                            if (assemblyProject != null)
                            {
                                var projectCompilation = await assemblyProject.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
                                infos = GetAccessibleOutOfScopeDeclarationInfos(compilation, projectCompilation, namespacesInScope, cancellationToken);
                            }
                            else
                            {
                                infos = GetAccessibleOutOfScopeDeclarationInfos(compilation, assembly, namespacesInScope, cancellationToken);
                            }

                            foreach (var info in infos)
                            {
                                builder.Add(CreateCompletionItem(info));
                            }
                        }
                    }

                    _debug_total_time_with_ItemCreation = Environment.TickCount - tick;
                    return builder.ToImmutableAndFree();
                }
            }

            return ImmutableArray<CompletionItem>.Empty;
        }

        public static HashSet<INamespaceSymbol> GetNamespacesInScope(SemanticModel semanticModel, SyntaxNode location, CancellationToken cancellationToken)
        {
            var result = new HashSet<INamespaceSymbol>(semanticModel.GetUsingNamespacesInScope(location));

            var containingNamespaceDeclaration = location.GetAncestorOrThis<Syntax.NamespaceDeclarationSyntax>();
            var namespaceSymbol = semanticModel.GetDeclaredSymbol(containingNamespaceDeclaration, cancellationToken);

            if (namespaceSymbol != null)
            {
                while (!namespaceSymbol.IsGlobalNamespace)
                {
                    result.Add(namespaceSymbol);
                    namespaceSymbol = namespaceSymbol.ContainingNamespace;
                }
            }

            return result;
        }

        private IEnumerable<TypeDeclarationInfo> GetAccessibleOutOfScopeDeclarationInfos(
            Compilation compilation,
            Compilation fromCompilation,
            ImmutableHashSet<string> namespacesInScope,
            CancellationToken cancellationToken)
        {
            var isInternalVisible = compilation.Assembly.IsSameAssemblyOrHasFriendAccessTo(fromCompilation.Assembly);

            var tick = Environment.TickCount;

            var infos = fromCompilation.GetTypeDeclarationInfos();

            tick = Environment.TickCount - tick;

            _debug_total_compilation++;
            _debug_total_compilation_decl += infos.Length;
            _debug_total_compilation_time += tick;

            return infos.Where(info =>
                    info.NamespaceName.Length > 0 &&
                    !namespacesInScope.Contains(info.NamespaceName) &&
                    (isInternalVisible
                        ? info.Accessibility >= Accessibility.Internal
                        : info.Accessibility == Accessibility.Public));
        }

        private IEnumerable<TypeDeclarationInfo> GetAccessibleOutOfScopeDeclarationInfos(
            Compilation compilation,
            IAssemblySymbol fromAssembly,
            ImmutableHashSet<string> namespacesInScope,
            CancellationToken cancellationToken)
        {
            var isInternalVisible = compilation.Assembly.IsSameAssemblyOrHasFriendAccessTo(fromAssembly);

            var tick = Environment.TickCount;

            var infos = GetTypeDeclarationInfos(fromAssembly);

            tick = Environment.TickCount - tick;

            _debug_total_pe++;
            _debug_total_pe_decl += infos.Length;
            _debug_total_pe_time += tick;

            return infos.Where(info =>
                    info.NamespaceName.Length > 0 &&
                    !namespacesInScope.Contains(info.NamespaceName) &&
                    (isInternalVisible
                        ? info.Accessibility >= Accessibility.Internal
                        : info.Accessibility == Accessibility.Public));
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

        private const string GenericTypeNameManglingString = "`";
        private static readonly string[] s_aritySuffixesOneToNine = { "`1", "`2", "`3", "`4", "`5", "`6", "`7", "`8", "`9" };

        private static string GetAritySuffix(int arity)
        {
            Debug.Assert(arity > 0);
            return (arity <= 9) ? s_aritySuffixesOneToNine[arity - 1] : string.Concat(GenericTypeNameManglingString, arity.ToString(CultureInfo.InvariantCulture));
        }

        private static string ComposeAritySuffixedMetadataName(string name, int arity)
        {
            return arity == 0 ? name : name + GetAritySuffix(arity);
        }

        private static CompletionItem CreateCompletionItem(TypeDeclarationInfo declarationInfo)
        {
            var metadataname = ComposeAritySuffixedMetadataName(
                GetFullyQualifiedName(declarationInfo.NamespaceName, declarationInfo.TypeName),
                declarationInfo.Arity);

            return CommonCompletionItem.Create(
                declarationInfo.Arity == 0 ? declarationInfo.TypeName : declarationInfo.TypeName + "<>",
                displayTextSuffix: " (in " + (declarationInfo.NamespaceName.Length == 0 ? "global" : declarationInfo.NamespaceName) + ")",
                CompletionItemRules.Default,
                glyph: GetGlyph(declarationInfo),
                sortText: declarationInfo.TypeName,
                properties: ImmutableDictionary<string, string>.Empty.Add(MetadataNameString, metadataname));
        }

        private static string GetFullyQualifiedName(string namespaceName, string typeName)
        {
            if (namespaceName.Length == 0)
            {
                return typeName;
            }
            else
            {
                return namespaceName + "." + typeName;
            }
        }

        private static Glyph GetGlyph(TypeDeclarationInfo declarationInfo)
        {
            Glyph publicIcon;
            switch (declarationInfo.Kind)
            {
                case TypeKind.Interface:
                    publicIcon = Glyph.InterfacePublic;
                    break;
                case TypeKind.Class:
                    publicIcon = Glyph.ClassPublic;
                    break;
                case TypeKind.Struct:
                    publicIcon = Glyph.StructurePublic;
                    break;
                case TypeKind.Delegate:
                    publicIcon = Glyph.DelegatePublic;
                    break;
                case TypeKind.Enum:
                    publicIcon = Glyph.EnumPublic;
                    break;
                default:
                    throw new ArgumentException();
            }

            switch (declarationInfo.Accessibility)
            {
                case Accessibility.Private:
                    publicIcon += Glyph.ClassPrivate - Glyph.ClassPublic;
                    break;

                case Accessibility.Protected:
                case Accessibility.ProtectedAndInternal:
                case Accessibility.ProtectedOrInternal:
                    publicIcon += Glyph.ClassProtected - Glyph.ClassPublic;
                    break;

                case Accessibility.Internal:
                    publicIcon += Glyph.ClassInternal - Glyph.ClassPublic;
                    break;
            }

            return publicIcon;
        }

        private async Task<bool> IsSemanticTriggerCharacterAsync(Document document, int characterPosition, CancellationToken cancellationToken)
        {
            bool? result = await IsTriggerOnDotAsync(document, characterPosition, cancellationToken).ConfigureAwait(false);
            if (result.HasValue)
            {
                return result.Value;
            }

            return true;
        }

        private async Task<bool?> IsTriggerOnDotAsync(Document document, int characterPosition, CancellationToken cancellationToken)
        {
            var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            if (text[characterPosition] != '.')
            {
                return null;
            }

            // don't want to trigger after a number.  All other cases after dot are ok.
            var tree = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var token = tree.FindToken(characterPosition);
            if (token.Kind() == SyntaxKind.DotToken)
            {
                token = token.GetPreviousToken();
            }

            return token.Kind() != SyntaxKind.NumericLiteralToken;
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
