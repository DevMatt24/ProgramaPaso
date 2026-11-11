namespace ProgramaPaso.Domain;

public class QuizEngine
{
    private readonly Random _rng = new();

    // Garantiza que aparezcan ejercicios interactivos (completar/ordenar) mezclados con los de opción
    public List<Question> BuildQuiz(List<Question> pool, int count)
    {
        var interactive = pool.Where(q => q.Type != "choice")
            .OrderBy(_ => _rng.Next())
            .Take(Math.Min(3, count / 2))
            .ToList();

        var rest = pool.Except(interactive)
            .OrderBy(_ => _rng.Next())
            .Take(count - interactive.Count);

        return interactive.Concat(rest).OrderBy(_ => _rng.Next()).ToList();
    }

    // En C# las mayúsculas importan, los espacios no
    public static bool CheckText(Question q, string given)
    {
        var g = Normalize(given);
        return q.Answers.Any(a => Normalize(a) == g);
    }

    private static string Normalize(string s) => new(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
