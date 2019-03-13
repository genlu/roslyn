// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.CodeAnalysis.Completion.Providers
{
    internal static class TypeImportCompletionItem
    {
        public static CompletionItem Create(TypeDeclarationInfo declarationInfo)
        {
            var metadataname = ComposeAritySuffixedMetadataName(
                GetFullyQualifiedName(declarationInfo.ContainingNamespace, declarationInfo.Name),
                declarationInfo.Arity);

            return CommonCompletionItem.Create(
                declarationInfo.Arity == 0 ? declarationInfo.Name : declarationInfo.Name + "<>",
                displayTextSuffix: $" (in {declarationInfo.ContainingNamespace})",
                CompletionItemRules.Default,
                glyph: GetGlyph(declarationInfo),
                sortText: declarationInfo.Name,
                properties: ImmutableDictionary<string, string>.Empty
                    .Add(MetadataNameString, metadataname)
                    .Add(ContainingNamespaceString, declarationInfo.ContainingNamespace));
        }

        public static bool TryGetMetadataName(CompletionItem item, out string metadataName)
        {
            return item.Properties.TryGetValue(MetadataNameString, out metadataName);
        }

        public static bool TryGetContainingNamespace(CompletionItem item, out string ContainingNamespace)
        {
            return item.Properties.TryGetValue(ContainingNamespaceString, out ContainingNamespace);
        }

        private const string MetadataNameString = "MetadataName";
        private const string ContainingNamespaceString = "ContainingNamespace";

        private const string GenericTypeNameManglingString = "`";
        private static readonly string[] s_aritySuffixesOneToNine = { "`1", "`2", "`3", "`4", "`5", "`6", "`7", "`8", "`9" };

        private static string GetAritySuffix(int arity)
        {
            Debug.Assert(arity > 0);
            return (arity <= 9) ? s_aritySuffixesOneToNine[arity - 1] : string.Concat(GenericTypeNameManglingString, arity.ToString(CultureInfo.InvariantCulture));
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

        private static string ComposeAritySuffixedMetadataName(string name, int arity)
        {
            return arity == 0 ? name : name + GetAritySuffix(arity);
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
    }
}
