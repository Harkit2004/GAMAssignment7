// File: Utility.cs
using OpenTK.Graphics.OpenGL4; // OpenGL API
using OpenTK.Mathematics; // Math types for position

namespace OpenTK_Sprite_Animation
{
    // High-level character state driven by input
    public enum State
    {
        None,
        Walk_Left,
        Walk_Right,
        Run_Left,
        Run_Right,
        Jump
    }

    public class Character
    {
        private readonly int _shader; // Program containing uniforms
        private readonly int _texIdle;
        private readonly int _texWalk;
        private readonly int _texRun;
        private readonly int _texJump;

        // Animation timing and frame counts (adjust to your sheets)
        private const float FrameTimeIdle = 0.18f;
        private const float FrameTimeWalk = 0.12f;
        private const float FrameTimeRun = 0.08f;
        private const float FrameTimeJump = 0.10f;
        private const int FramesIdle = 6;
        private const int FramesWalk = 8;
        private const int FramesRun = 8;
        private const int FramesJump = 12;

        // Sheet layout (all assumed1 row, same frame size; change if needed)
        private const float FrameW = 128f;
        private const float FrameH = 128f;
        private const float GapX = 0f; // horizontal spacing between frames in pixels
        private const float TotalW = FrameW + GapX;
        private const float SheetH = FrameH; // one row

        // Physics
        private const float WalkSpeed = 220f; // px/s
        private const float RunSpeed = 360f; // px/s
        private const float JumpSpeed = 780f; // px/s upward
        private const float Gravity = -1400f; // px/s^2
        private readonly float _halfHeight; // character half-height in pixels
        private readonly float _groundTopY; // y coordinate of the ground's top surface

        // Runtime state
        private Vector2 _pos;
        private Vector2 _vel;
        private bool _onGround;
        private State _animState = State.None;
        private float _timer; // frame timer
        private int _frame; // current column

        public Character(int shader, int texIdle, int texWalk, int texRun, int texJump,
            Vector2 startPos, float halfHeight, float groundTopY)
        {
            _shader = shader;
            _texIdle = texIdle;
            _texWalk = texWalk;
            _texRun = texRun;
            _texJump = texJump;
            _pos = startPos;
            _halfHeight = halfHeight;
            _groundTopY = groundTopY;
            _onGround = true;
            SetAnimation(State.None, resetFrame: true);
            ApplyFrameUV(0, 0, FramesIdle); // visible starting frame
        }

        public void Update(float dt, State moveInput, bool jumpKeyDown)
        {
            // Horizontal velocity based on input
            float desiredVX = 0f;
            bool left = moveInput == State.Walk_Left || moveInput == State.Run_Left;
            bool right = moveInput == State.Walk_Right || moveInput == State.Run_Right;
            bool running = moveInput == State.Run_Left || moveInput == State.Run_Right;
            if (left) desiredVX = -(running ? RunSpeed : WalkSpeed);
            else if (right) desiredVX = (running ? RunSpeed : WalkSpeed);
            _vel.X = desiredVX;

            // Jump initiate only from ground
            if (jumpKeyDown && _onGround)
            {
                _vel.Y = JumpSpeed;
                _onGround = false;
                SetAnimation(State.Jump, resetFrame: true);
            }

            // Gravity
            if (!_onGround)
                _vel.Y += Gravity * dt;

            // Integrate
            _pos += _vel * dt;

            // Ground collision
            float minY = _groundTopY + _halfHeight;
            if (_pos.Y <= minY)
            {
                _pos.Y = minY;
                _vel.Y = 0f;
                _onGround = true;
            }

            // Decide animation based on state/physics
            if (_onGround)
            {
                if (desiredVX > 0f)
                    SetAnimation(running ? State.Run_Right : State.Walk_Right);
                else if (desiredVX < 0f)
                    SetAnimation(running ? State.Run_Left : State.Walk_Left);
                else
                    SetAnimation(State.None); // idle
            }
            else
            {
                SetAnimation(State.Jump); // in air
            }

            // Step animation
            float ft = GetFrameTime(_animState);
            int frameCount = GetFrameCount(_animState);
            _timer += dt;
            if (_timer >= ft)
            {
                _timer -= ft;
                _frame = (_frame + 1) % frameCount;
            }

            // Update UVs for current sheet/frame
            ApplyFrameUV(_frame, 0, frameCount);
        }

        public void Render()
        {
            // Select active texture and mirroring
            int texture = _texIdle;
            bool mirror = false;
            switch (_animState)
            {
                case State.Walk_Left: texture = _texWalk; mirror = true; break;
                case State.Walk_Right: texture = _texWalk; mirror = false; break;
                case State.Run_Left: texture = _texRun; mirror = true; break;
                case State.Run_Right: texture = _texRun; mirror = false; break;
                case State.Jump:
                    // Use walk/run mirror based on vx when in air
                    mirror = _vel.X < 0f; texture = _texJump; break;
                case State.None: default: texture = _texIdle; mirror = false; break;
            }

            // If mirror is needed, flip UV width
            if (mirror)
                FlipU();

            // Upload model transform
            int modelLoc = GL.GetUniformLocation(_shader, "model");
            Matrix4 model = Matrix4.CreateTranslation(_pos.X, _pos.Y, 0f);
            GL.UniformMatrix4(modelLoc, false, ref model);

            // Bind texture and draw
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        }

        private float GetFrameTime(State s) => s switch
        {
            State.Run_Left or State.Run_Right => FrameTimeRun,
            State.Walk_Left or State.Walk_Right => FrameTimeWalk,
            State.Jump => FrameTimeJump,
            _ => FrameTimeIdle,
        };

        private int GetFrameCount(State s) => s switch
        {
            State.Run_Left or State.Run_Right => FramesRun,
            State.Walk_Left or State.Walk_Right => FramesWalk,
            State.Jump => FramesJump,
            _ => FramesIdle,
        };

        private void SetAnimation(State s, bool resetFrame = false)
        {
            if (_animState == s && !resetFrame) return;
            _animState = s;
            _timer = 0f;
            _frame = 0;
        }

        // Upload UVs for (col,row) given frameCount to compute sheet width
        private void ApplyFrameUV(int col, int row, int frameCount)
        {
            float sheetW = (frameCount * TotalW) - GapX;

            float u = (col * TotalW) / sheetW;
            float v = (row * FrameH) / SheetH;
            float uw = FrameW / sheetW;
            float vh = FrameH / SheetH;

            GL.UseProgram(_shader);
            int off = GL.GetUniformLocation(_shader, "uOffset");
            int sz = GL.GetUniformLocation(_shader, "uSize");
            GL.Uniform2(off, u, v);
            GL.Uniform2(sz, uw, vh);
        }

        // Mirror current frame horizontally by inverting u-size
        private void FlipU()
        {
            int sz = GL.GetUniformLocation(_shader, "uSize");
            int off = GL.GetUniformLocation(_shader, "uOffset");
            float frameCount = GetFrameCount(_animState);
            float sheetW = (frameCount * TotalW) - GapX;
            float u = (_frame * TotalW) / sheetW;
            float uw = FrameW / sheetW;
            // shift to right edge and flip width
            GL.Uniform2(off, u + uw, 0f);
            GL.Uniform2(sz, -uw, FrameH / SheetH);
        }
    }
}
