// File: Game.cs
using OpenTK.Graphics.OpenGL4;                       // OpenGL API
using OpenTK.Windowing.Common;                       // Frame events (OnLoad/OnUpdate/OnRender)
using OpenTK.Windowing.Desktop;                      // GameWindow/NativeWindowSettings
using OpenTK.Windowing.GraphicsLibraryFramework;     // Keyboard state
using OpenTK.Mathematics;                            // Matrix4, Vector types
using ImageSharp = SixLabors.ImageSharp.Image;       // Alias for brevity
using SixLabors.ImageSharp.PixelFormats;             // Rgba32 pixel type

namespace OpenTK_Sprite_Animation
{
    public class SpriteAnimationGame : GameWindow
    {
        private Character _character;                 // Handles animation state + physics + UV
        private int _shaderProgram;                   // Linked GLSL program
        private int _vao, _vbo;                       // Character quad geometry
        private int _groundVao, _groundVbo;           // Ground geometry
        private int _texIdle, _texWalk, _texRun, _texJump; // Sprite sheets per state
        private int _whiteTex;                        // 1x1 white for ground

        // Ground config
        private const float GroundHeight = 40f;        // pixels
        private const float GroundTopY = 80f;         // Y of top surface in world coords (bottom margin)

        public SpriteAnimationGame()
            : base(
                new GameWindowSettings(),
                new NativeWindowSettings { Size = (800, 600), Title = "Sprite Animation" })
        { }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0.15f, 0.17f, 0.22f, 1f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _shaderProgram = CreateShaderProgram();

            // Load sheets
            _texIdle = LoadTexture("Idle.png");
            _texWalk = LoadTexture("Walk.png");
            _texRun = LoadTexture("Run.png");
            _texJump = LoadTexture("Jump.png");

            // Character quad (128x128 visual size centered on its origin)
            float w = 128f, h = 128f;                     // 128x128
            float[] quad =
            {
                -w, -h, 0f, 0f,
                 w, -h, 1f, 0f,
                 w,  h, 1f, 1f,
                -w,  h, 0f, 1f
            };
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quad.Length * sizeof(float), quad, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.UseProgram(_shaderProgram);

            int texLoc = GL.GetUniformLocation(_shaderProgram, "uTexture");
            GL.Uniform1(texLoc, 0);

            int projLoc = GL.GetUniformLocation(_shaderProgram, "projection");
            Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, 800, 0, 600, -1, 1);
            GL.UniformMatrix4(projLoc, false, ref ortho);

            // Ground geometry (simple rectangle across the width)
            float gx = 0f, gy = GroundTopY - GroundHeight; // bottom-left of ground rect
            float gw = 800f, gh = GroundHeight;
            float[] groundVerts =
            {
                // pos.xy, uv
                gx,  gy, 0f, 0f,
                gx + gw, gy, 1f, 0f,
                gx + gw, gy + gh, 1f, 1f,
                gx,  gy + gh, 0f, 1f
            };
            _groundVao = GL.GenVertexArray();
            GL.BindVertexArray(_groundVao);

            _groundVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _groundVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, groundVerts.Length * sizeof(float), groundVerts, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            _whiteTex = CreateSolidTexture(1, 1, 200, 220, 235, 255); // light ground color

            // Model identity for ground; character sets its own model per-frame
            int modelLoc = GL.GetUniformLocation(_shaderProgram, "model");
            Matrix4 model = Matrix4.Identity;
            GL.UniformMatrix4(modelLoc, false, ref model);

            // Create character in the center above the ground
            Vector2 start = new Vector2(400f, GroundTopY + h + 1f);
            _character = new Character(_shaderProgram, _texIdle, _texWalk, _texRun, _texJump, start, h, GroundTopY);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            var k = KeyboardState;
            State move = State.None;
            if (k.IsKeyDown(Keys.Right))
                move = (k.IsKeyDown(Keys.LeftShift) || k.IsKeyDown(Keys.RightShift)) ? State.Run_Right : State.Walk_Right;
            else if (k.IsKeyDown(Keys.Left))
                move = (k.IsKeyDown(Keys.LeftShift) || k.IsKeyDown(Keys.RightShift)) ? State.Run_Left : State.Walk_Left;

