// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.Completion
{
    public enum CompletionItemSelectionBehavior
    {
        Default,

        /// <summary>
        /// If no text has been typed, the item should be soft selected. This is appropriate for 
        /// completion providers that want to provide suggestions that shouldn't interfere with 
        /// typing.  For example a provider that comes up on space might offer items that are soft
        /// selected so that an additional space (or other puntuation character) will not then 
        /// commit that item.
        /// </summary>
        SoftSelection,

        /// <summary>
        /// If no text has been typed, the item should be hard selected.  This is appropriate for
        /// completion providers that are providing suggestions the user is nearly certain to 
        /// select.  Because the item is hard selected, any commit characters typed after it will
        /// cause it to be committed.
        /// </summary>
        HardSelection,
    }
}
