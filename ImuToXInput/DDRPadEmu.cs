using WindowsInput;
using WindowsInput.Native;

namespace DDRPadEmu
{
    public class DdrPadEmulator
    {
        private readonly InputSimulator _sim;

        public DdrPadEmulator()
        {
            _sim = new InputSimulator();
        }

        public void Up(bool pressed)
        {
            if (pressed)
            {
                _sim.Keyboard.KeyDown(VirtualKeyCode.UP);
            } else
            {
                _sim.Keyboard.KeyUp(VirtualKeyCode.UP);
            }
        }

        public void Down(bool pressed)
        {
            if (pressed)
            {
                _sim.Keyboard.KeyDown(VirtualKeyCode.DOWN);
            } else
            {
                _sim.Keyboard.KeyUp(VirtualKeyCode.DOWN);
            }
        }

        public void Left(bool pressed)
        {
            if (pressed)
            {
                _sim.Keyboard.KeyDown(VirtualKeyCode.LEFT);
            } else
            {
                _sim.Keyboard.KeyUp(VirtualKeyCode.LEFT);
            }
        }

        public void Right(bool pressed)
        {
            if (pressed)
            {
                _sim.Keyboard.KeyDown(VirtualKeyCode.RIGHT);
            } else
            {
                _sim.Keyboard.KeyUp(VirtualKeyCode.RIGHT);
            }
        }
    }
}
