// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CodeAnalysis.Completion
{
    /// <summary>
    /// An additional hint to the sorting algorithm that takes precedence over text-based sorting.
    /// </summary>
    public static class SortPriority
    {
        public static readonly int Low = int.MinValue / 2;
        public static readonly int Default = 0;
        public static readonly int High = int.MaxValue / 2;
    }
}
