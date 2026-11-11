namespace ProgramaPaso.Domain;

public record LevelInfo(int Number, string Title, bool Available);

public static class Curriculum
{
    // Subí este número a medida que agregues nivelN.json
    public const int AvailableLevels = 3;

    public static readonly LevelInfo[] Levels =
    {
        new(1, "Variables, constantes, tipos y operadores", true),
        new(2, "Condicionales: if, else, switch, && y ||", true),
        new(3, "Repetitivas: for y while", true),
        new(4, "Contadores, acumuladores y banderas", false),
        new(5, "Métodos", false),
        new(6, "Vectores", false),
        new(7, "Cadenas de caracteres", false),
        new(8, "Depurar código", false),
        new(9, "Integrador tipo parcial", false),
    };
}
