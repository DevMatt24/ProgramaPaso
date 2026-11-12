using Microsoft.JSInterop;
using ProgramaPaso.Domain;

namespace ProgramaPaso.Services;

// Ejecuta el código del alumno con las pruebas del reto (usa wwwroot/js/code-runner.js)
public class CodeRunnerService
{
    private readonly IJSRuntime _js;
    public CodeRunnerService(IJSRuntime js) => _js = js;

    public async Task<CodeRunResult> RunAsync(string code, string method, List<CodeTest> tests)
    {
        try
        {
            var payload = tests.Select(t => new { args = t.Args, expected = t.Expected }).ToList();
            return await _js.InvokeAsync<CodeRunResult>("ppCode.run", code, method, payload);
        }
        catch (Exception ex)
        {
            return new CodeRunResult { Fatal = "No pude ejecutar las pruebas: " + ex.Message };
        }
    }
}
