// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Completion
{
    /// <summary>
    /// Rules for how the individual items are handled.
    /// </summary>
    public sealed class CompletionItemRules
    {
        /// <summary>
        /// The rule used when no rule is specified when constructing a <see cref="CompletionItem"/>.
        /// </summary>
        public static readonly CompletionItemRules Default =
            new(
                filterCharacterRules: default,
                commitCharacterRules: default,
                enterKeyRule: EnterKeyRule.Default,
                formatOnCommit: false,
                matchPriority: Completion.MatchPriority.Default,
                sortPriority: Completion.SortPriority.Default,
                selectionBehavior: CompletionItemSelectionBehavior.Default);

        /// <summary>
        /// Rules that modify the set of characters that can be typed to filter the list of completion items.
        /// </summary>
        public ImmutableArray<CharacterSetModificationRule> FilterCharacterRules { get; }

        /// <summary>
        /// Rules that modify the set of characters that can be typed to cause the selected item to be committed.
        /// </summary>
        public ImmutableArray<CharacterSetModificationRule> CommitCharacterRules { get; }

        /// <summary>
        /// A rule about whether the enter key is passed through to the editor after the selected item has been committed.
        /// </summary>
        public EnterKeyRule EnterKeyRule { get; }

        /// <summary>
        /// True if the modified text should be formatted automatically.
        /// </summary>
        public bool FormatOnCommit { get; }

        /// <summary>
        /// An additional hint to the selection algorithm that can augment or override the existing text-based matching.
        /// Refer to <see cref="Completion.MatchPriority"/> for preset values.
        /// </summary>
        public int MatchPriority { get; }

        /// <summary>
        /// An additional hint to the sorting algorithm that takes precedence over text-based sorting.
        /// Refer to <see cref="Completion.SortPriority"/> for preset values.
        /// </summary>
        public int SortPriority { get; }

        /// <summary>
        /// How this item should be selected when the completion list first appears and
        /// before the user has typed any characters.
        /// </summary>
        public CompletionItemSelectionBehavior SelectionBehavior { get; }

        private CompletionItemRules(
            ImmutableArray<CharacterSetModificationRule> filterCharacterRules,
            ImmutableArray<CharacterSetModificationRule> commitCharacterRules,
            EnterKeyRule enterKeyRule,
            bool formatOnCommit,
            int matchPriority,
            int sortPriority,
            CompletionItemSelectionBehavior selectionBehavior)
        {
            FilterCharacterRules = filterCharacterRules.NullToEmpty();
            CommitCharacterRules = commitCharacterRules.NullToEmpty();
            EnterKeyRule = enterKeyRule;
            FormatOnCommit = formatOnCommit;
            MatchPriority = matchPriority;
            SortPriority = sortPriority;
            SelectionBehavior = selectionBehavior;
        }

        /// <summary>
        /// Creates a new <see cref="CompletionItemRules"/> instance.
        /// </summary>
        /// <param name="filterCharacterRules">Rules about which keys typed are used to filter the list of completion items.</param>
        /// <param name="commitCharacterRules">Rules about which keys typed caused the completion item to be committed.</param>
        /// <param name="enterKeyRule">Rule about whether the enter key is passed through to the editor after the selected item has been committed.</param>
        /// <param name="formatOnCommit">True if the modified text should be formatted automatically.</param>
        /// <param name="matchPriority">True if the related completion item should be initially selected.</param>
        /// <returns></returns>
        public static CompletionItemRules Create(
            ImmutableArray<CharacterSetModificationRule> filterCharacterRules,
            ImmutableArray<CharacterSetModificationRule> commitCharacterRules,
            EnterKeyRule enterKeyRule,
            bool formatOnCommit,
            int? matchPriority)
        {
            return Create(
                filterCharacterRules, commitCharacterRules,
                enterKeyRule, formatOnCommit, matchPriority,
                sortPriority: Completion.SortPriority.Default,
                selectionBehavior: CompletionItemSelectionBehavior.Default);
        }

        /// <summary>
        /// Creates a new <see cref="CompletionItemRules"/> instance.
        /// </summary>
        /// <param name="filterCharacterRules">Rules about which keys typed are used to filter the list of completion items.</param>
        /// <param name="commitCharacterRules">Rules about which keys typed caused the completion item to be committed.</param>
        /// <param name="enterKeyRule">Rule about whether the enter key is passed through to the editor after the selected item has been committed.</param>
        /// <param name="formatOnCommit">True if the modified text should be formatted automatically.</param>
        /// <param name="matchPriority">True if the related completion item should be initially selected.</param>
        /// <param name="selectionBehavior">How this item should be selected if no text has been typed after the completion list is brought up.</param>
        /// <returns></returns>
        public static CompletionItemRules Create(
            ImmutableArray<CharacterSetModificationRule> filterCharacterRules = default,
            ImmutableArray<CharacterSetModificationRule> commitCharacterRules = default,
            EnterKeyRule enterKeyRule = EnterKeyRule.Default,
            bool formatOnCommit = false,
            int? matchPriority = null,
            int? sortPriority = null,
            CompletionItemSelectionBehavior selectionBehavior = CompletionItemSelectionBehavior.Default)
        {
            if (filterCharacterRules.IsDefaultOrEmpty &&
                commitCharacterRules.IsDefaultOrEmpty &&
                enterKeyRule == Default.EnterKeyRule &&
                formatOnCommit == Default.FormatOnCommit &&
                matchPriority.GetValueOrDefault() == Default.MatchPriority &&
                sortPriority.GetValueOrDefault() == Default.SortPriority &&
                selectionBehavior == Default.SelectionBehavior)
            {
                return Default;
            }
            else
            {
                return new CompletionItemRules(
                    filterCharacterRules, commitCharacterRules, enterKeyRule, formatOnCommit,
                    matchPriority.GetValueOrDefault(), sortPriority.GetValueOrDefault(), selectionBehavior);
            }
        }

        /// <summary>
        /// Creates a new <see cref="CompletionItemRules"/> instance--internal for TypeScript.
        /// </summary>
        /// <param name="filterCharacterRules">Rules about which keys typed are used to filter the list of completion items.</param>
        /// <param name="commitCharacterRules">Rules about which keys typed caused the completion item to be committed.</param>
        /// <param name="enterKeyRule">Rule about whether the enter key is passed through to the editor after the selected item has been committed.</param>
        /// <param name="formatOnCommit">True if the modified text should be formatted automatically.</param>
        /// <param name="preselect">True if the related completion item should be initially selected.</param>
        /// <returns></returns>
        internal static CompletionItemRules Create(
            ImmutableArray<CharacterSetModificationRule> filterCharacterRules,
            ImmutableArray<CharacterSetModificationRule> commitCharacterRules,
            EnterKeyRule enterKeyRule,
            bool formatOnCommit,
            bool preselect)
        {
            var matchPriority = preselect ? Completion.MatchPriority.Preselect : Completion.MatchPriority.Default;
            return CompletionItemRules.Create(filterCharacterRules, commitCharacterRules, enterKeyRule, formatOnCommit, matchPriority);
        }

        private CompletionItemRules With(
            Optional<ImmutableArray<CharacterSetModificationRule>> filterRules = default,
            Optional<ImmutableArray<CharacterSetModificationRule>> commitRules = default,
            Optional<EnterKeyRule> enterKeyRule = default,
            Optional<bool> formatOnCommit = default,
            Optional<int> matchPriority = default,
            Optional<int> sortPriority = default,
            Optional<CompletionItemSelectionBehavior> selectionBehavior = default)
        {
            var newFilterRules = filterRules.HasValue ? filterRules.Value : FilterCharacterRules;
            var newCommitRules = commitRules.HasValue ? commitRules.Value : CommitCharacterRules;
            var newEnterKeyRule = enterKeyRule.HasValue ? enterKeyRule.Value : EnterKeyRule;
            var newFormatOnCommit = formatOnCommit.HasValue ? formatOnCommit.Value : FormatOnCommit;
            var newMatchPriority = matchPriority.HasValue ? matchPriority.Value : MatchPriority;
            var newSortPriority = sortPriority.HasValue ? sortPriority.Value : SortPriority;
            var newSelectionBehavior = selectionBehavior.HasValue ? selectionBehavior.Value : SelectionBehavior;

            if (newFilterRules == FilterCharacterRules &&
                newCommitRules == CommitCharacterRules &&
                newEnterKeyRule == EnterKeyRule &&
                newFormatOnCommit == FormatOnCommit &&
                newMatchPriority == MatchPriority &&
                newSortPriority == SortPriority &&
                newSelectionBehavior == SelectionBehavior)
            {
                return this;
            }
            else
            {
                return Create(
                    newFilterRules, newCommitRules,
                    newEnterKeyRule, newFormatOnCommit,
                    newMatchPriority, newSortPriority, newSelectionBehavior);
            }
        }

        /// <summary>
        /// Creates a copy of this <see cref="CompletionItemRules"/> with the <see cref="FilterCharacterRules"/> property changed.
        /// </summary>
        public CompletionItemRules WithFilterCharacterRules(ImmutableArray<CharacterSetModificationRule> filterCharacterRules)
            => With(filterRules: filterCharacterRules);

        internal CompletionItemRules WithFilterCharacterRule(CharacterSetModificationRule rule)
            => With(filterRules: ImmutableArray.Create(rule));

        internal CompletionItemRules WithCommitCharacterRule(CharacterSetModificationRule rule)
            => With(commitRules: ImmutableArray.Create(rule));

        /// <summary>
        /// Creates a copy of this <see cref="CompletionItemRules"/> with the <see cref="CommitCharacterRules"/> property changed.
        /// </summary>
        public CompletionItemRules WithCommitCharacterRules(ImmutableArray<CharacterSetModificationRule> commitCharacterRules)
            => With(commitRules: commitCharacterRules);

        /// <summary>
        /// Creates a copy of this <see cref="CompletionItemRules"/> with the <see cref="EnterKeyRule"/> property changed.
        /// </summary>
        public CompletionItemRules WithEnterKeyRule(EnterKeyRule enterKeyRule)
            => With(enterKeyRule: enterKeyRule);

        /// <summary>
        /// Creates a copy of this <see cref="CompletionItemRules"/> with the <see cref="FormatOnCommit"/> property changed.
        /// </summary>
        public CompletionItemRules WithFormatOnCommit(bool formatOnCommit)
            => With(formatOnCommit: formatOnCommit);

        /// <summary>
        /// Creates a copy of this <see cref="CompletionItemRules"/> with the <see cref="MatchPriority"/> property changed.
        /// </summary>
        public CompletionItemRules WithMatchPriority(int matchPriority)
            => With(matchPriority: matchPriority);

        /// <summary>
        /// Creates a copy of this <see cref="CompletionItemRules"/> with the <see cref="SortPriority"/> property changed.
        /// </summary>
        public CompletionItemRules WithSortPriority(int sortPriority)
            => With(sortPriority: sortPriority);

        /// <summary>
        /// Creates a copy of this <see cref="CompletionItemRules"/> with the <see cref="SelectionBehavior"/> property changed.
        /// </summary>
        public CompletionItemRules WithSelectionBehavior(CompletionItemSelectionBehavior selectionBehavior)
            => With(selectionBehavior: selectionBehavior);
    }
}
