namespace ProgramaPaso.Domain;

public class Question
{
    public string Id { get; set; } = "";
    public string Topic { get; set; } = "";
    public int Level { get; set; }

    // "choice" (elegir), "fill" (completar/escribir), "order" (ordenar líneas) o "bug" (marcar líneas con error)
    public string Type { get; set; } = "choice";

    public string Prompt { get; set; } = "";
    public string Code { get; set; } = "";

    // choice
    public List<string> Options { get; set; } = new();
    public int Correct { get; set; }

    // fill: respuestas aceptadas. Usá ___ en Code para marcar el hueco (opcional)
    public List<string> Answers { get; set; } = new();

    // order: líneas en el orden correcto. bug: las líneas del código a depurar
    public List<string> Lines { get; set; } = new();

    // bug: números de línea con error, contando desde 1
    public List<int> BugLines { get; set; } = new();

    public string Explanation { get; set; } = "";
    public string Example { get; set; } = "";

    public string CorrectText => Type switch
    {
        "fill" => Answers.FirstOrDefault() ?? "",
        "order" => string.Join("\n", Lines),
        "bug" => FormatLines(BugLines),
        _ => Correct >= 0 && Correct < Options.Count ? Options[Correct] : ""
    };

    // bug: código con las líneas numeradas
    public string NumberedCode => string.Join("\n", Lines.Select((l, i) => $"{i + 1,2}  {l}"));

    public static string FormatLines(IReadOnlyCollection<int> numbers)
        => numbers.Count == 1 ? $"Línea {numbers.First()}" : $"Líneas {string.Join(", ", numbers)}";
}

public record AnswerResult(Question Question, string Given, bool IsCorrect);

public record QuizResult(List<AnswerResult> Answers)
{
    public int Score => Answers.Count == 0 ? 0
        : (int)Math.Round(Answers.Count(a => a.IsCorrect) * 100.0 / Answers.Count);

    public IEnumerable<AnswerResult> Mistakes => Answers.Where(a => !a.IsCorrect);
    public bool Passed(int minimum = 70) => Score >= minimum;
}
