namespace ProgramaPaso.Domain;

public class QuizEngine
{
    private readonly Random _rng = new();

    // Garantiza que aparezcan ejercicios interactivos (completar/ordenar) mezclados con los de opción
    public List<Question> BuildQuiz(List<Question> pool, int count)
    {
        // Nivel de retos: van en el orden del archivo (de menos a más difícil)
        if (pool.Any(q => q.Type == "code")) return pool.Take(count).ToList();

        var interactive = pool.Where(q => q.Type != "choice")
            .OrderBy(_ => _rng.Next())
            .Take(Math.Min(3, count / 2))
            .ToList();

        var rest = pool.Except(interactive)
            .OrderBy(_ => _rng.Next())
            .Take(count - interactive.Count);

        return interactive.Concat(rest).OrderBy(_ => _rng.Next()).ToList();
    }

    // Arma el quiz según el dominio del alumno por tema:
    //  - los temas más flojos tienen más probabilidad de salir
    //  - hasta 2 preguntas son de REPASO de niveles anteriores (solo temas con dominio < 65)
    public List<Question> BuildAdaptiveQuiz(List<Question> pool, List<Question> reviewPool,
        IReadOnlyDictionary<string, double> mastery, int count)
    {
        if (pool.Any(q => q.Type == "code")) return pool.Take(count).ToList();   // retos: orden fijo

        double Weak(Question q) => 100 - (mastery.TryGetValue(q.Topic, out var m) ? m : 50);

        var review = new List<Question>();
        if (reviewPool.Count > 0 && count >= 6)
        {
            var weakOnes = reviewPool.Where(q => Weak(q) >= 35).ToList();
            review = WeightedSample(weakOnes, Weak, Math.Min(2, count / 4));
        }

        var need = count - review.Count;
        var interactive = WeightedSample(pool.Where(q => q.Type != "choice").ToList(), Weak, Math.Min(3, need / 2));
        var rest = WeightedSample(pool.Except(interactive).ToList(), Weak, need - interactive.Count);

        return review.Concat(interactive).Concat(rest).OrderBy(_ => _rng.Next()).ToList();
    }

    private List<Question> WeightedSample(List<Question> items, Func<Question, double> weakness, int take)
    {
        var left = items.ToList();
        var picked = new List<Question>();
        while (picked.Count < take && left.Count > 0)
        {
            var weights = left.Select(q => 10 + weakness(q)).ToList();   // base 10: ninguna queda con probabilidad 0
            var r = _rng.NextDouble() * weights.Sum();
            int i = 0;
            for (; i < weights.Count - 1; i++)
            {
                r -= weights[i];
                if (r <= 0) break;
            }
            picked.Add(left[i]);
            left.RemoveAt(i);
        }
        return picked;
    }

    // En C# las mayúsculas importan, los espacios no
    public static bool CheckText(Question q, string given)
    {
        var g = Normalize(given);
        return q.Answers.Any(a => Normalize(a) == g);
    }

    private static string Normalize(string s) => new(s.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
