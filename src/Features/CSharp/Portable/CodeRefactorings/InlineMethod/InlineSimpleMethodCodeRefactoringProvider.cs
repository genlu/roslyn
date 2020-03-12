// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CodeRefactorings.InlineMethod;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod
{
    internal sealed class InlineSimpleMethodCodeRefactoringProvider
        : AbstractInlineSimpleMethodCodeRefactoringProvider<MethodDeclarationSyntax>
    {
        protected override bool IsSimpleMethod(MethodDeclarationSyntax methodDeclaration)
            => methodDeclaration.ExpressionBody != null
                && methodDeclaration.TypeParameterList == null
                && methodDeclaration.ExplicitInterfaceSpecifier == null;
    }
}
