// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.IntegrationTest.Utilities;
using Microsoft.VisualStudio.IntegrationTest.Utilities.Input;
using Microsoft.VisualStudio.Utilities;
using Roslyn.Test.Utilities;
using Xunit;
using ProjectUtils = Microsoft.VisualStudio.IntegrationTest.Utilities.Common.ProjectUtils;

namespace Roslyn.VisualStudio.IntegrationTests.CSharp
{
    [Collection(nameof(SharedIntegrationHostFixture))]
    public class CSharpRazorIntellisense : AbstractEditorTest
    {
        protected override string LanguageName => LanguageNames.CSharp;

        public CSharpRazorIntellisense(VisualStudioInstanceFactory instanceFactory)
            : base(instanceFactory, nameof(CSharpRazor), WellKnownProjectTemplates.WebTemplate, WellKnownProjectTemplates.FrameworkMvcTemplateParameters)
        {
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync().ConfigureAwait(true);

            // Disable import completion.
            VisualStudio.Workspace.SetImportCompletionOption(false);
        }

        [WpfFact, Trait(Traits.Feature, Traits.Features.Razor)]
        [WorkItem(51702, "https://github.com/dotnet/roslyn/issues/51702")]
        public void TypingInScriptBlock()
        {
            var project = new ProjectUtils.Project(ProjectName);
            VisualStudio.SolutionExplorer.OpenFile(project, "Views/Home/Index.cshtml");

            VisualStudio.Editor.SetText(
@"<html>
<head> 
    <script> 
        const promises = bodies.map(b => { 
            const configurationType: string | undefined = b.body?.configuration?.type; 
            console.log(configurationType);

            console.log(""$$"")
 
            switch (configurationType) {
 
                case ""designerJson"": 
                    const p = Promise.resolve({ lastAuthor: b.body.configuration.designerJson.authoredBy?.uniqueName, type: configurationType as string, pipelineEntry: b.pipelineEntry }); 
                    return p;
 
                case ""yaml"": 
                    switch (b.body.configuration.repository.type) { 
                        case ""azureReposGit"": 
                            const s = getJson(""https://dev.azure.com/"" + orgName + ""/"" + b.pipelineEntry.project + ""/_apis/git/repositories/"" + b.body.configuration.repository.id + ""/commits?api-version=6.1-preview.1&searchCriteria.itemPath="" + b.body.configuration.path, {} 
                            ).then(v => { 
                                if (v.body.innerException) { 
                                    return { lastAuthor: ""unknown (exception getting YAML)"", type: configurationType as string, pipelineEntry: b.pipelineEntry } 
                                } 
                            }); 
                            return s; 
                        } 

                default: 
                    throw new Error(""aaah "" + configurationType); 
            } 
        }); 
    </script> 
</head> 
<body> 
</body> 
</html>");

            VisualStudio.Editor.PlaceCaret("$$", bufferContentType: StandardContentTypeNames.Projection);
            VisualStudio.Editor.Activate();

            VisualStudio.Editor.SendKeys("@Mod");
            VisualStudio.Editor.TriggerCompletion();
            Assert.Equal("Model", VisualStudio.Editor.GetCurrentCompletionItem());

            VisualStudio.Editor.SendKeys(VirtualKey.Tab);
            VisualStudio.Editor.Verify.TextContains(
@"<html>
<head> 
    <script> 
        const promises = bodies.map(b => { 
            const configurationType: string | undefined = b.body?.configuration?.type; 
            console.log(configurationType);

            console.log(""@Model"")
 
            switch (configurationType) {
 
                case ""designerJson"": 
                    const p = Promise.resolve({ lastAuthor: b.body.configuration.designerJson.authoredBy?.uniqueName, type: configurationType as string, pipelineEntry: b.pipelineEntry }); 
                    return p;
 
                case ""yaml"": 
                    switch (b.body.configuration.repository.type) { 
                        case ""azureReposGit"": 
                            const s = getJson(""https://dev.azure.com/"" + orgName + ""/"" + b.pipelineEntry.project + ""/_apis/git/repositories/"" + b.body.configuration.repository.id + ""/commits?api-version=6.1-preview.1&searchCriteria.itemPath="" + b.body.configuration.path, {} 
                            ).then(v => { 
                                if (v.body.innerException) { 
                                    return { lastAuthor: ""unknown (exception getting YAML)"", type: configurationType as string, pipelineEntry: b.pipelineEntry } 
                                } 
                            }); 
                            return s; 
                        } 

                default: 
                    throw new Error(""aaah "" + configurationType); 
            } 
        }); 
    </script> 
</head> 
<body> 
</body> 
</html>");
        }
    }
}
