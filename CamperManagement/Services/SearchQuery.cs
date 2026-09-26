using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace CamperManagement.Services;

public static class SearchQuery
{
    public static IReadOnlyList<string> Parse(string? query)
    {
        var terms = new List<string>();
        var term = new StringBuilder();
        var quoted = false;
        void Flush()
        {
            if (term.Length > 0)
            {
                terms.Add(term.ToString());
                term.Clear();
            }
        }
        foreach (var ch in query ?? "")
            if (ch == '"')
            {
                Flush();
                quoted = !quoted;
            }
            else if (char.IsWhiteSpace(ch) && !quoted)
                Flush();
            else
                term.Append(ch);
        Flush();
        return terms;
    }
    public static bool Matches(string? query, params string?[] fields) => MatchesTerms(Parse(query), fields);
    public static bool MatchesTerms(IReadOnlyList<string> terms, params string?[] fields) => terms.All(term => fields.Any(field => field?.Contains(term, StringComparison.OrdinalIgnoreCase) == true));
}
