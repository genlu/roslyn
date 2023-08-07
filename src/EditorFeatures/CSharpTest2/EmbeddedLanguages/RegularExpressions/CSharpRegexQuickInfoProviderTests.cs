// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.UnitTests.Workspaces;
using Microsoft.CodeAnalysis.LanguageService;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.EmbeddedLanguages.RegularExpressions
{
    [UseExportProvider]
    public class CSharpRegexQuickInfoProviderTests
    {
        [Theory, CombinatorialData]
        public async Task TestRegexString(bool useNET7)
        {
            var code = """
                using System.Text.RegularExpressions;

                class A
                {
                    public static void Main()
                    {
                        var regex = new Regex(@"$$\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b");
                    }
                }
                """;

            using (var workspace = GetTestWorkspace(code, useNET7))
            {
                var position = workspace.Documents.Single().CursorPosition.Value;
                var document = workspace.CurrentSolution.GetDocument(workspace.Documents.First().Id);
                var actualItem = await GetQuickInfoItemAsync(document, position);
                
                // only has semantic QuickInfo since no regex provider is implemented yet
                Assert.Equal("class System.String", GetJoinedText(actualItem, QuickInfoSectionKinds.Description));
            }
        }

        static string GetJoinedText(QuickInfoItem item, string kind)
        {
            return item.Sections.First(s => s.Kind == kind).TaggedParts.JoinText();
        }

        private static TestWorkspace GetTestWorkspace(string code, bool useNet7 = true)
        {
            var commonReferenceSetting = useNet7 ? "CommonReferencesNet7" : "CommonReferencesNet45";

            var markup = $"""
                <Workspace>
                    <Project Language="C#" {commonReferenceSetting}="true">
                        <Document>
                {code}
                        </Document>
                    </Project>
                </Workspace>
                """;

            return TestWorkspace.Create(markup, composition: FeaturesTestCompositions.Features);
        }

        private static Task<QuickInfoItem> GetQuickInfoItemAsync(Document document, int position)
        {
            var quickInfoService = document.GetRequiredLanguageService<QuickInfoService>();
            return quickInfoService.GetQuickInfoAsync(document, position, SymbolDescriptionOptions.Default, CancellationToken.None);
        }
    }
}

