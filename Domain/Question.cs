namespace ProgramaPaso.Domain;

public class Question
{
    public string Id { get; set; } = "";
    public string Topic { get; set; } = "";
    public int Level { get; set; }

    // "choice" (elegir opción), "fill" (completar/escribir) u "order" (ordenar líneas)
    public string Type { get; set; } = "choice";

    public string Prompt { get; set; } = "";
    public string Code { get; set; } = "";

    // choice
    public List<string> Options { get; set; } = new();
    public int Correct { get; set; }

    // fill: respuestas aceptadas. Usá ___ en Code para marcar el hueco (opcional)
    public List<string> Answers { get; set; } = new();

    // order: líneas en el orden correcto
    public List<string> Lines { get; set; } = new();

    public string Explanation { get; set; } = "";
    public string Example { get; set; } = "";

    public string CorrectText => Type switch
    {
        "fill" => Answers.FirstOrDefault() ?? "",
        "order" => string.Join("\n", Lines),
        _ => Correct >= 0 && Correct < Options.Count ? Options[Correct] : ""
    };
}

public record AnswerResult(Question Question, string Given, bool IsCorrect);

public record QuizResult(List<AnswerResult> Answers)
{
    public int Score => Answers.Count == 0 ? 0
        : (int)Math.Round(Answers.Count(a => a.IsCorrect) * 100.0 / Answers.Count);

    public IEnumerable<AnswerResult> Mistakes => Answers.Where(a => !a.IsCorrect);
    public bool Passed(int minimum = 70) => Score >= minimum;
}
