// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis
{
    public readonly struct TypeDeclarationInfo
    {
        public TypeDeclarationInfo(string typeName, string namespaceName, TypeKind kind, Accessibility accessibility, int arity)
        {
            Name = typeName;
            ContainingNamespace = namespaceName;
            Kind = kind;
            Accessibility = accessibility;
            Arity = arity;
        }

        public string Name { get; }

        public string ContainingNamespace { get; }

        public TypeKind Kind { get; }

        public Accessibility Accessibility { get; }

        public int Arity { get; }
    }
}
