namespace ProgramaPaso.Domain;

// Zone: nombre del lugar en el mapa · Title: qué se practica en ese nivel
public record LevelInfo(int Number, string Zone, string Icon, string Title, bool Available);

public static class Curriculum
{
    // Subí este número a medida que agregues nivelN.json
    public const int AvailableLevels = 9;

    public static readonly LevelInfo[] Levels =
    {
        new(1, "Valle de las Variables", "🌱", "Variables, constantes, tipos y operadores", true),
        new(2, "Encrucijada de Condiciones", "🔀", "if, else, switch, && y ||", true),
        new(3, "Laberinto de Bucles", "🔁", "for y while", true),
        new(4, "Taller de Contadores", "🧮", "Contadores, acumuladores y banderas", true),
        new(5, "Forja de Métodos", "⚒️", "Métodos: parámetros y retorno", true),
        new(6, "Mina de Vectores", "⛏️", "Vectores: recorrer, buscar y contar", true),
        new(7, "Torre de Cadenas", "🗼", "Cadenas de caracteres", true),
        new(8, "Cueva de los Bugs", "🐞", "Depurar código", true),
        new(9, "Fortaleza del Integrador", "🏰", "Corregir y escribir código como en el parcial", true),
    };
}
