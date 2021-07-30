// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.VisualStudio.IntegrationTest.Utilities
{
    public static class WellKnownProjectTemplates
    {
        public const string ClassLibrary = nameof(ClassLibrary);
        public const string ConsoleApplication = nameof(ConsoleApplication);
        public const string Website = nameof(Website);
        public const string WinFormsApplication = nameof(WinFormsApplication);
        public const string WpfApplication = nameof(WpfApplication);
        public const string WebApplication = nameof(WebApplication);
        public const string CSharpNetCoreClassLibrary = "Microsoft.CSharp.NETCore.ClassLibrary";
        public const string VisualBasicNetCoreClassLibrary = "Microsoft.VisualBasic.NETCore.ClassLibrary";
        public const string CSharpNetCoreConsoleApplication = "Microsoft.CSharp.NETCore.ConsoleApplication";
        public const string VisualBasicNetCoreConsoleApplication = "Microsoft.VisualBasic.NETCore.ConsoleApplication";
        public const string CSharpNetStandardClassLibrary = "Microsoft.CSharp.NETStandard.ClassLibrary";
        public const string VisualBasicNetStandardClassLibrary = "Microsoft.VisualBasic.NETStandard.ClassLibrary";
        public const string CSharpNetCoreUnitTest = "Microsoft.CSharp.NETCore.UnitTest";
        public const string CSharpNetCoreXUnitTest = "Microsoft.CSharp.NETCore.XUnitTest";
        public const string BlazorApplication = "BlazorTemplate.vstemplate";
        public const string WebTemplate = "WebTemplate.vstemplate";
        public const string WebTemplateCore = nameof(WebTemplateCore);

        /// <summary>
        /// Combined with <see cref="BlazorApplication"/> to create a blazor application.
        /// </summary>
        public const string BlazorTemplateParameters = "|$groupid$=Microsoft.Web.Blazor.Wasm|$platformversion$=3.1";

        /// <summary>
        /// Combined with <see cref="WebTemplateCore"/> to create a ASP.Net Core application with sample razor pages.
        /// </summary>
        public const string RazorPageTemplateParameters = "|$groupid$=Microsoft.Web.RazorPages|$platformversion$=3.1";

        /// <summary>
        /// Combined with <see cref="WebTemplate"/> to create a ASP.Net MVC application.
        /// </summary>
        public const string FrameworkMvcTemplateParameters = "|$targetframeworkversion$=4.7.2|$basetemplateid$=Microsoft.WAP.CSharp.MvcBasicApplication.v5.0|$applyauth$=NoAuth|$applyframeworkreferences$=MVC";
    }
}
