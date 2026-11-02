#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Masterly.FluentRegex.Learning;

/// <summary>
/// Learns one pattern for examples that can be aligned token by token. It starts with the most general
/// option per position that the examples allow, then specializes positions one step at a time until
/// every negative example is rejected, and finally undoes any specialization that turned out unnecessary.
/// </summary>
internal sealed class AlignedLearner
{
    private readonly IReadOnlyList<AlignedColumn> _columns;
    private readonly LearningContext _context;
    private readonly int[] _levels;

    private AlignedLearner(IReadOnlyList<AlignedColumn> columns, LearningContext context)
    {
        _columns = columns;
        _context = context;
        _levels = new int[columns.Count];
    }

    /// <summary>
    /// Learns the elements for <paramref name="examples"/>, or returns null when they cannot be aligned at
    /// <paramref name="granularity"/> or no specialization rejects every negative example.
    /// </summary>
    public static IReadOnlyList<LearnedElement>? TryLearn(IReadOnlyList<string> examples, TokenGranularity granularity, LearningContext context)
    {
        IReadOnlyList<AlignedColumn>? columns = BuildColumns(examples, granularity);
        if (columns == null)
        {
            return null;
        }

        AlignedLearner learner = new AlignedLearner(columns, context);
        if (!LearningContext.AcceptsAll(learner.CompileCurrent(), examples))
        {
            return null;
        }

        return learner.Separate() ? learner.Compose() : null;
    }

    private static IReadOnlyList<AlignedColumn>? BuildColumns(IReadOnlyList<string> examples, TokenGranularity granularity)
    {
        List<IReadOnlyList<ExampleToken>> tokenized = examples.Select(e => ExampleTokenizer.Tokenize(e, granularity)).ToList();
        string signature = ExampleTokenizer.SignatureOf(tokenized[0]);
        if (tokenized.Any(tokens => ExampleTokenizer.SignatureOf(tokens) != signature))
        {
            return null;
        }

        List<AlignedColumn> columns = new List<AlignedColumn>(signature.Length);
        for (int position = 0; position < signature.Length; position++)
        {
            List<string> texts = tokenized.Select(tokens => tokens[position].Text).ToList();
            AlignedColumn? column = AlignedColumn.Create(texts, tokenized[0][position].Category);
            if (column == null)
            {
                return null;
            }

            columns.Add(column);
        }

        return columns;
    }

    private bool Separate()
    {
        IReadOnlyList<string> accepted = _context.AcceptedNegatives(CompileCurrent());
        while (accepted.Count > 0)
        {
            if (!SpecializeOneStep(accepted))
            {
                return false;
            }

            accepted = _context.AcceptedNegatives(CompileCurrent());
        }

        Relax();
        return true;
    }

    /// <summary>
    /// Specializes the position whose next level rejects the most of the still-accepted negatives.
    /// When no single step rejects any of them, the leftmost position that can still specialize moves,
    /// so the search always makes progress and ends at the exact enumeration of every position.
    /// </summary>
    private bool SpecializeOneStep(IReadOnlyList<string> accepted)
    {
        int best = -1;
        int bestRejected = -1;
        for (int position = 0; position < _columns.Count; position++)
        {
            if (_levels[position] >= _columns[position].MaxLevel)
            {
                continue;
            }

            _levels[position]++;
            Regex trial = CompileCurrent();
            _levels[position]--;

            int rejected = accepted.Count(n => !trial.IsMatch(n));
            if (rejected > bestRejected)
            {
                best = position;
                bestRejected = rejected;
            }
        }

        if (best < 0)
        {
            return false;
        }

        _levels[best]++;
        return true;
    }

    /// <summary>Generalizes each position back as far as possible while every negative stays rejected.</summary>
    private void Relax()
    {
        for (int position = 0; position < _columns.Count; position++)
        {
            while (_levels[position] > 0)
            {
                _levels[position]--;
                if (_context.AcceptedNegatives(CompileCurrent()).Count > 0)
                {
                    _levels[position]++;
                    break;
                }
            }
        }
    }

    private Regex CompileCurrent() => _context.Compile(Compose());

    private IReadOnlyList<LearnedElement> Compose()
    {
        List<LearnedElement> elements = new List<LearnedElement>();
        for (int position = 0; position < _columns.Count; position++)
        {
            elements.AddRange(_columns[position].At(_levels[position]));
        }

        return LearnedElement.Simplify(elements);
    }
}
