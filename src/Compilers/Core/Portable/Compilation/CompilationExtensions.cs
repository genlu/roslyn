// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;

namespace Microsoft.CodeAnalysis
{
    public static class CompilationExtensions
    {
        public static INamespaceDeclaration GetDeclarationRoot(this Compilation compilation)
        {
            return compilation.GetDeclarationRoot();
        }
    }
}
