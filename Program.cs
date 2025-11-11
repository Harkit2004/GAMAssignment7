// File: Program.cs
namespace OpenTK_Sprite_Animation
{
    // --- Entry point ---------------------------------------------------------------------------
    internal class Program
    {
        private static void Main()
        {
            using var game = new SpriteAnimationGame(); // Ensures Dispose/OnUnload is called
            game.Run();                                  // Game loop: Load -> (Update/Render)* -> Unload
        }
    }
}
