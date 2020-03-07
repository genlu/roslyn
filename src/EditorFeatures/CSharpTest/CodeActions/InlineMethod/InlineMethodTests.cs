// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CodeRefactorings.InlineMethod;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings.InlineMethod
{
    public class InlineMethodTests : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, TestParameters parameters)
            => new InlineMethodCodeRefactoringProvider();

        [Fact]
        public async Task InlinePrivateExpressionBody()
        {
            await TestInRegularAndScriptAsync(@"
class C
{
    void M1()
    {
        var x = [|M2();|]
    }

    bool M2() => true;
}", @"
class C
{
    void M1()
    {
        var x = true;
    }
}");
        }
    }
}
