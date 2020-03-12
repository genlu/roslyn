// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using static Microsoft.CodeAnalysis.CodeActions.CodeAction;

namespace Microsoft.CodeAnalysis.CodeRefactorings.InlineMethod
{
    internal abstract class AbstractInlineSimpleMethodCodeRefactoringProvider<TMethodDeclarationSyntax> : CodeRefactoringProvider
        where TMethodDeclarationSyntax : SyntaxNode
    {
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var (document, span, cancellationToken) = context;

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var syntaxFacts = document.GetRequiredLanguageService<ISyntaxFactsService>();

            if (!(span.IsEmpty
                && syntaxFacts.IsOnMethodHeader(root, span.Start, out var node)
                && node is TMethodDeclarationSyntax methodDeclarationNode
                && IsSimpleMethod(methodDeclarationNode)))
            {
                return;
            }

            var syntaxGenerator = document.GetRequiredLanguageService<SyntaxGenerator>();
            var modifiers = syntaxGenerator.GetModifiers(methodDeclarationNode);

            if (modifiers.IsAbstract
                || modifiers.IsUnsafe
                || modifiers.IsRef
                || modifiers.IsAsync
                || modifiers.IsPartial)
            {
                return;
            }

            var semanticModel = await document.GetRequiredSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (!(semanticModel.GetDeclaredSymbol(node, cancellationToken) is IMethodSymbol methodSymbol)
                || methodSymbol.IsExtensionMethod
                || methodSymbol.Arity > 0)
            {
                return;
            }

            context.RegisterRefactoring(new InlineMethodCodeAction("Inline simple method", c => InlineSimpleMethodAsync(document, methodSymbol, c)));
        }

        private async Task<Solution> InlineSimpleMethodAsync(Document document, IMethodSymbol methodSymbol, CancellationToken cancellationToken)
        {
            var progress = new StreamingProgressCollector(StreamingFindReferencesProgress.Instance);
            await SymbolFinder.FindReferencesAsync(
                symbolAndProjectId: SymbolAndProjectId.Create(methodSymbol, document.Project.Id),
                solution: document.Project.Solution,
                documents: null,
                progress: progress,
                options: FindReferencesSearchOptions.Default,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var referencedSymbols = progress.GetReferencedSymbols();
            var directReferences = referencedSymbols.First(s => SymbolEqualityComparer.Default.Equals(methodSymbol, s.Definition));

            return document.Project.Solution;
        }

        protected abstract bool IsSimpleMethod(TMethodDeclarationSyntax methodDeclaration);

        private class InlineMethodCodeAction : SolutionChangeAction
        {
            public InlineMethodCodeAction(string title, Func<CancellationToken, Task<Solution>> createChangedSolution)
                : base(title, createChangedSolution)
            {
            }
        }
    }
}
