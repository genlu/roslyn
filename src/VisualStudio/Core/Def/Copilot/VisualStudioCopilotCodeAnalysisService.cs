// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Copilot;
using Microsoft.CodeAnalysis.ErrorReporting;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Copilot;
using Microsoft.VisualStudio.Shell.ServiceBroker;

namespace Microsoft.VisualStudio.LanguageServices.Copilot;

[ExportWorkspaceService(typeof(ICopilotCodeAnalysisService), layer: ServiceLayer.Host), Shared]
internal sealed class VisualStudioCopilotCodeAnalysisService : ICopilotCodeAnalysisService
{
    private const string Id = "Microsoft.CodeAnalysis.CopilotAnalyzer";
    private IVsService<SVsBrokeredServiceContainer, IBrokeredServiceContainer> BrokeredServiceContainer { get; }

    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public VisualStudioCopilotCodeAnalysisService(IVsService<SVsBrokeredServiceContainer, IBrokeredServiceContainer> brokeredServiceContainer)
    {
        BrokeredServiceContainer = brokeredServiceContainer;
    }

    public async Task<ImmutableArray<Diagnostic>> AnalyzeDocumentAsync(Document document, CancellationToken cancellationToken)
    {

        var serviceContainer = await BrokeredServiceContainer.GetValueAsync(cancellationToken).ConfigureAwait(false);
        var serviceBroker = serviceContainer.GetFullAccessServiceBroker();
        try
        {
            return await AnalyzeDocumentWorkerAsync(serviceBroker, document, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (FatalError.ReportAndCatchUnlessCanceled(e, cancellationToken))
        {
            return ImmutableArray<Diagnostic>.Empty;
        }
    }

    // Guard against when Copilot chat extension is not installed
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeDocumentWorkerAsync(IServiceBroker serviceBroker, Document document, CancellationToken cancellationToken)
    {
        using (var copilotService = await serviceBroker.GetProxyAsync<ICopilotService>(CopilotDescriptors.CopilotService, cancellationToken).ConfigureAwait(false))
        {
            if (copilotService is not null && await copilotService.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false))
            {
                var options = new CopilotSessionOptions(new CopilotClientId(Id));
                using var session = await copilotService.StartSessionAsync(options, cancellationToken).ConfigureAwait(false);

                var request = CreateRequest(document);

                var response = await session.SendRequestAsync(request, cancellationToken).ConfigureAwait(false);
                return ParseResponse(response);
            }
        }

        return ImmutableArray<Diagnostic>.Empty;

#pragma warning disable IDE0060 // Remove unused parameter

        static CopilotRequest CreateRequest(Document document)
            => throw new NotImplementedException();

        static ImmutableArray<Diagnostic> ParseResponse(CopilotResponse response)
            => throw new NotImplementedException();

#pragma warning restore IDE0060 // Remove unused parameter
    }
}
