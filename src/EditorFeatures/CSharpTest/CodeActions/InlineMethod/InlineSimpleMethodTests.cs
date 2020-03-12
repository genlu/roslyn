// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CodeRefactorings.InlineMethod;
using Microsoft.CodeAnalysis.CSharp.CodeRefactorings.InlineMethod;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.CodeRefactorings.InlineMethod
{
    public class InlineSimpleMethodTests : AbstractCSharpCodeActionTest
    {
        protected override CodeRefactoringProvider CreateCodeRefactoringProvider(Workspace workspace, TestParameters parameters)
            => new InlineSimpleMethodCodeRefactoringProvider();

        [Fact]
        public async Task InlineExpressionBody()
        {
            await TestInRegularAndScriptAsync(@"
public class C
{
    public void M1()
    {
        var x = M();
    }

    public bool [||]M() => true;
}", @"
class C
{
    void M1()
    {
        var x = true;
    }
}");
        }

        [Fact]
        public async Task InlineInterfaceMethodExpressionBody()
        {
            await TestInRegularAndScriptAsync(@"
public interface I
{
    bool M();
}
public class C : I
{
    public void M1()
    {
        //var x = M();
    }

    public void M2(I i)
    {
        var x = i.M();
    }

    public bool [||]M() => true;
}", @"");
        }
    }
}
