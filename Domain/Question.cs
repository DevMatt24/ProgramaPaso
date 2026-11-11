namespace ProgramaPaso.Domain;

public class Question
{
    public string Id { get; set; } = "";
    public string Topic { get; set; } = "";
    public int Level { get; set; }
    public string Prompt { get; set; } = "";
    public string Code { get; set; } = "";          // NUEVO: fragmento de código opcional
    public List<string> Options { get; set; } = new();
    public int Correct { get; set; }
    public string Explanation { get; set; } = "";
    public string Example { get; set; } = "";
}

public record AnswerResult(Question Question, int Selected)
{
    public bool IsCorrect => Selected == Question.Correct;
}

public record QuizResult(List<AnswerResult> Answers)
{
    public int Score => Answers.Count == 0 ? 0
        : (int)Math.Round(Answers.Count(a => a.IsCorrect) * 100.0 / Answers.Count);

    public IEnumerable<AnswerResult> Mistakes => Answers.Where(a => !a.IsCorrect);
    public bool Passed(int minimum = 70) => Score >= minimum;
}