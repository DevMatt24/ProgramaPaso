namespace ProgramaPaso.Domain;

public record LevelInfo(int Number, string Title, bool Available);

public static class Curriculum
{
    // Subí este número a medida que agregues nivelN.json
    public const int AvailableLevels = 9;

    public static readonly LevelInfo[] Levels =
    {
        new(1, "Variables, constantes, tipos y operadores", true),
        new(2, "Condicionales: if, else, switch, && y ||", true),
        new(3, "Repetitivas: for y while", true),
        new(4, "Contadores, acumuladores y banderas", true),
        new(5, "Métodos", true),
        new(6, "Vectores", true),
        new(7, "Cadenas de caracteres", true),
        new(8, "Depurar código", true),
        new(9, "Integrador tipo parcial: corregir y escribir código", true),
    };
}
