namespace ProgramaPaso.Domain;

// Name: nombre corto de la tarjeta · Icon: símbolo de código · Title: qué se practica en ese nivel
public record LevelInfo(int Number, string Name, string Icon, string Title, bool Available);

public static class Curriculum
{
    // Subí este número a medida que agregues nivelN.json
    public const int AvailableLevels = 9;

    public static readonly LevelInfo[] Levels =
    {
        new(1, "Variables", "x=", "Variables, constantes, tipos y operadores", true),
        new(2, "Condiciones", "if", "if, else, switch, && y ||", true),
        new(3, "Bucles", "↻", "for y while", true),
        new(4, "Contadores", "Σ", "Contadores, acumuladores y banderas", true),
        new(5, "Métodos", "ƒ()", "Parámetros, retorno y ámbito", true),
        new(6, "Vectores", "[ ]", "Recorrer, buscar y contar", true),
        new(7, "Cadenas", "\" \"", "Trim, ToLower, Contains, Substring, Split", true),
        new(8, "Depuración", "{!}", "Encontrar y corregir errores", true),
        new(9, "Integrador", "</>", "Corregir y escribir código como en el parcial", true),
    };
}
