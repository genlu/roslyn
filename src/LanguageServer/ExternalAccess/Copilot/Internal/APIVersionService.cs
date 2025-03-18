// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

namespace Microsoft.CodeAnalysis.LanguageServer.ExternalAccess.Copilot.Internal;

[Shared]
[Method("roslyn/copilot/getExternalAccessVersion")]
[ExportCSharpVisualBasicStatelessLspService(typeof(CopilotAPIVersionRequestHandler))]
internal sealed class CopilotAPIVersionRequestHandler : ILspServiceRequestHandler<CopilotAPIVersionRequest, int>
{
    private const int CurrentVersion = 0;

    [ImportingConstructor]
    [Obsolete("This exported object must be obtained through the MEF export provider.", error: true)]
    public CopilotAPIVersionRequestHandler()
    {
    }

    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => false;

    public Task<int> HandleRequestAsync(CopilotAPIVersionRequest param, RequestContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(CurrentVersion);
    }
}

internal sealed class CopilotAPIVersionRequest
{
    [JsonPropertyName("extensionVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExtensionVersion { get; set; }
}