            bool jump = k.IsKeyPressed(Keys.Space);

            _character.Update((float)e.Time, move, jump);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(_shaderProgram);

            // Draw ground
            GL.BindVertexArray(_groundVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _whiteTex);
            int modelLoc = GL.GetUniformLocation(_shaderProgram, "model");
            Matrix4 model = Matrix4.Identity;
            GL.UniformMatrix4(modelLoc, false, ref model);
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);

            // Draw character
            GL.BindVertexArray(_vao);
            _character.Render();

            SwapBuffers();
        }

        protected override void OnUnload()
        {
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteTexture(_texIdle);
            GL.DeleteTexture(_texWalk);
            GL.DeleteTexture(_texRun);
            GL.DeleteTexture(_texJump);
            GL.DeleteTexture(_whiteTex);
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            GL.DeleteBuffer(_groundVbo);
            GL.DeleteVertexArray(_groundVao);
            base.OnUnload();
        }

        private int CreateShaderProgram()
        {
            // Vertex Shader: transforms positions, flips V in UVs (image origin vs GL origin)
            string vs = @"
                #version 330 core
                layout(location = 0) in vec2 aPosition;
                layout(location = 1) in vec2 aTexCoord;
                out vec2 vTexCoord;
                uniform mat4 projection;
                uniform mat4 model;
                void main() {
                    gl_Position = projection * model * vec4(aPosition, 0.0, 1.0);
                    vTexCoord = vec2(aTexCoord.x, 1.0 - aTexCoord.y); // flip V so PNGs read intuitively
                }";

            // Fragment Shader: samples sub-rect of the sheet using uOffset/uSize
            string fs = @"
                #version 330 core
                in vec2 vTexCoord;
                out vec4 color;
                uniform sampler2D uTexture; // bound to texture unit 0
                uniform vec2 uOffset;       // normalized UV start (0..1)
                uniform vec2 uSize;         // normalized UV size  (0..1)
                void main() {
                    vec2 uv = uOffset + vTexCoord * uSize;
                    color = texture(uTexture, uv);
                }";

            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, vs);
            GL.CompileShader(v);
            CheckShaderCompile(v, "VERTEX");

            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, fs);
            GL.CompileShader(f);
            CheckShaderCompile(f, "FRAGMENT");

            int p = GL.CreateProgram();
            GL.AttachShader(p, v);
            GL.AttachShader(p, f);
            GL.LinkProgram(p);
            CheckProgramLink(p);

            GL.DetachShader(p, v);
            GL.DetachShader(p, f);
            GL.DeleteShader(v);
            GL.DeleteShader(f);

            return p;
        }

        private static void CheckShaderCompile(int shader, string stage)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0)
                throw new Exception($"{stage} SHADER COMPILE ERROR:\n{GL.GetShaderInfoLog(shader)}");
        }

        private static void CheckProgramLink(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
            if (ok == 0)
                throw new Exception($"PROGRAM LINK ERROR:\n{GL.GetProgramInfoLog(program)}");
        }

        private int LoadTexture(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Texture not found: {path}", path);

            using var img = ImageSharp.Load<Rgba32>(path); // decode to RGBA8

            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);

            // Copy raw pixels to managed buffer then upload
            var pixels = new byte[4 * img.Width * img.Height];
            img.CopyPixelDataTo(pixels);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                          img.Width, img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            // Nearest: prevents bleeding between adjacent frames on the atlas
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // Clamp: avoid wrap artifacts at frame borders
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            return tex;
        }

        private int CreateSolidTexture(int width, int height, byte r, byte g, byte b, byte a)
        {
            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);
            var pixels = new byte[width * height * 4];
            for (int i = 0; i < width * height; i++)
            {
                pixels[i * 4 + 0] = r;
                pixels[i * 4 + 1] = g;
                pixels[i * 4 + 2] = b;
                pixels[i * 4 + 3] = a;
            }
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            return tex;
        }
    }
}
