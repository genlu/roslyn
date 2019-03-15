// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis
{
    public interface INamespaceOrTypeDeclaration
    {
        bool IsNamespace { get; }

        bool IsType { get; }

        string Name { get; }
    }

    public interface INamespaceDeclaration : INamespaceOrTypeDeclaration
    {
        ImmutableArray<INamespaceOrTypeDeclaration> Children { get; }
    }

    public interface ITypeDeclaration : INamespaceOrTypeDeclaration
    {
        TypeKind TypeKind { get; }

        Accessibility Accessibility { get; }

        int Arity { get; }
    }
}
