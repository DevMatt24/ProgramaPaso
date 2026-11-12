namespace ProgramaPaso.Domain;

public class Question
{
    public string Id { get; set; } = "";
    public string Topic { get; set; } = "";
    public int Level { get; set; }

    // "choice" (elegir), "fill" (completar/escribir), "order" (ordenar líneas),
    // "bug" (marcar líneas con error) o "code" (escribir/corregir código con pruebas)
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

    // code: enunciado largo, método a probar, pruebas, pistas y solución de referencia.
    // En "code", Code es el código inicial que ve el alumno.
    public string Statement { get; set; } = "";
    public string Method { get; set; } = "";
    public List<CodeTest> Tests { get; set; } = new();
    public List<CodeHint> Hints { get; set; } = new();
    public string Solution { get; set; } = "";

    public string Explanation { get; set; } = "";
    public string Example { get; set; } = "";

    public string CorrectText => Type switch
    {
        "fill" => Answers.FirstOrDefault() ?? "",
        "order" => string.Join("\n", Lines),
        "bug" => FormatLines(BugLines),
        "code" => Solution,
        _ => Correct >= 0 && Correct < Options.Count ? Options[Correct] : ""
    };

    // bug: código con las líneas numeradas
    public string NumberedCode => string.Join("\n", Lines.Select((l, i) => $"{i + 1,2}  {l}"));

    public static string FormatLines(IReadOnlyCollection<int> numbers)
        => numbers.Count == 1 ? $"Línea {numbers.First()}" : $"Líneas {string.Join(", ", numbers)}";
}

// Fraction: parte del puntaje que se lleva (0 a 1). -1 = todo o nada según IsCorrect.
public record AnswerResult(Question Question, string Given, bool IsCorrect, double Fraction = -1, int HintsUsed = 0)
{
    public double Earned => Fraction >= 0 ? Fraction : (IsCorrect ? 1 : 0);
}

public record QuizResult(List<AnswerResult> Answers)
{
    public int Score => Answers.Count == 0 ? 0
        : (int)Math.Round(Answers.Sum(a => a.Earned) * 100.0 / Answers.Count);

    public IEnumerable<AnswerResult> Mistakes => Answers.Where(a => !a.IsCorrect);
    public bool Passed(int minimum = 70) => Score >= minimum;
}
