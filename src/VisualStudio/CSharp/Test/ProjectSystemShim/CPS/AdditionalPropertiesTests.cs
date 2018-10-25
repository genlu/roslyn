// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem.CPS;
using Microsoft.VisualStudio.LanguageServices.UnitTests.ProjectSystemShim.Framework;
using Roslyn.Test.Utilities;
using Xunit;

namespace Roslyn.VisualStudio.CSharp.UnitTests.ProjectSystemShim.CPS
{
    [UseExportProvider]
    public class AdditionalPropertiesTests
    {
        [WpfFact]
        [Trait(Traits.Feature, Traits.Features.ProjectSystemShims)]
        public void SetProperty_DefaultNamespace_CPS()
        {
            using (var environment = new TestEnvironment())
            using (var project = CSharpHelpers.CreateCSharpCPSProject(environment, "Test"))
            {
                Assert.Null(DefaultNamespaceOfSingleProject(environment));

                var defaultNamespace = "Foo.Bar";
                project.SetProperty(AdditionalPropertyNames.DefaultNamespace, defaultNamespace);
                Assert.Equal(defaultNamespace, DefaultNamespaceOfSingleProject(environment));
            }

            string DefaultNamespaceOfSingleProject(TestEnvironment environment)
                => environment.Workspace.CurrentSolution.Projects.Single().DefaultNamespace;
        }
    }
}
