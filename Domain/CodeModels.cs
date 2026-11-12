using System.Text.Json;

namespace ProgramaPaso.Domain;

// Una prueba automática: se llama al método con Args y se espera Expected
public class CodeTest
{
    public List<JsonElement> Args { get; set; } = new();
    public JsonElement Expected { get; set; }
    public string Label { get; set; } = "";   // texto opcional para mostrar en lugar de la llamada completa
}

// Pista de un reto: mostrarla le resta Cost % al puntaje de ese reto
public class CodeHint
{
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Code { get; set; } = "";
    public int Cost { get; set; }
}

public class CodeIssue
{
    public int Line { get; set; }
    public string Message { get; set; } = "";
}

public class CodeTestResult
{
    public bool Ok { get; set; }
    public string Got { get; set; } = "";
    public string? Error { get; set; }
    public string Output { get; set; } = "";
}

public class CodeRunResult
{
    public List<CodeIssue> Issues { get; set; } = new();
    public List<CodeTestResult> Tests { get; set; } = new();
    public string? Fatal { get; set; }

    public bool AllPassed => Fatal is null && Issues.Count == 0 && Tests.Count > 0 && Tests.All(t => t.Ok);
}
