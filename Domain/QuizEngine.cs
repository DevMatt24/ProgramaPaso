namespace ProgramaPaso.Domain;

public class QuizEngine
{
    private readonly Random _rng = new();

    public List<Question> BuildQuiz(List<Question> pool, int count)
        => pool.OrderBy(_ => _rng.Next()).Take(count).ToList();

    public QuizResult Evaluate(List<Question> quiz, int[] selected)
        => new(quiz.Select((q, i) => new AnswerResult(q, selected[i])).ToList());
}